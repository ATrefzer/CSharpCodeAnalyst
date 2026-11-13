# Parser-Änderungen November 2026

Vier Änderungen am Parser, ausgelöst durch Fehlalarme der Dead-Code-Analyse. Dieses Dokument erklärt, *wo*
sie sitzen und *wie* sie arbeiten. Das *Warum* der Modellierung steht als Entscheidungs-Log in
[corrections-and-updates.md](corrections-and-updates.md), die Landkarte des Parsers in
[parser-architecture.md](parser-architecture.md).

## Kurz gesagt

Alle vier Fälle haben dasselbe Muster: Im Code passiert ein Aufruf, den niemand hinschreibt. Es gibt
keinen Syntaxknoten dafür, also fand Phase 2 keine Kante, und das Ziel des Aufrufs sah unbenutzt aus.

| # | Was im Code passiert | Neue Kante | Wo |
|---|---|---|---|
| 1 | `record Order(int Id)` setzt `Id` | Primärkonstruktor → `set_Id` | `DeclarationAnalyzer` |
| 2 | `Derived() { }` ruft `Base()` | Konstruktor → Basiskonstruktor | `DeclarationAnalyzer` |
| 3 | `[Mark(1)]` ruft `MarkAttribute(int)` | dekoriertes Element → Attribut-Konstruktor | `DeclarationAnalyzer` |
| 4 | `[Mark(Name = "x")]` setzt `Name` | Ziel ist `set_Name` statt `get_Name` | `PropertyAccessClassifier` |

Alles liegt in **Phase 2**. An Phase 1, an `Key()`, an den Walkern, am `SyntaxNodeAnalyzer` und an der
Auflösungskaskade des `RelationshipBuilder` hat sich nichts geändert.

```
RelationshipAnalyzer                          (unverändert)
        │
        ▼
DeclarationAnalyzer      ←── 1: LinkPositionalPropertyInitialization
        │                    2: LinkImplicitBaseConstructorCall
        │                    3: AnalyzeAttributeRelationships (+ eine Kante)
        ▼
MethodBodyWalker / LambdaBodyWalker           (unverändert)
        │
        ▼
SyntaxNodeAnalyzer                            (unverändert)
        │   └─ PropertyAccessClassifier  ←── 4: ein zusätzlicher Fall
        ▼
RelationshipBuilder                           (unverändert)
```

Die Änderungen 2 und 3 teilen sich einen kleinen Helfer, `LinkConstructorCall`. Er enthält die Regel, die
vorher schon an drei Stellen galt: Ein Konstruktor wird nur verknüpft, wenn er ein Element des Graphen
ist. Generierte und externe Konstruktoren sind keine Elemente und bekommen keine Kante, auch keinen
Rückfall auf den Typ.

## 1. Der Record-Konstruktor setzt seine Positional Properties

### Das Problem

```csharp
public record Order(int Id, Money Total);
```

Phase 1 legt dafür sieben Elemente unter `Order` an: den Konstruktor `.ctor`, die Properties `Id` und
`Total`, und pro Property die Accessoren `get_…` und `set_…` (der Init-Accessor heißt in den Metadaten
`set_Id`). Phase 2 fand für `set_Id` aber nie eine eingehende Kante:

- `new Order(1, total)` erzeugt `Creates Order` und `Calls Order..ctor`.
- Der Konstruktor schreibt direkt ins Backing Field. Einen Aufruf von `set_Id` gibt es im Code nicht.
- `set_Id` wird nur von `with { Id = 5 }` oder einem Objekt-Initialisierer aufgerufen.

Ergebnis: ein Dead-Code-Fund pro Positional Parameter, für einen Accessor, den niemand geschrieben hat
und niemand löschen kann.

### Wo der Fix sitzt

`DeclarationAnalyzer.AnalyzeMethodRelationships` läuft für jede Methode, also auch für den
Primärkonstruktor. Dessen Besonderheit kennt der Code schon: Seine Deklarationssyntax ist die
**Typdeklaration**, und die darf nicht als Methodenkörper durchlaufen werden. An genau dieser Weiche hängt
jetzt ein zusätzlicher Aufruf:

