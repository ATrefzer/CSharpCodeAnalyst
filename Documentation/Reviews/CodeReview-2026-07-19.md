# Code Review 2026-07-19

Drittes Review der Assemblies `CSharpCodeAnalyst.CodeParser` und `CSharpCodeAnalyst.CodeGraph`. Gleiche Fragestellung wie beim letzten Mal: Fehler (insbesondere falsch geparste C#-Syntax) und Vereinfachungspotenzial. Alle Punkte unter 1. wurden mit kleinen Repro-Snippets gegen `Parser.ParseSourceAsync` **praktisch verifiziert** (die Snippets stehen jeweils dabei); die Punkte unter 2./3. sind Code-Lektüre.

Vorweg: Die Punkte aus dem Review vom 11.07. sind sauber umgesetzt — die Walker-Vereinheitlichung, Indexer, Operatoren/Konversionen, Query-Syntax, verschachtelte Lambdas, Projekt-Whitelist, TFM-Ranking und der Split des `RelationshipAnalyzer` in `DeclarationAnalyzer` / `SyntaxNodeAnalyzer` / `RelationshipBuilder` sind genau so herausgekommen, wie man es sich wünscht. Die neue Struktur ist deutlich besser lesbar; die Fehler unten sind fast alle Altbestand, den erst die neuen gezielten Tests sichtbar machen.

------

## 1. Fehler (verifiziert)

### a) Member-Implements-Kanten fehlen bei generischen Interfaces — die größte Lücke

```csharp
public interface IHandler<T> { void Handle(T item); }
public class Item { }
public class ItemHandler : IHandler<Item> { public void Handle(Item item) { } }   // geschlossen
public class GenHandler<T> : IHandler<T>  { public void Handle(T item) { } }      // offen
public interface IPlain { void Run(); }
public class PlainImpl : IPlain { public void Run() { } }                          // Kontrollgruppe
```

Ergebnis des Parsers:

```
Implements: ItemHandler -> IHandler          ✓ (Typ-Ebene)
Implements: GenHandler -> IHandler           ✓
Implements: PlainImpl -> IPlain              ✓
Implements: PlainImpl.Run -> IPlain.Run      ✓
Implements: ItemHandler.Handle -> IHandler.Handle   ✗ FEHLT
Implements: GenHandler.Handle -> IHandler.Handle    ✗ FEHLT
```

Sobald das Interface generisch ist, fehlen **alle** Member-Implements-Kanten — bei geschlossener wie offener Implementierung. Zwei unabhängige Ursachen:

1. **Key-Mismatch in der Interface-Map.** [HierarchyAnalyzer.cs:90](../../CSharpCodeAnalyst.CodeParser/Parser/HierarchyAnalyzer.cs) befüllt `BuildInterfaceImplementations` mit `interfaceSymbol.Key()` aus `AllInterfaces` — dort stehen aber die **konstruierten** Interfaces (`IHandler<Item>`, Key `…<Item>`). Nachgeschlagen wird in `DeclarationAnalyzer.FindTypesImplementingInterface` mit dem Key der **Definition** (`IHandler<T>`, Key `…<T>`). Für geschlossene Implementierungen findet der Lookup darum nie einen Kandidaten. Fix: beim Befüllen `interfaceSymbol.OriginalDefinition.Key()` verwenden. (Der Fehler ist Altbestand — schon der lineare Scan vor der C1-Optimierung verglich `i.Key() == interfaceKey` unnormalisiert.)

2. **Roslyn-Falle in `FindImplementationForInterfaceMember`.** [DeclarationAnalyzer.cs:320](../../CSharpCodeAnalyst.CodeParser/Parser/DeclarationAnalyzer.cs) übergibt das Member-Symbol der Interface-**Definition** (`IHandler<T>.Handle`). Roslyns `FindImplementationForInterfaceMember` verlangt aber das Member des **konstruierten** Interfaces, das der Typ tatsächlich implementiert (`IHandler<Item>.Handle` bzw. `IHandler<T_GenHandler>.Handle`) — mit dem Definitionssymbol kommt `null` zurück. Darum scheitert auch die offene Form (`GenHandler<T> : IHandler<T>`), obwohl dort der Map-Key zufällig passt. Fix: im implementierenden Typ das konstruierte Interface mit passender `OriginalDefinition` aus `AllInterfaces` heraussuchen, das korrespondierende Member (gleiche `OriginalDefinition`) nehmen und **damit** `FindImplementationForInterfaceMember` aufrufen.

**Auswirkung:** `IValidator<T>`, `IRepository<T>`, `IHandler<T>` & Co. sind in realem Code allgegenwärtig. Ohne die Member-Kante findet „Find implementations" auf dem Interface-Member nichts, und `FollowIncomingCallsHeuristically` kann virtuelle Aufrufe über generische Interfaces nicht verfolgen — die Aufrufkette reißt kommentarlos ab. Betrifft Methoden, Properties (inkl. Split-Accessoren via `AnalyzeAccessorAbstractions`) und Events gleichermaßen, da alle über `AddImplementationsForInterfaceMember` laufen.

**Warum haben die Tests nie angeschlagen?** Die TestSuite enthält **kein einziges generisches Interface** (`grep "interface \w+<"` ist leer). `GenericsParseTests` hat mit `StringValidator : IValidator<string>` zwar den Fall im Snippet, prüft aber keine Implements-Kanten; `ObjectOriented_InterfacesParseTests` prüft Implements nur für nicht-generische Interfaces. Wie bei den Indexern im letzten Review: rote Tests zuerst.

DONE — Tests: `GenericInterfaceImplementsParseTests` (inkl. Property-Member und Doppel-Konstruktion `IHandler<A>, IHandler<B>`). Fix: Map-Key auf `OriginalDefinition.Key()` normalisiert; Auflösung über das konstruierte Interface aus `AllInterfaces`. Der Key-basierte Vergleich deckt auch den Cross-Compilation-Fall ab — `FindCorrespondingSymbol`/`FindCompilation` wurden als toter Code entfernt. Dokumentiert in `corrections-and-updates.md`.

### b) Partielle Methoden: der Body wird nur analysiert, wenn der Implementierungsteil zuerst gesehen wird

```csharp
public partial class Worker
{
    public partial void Hook();                       // Definitionsteil
    public void Caller() { Hook(); }
}
public partial class Worker
{
    public partial void Hook() { var i = new Item(); } // Implementierungsteil
}
```

Steht der Definitionsteil zuerst (Datei-/Deklarationsreihenfolge!), fehlen `Worker.Hook -> Item` (Uses **und** Creates) komplett; dreht man die Reihenfolge um, sind sie da. Ursache: Definitions- und Implementierungsteil einer partiellen Methode sind in Roslyn **zwei verschiedene `IMethodSymbol`s** mit identischem `Key()`. Phase 1 speichert in `GetOrCreateCodeElement` das zuerst gesehene Symbol (das zweite löst nur die Trace-Warnung „Found element with multiple symbols" aus — die feuert hier also längst, nur schaut niemand hin). Phase 2 walkt ausschließlich `DeclaringSyntaxReferences` des gespeicherten Symbols — und die des Definitionsteils enthalten keinen Body. Zusätzlich entfallen die Source-Metriken (`CollectSourceMetrics` → `HasBody` = false).

Das ist **reihenfolgeabhängig und damit nicht deterministisch** gegenüber der Dateiaufteilung. Besonders relevant mit `IncludeGeneratedCode`: bei `[GeneratedRegex]`, `[RelayCommand]` & Co. schreibt der Nutzer den Definitionsteil, der Generator den Body — die Nutzerdateien werden zuerst verarbeitet, der generierte Body also systematisch verworfen. Partielle Properties (C# 13) haben dasselbe Problem.

Fix-Skizze: in Phase 1 bei `IMethodSymbol` mit `PartialImplementationPart != null` das Implementierungssymbol speichern — oder in Phase 2 zusätzlich die `DeclaringSyntaxReferences` von `PartialImplementationPart` walken.

DONE — Tests: `PartialMemberBodyParseTests` (beide Reihenfolgen, plus partielle Property). Fix: Phase 2 walkt beide Teile (`GetDeclaringSyntaxReferencesIncludingPartial`, Methoden und Properties); die Source-Metriken messen den Implementierungsteil. Partielle Events (C# 14) sind bewusst offen. Dokumentiert in `corrections-and-updates.md`.

### c) Default-Interface-Methoden erzeugen eine Implements-Selbstkante

```csharp
public interface IGreeter { void Greet() { } }   // DIM
public class Greeter : IGreeter { }
```

Ergebnis: `Implements: IGreeter.Greet -> IGreeter.Greet` — das Member „implementiert sich selbst". Roslyns `FindImplementationForInterfaceMember` liefert für Klassen, die die Default-Implementierung erben, das Interface-Member selbst zurück; `AddImplementationsForInterfaceMember` legt daraus ungeprüft eine Kante an. Die Zyklenerkennung bleibt verschont (Method↔Method-Implements filtert der `RelationshipClassifier`), aber der Graph ist verschmutzt: „Find specializations" auf dem DIM-Member liefert das Member selbst. Fix: `implementingElement == element` (bzw. Symbol-Gleichheit) ausschließen.

DONE — Tests: `DefaultInterfaceMethodParseTests` (Selbstkante weg, überschreibende Implementierung und DIM-Body-Analyse als Guards). Fix: Guard in `AddImplementationsForInterfaceMember`. Dokumentiert in `corrections-and-updates.md`.

### d) Das FullName-Format kippt global, sobald irgendeine Assembly den globalen Namespace nutzt

```
Nur Typen in Namespaces:        FullName = "InMemory.Demo.Foo"
+ ein Typ im globalen Namespace: FullName = "InMemory.global.Demo.Foo"   (ALLE Elemente!)
```

`Parser.InsertGlobalNamespaceIfUsed` fügt den `global`-Namespace **allen** Assemblies ein, sobald **eine** Assembly Inhalte direkt unter der Wurzel hat — und `CodeElement.MoveTo` schreibt dabei per `GetFullPath()` die FullNames des gesamten Teilbaums um. Damit hängt das Format jedes einzelnen `FullName` im Graphen davon ab, ob irgendwo in der Solution ein Top-Level-Statement-Projekt oder ein Typ ohne Namespace existiert. Konsequenz: Wer Architekturregeln oder Baselines gegen `FullName` schreibt (`ALLOW InMemory.Demo.** -> …`, `BaselineGenerator` erzeugt genau solche Zeilen), dessen Regeln matchen **alle** nicht mehr, sobald das erste solche Projekt dazukommt — oder das letzte verschwindet. Kein Parser-Bug im engen Sinn, aber eine stille Sollbruchstelle.

Vorschlag: das Format deterministisch machen — entweder den `global`-Namespace immer einfügen oder ihn nie in den FullName aufnehmen (`GetFullPath(omitGlobalNamespace: true)` existiert bereits). Beides ist ein Breaking Change für bestehende Regeldateien und gehört dokumentiert.

**Priorisierung:** (a) zuerst — dort verwaisen in Phase 1 sauber angelegte Elemente systematisch, exakt das Muster der Indexer/Operator-Lücken aus dem letzten Review. Danach (b), (d), (c). Gemäß CLAUDE.md gehört zu jedem Fix ein Kapitel in `Documentation/Roslyn/corrections-and-updates.md` (keiner der vier Punkte ist dort als bewusste Entscheidung dokumentiert — ich habe es geprüft) und, wie beim letzten Mal, vorab ein roter Test. Der TestSuite fehlt außerdem generell ein Modul mit generischen Interfaces.

------

## 2. Kleinere Parser-Beobachtungen (Code-Lektüre, nicht einzeln verifiziert)

- **Implizite benutzerdefinierte Konversionen** werden an den Positionen Initialisierer, Zuweisungs-Rechtsseite, `return`, Argument und Expression-Body erfasst — nicht aber bei `yield return 21.5;`, in Collection-/Array-Initialisierern (`new List<Celsius> { 21.5 }`), in Tupel-Literalen und in den Zweigen des `?:`-Operators. Wenn man die Konversions-Kanten ernst nimmt, sind das die nächsten Kandidaten.
- **`GetAwaiter`** (custom awaitables) ist als letzter offener Punkt der „impliziten Pattern-Aufrufe" aus dem letzten Review übrig (Deconstruct und GetEnumerator sind erledigt).
- **`AnalyzeInvocation` ignoriert `CandidateSymbols`.** `AnalyzeIdentifier`/`AnalyzeMemberAccess` retten mit `SingleMethodGroupCandidate` den Ein-Kandidaten-Fall — Invocations tun das nicht. In teilweise kompilierenden Solutions (der Normalfall bei fremden Repos) fällt ein Call mit eindeutigem Kandidaten komplett aus.
- **`WarnIfCodeElementHasMultipleSymbols`** greift mit `_elementIdToSymbolMap[existingElement.Id]` direkt zu. Aktuell sicher, aber die Invariante „alles in `_symbolKeyToElementMap` außer Namespaces ist auch in `_elementIdToSymbolMap`" ist implizit — die Accessor-Elemente verletzen sie bereits (bewusst, nur erreicht sie niemand über `GetOrCreateCodeElement`). Ein `TryGetValue` kostet nichts.
- **`SendParserPhase2Progress`**: schlägt das `CompareExchange` fehl, geht das Update verloren (kein Retry). Kosmetisch.
- `AnalyzeRelationships` gibt weiterhin ein synchron erfülltes `Task` zurück (bekannt aus Review 2, akzeptiert).

------

## 3. CodeGraph-Assembly

Die Assembly ist insgesamt in gutem Zustand; die Algorithmen habe ich gegen ihre Definitionen geprüft (PageRank inkl. Dangling-Umverteilung, Blast-Radius/Propagation-Cost als BFS ohne Startknoten, Eades-Lin-Smyth inkl. der Stale-Queue-Wächter, die MDL-Entropie inkl. der dokumentierten Degenerationsfälle) und **keine fachlichen Fehler** gefunden. Auch der `CodeGraphExplorer` (Kontext-/Forbidden-Set-Logik von `FollowIncomingCallsHeuristically`) hält seiner Dokumentation stand. Konkrete Punkte:

**a) Zyklengruppen teilen `Relationship`-Instanzen mit dem Master-Graphen.** `CodeGraphBuilder.GenerateDetailedCodeGraph` klont die Elemente (`IntegrateCodeElementFromOriginal` → `CloneSimple`), fügt aber die **Original**-Relationships ein ([CodeGraphBuilder.cs:83](../../CSharpCodeAnalyst.CodeGraph/Algorithms/Cycles/CodeGraphBuilder.cs)). `Relationship` ist veränderlich (`Attributes`, `SourceLocations`) — eine Mutation aus der Zyklen-Ansicht heraus träfe den geparsten Graphen. `CodeGraphExtensions.Clone` macht es richtig (klont die Kanten); die beiden Wege sollten sich einig werden.

**b) `RelationshipBuilder.AddRelationship`: linearer Scan unter dem globalen Lock — obwohl ein O(1)-Lookup bereitliegt.** `CodeElement.Relationships` ist inzwischen ein `HashSet<Relationship>`, dessen Gleichheit exakt das Tripel (Source, Target, Type) ist. Der `FirstOrDefault`-Scan ([RelationshipBuilder.cs:49](../../CSharpCodeAnalyst.CodeParser/Parser/RelationshipBuilder.cs)) kann durch `Relationships.TryGetValue(probe, out existing)` ersetzt werden — das entschärft nebenbei den in Review 2 notierten Serialisierungspunkt von Phase 2, ohne am Locking etwas zu ändern.

**c) Tarjan ist rekursiv.** Die DFS-Rekursionstiefe entspricht dem längsten Pfad im Suchgraphen; der enthält alle Member. Bei sehr großen Solutions (lange Call-Ketten) droht ein `StackOverflowException`, gegen die man sich nicht wehren kann. Eine iterative Variante (expliziter Stack) wäre die robuste Form — betrifft `Tarjan.FindStronglyConnectedComponents`, das auch `SystemMetricsAnalysis.CalculateCyclicity` nutzt (dort nur Typ-Ebene, unkritischer).

**d) Kleinigkeiten:**

