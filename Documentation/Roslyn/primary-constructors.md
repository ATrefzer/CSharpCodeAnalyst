# Primärkonstruktor-Mechanik

Wie `HierarchyAnalyzer` und `DeclarationAnalyzer` Primary Constructors, Record-Properties und "gefangene" Parameter im Codegraphen abbilden (Commit `867b961`, siehe auch [corrections-and-updates.md](corrections-and-updates.md) für die Kurzfassung als Entscheidungs-Log-Eintrag).

## Kurz gesagt

Roslyn gibt einem Primary Constructor keine eigene Deklarationssyntax — sein `DeclaringSyntaxReferences` zeigt auf die *Typdeklaration selbst*. `HierarchyAnalyzer` hatte davor nur `ConstructorDeclarationSyntax` erkannt, also war der Konstruktor unsichtbar — und mit ihm jedes Feld, das ein Parameter durch Verwendung im Methodenkörper implizit wird. Der Fix hängt sich an die Stelle, an der die Typdeklaration ohnehin schon ihr `CodeElement` bekommt, und erzeugt dort synchron die fehlenden Kinder.

## 00 Grundlagen: Symbol, Syntax und die zwei Wege

Die Begriffe, auf denen alles Folgende aufbaut — wer das schon sicher hat, kann direkt zu Abschnitt 01 springen.

**Primary Constructor.** So heißt die Parameterliste direkt hinter dem Typnamen, bei `class` und `record` gleichermaßen: `class X(int i)`, `record X(int i)`. Beide erzeugen einen echten Konstruktor — nur eben ohne eigene `ConstructorDeclarationSyntax`.

**Symbol ≠ Syntax-Knoten.** `ISymbol.DeclaringSyntaxReferences` ist keine Liste von `SyntaxNode`, sondern von `SyntaxReference` — einer leichtgewichtigen "Koordinate" (Baum + Textspanne), die den Knoten bewusst *nicht* im Speicher hält. Erst `.GetSyntax()` löst sie zu einem echten `SyntaxNode` auf. Grund: Symbole können langlebiger sein als Syntaxbäume (Stichwort `partial class` über mehrere Dateien) — hielte man den Knoten direkt fest, würde man ungefragt den ganzen Baum im Speicher fixieren.

Der Primary Constructor **hat** also ein Symbol und eine Syntax-Referenz — nur zeigt die nicht auf einen eigenen Knoten, sondern auf denselben `ClassDeclarationSyntax`/`RecordDeclarationSyntax`-Knoten wie der Typ selbst. Es gibt keinen eigenen Konstruktor-Knoten, weil die Parameterliste Teil der Typzeile ist.

**`IsImplicitlyDeclared` heißt "kein geschriebener Text", nicht "kein eigener Knotentyp".** Die Parameterliste eines Primary Constructors ist geschriebener Text → `IsImplicitlyDeclared == false`, obwohl sie sich einen Knoten mit dem Typ teilt. Fallstrick bei Records: Der vom Compiler synthetisierte Kopierkonstruktor (für `with`-Ausdrücke) zeigt in seiner `DeclaringSyntaxReferences` *ebenfalls* auf die Typdeklaration — weil es keinen besseren Ort für die gemeldete Position gibt — ist aber wirklich implizit (`IsImplicitlyDeclared == true`). Ohne diese Unterscheidung würde man leicht den falschen der beiden Konstruktoren erwischen.

**Record-Property vs. Class-Field — unterschiedliche Bedingung.**

| | `class X(int i)` | `record X(int i)` |
| --- | --- | --- |
| Konstruktor-Symbol | ✓ (Syntax = Typknoten) | ✓ (Syntax = Typknoten) |
| Property? | nie | **immer**, unabhängig von Nutzung |
| Field? | nur wenn `i` im Body benutzt wird | nie (Property übernimmt die Rolle) |

**Zwei getrennte, parallele Erzeugungswege — kein gemeinsamer Code.**

