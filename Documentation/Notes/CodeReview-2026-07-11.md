# Code Review 2026-07-11

## 1. Fehler

**a) `LambdaBodyWalker` verschluckt Argumente bei Aufrufen lokaler Funktionen** — [LambdaBodyWalker.cs:63](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/LambdaBodyWalker.cs:63) Bei `MethodKind.LocalFunction` wird mit `return` die ganze Visit-Methode verlassen, **bevor** `base.VisitInvocationExpression(node)` läuft. Bei `x => LocalFunc(new Foo())` wird damit auch `new Foo()` nie besucht — die Abhängigkeit geht verloren. Der `MethodBodyWalker` macht es richtig: dort sitzt der Early-Return im Handler (`AnalyzeInvocation`), der Walker ruft `base` trotzdem auf. Fix: nur das Anlegen der Relationship überspringen, nicht die Traversierung.

DONE

**b) TFM-Ranking: `net472` schlägt `net48`** — [ProjectSelector.cs:134](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/ProjectSelector.cs:134) Die Regex `^net(\d)(\d)(\d?)$` macht aus `net472` → `Version(4, 72)` und aus `net48` → `Version(4, 8)`. Da 72 > 8, gewinnt bei einem Multi-Target `net472;net48` das falsche (ältere) Framework. Korrekt wäre `4.7.2` vs. `4.8`. Praktisch nur relevant, wenn ein Projekt ausschließlich alte Frameworks multi-targeted, aber das Ergebnis ist dann deterministisch falsch.

DONE

**c) Plausibilitätscheck ist tote Logik** — [CodeGraphPlausibilityChecks.cs:31](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/CodeGraphPlausibilityChecks.cs:31) `node.SourceLocations.Select(...).ToHashSet()` dedupliziert bereits — das anschließende `if (!hash.Add(location))` kann darum **nie** fehlschlagen. Der Check "Duplicate location found" feuert nie, egal wie viele Duplikate es gibt. Das `.ToHashSet()` muss weg (oder man vergleicht `list.Count != set.Count`). Betrifft nur DEBUG, aber die Prüfung, die du dir dort versprichst, existiert faktisch nicht.

DONE

**d) `IsUnrecognizedProject` ist case-sensitiv** — [HierarchyAnalyzer.cs:545](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/HierarchyAnalyzer.cs:545) `unrecognized.Contains(ext)` matcht `.Vbproj` nicht. Außerdem ist die Blacklist unvollständig (`.sqlproj`, `.esproj`, `.wapproj`, `.shproj`, `.dcproj` …). Robuster wäre eine Whitelist: alles außer `.csproj` ablehnen (der Adhoc-Pfad nutzt bewusst `"InMemory.csproj"`, bliebe also intakt). Nebenbei: die Liste wird bei jedem Aufruf neu alloziert — `static readonly` reicht.

DONE

**e) Kleinigkeiten**

- `RelationshipAnalyzer` ist nicht wiederverwendbar, sieht aber öffentlich so aus: `_externalCodeElementCache` und `_lastProgress` werden in `AnalyzeRelationships` nie zurückgesetzt. Ein zweiter Aufruf auf derselben Instanz würde externe Elemente des ersten Graphen in den zweiten leaken. `Parser` erzeugt zwar immer eine frische Instanz, aber die API erlaubt den Missbrauch.
- `AnalyzeRelationships` gibt `Task` zurück, läuft aber komplett synchron (inkl. `GetSemanticModelAsync().Result` überall). Das ist kein Deadlock-Risiko (Roslyn nutzt intern `ConfigureAwait(false)`), aber die Signatur verspricht Asynchronität, die es nicht gibt — der Aufrufer blockiert seinen Thread für die gesamte Phase 2.
- [Initializer.cs:39](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/Initializer.cs:39): Der Fallback kennt nur VS 2022 **Professional** — Community/Enterprise/BuildTools-Installationen scheitern, wenn `RegisterDefaults()` vorher schon geworfen hat.