- `DgmlFileBuilder.IsNonPrintableCharacter` erklärt jedes Nicht-ASCII-Zeichen (`> 127`) für nicht druckbar — ein Element `Größe` bekommt das Label `Cryptic_Größe`. Gemeint war vermutlich echte Steuerzeichen.
- `CodeGraph.Nodes` ist ein öffentliches Feld; ein Property wäre konsistent mit dem Rest.
- `InsertGlobalNamespaceIfUsed` ruft `MoveTo` pro Assembly-Kind auf, und jedes `MoveTo` schreibt per DFS die FullNames des **gesamten** neuen Teilbaums neu — quadratisch bei vielen Kindern. Einmal am Ende reicht.
- `CodeGraph.DeleteRelationships` ist O(n·m) (`RemoveWhere(relationships.Contains)` mit einer Liste); ein `HashSet` als Parameter genügt.
- `PlantUmlExport.SanitizeName(node.Name, false)` verstümmelt Anzeigenamen (`List<T>` → `List_T_`), obwohl sie ohnehin in Anführungszeichen stehen — nur der Alias braucht die Ersetzungen.
- Ein deutscher Kommentar in [CodeGraphExplorer.cs:826](../../CSharpCodeAnalyst.CodeGraph/Exploration/CodeGraphExplorer.cs) („Nur in die Queue werfen, …") — der Rest der Datei ist englisch.
- `IncompleteLogicException` wird nirgends mehr geworfen — kann weg.

------

**TL;DR:** Vier verifizierte Fehler, alle im Parser-Umfeld: (a) Member-Implements-Kanten fehlen bei **allen** generischen Interfaces (Key-Mismatch in der Phase-1-Map **plus** falsches Symbol an Roslyns `FindImplementationForInterfaceMember` — die TestSuite enthält kein einziges generisches Interface, darum unbemerkt); (b) bei partiellen Methoden wird der Body nur analysiert, wenn der Implementierungsteil zufällig zuerst kommt — reihenfolgeabhängiger Verlust ganzer Abhängigkeitssätze, systematisch bei Source-Generatoren; (c) Default-Interface-Methoden bekommen eine Implements-Selbstkante; (d) das FullName-Format aller Elemente kippt, sobald die erste Assembly den globalen Namespace nutzt — stille Sollbruchstelle für Architekturregeln und Baselines. Die CodeGraph-Assembly ist fachlich sauber; dort lohnen sich die geteilten Relationship-Instanzen der Zyklengruppen, der O(1)-Lookup in `AddRelationship` und ein iterativer Tarjan.