- *Seitenkanal, einmal pro Typ:* `CreatePrimaryConstructorElement`, ausgelöst direkt nachdem das Typ-Element steht, noch vor der Rekursion. Holt sich Konstruktor und Parameter **direkt vom Symbol** (`typeSymbol.InstanceConstructors`, `primaryConstructor.Parameters`) — ganz ohne einen Syntax-Knoten dafür zu suchen. Erzeugt Konstruktor + (bei Bedarf) Field.
- *Normale Rekursion, pro Parameter überall im Code:* der `switch` in `ProcessNodeForHierarchy` trifft `case ParameterSyntax` für jeden Parameter, den er besucht — Methoden, Lambdas, Konstruktoren, alles. Für praktisch alle davon liefert die Prüfung `null`. Nur bei einer Record-Positionsparameter-Liste findet `FindPositionalProperty` etwas — rückwärts gesucht: nicht "welches Symbol hat dieser Parameter", sondern "welche Property hat *diesen Parameter* als ihre eigene Deklaration".

**Wie beide Wege trotzdem zueinander finden: `Key()`.** Egal auf welchem Weg man an ein Symbol kommt — vom Konstruktor aus, oder später bei einer Verwendung im Methodenkörper — `Key()` baut aus der Symbolkette (Name + Elternkette bis zur Assembly) einen stabilen Text-Schlüssel, unabhängig von der konkreten Objektinstanz. Darüber findet Phase 2 später das in Phase 1 angelegte Element wieder, auch wenn beide Zugriffe technisch unterschiedliche `IParameterSymbol`-Instanzen liefern.

## 01 Das Grundproblem

Für einen klassischen Konstruktor ist die Zuordnung eindeutig: eigene Syntax, eigenes Symbol.

```csharp
class Service
{
    private readonly ILogger _logger;
    public Service(ILogger logger) { _logger = logger; }  // ConstructorDeclarationSyntax → eigenes Symbol
}
```

Bei einem Primary Constructor gibt es diese eigene Syntax nicht:

```csharp
class Service(ILogger logger)
{
    // kein ConstructorDeclarationSyntax irgendwo — der Konstruktor "wohnt" in der Klassenzeile selbst
}
```

Fragt man Roslyn nach `constructor.DeclaringSyntaxReferences`, bekommt man die `ClassDeclarationSyntax` zurück — denselben Knoten, der auch die Klasse selbst deklariert. Der alte `switch` in `HierarchyAnalyzer` hatte `case ConstructorDeclarationSyntax`, und das feuert für diesen Knoten nie. Ergebnis: ein Record oder eine Primary-Constructor-Klasse war im Graphen ein **leerer Typ** — kein Konstruktor-Element, keine Properties, keine Felder.

## 02 Drei Kindelemente, ein Auslöser

Der Fix fügt keinen neuen Durchlauf hinzu. Er hängt sich an die Stelle, an der `ProcessNodeForHierarchy` für die Typdeklaration ohnehin schon ein `CodeElement` erzeugt hat — und erzeugt von dort aus, bevor in die Kindknoten rekursiert wird, bis zu drei weitere Elemente:

| Element | Wo im Code | Wann |
| --- | --- | --- |
| `Method` Konstruktor | `CreatePrimaryConstructorElement` | Immer, wenn die Typdeklaration eine `ParameterList` trägt (also ein echter Primary Constructor ist) |
| `Property` Record-Property | `FindPositionalProperty`, über `case ParameterSyntax` | Pro Positionsparameter eines Records — nicht bei class/struct |
| `Field` gefangener Parameter | `CreateCapturedParameterField` | Pro Parameter, den der Compiler intern als Feld gespeichert hat |

## 03 Der Baumlauf am Beispiel

Als durchgängiges Beispiel:

```csharp
class Service(ILogger logger)
{
    public void Start() => logger.Log("start");
    public void Stop()  => logger.Log("stop");
}
```

`logger` wird in *zwei* Methodenkörpern verwendet — nicht nur in einem Feld-Initialisierer. Damit entscheidet der Compiler selbst, ihn als echtes Feld zu speichern (dazu mehr in Abschnitt 04). So läuft `ProcessNodeForHierarchy` den Baum ab:

