# Parser-Änderung November 2026

Eine Änderung am Parser, ausgelöst durch Fehlalarme der Dead-Code-Analyse. Dieses Dokument erklärt, *wo*
sie sitzt und *wie* sie arbeitet. Das *Warum* der Modellierung steht als Entscheidungs-Log in
[corrections-and-updates.md](corrections-and-updates.md) (Kapitel „The primary constructor initializes the
positional properties"), die Landkarte des Parsers in [parser-architecture.md](parser-architecture.md).

## Kurz gesagt

Der Primärkonstruktor eines Records bekommt eine `Calls`-Kante auf die Init-Accessoren seiner Positional
Properties. Das ist eine Methode von etwa 20 Zeilen im `DeclarationAnalyzer`, also in **Phase 2**.

An Phase 1, an `Key()`, an den Walkern, am `SyntaxNodeAnalyzer` und an der Auflösungskaskade des
`RelationshipBuilder` hat sich nichts geändert.

```
RelationshipAnalyzer                          (unverändert)
        │
        ▼
DeclarationAnalyzer      ←── neu: LinkPositionalPropertyInitialization
        │
        ▼
MethodBodyWalker / LambdaBodyWalker           (unverändert)
        │
        ▼
SyntaxNodeAnalyzer                            (unverändert)
        │
        ▼
RelationshipBuilder                           (unverändert)
```

## Das Problem

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

## Wo der Fix sitzt

`DeclarationAnalyzer.AnalyzeMethodRelationships` läuft für jede Methode, also auch für den
Primärkonstruktor. Dessen Besonderheit kennt der Code schon: Seine Deklarationssyntax ist die
**Typdeklaration**, und die darf nicht als Methodenkörper durchlaufen werden. An genau dieser Weiche hängt
jetzt ein zusätzlicher Aufruf:

```csharp
if (syntax is TypeDeclarationSyntax typeDeclaration)
{
    LinkPositionalPropertyInitialization(methodElement, methodSymbol);   // neu
    AnalyzePrimaryConstructorBody(methodElement, typeDeclaration, semanticModel);
    continue;
}
```

## Wie es arbeitet

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

## Der Graph vorher und nachher

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

## Was sich dadurch sonst ändert

Praktisch nichts. Die Kante bleibt innerhalb des Typs, also sehen Zyklensuche, Schichtregeln und
Typ-Graph sie nicht. Die Kohäsionsmetrik lässt Konstruktoren ohnehin weg.

Für die Dead-Code-Analyse: Liest niemand `Order.Total`, bleibt das ein Fund. Er steht jetzt als
`Order.Total.get_Total` in der Liste statt als `Order.Total`, weil der Init-Accessor über den Konstruktor
referenziert ist.

## Was bewusst nicht im Parser ist

Im selben Zug war zunächst eine Erkennung von Serializer-Aufrufen eingebaut (`JsonSerializer.Serialize(dto)`
→ die Properties des DTOs gelten als per Reflection erreicht). Sie ist wieder entfernt: Das ist Wissen über
Bibliotheken, nicht über den Code, und es hätte nur einer Analyse gedient. Die Begründung steht in
[corrections-and-updates.md](corrections-and-updates.md) unter „Not recorded: types handed to a serializer".

## Tests

[PositionalRecordMemberParseTests.cs](../../Tests/UnitTests/Parser/PositionalRecordMemberParseTests.cs) –
die drei Fälle am Ende: Record, ausgeschriebene Property, Klasse mit Primärkonstruktor.