Die Thread-Sicherheit von Phase 2 habe ich gezielt geprüft und **keine** Race gefunden: alle Relationship-Mutationen (auch die Cross-Element-Fälle wie `AddImplementationsForInterfaceMember`, wo das Source-Element ein fremdes Element ist) laufen über das eine globale `_lock`; `element.Attributes` wird nur vom Thread des jeweiligen Elements angefasst; der External-Cache lockt selbst.

------

## 2. Nicht erfasste C#-Syntax / fehlende Abhängigkeiten

Sortiert nach Relevanz. Die dokumentierten, bewussten Entscheidungen (nameof, Lambdas als Uses, implizite Ctors) habe ich gegen `corrections-and-updates.md` abgeglichen und hier ausgelassen.

**a) Indexer-Zugriffe (`obj[i]`, `obj?[i]`) — die größte Lücke.** Kein Walker behandelt `ElementAccessExpressionSyntax` / `ElementBindingExpressionSyntax`. `GetSymbolInfo` darauf würde das Indexer-`IPropertySymbol` liefern, aber es wird nie abgefragt. Ergebnis: Indexer werden in Phase 1 sauber als Elemente angelegt (inkl. Overload-Unterscheidung im `Key()`!), ihre Bodies werden analysiert — aber **kein einziger Aufrufer bekommt je eine Kante zum Indexer**. Interne Indexer erscheinen im Graphen als unbenutzt. Pikant: der Doku-Kommentar in [PropertyAccessClassifier.cs:53](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/PropertyAccessClassifier.cs:53) nennt `ElementAccessExpressionSyntax` explizit als erwarteten Input — der Classifier ist dafür vorbereitet (Read/Write/ReadWrite würde sofort funktionieren), es wurde nur nie verdrahtet. Die TestSuite enthält auch keinen Indexer-*Zugriff* (`this[` kommt dort nicht vor), darum haben die Approval-Tests es nie gezeigt.

**b) Benutzerdefinierte Operatoren und Konversionen.** `OperatorDeclarationSyntax`/`ConversionOperatorDeclarationSyntax` werden in Phase 1 als Methoden angelegt — aber ihre **Verwendung** erzeugt keine Kante: `a + b`, `a == b`, `!a`, implizite Konversionen (`Foo f = bar;`) und explizite Casts rufen `GetSymbolInfo` auf dem Operator-Ausdruck nie ab. `VisitBinaryExpression` behandelt nur `is`/`as`. Beim Cast wird immerhin der Zieltyp erfasst, die Operator-Methode nicht. Wie die Indexer erscheinen Operatoren damit stets als unbenutzt. (Für Klassen mit Operator-Overloads — z. B. Vektor-Typen — fehlen dadurch echte Kopplungen.)

**c) LINQ-Query-Syntax.** `from x in xs where P(x) select F(x)`: Die Ausdrücke *innerhalb* der Klauseln werden erfasst (Identifier/MemberAccess-Traversierung), aber (1) die impliziten `Where`/`Select`/`Join`-Aufrufe nicht (bei `IncludeExternals` fehlt die Uses-Kante zu `Enumerable`), und (2) die Klausel-Bodies sind semantisch Lambdas, laufen aber durch den `MethodBodyWalker` — sie bekommen `Calls` statt der Lambda-Semantik `Uses`. Inkonsistent zu eurer dokumentierten Lambda-Philosophie.

**d) Verschachtelte Lambdas werden komplett verworfen** — [LambdaBodyWalker.cs:114](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/LambdaBodyWalker.cs:114) Ist in der Tabelle in `SyntaxWalkerBase` als „skipped(!)" dokumentiert, aber m. E. eher ein akzeptierter Bug als eine Modellierungsentscheidung: Bei `x => y => Foo(y)` gehen die Abhängigkeiten des inneren Lambdas ersatzlos verloren. Die Uses-Semantik des äußeren Walkers würde für innere Lambdas genauso gelten — einfach weiter mit demselben Walker traversieren wäre konsistenter. Kommt bei `Func<T, Func<U>>`-Factories und Fluent-APIs real vor.

**e) Primary-Constructor-Basisaufruf-Argumente.** `class Foo() : Base(Helper.Create())` — die Argumentliste im `PrimaryConstructorBaseTypeSyntax` wird nie gewalkt (Typ-Deklarationen haben keinen Body-Walk). `Helper.Create` fehlt. Parameter-*Typen* des Primary Constructors sind abgedeckt (`AnalyzePrimaryConstructorParameters`), die Argumente nicht.