```
Syntaxbaum (Roslyn)                                  Codegraph (Ergebnis)
────────────────────                                 ─────────────────────
ClassDeclarationSyntax "Service"    ──erzeugt───────▶ Class "Service"
  │                                  ┌──①────────────▶   Method "Service..ctor"
  │                                  └──②────────────▶   Field "logger"
  ├─ ParameterList
  │    └─ ParameterSyntax "logger" ──∅ (kein Record)
  ├─ MethodDeclarationSyntax "Start" ──normale Rek.──▶   Method "Start"
  └─ MethodDeclarationSyntax "Stop"  ──normale Rek.──▶   Method "Stop"

① CreatePrimaryConstructorElement — Seitenkanal, läuft synchron bevor rekursiert wird
② CreateCapturedParameterField  — pro tatsächlich gefangenem Parameter
∅ FindPositionalProperty → null, weil "Service" kein Record ist
```

Der entscheidende Satz steht im Code selbst, direkt nachdem `GetOrCreateCodeElementWithNamespaceHierarchy` das `Class`-Element erzeugt hat und **bevor** die Schleife über `node.ChildNodes()` beginnt:

```csharp
if (symbol is INamedTypeSymbol namedTypeSymbol)
{
    CreatePrimaryConstructorElement(namedTypeSymbol, node, element);
}

foreach (var childNode in node.ChildNodes())
{
    ProcessNodeForHierarchy(childNode, semanticModel, element);  // hier kommt später auch ParameterList/ParameterSyntax dran
}
```

Konstruktor und Feld entstehen also *nicht* als Reaktion auf einen eigenen Syntax-Knoten, wie jedes andere Element im `switch` — es gibt für sie keinen eigenen Knoten. Sie werden erzeugt, weil die Klasse selbst gefunden wurde, ausgelöst von der Existenz der `ParameterList` in der Klassenzeile, nicht von einem Besuch dieses Knotens.

## 04 Woher weiß der Parser, dass `logger` "gefangen" ist?

Das entscheidet nicht der Parser — das entscheidet der **C#-Compiler selbst**, und zwar sichtbar. Wird ein Primary-Constructor-Parameter irgendwo im Methodenkörper verwendet (nicht nur in einem Feld-/Property-Initialisierer), erzeugt der Compiler intern ein verstecktes Feld namens `<logger>P`. Wird der Parameter dagegen nie oder nur im Initialisierer benutzt, entsteht kein solches Feld — der Wert wird direkt verdrahtet, es gibt keinen Speicherort, der geteilt werden könnte.

```csharp
private static void CreateCapturedParameterField(...)
{
    var captureFieldName = $"<{parameter.Name}>P";
    var isCaptured = typeSymbol.GetMembers()
        .OfType<IFieldSymbol>()
        .Any(field => field is { IsImplicitlyDeclared: true, AssociatedSymbol: null }
                      && field.Name == captureFieldName);

    if (!isCaptured) { return; }   // nichts zu teilen → kein Field-Element
    ...
}
```

`CreateCapturedParameterField` fragt also nicht "wird `logger` irgendwo verwendet?", sondern *fragt den Compiler*, ob er dafür schon ein Feld angelegt hat. Das ist der zuverlässige Weg — der Parser müsste sonst selbst jeden Methodenkörper der Klasse nach Referenzen auf den Parameter durchsuchen.

Wichtig: `AssociatedSymbol: null` grenzt das vom *anderen* Fall ab, in dem ein Parameter ebenfalls ein implizites Feld erzeugt — einer Record-Positionsproperty. Deren Backing-Field hat die Property als `AssociatedSymbol` und wird hier bewusst ausgeschlossen, weil dort schon die Property selbst (Abschnitt 06) das Element ist.

**Warum ein Field, keine eigene Konstruktor-Elternschaft.** Das neue Feld-Element hängt als **Kind des Typs**, nicht als Kind des Konstruktors. Grund: `MemberRole` stuft Konstruktoren als Lifecycle-Member ein, die aus dem "eigentlichen" Member-Graphen herausgehalten werden — wäre das Feld ein Kind des Konstruktors, würde es mit ihm verschwinden. Genau das würde die Kohäsions-Messung wieder kaputtmachen, die dieser ganze Umbau erst ermöglicht (Abschnitt 07).

## 05 Woher kommt das Symbol für den Field-Fall überhaupt?