```csharp
if (syntax is TypeDeclarationSyntax typeDeclaration)
{
    LinkPositionalPropertyInitialization(methodElement, methodSymbol);   // neu
    AnalyzePrimaryConstructorBody(methodElement, methodSymbol.ContainingType, typeDeclaration, semanticModel);
    continue;
}
```

### Wie es arbeitet

`LinkPositionalPropertyInitialization` geht die Parameter des Konstruktors durch und sucht zu jedem die
Property, die er deklariert. Roslyn bietet dafür keinen direkten Weg (Parameter → Property), also wird
rückwärts gesucht, **genauso wie in Phase 1** (`HierarchyAnalyzer.FindPositionalProperty`):

```
für jeden Parameter p des Primärkonstruktors:
    parameterSyntax = p.DeclaringSyntaxReferences.First().GetSyntax()
    property = die Property gleichen Namens im Typ,
               deren Deklaration GENAU DIESE ParameterSyntax ist
    wenn property.SetMethod ein Element hat:
        Calls-Kante  Konstruktor → set_<Name>
```

Drei Details sind dabei tragend:

- **„Deren Deklaration diese ParameterSyntax ist"** ist der Filter, der alles Übrige aussortiert. Bei
  `class Service(ILogger logger)` gibt es keine Property. Bei
  `record Explicit(int Id) { public int Id { get; init; } = Id; }` gibt es eine, aber ihre Deklaration
  ist die `PropertyDeclarationSyntax`, nicht der Parameter. In beiden Fällen entsteht keine Kante.
- **`property.SetMethod`** ist ein `IMethodSymbol`. Phase 1 hat die Accessor-Elemente aus genau diesen
  Symbolen erzeugt (`CreatePropertyAccessorElements`), also findet `FindInternalCodeElement(setter)` das
  Element über denselben `Key()`. Kein neuer Lookup-Weg.
- Geschrieben wird über `AddCallsRelationship`, den normalen Weg für Aufrufe.

### Der Graph vorher und nachher

```
vorher                               nachher
Order                                Order
├─ .ctor                             ├─ .ctor ──Calls──► Id.set_Id
├─ Id                                │        ──Calls──► Total.set_Total
│  ├─ get_Id                         ├─ Id
│  └─ set_Id     (keine Kante)       │  ├─ get_Id
└─ Total                             │  └─ set_Id
   ├─ get_Total                      └─ Total
   └─ set_Total  (keine Kante)          ├─ get_Total
                                        └─ set_Total
```

Die Kante bildet nicht das IL ab (das schreibt das Backing Field), sondern die Aussage „der Konstruktor
setzt die Property". Dasselbe Prinzip wie bei Feld-Initialisierern: Der Graph modelliert, welches Element
eine Abhängigkeit *besitzt*, und ein Backing Field ist kein Element.

Für die Dead-Code-Analyse: Liest niemand `Order.Total`, bleibt das ein Fund. Er steht jetzt als
`Order.Total.get_Total` in der Liste statt als `Order.Total`, weil der Init-Accessor über den Konstruktor
referenziert ist.

## 2. Der Basiskonstruktor-Aufruf, den niemand schreibt

### Das Problem

```csharp
public abstract class Base
{
    protected Base() { }
}

public class Derived : Base
{
    public Derived() { }      // ruft Base(), ohne ": base()"
}
```

Ein geschriebenes `: base(...)` ist ein `ConstructorInitializerSyntax`, den der `MethodBodyWalker` besucht.
Fehlt es, gibt es keinen Knoten, den ein Walker besuchen könnte. `Base()` hatte also keine eingehende
Kante. Für einen `protected`-Konstruktor ist das der Normalfall.

### Die drei Formen

Der Aufruf findet in drei Formen statt, und jede hat ihre eigene Stelle im `DeclarationAnalyzer`:

| Die abgeleitete Klasse hat | Stelle | Quelle der Kante |
|---|---|---|
| einen Konstruktor ohne Initializer | `AnalyzeMethodRelationships` | dieser Konstruktor |
| einen Primärkonstruktor, Basisklausel ohne Argumentliste | `AnalyzePrimaryConstructorBody` | der Primärkonstruktor |
| gar keinen Konstruktor | `LinkBaseCallOfTheCompilerGeneratedConstructor` | der **Typ** |