**f) Enum-Member-Initialisierer.** `enum E { A = Other.Const }` — `EnumMemberDeclarationSyntax` wird in Phase 1 nicht behandelt (bewusst, Enum-Member sind keine Elemente), aber dadurch wird der Initialisierer auch in Phase 2 nie gewalkt: Kante `E → Other` fehlt.

**g) Generische Method-Groups als freistehender Ausdruck.** `Func<int> f = GetValue<int>;` — `GenericNameSyntax` ist kein `IdentifierNameSyntax`, `VisitIdentifierName` feuert nicht, kein Override für `VisitGenericName`. Die Uses/IsMethodGroup-Kante fehlt. Die Member-Access-Form `obj.GetValue<int>` ist dagegen abgedeckt.

**h) Typargumente bei statischem Zugriff auf konstruierte Generics.** `Foo<Bar>.StaticMethod()` / `Foo<Bar>.Instance` — die Calls-Kante zu `Foo<T>.StaticMethod` entsteht (Normalisierung), aber `Bar` geht verloren: der `GenericNameSyntax`-Teil wird nur bis zum Identifier `Bar` traversiert, und `AnalyzeIdentifier` ignoriert Typ-Symbole. Bei `new Foo<Bar>()` und Felddeklarationen ist `Bar` dagegen abgedeckt (dort läuft es über `AddNamedTypeRelationship`, das TypeArguments walkt).

**i) Kleinere Lücken:**

- Attribut-Argumente auf **Typ-Ebene**: `[Foo(typeof(Bar))] class C` verliert `Bar` (auf Methoden/Properties wird es erfasst, weil dort die ganze Deklaration gewalkt wird — inkonsistent).
- `stackalloc Foo[n]` — Elementtyp nicht erfasst (Arrays via `new Foo[n]` sind abgedeckt).
- Function-Pointer-Signaturen (`delegate*<...>`) — im Code als bewusst offen kommentiert.
- Implizite Pattern-Aufrufe: `Deconstruct` bei `var (a, b) = x`, `GetEnumerator` bei `foreach` über eigene Typen, `GetAwaiter`. Der Typ selbst ist meist über andere Kanten erfasst, die Methoden nicht.
- `symbolInfo.CandidateSymbols` wird nirgends genutzt — in Code mit Compile-Fehlern (teilweise geladene Solutions!) fällt die Abhängigkeit dann komplett aus, obwohl Roslyn Kandidaten liefert. Für ein Analyse-Tool, das auch unvollständige Solutions parst, wäre der beste Kandidat besser als nichts.

Mein Vorschlag zur Priorisierung: (a) und (b) beheben — beides Fälle, in denen in Phase 1 angelegte Elemente systematisch verwaist sind; (d) und (e) danach. Und gemäß eurer eigenen CLAUDE.md-Regel gehört jede dieser Entscheidungen dann als Kapitel in `corrections-and-updates.md`.

------

## 3. Lesbarkeit / Struktur

Vorweg: Der Code ist deutlich besser dokumentiert als der Durchschnitt — die XML-Kommentare erklären das *Warum* (Roslyn-Fallen, Modellierungsentscheidungen), die Tabelle in `SyntaxWalkerBase` und die Auslagerung von `PropertyAccessClassifier`/`ProjectSelector` als reine, testbare Klassen sind genau das richtige Muster. Das Problem ist konzentriert in einer Datei.

**`RelationshipAnalyzer` (~1500 Zeilen) macht drei Jobs.** Er ist gleichzeitig:

1. **Orchestrator** (paralleler Loop, Progress, Global Statements) — ~150 Zeilen
2. **Deklarations-Analyse** (Signaturen, Vererbung, Interface-Implementierungen, Property-Abstraktionen) — ~500 Zeilen
3. **`ISyntaxNodeHandler`-Implementierung** (Body-Analyse) plus **Relationship-Store** (`AddRelationship`, Lookup-/Fallback-Kaskade, External-Cache-Anbindung) — der Rest