Der `case ParameterSyntax`-Zweig (Abschnitt 06) hat mit dem Feld-Fall nichts zu tun — er sucht ausschließlich nach einer Positional-Property und liefert für eine `class` immer `null`. Das Symbol für `logger`, das als Anker für das `Field`-Element dient, kommt auf einem ganz anderen Weg:

```csharp
private void CreatePrimaryConstructorElement(INamedTypeSymbol typeSymbol, SyntaxNode node, CodeElement typeElement)
{
    var primaryConstructor = typeSymbol.InstanceConstructors.FirstOrDefault(...);
    foreach (var parameter in primaryConstructor.Parameters)   // ← direkt vom Symbol, keine Syntax nötig
    {
        CreateCapturedParameterField(typeSymbol, parameter, typeElement);
    }
}
```

`primaryConstructor` ist bereits ein fertiges `IMethodSymbol`. Ein Methodensymbol trägt seine Parameter als `.Parameters`-Property direkt mit sich — man muss dafür keinen `ParameterSyntax`-Knoten im Baum finden. Das ist "symbol-first" statt "syntax-first": die meisten `case`-Zweige holen das Symbol *aus* einem gerade besuchten Syntax-Knoten (`GetDeclaredSymbol(node)`); hier läuft es umgekehrt.

Wenn später beim Body-Walk von `Start()` das Identifier `logger` besucht wird, fragt Phase 2 ganz normal `semanticModel.GetSymbolInfo(identifierSyntax).Symbol` — das liefert technisch eine andere `IParameterSymbol`-Instanz, aber dasselbe logische Symbol. Der Abgleich gelingt über `Key()` (siehe Grundlagen oben), nicht über Objektidentität und nicht über einen gemeinsam besuchten Syntax-Knoten.

## 06 Und bei einem Record?

> **Warum nicht einfach über `case PropertyDeclarationSyntax`?** Weil es für eine rein positionale Record-Property **gar keinen** `PropertyDeclarationSyntax`-Knoten im Baum gibt — nirgends. Der Compiler synthetisiert nur das Symbol (`IPropertySymbol`), fügt aber keinen entsprechenden Syntax-Knoten in den geparsten Baum ein. Der einzige Ort im ganzen Quelltext, an dem überhaupt etwas von dieser Property steht, ist die Parameterliste selbst — der `ParameterSyntax`-Knoten `Id`. Der reguläre Weg hat also schlicht nichts, worauf er treffen könnte; deshalb der Umweg über `case ParameterSyntax` und die Rückwärtssuche unten. (Wichtig für später: eine Kante auf `order.Id` zeigt danach auf das **Property**-Element, nicht auf einen Parameter — `order.Id` löst über normale Member-Auflösung zum Property-Symbol auf.)

Bei `record Order(int Id, Money Total)` greift derselbe `case ParameterSyntax`-Zweig, aber diesmal *findet* `FindPositionalProperty` etwas. Der Weg ist ungewöhnlich, weil Roslyn keine direkte Route "Parameter → zugehörige Property" anbietet — er geht rückwärts: gesucht wird die Property des Typs, deren *eigene* Deklarationssyntax zufällig genau dieser Parameterknoten ist.

```csharp
return parameterSymbol.ContainingType?
    .GetMembers(parameterSymbol.Name)
    .OfType<IPropertySymbol>()
    .FirstOrDefault(property => property.DeclaringSyntaxReferences
        .Any(reference => reference.GetSyntax() == parameterSyntax));
```

Das löst nebenbei den Sonderfall elegant: Schreibt man die Property in einem Record explizit selbst aus —

```csharp
record Order(int Id) { public int Id { get; init; } = Id; }
```

— dann ist die Deklarationssyntax dieser Property die `PropertyDeclarationSyntax`, nicht der Parameter. `FindPositionalProperty` findet dafür nichts, gibt `null` zurück, und die ganz normale `case PropertyDeclarationSyntax`-Verzweigung erzeugt das Element stattdessen — ohne dass irgendjemand diesen Fall extra behandeln musste. Es entsteht **keine doppelte Property**: `case PropertyDeclarationSyntax` trifft für die ausgeschriebene Zeile, `case ParameterSyntax` geht für dieselbe Property bewusst ins Leere (getestet in `AWrittenOutProperty_IsNotCreatedTwice`, [PositionalRecordMemberParseTests.cs](../../Tests/UnitTests/Parser/PositionalRecordMemberParseTests.cs)).