Die dritte Zeile ist die Modellierungsentscheidung. Der Compiler erzeugt dort einen Konstruktor, und der
macht den Aufruf. Ein generierter Konstruktor ist aber kein Element. Also trägt der Typ die Kante,
genauso wie ein Enum die Abhängigkeiten seiner Member-Initialisierer trägt.

### Welcher Basiskonstruktor gemeint ist

Die Frage lautet: Hat die Basisklasse mehrere Konstruktoren, welchen ruft ein `base()` ohne Argumente?
Meistens gibt es nur einen Kandidaten. Bei mehreren entscheidet die Überladungsauflösung von C#, und die
Antwort kommt bei uns aus zwei Quellen, weil Roslyn sie nur für die erste Form gibt.

**Geschriebener Konstruktor: Roslyn fragen.** Der Compiler hat den Aufruf gebunden, obwohl ihn niemand
geschrieben hat. Er steht nicht im Syntaxbaum, aber im Operationsbaum:

```csharp
semanticModel.GetOperation(constructorDeclaration)   // IConstructorBodyOperation
    .Initializer                                     // implizites base(...)
    .Operation.TargetMethod                          // der gemeinte Konstruktor
```

Das ist die Auflösung des Compilers selbst und damit exakt (`LinkImplicitBaseCallOfWrittenConstructor`).

**Primärkonstruktor und generierter Konstruktor: selbst einstufen.** Hier gibt es nichts zu fragen.
`GetOperation` liefert für die Typdeklaration, ihre Parameterliste und den Basistyp `null`, und ein
generierter Konstruktor hat gar keine Syntax. `FindBaseConstructorsCalledWithoutArguments` stuft die
Kandidaten deshalb in der Reihenfolge ein, die C# bei leerer Argumentliste anwendet:

```
Kandidaten = geschriebene Instanzkonstruktoren des Basistyps
             (private nur, wenn der abgeleitete Typ im Basistyp geschachtelt ist)
1. der parameterlose
2. sonst die, deren Parameter alle optional sind              Base(int size = 4)
3. sonst die, die ihr params-Array brauchen                   Base(params int[] values)
```

Die Reihenfolge ist der Punkt: Steht `Base(params int[] values)` vor `Base(int value = 0)`, ruft C# den
zweiten. Die erste Fassung nahm den zuerst deklarierten und ließ damit den Konstruktor, der wirklich
läuft, ohne Kante.

Innerhalb der dritten Stufe vergleicht C# noch Parameterzahl und Elementtyp (`params string[]` schlägt
`params object[]`). Das ist nicht nachgebaut. Bleiben in einer Stufe mehrere Konstruktoren übrig, werden
**alle** verknüpft: Eine Kante zu viel verdeckt einen Fund, eine Kante auf dem falschen erfindet einen.

Die Ausnahme für `private` gibt es wegen der geschlossenen Hierarchie:
`abstract class Shape { private Shape() { } public sealed class Circle : Shape { } }`.

### Was keine Kante bekommt

- **Ein Konstruktor mit `: this(...)`.** Er erreicht die Basis über den anderen Konstruktor.
- **Ein statischer Konstruktor.** Er ruft keinen Basiskonstruktor.
- **Ein generierter oder externer Basiskonstruktor.** Beide sind keine Elemente; es bleibt bei der
  `Inherits`-Kante.

Die Kante trägt `IsBaseCall` wie ihr geschriebenes Gegenstück. Zwischen Typen entsteht keine neue
Abhängigkeit: `Derived → Base` stand schon als `Inherits` im Graphen.

## 3. Ein Attribut anzuwenden ruft seinen Konstruktor

```csharp
[Mark(1)]
public class Decorated;
```

`[Mark(1)]` nennt nur den Attribut-*Typ*. Die einzige Kante war `UsesAttribute` auf die Klasse
`MarkAttribute`; der Konstruktor `MarkAttribute(int)` hatte keine eingehende Referenz.

Roslyn hat die Überladung schon aufgelöst: `AttributeData.AttributeConstructor`.
`AnalyzeAttributeRelationships` ist die eine Stelle, die für jedes Element die Attribute durchgeht und
`UsesAttribute` schreibt. Dort kommt jetzt die `Calls`-Kante vom dekorierten Element auf den Konstruktor
dazu, für Klassen, Felder, Properties und Methoden gleichermaßen.