Der lohnendste Schnitt ist der **Relationship-Store**: eine Klasse, die `_codeGraph`, `_artifacts`, `_lock` und den External-Cache besitzt und `AddRelationship` + die Fallback-Kaskade (`TryFindInternalElementWithNormalization` → ContainingType → External) anbietet. Das hat drei Effekte auf einmal:

- Die nullable Felder `_artifacts?`/`_codeGraph?` mit den `!`-Zugriffen verschwinden — der Store bekommt sie im Konstruktor, die temporale Kopplung („erst `AnalyzeRelationships` aufrufen, sonst NRE") ist weg. Da der Analyzer ohnehin pro Parse neu erzeugt wird, kostet das nichts.
- Die gesamte Locking-Story liegt an einem Ort statt über die Datei verteilt.
- Die Deklarations-Analyse (Punkt 2) kann in eine eigene Klasse, die nur noch den Store benutzt.

**Weitere konkrete Punkte:**

- **`AddTypeRelationshipPublic` / `AddSymbolRelationshipPublic`**: Das `Public`-Suffix dokumentiert eine Verlegenheit. Die Methoden sind Teil von `ISyntaxNodeHandler` — auf dem Interface heißen sie sinnvollerweise einfach `AddTypeRelationship`/`AddSymbolRelationship`; die private Methode dahinter kann der Analyzer nennen, wie er will.
- **Die beiden Walker teilen viel Logik mit subtilen Abweichungen.** `LambdaBodyWalker.VisitInvocationExpression` ist eine handkopierte Variante von `AnalyzeInvocation` (minus Events, plus Uses) — genau dort ist Bug 1a entstanden. `AnalyzeIdentifier`/`AnalyzeMemberAccess` zeigen das bessere Muster (ein `RelationshipType`-Parameter). Dasselbe für Invocation und ObjectCreation, und `LambdaBodyWalker` schrumpft auf die drei „skip nested lambda"-Overrides.
- **`DetermineCallAttributes` + `AnalyzeMemberAccessCallType`** (~70 Zeilen, fast pur statisch) → eigene statische Klasse `CallAttributeClassifier`, analog zu `PropertyAccessClassifier`. Direkt unit-testbar.
- **`ISyntaxNodeHandler`** ist mit 13 Methoden breit und mischt zwei Abstraktionsebenen („analysiere dieses Syntax-Konstrukt" vs. „füge diese Kante hinzu"). Nach der Walker-Vereinheitlichung fallen die Add*-Methoden vermutlich ganz raus.
- **Auskommentierten Code entfernen**: `DebugRelationship` (leerer Body mit auskommentiertem `Debugger.Break`), der auskommentierte Extension-Method-Block in `AnalyzeMethodRelationships`, diverse `//Trace.WriteLine`. Git erinnert sich.
- **`HierarchyAnalyzer`** (560 Zeilen) finde ich dagegen unkritisch: der große Switch in `ProcessNodeForHierarchy` ist tabellenartig und gut kommentiert — den würde ich so lassen. Nur `IsUnrecognizedProject` (siehe 1d) und der deutsche Satzfetzen im Kommentar Zeile 254 („z. B.") fallen auf.
- **Performance-Fußnote** für große Solutions: `AddRelationship` macht einen linearen Scan über `source.Relationships` unter dem **einen globalen Lock** — das ist der Serialisierungspunkt der gesamten Phase 2. Ein `HashSet`/Dictionary pro Element (Key: TargetId+Type) oder feiner granulares Locking wäre der erste Hebel, falls Phase 2 je zu langsam wird.

**TL;DR:** Zwei echte kleine Bugs (Lambda-Argumente bei lokalen Funktionen, net472/net48-Ranking) plus ein toter Debug-Check; die zwei relevantesten Erfassungslücken sind Indexer-Zugriffe und Operator-Verwendungen — beides Konstrukte, deren Deklarationen ihr sauber modelliert, deren Nutzung aber nie ankommt. Strukturell würde ich nur den `RelationshipAnalyzer` aufteilen (Relationship-Store herauslösen, Walker vereinheitlichen); der Rest der Assembly ist gut geschnitten und überdurchschnittlich dokumentiert.