Umgekehrt gilt: `case PropertyDeclarationSyntax` wird bei einer rein positionalen Record-Property (ohne Ausschreiben) **nie** getroffen, weil es dafür überhaupt keinen `PropertyDeclarationSyntax`-Knoten im geparsten Baum gibt — der einzige Ort im Quelltext, an dem etwas von dieser Property steht, ist die Parameterliste selbst. Genau deshalb musste `case ParameterSyntax` überhaupt eingeführt werden.

Bei einer `class` mit Primary Constructor wird `case PropertyDeclarationSyntax` ebenfalls nie getroffen — aber aus einem anderen Grund: Eine `class`/`struct` synthetisiert für ihre Primary-Constructor-Parameter grundsätzlich keine Property (nur `record` tut das), es gibt also gar keine Property, die man treffen könnte.

## 07 Warum das mehr ist als Vollständigkeit: die Kohäsions-Metrik

Der eigentliche Auslöser für diesen Umbau war nicht "der Graph sollte vollständig sein", sondern ein konkret falsches Messergebnis. Die Kohäsions-Metrik zählt, wie viele Member-Gruppen (Partitionen) eine Klasse hat, indem sie schaut, welche Member sich gemeinsamen Zustand teilen. Ohne Feld-Element für `logger` sehen `Start` und `Stop` so aus, als hätten sie **nichts** gemeinsam — eine Klasse, die exakt gleich aufgebaut ist wie eine mit ausgeschriebenem Feld, wurde als schlecht kohäsiv gemeldet, nur weil C# 12 kürzer schreibt.

```
OHNE Feld-Element (vorher)              MIT Feld-Element (jetzt)
──────────────────────────              ─────────────────────────
Service                                 Service
 ├─ Start   ┐ kein gemeinsamer           ├─ Start ──Uses──┐
 └─ Stop    ┘ Knoten                     └─ Stop  ──Uses──┴─▶ Field "logger"
             ⇒ 2 Partitionen                                  ⇒ 1 Partition, korrekt kohäsiv
```

Dieselbe Klasse, zweimal gemessen. Ohne Feld-Element haben `Start` und `Stop` keinen gemeinsamen Knoten und erscheinen als zwei unabhängige Partitionen. Mit dem neuen `Field`-Element für `logger` zeigen beide Methoden per `Uses`-Kante darauf — genau die Verbindung, die eine handgeschriebene `private readonly ILogger _logger`-Variante schon immer hatte. Getestet in `TheCSharp12FormAndTheWrittenOutForm_AgreeOnCohesion` ([CapturedPrimaryConstructorParameterParseTests.cs](../../Tests/UnitTests/Parser/CapturedPrimaryConstructorParameterParseTests.cs)).

## 08 Phase 2: Wer zieht die `Uses`-Kante?

Phase 1 (`HierarchyAnalyzer`) hat jetzt die drei Elemente. Die Kanten zwischen ihnen zieht wie immer Phase 2 (`RelationshipAnalyzer`, aufgeteilt in `DeclarationAnalyzer` und `SyntaxNodeAnalyzer`). Drei Stellen sind dafür relevant — und die erste davon musste gar nicht neu geschrieben werden:

### 08.1 Parametertypen: der bestehende Mechanismus greift von selbst

`AnalyzeMethodRelationships` lief schon immer für **jedes** `IMethodSymbol`-Element und zieht für jeden Parameter eine `Uses`-Kante auf dessen Typ:

```csharp
foreach (var parameter in methodSymbol.Parameters)
{
    _builder.AddTypeRelationship(methodElement, parameter.Type, RelationshipType.Uses, methodLocation);
}
```

Weil der Primary Constructor jetzt ein ganz normales `Method`-Element ist, läuft er automatisch durch dieselbe Funktion — ohne dass hier irgendetwas primary-constructor-spezifisch geändert werden musste. Das ist auch der Grund, warum die alte Spezialbehandlung `AnalyzePrimaryConstructorParameters` (die die Parametertypen blind, unabhängig von tatsächlicher Verwendung, auf das *Typ*-Element anheftete) komplett gelöscht werden konnte: Der allgemeine Weg deckt den Fall jetzt sauberer ab.