Ein Attribut ohne geschriebenen Konstruktor bekommt keine Kante.

## 4. Benannte Attribut-Argumente setzen die Property

```csharp
[Mark(Name = "x")]
```

Hier gab es die Kante schon, sie zeigte nur auf den falschen Accessor. Ob ein Property-Zugriff den Getter
oder den Setter meint, entscheidet der `PropertyAccessClassifier` rein syntaktisch, und er kannte als
Schreibzugriff nur Zuweisungen und `++`/`--`. `Name = "x"` in einem Attribut ist aber keine
`AssignmentExpressionSyntax`, sondern ein `NameEqualsSyntax`. Der Fall fiel deshalb auf „Lesen" durch:
`get_Name` galt als benutzt, `set_Name` als tot.

Der Classifier hat jetzt einen dritten Fall:

```csharp
case NameEqualsSyntax { Parent: AttributeArgumentSyntax }:
    return PropertyAccessKind.Write;
```

Die Einschränkung auf `AttributeArgumentSyntax` ist nötig, weil derselbe Knoten in `new { Name = x }`
vorkommt. Dort gibt es keinen Setter.

## Was die Änderungen sonst bewirken

Wenig, und das ist Absicht:

- Kante 1 bleibt innerhalb des Typs. Zyklensuche, Schichtregeln und Typ-Graph sehen sie nicht, und die
  Kohäsionsmetrik lässt Konstruktoren ohnehin weg.
- Kante 2 verläuft parallel zu einer `Inherits`-Kante, Kante 3 parallel zu einer `UsesAttribute`-Kante.
  Auf Typ-Ebene kommt also keine Abhängigkeit hinzu, die nicht schon da war.
- Änderung 4 verschiebt ein Ziel innerhalb derselben Property.

## Was bewusst nicht im Parser ist

**Serializer-Aufrufe.** Im selben Zug war zunächst eine Erkennung eingebaut (`JsonSerializer.Serialize(dto)`
→ die Properties des DTOs gelten als per Reflection erreicht). Sie ist wieder entfernt: Das ist Wissen über
Bibliotheken, nicht über den Code, und es hätte nur einer Analyse gedient. Die Begründung steht in
[corrections-and-updates.md](corrections-and-updates.md) unter „Not recorded: types handed to a serializer".

**Attribute auf Parametern, Rückgabewerten und Typparametern.** Diese Symbole sind keine Elemente, also
sieht `AnalyzeAttributeRelationships` sie nicht. Für sie entsteht weder die Konstruktor-Kante noch
`UsesAttribute`. Das ist eine offene Lücke, keine Entscheidung.

## Tests

- [PositionalRecordMemberParseTests.cs](../../Tests/UnitTests/Parser/PositionalRecordMemberParseTests.cs),
  die drei Fälle am Ende: Record, ausgeschriebene Property, Klasse mit Primärkonstruktor.
- [ImplicitBaseConstructorCallParseTests.cs](../../Tests/UnitTests/Parser/ImplicitBaseConstructorCallParseTests.cs):
  alle drei Formen, dazu `this(...)`, statischer Konstruktor, optionale Parameter, geschachtelter Typ mit
  privatem Basiskonstruktor, generische Basis, generierte und externe Basis.
- [ImplicitBaseConstructorOverloadParseTests.cs](../../Tests/UnitTests/Parser/ImplicitBaseConstructorOverloadParseTests.cs):
  mehrere Basiskonstruktoren, die ohne Argumente aufrufbar sind - optional gegen `params`, zwei
  `params`-Arrays, parameterlos gegen optional.
- [AttributeConstructorParseTests.cs](../../Tests/UnitTests/Parser/AttributeConstructorParseTests.cs):
  Konstruktor von jeder Art Element aus, die richtige Überladung, benannte Argumente als Schreibzugriff.
- [PropertyAccessClassifierTests.cs](../../Tests/UnitTests/Parser/PropertyAccessClassifierTests.cs):
  das benannte Attribut-Argument und die Abgrenzung zum anonymen Objekt.