### 08.2 Base-Aufrufe: `AnalyzePrimaryConstructorBody`

Ein Primary Constructor hat keinen Methodenkörper zum Ablaufen — aber er kann eine Basisklasse mit Argumenten aufrufen: `class Derived(int n) : Base(Helper.Scale(n))`. Die Methodenanalyse erkennt an der Deklarationssyntax, dass es sich um eine Typdeklaration statt eines echten Bodys handelt, und wechselt auf einen eingeschränkten Pfad, der nur die `BaseList`-Argumente abläuft — alles andere aus der Typdeklaration zu übernehmen würde jedem anderen Member der Klasse fälschlich den Konstruktor als "Aufrufer" zuschreiben.

### 08.3 Verwendung im Methodenkörper: der neue Zweig in `AnalyzeIdentifier`

Das ist die Kante aus Abschnitt 07. Trifft der normale Identifier-Walk (der jedes `logger` in `logger.Log(...)` ohnehin schon besucht) auf ein Symbol, das ein `IParameterSymbol` ist *und* dafür ein Graph-Element existiert, entsteht eine `Uses`-Kante vom aufrufenden Member zu genau diesem Feld-Element:

```csharp
else if (symbol is IParameterSymbol parameterSymbol &&
         _builder.FindInternalCodeElement(parameterSymbol) is { } capturedParameter)
{
    _builder.AddRelationship(sourceElement, RelationshipType.Uses, capturedParameter, ...);
}
```

Die Bedingung `FindInternalCodeElement(...) is not null` ist bewusst **ohne** Rückfall auf den umschließenden Typ formuliert. Für einen gewöhnlichen Methodenparameter oder eine lokale Variable gibt es kein Element — dieser Zweig feuert für sie schlicht nicht, und aus jeder normalen Parameterverwendung im ganzen Codebestand entsteht keine neue Kante.

## 09 Aufräumen: `IsExplicitConstructor` verschwindet

Vor diesem Umbau gab es an vier Stellen dieselbe Frage, doppelt gestellt: `constructor.IsExplicitConstructor() && _builder.FindInternalCodeElement(constructor) is not null`. `IsExplicitConstructor()` war reine Handarbeit, um genau die Konstruktoren auszuschließen, die kein Element hatten — Primary Constructors eben. Jetzt, wo ein Primary Constructor ein Element *hat*, ist diese Vorprüfung redundant: `FindInternalCodeElement(...) is not null` beantwortet die eigentliche Frage ("ist das im Graphen?") allein und korrekt für beide Fälle. Die Hilfsmethode wurde ersatzlos entfernt.

Sichtbarer Effekt an der Oberfläche: `new Service(x)` zieht jetzt eine `Calls Service..ctor`-Kante neben der `Creates Service`-Kante — genau wie es die ausgeschriebene Langform schon immer tat.

## In drei Sätzen

1. **Ein Auslöser, drei Kinder — aber zwei unabhängige Wege.** Sobald die Typdeklaration ihr `Class`-/`Record`-Element bekommt, hängt `CreatePrimaryConstructorElement` synchron den Konstruktor und — pro tatsächlich vom Compiler eingefangenem Parameter — ein `Field`-Element an, direkt vom Symbol aus. Die Record-Property dagegen kommt über die ganz normale Rekursion und den eigens dafür eingeführten `case ParameterSyntax`.
2. **Der Compiler verrät die Antwort.** Ob ein Parameter "gefangen" ist, wird nicht geraten, sondern am vom Compiler selbst erzeugten `<name>P`-Feld abgelesen; bei Records liest `FindPositionalProperty` die zugehörige Property rückwärts über deren Deklarationssyntax aus.
3. **Phase 2 profitiert doppelt.** Weil der Konstruktor jetzt ein ganz normales `Method`-Element ist, laufen die längst vorhandenen generischen Mechanismen (Parametertypen, Konstruktor-Aufrufe) automatisch mit — nur die *Verwendung* des gefangenen Parameters im Methodenkörper brauchte einen eigenen, engen Zweig in `AnalyzeIdentifier`.
