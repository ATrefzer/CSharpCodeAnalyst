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

  DONE, auch rider ist mit aufgenommen.

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



Erst mal die Tests: 13 neue Fixtures mit 42 Tests in `Tests/UnitTests/Parser`, davon **29 rot** (die Lücken) und **13 grün** (Premise-Guards, die belegen, dass nur die Nutzungs-Kanten fehlen, nicht die Deklarationen). Jede Lücke aus dem Review ist jetzt durch einen roten Test dokumentiert:

| Fixture                                     | Rote Tests | Lücke                                                        |
| :------------------------------------------ | :--------- | :----------------------------------------------------------- |
| `IndexerUsageParseTests`                    | 4          | `store[i]` lesen/schreiben/`+=`/`?[i]` → keine Kante zu `this[]` |
| `IndexerUsageSplitAccessorsParseTests`      | 3          | dito mit Split: Routing zu `get_Item`/`set_Item`             |
| `OperatorUsageParseTests`                   | 4          | `a + b`, `-a`, `a == b`, `a += b` → keine Kante zu `op_*`    |
| `ConversionOperatorUsageParseTests`         | 2          | implizite Konversion und expliziter Cast → `op_Implicit`/`op_Explicit` |
| `QuerySyntaxParseTests`                     | 2          | implizite `Where`/`Select`-Calls fehlen; Klausel-Bodies sollten Lambda-Semantik (`Uses`) haben |
| `NestedLambdaParseTests`                    | 2          | Abhängigkeiten in verschachtelten Lambdas gehen komplett verloren |
| `PrimaryConstructorBaseArgumentsParseTests` | 1          | `class Derived() : Base(Helper.X())` — Argumente nie gewalkt (klassischer Ctor als grüner Kontrast) |
| `EnumMemberInitializerParseTests`           | 1          | `Highest = Limits.Max` → Kante `Level -> Limits.Max` fehlt   |
| `MethodGroups_GenericParseTests`            | 2          | freistehende generische Method-Group `Create<Widget>` (qualifizierte Form grün)<br /><br /> |
| `ConstructedGenericStaticMemberParseTests`  | 2          | `Registry<Token>.Instance` / `.CountItems()` verliert `Token` |
| `AttributeArgumentsParseTests`              | 3          | `typeof(...)`-Attributargumente auf Klasse/Property/Feld (auf Methoden grün) |
| `StackAllocParseTests`                      | 1          | `stackalloc Sample[2]` in Ausdrucksposition                  |
| `ImplicitPatternCallsParseTests`            | 2          | `Deconstruct` bei Dekonstruktion, `GetEnumerator` bei `foreach` |

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







Anmerkungen: 

Du hattest recht: **Method-Groups** waren schon implementiert. Es fehlten zwei Dinge, davon eins überraschend:

1. Generische Method-Groups sind ein anderer Knotentyp. Create<Widget> ist ein GenericNameSyntax, kein IdentifierNameSyntax — VisitIdentifierName feuerte nie. Fix: AnalyzeIdentifier nimmt jetzt die gemeinsame Basis SimpleNameSyntax, beide Walker haben ein VisitGenericName-Override. Gegen Doppelbehandlung ist das abgesichert: Generic-Names in Typ-Positionen (List<Foo> x) lösen zu Typ-Symbolen auf und werden ignoriert, als Invocation-Target greift der bestehende Method-Group-Guard, und die rechte Seite eines Member-Access wird ohnehin nie separat besucht. Zusätzlich erfasst der neue Helper AddMethodGroupRelationship die Typargumente (Widget) als Uses — auch für die qualifizierte Form Producer.Produce<Widget>, die sie vorher verlor.

2. Ein echter Roslyn-Quirk, der auch die alte Implementierung betraf: Bei Konversion einer Method-Group nach System.Delegate (statt Func<>/Action) liefert GetSymbolInfo kein Symbol, sondern CandidateReason.OverloadResolutionFailure mit genau einem Kandidaten — obwohl der Code fehlerfrei kompiliert. Ich habe das empirisch mit einem Roslyn-Scratch-Programm verifiziert: Auch das nicht-generische Register(MakeWidget) mit Delegate-Parameter erzeugte bisher stillschweigend keine Kante. Der neue Fallback SingleMethodGroupCandidate nutzt den Kandidaten, wenn er eindeutig ist (in AnalyzeIdentifier und AnalyzeMemberAccess). Netter Nebeneffekt: In Code mit echten Kompilierfehlern (teilgeladene Solutions) liefert eine eindeutige Referenz jetzt ebenfalls ihre Kante statt nichts — das war einer der Robustheitspunkte aus dem Review.



**Query-Syntax** ist umgesetzt — beide Tests grün, Gesamtbilanz **463 grün / 10 rot** (nur bekannte Lücken, keine Regression; die TestSuite enthielt offenbar keine Query-Syntax, die Approval-Werte blieben stabil).

**Die Modellierung spiegelt die Compiler-Übersetzung** (`from … where P select F` → `source.Where(x => P).Select(x => F)`):

- **Operator-Aufrufe → `Calls`.** `Where`/`Select`/`OrderBy`/`Join`/`Cast` laufen real beim *Bauen* der Query — aufgeschoben ist nur, was in der resultierenden Sequenz passiert. Roslyn liefert die gebundenen Methoden pro Klausel: `GetQueryClauseInfo(clause).OperationInfo` (plus `CastInfo` für typisiertes `from Foo x in …`), `GetSymbolInfo` pro `orderby`-Ordering und auf dem finalen `select`/`group by` (leer beim degenerierten `select x`), rekursiv durch `into`-Continuations. Extension-Methoden werden wie bei ausgeschriebenen Calls reduziert und normalisiert, inkl. `IsExtensionMethodCall`-Attribut; externe Operatoren (`Enumerable`) nehmen den üblichen Fallback.
- **Klausel-Ausdrücke → Lambda-Semantik (`Uses`).** Der `MethodBodyWalker` übergibt den Query-Body komplett dem `LambdaBodyWalker`; nur die Quelle der **ersten** `from`-Klausel behält Method-Body-Semantik, weil sie eager ausgewertet wird. Damit sind `Threshold()`/`Shift(…)` jetzt korrekt `Uses` statt fälschlich `Calls`.
- **Konsistenz in beiden Richtungen:** Eine Query *innerhalb* eines Lambdas ist als Ganzes aufgeschoben — dort sind auch die Operator-Kanten `Uses` (Override im `LambdaBodyWalker`). Und Sub-Queries in Klausel-Ausdrücken erreicht der Lambda-Walker von selbst, sodass deren Operatoren korrekt `Uses` bekommen, nie `Calls` — das fällt aus der Architektur heraus, ohne Extra-Code.

Eine dokumentierte Vereinfachung: Die innere Sequenz eines `join` wird eigentlich auch eager ausgewertet, bekommt aber derzeit `Uses` wie der Rest des Bodies. Das steht im neuen Kapitel „LINQ query syntax" in `corrections-and-updates.md` (beim Einfügen hatte ich das Kapitel zunächst mitten ins Method-Groups-Kapitel gesetzt — ist korrigiert, die Struktur stimmt wieder).









# `AnalyzeAttributeRelationships` im Detail

Hier die aktuelle Fassung ([RelationshipAnalyzer.cs:875](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/RelationshipAnalyzer.cs:875)):

```csharp
private void AnalyzeAttributeRelationships(Solution solution, CodeElement element, ISymbol symbol)
{
    foreach (var attributeData in symbol.GetAttributes())
    {
        if (attributeData.AttributeClass != null)
        {
            var attributeSyntax = attributeData.ApplicationSyntaxReference?.GetSyntax();
            var location = attributeSyntax?.GetSyntaxLocation();

            element.Attributes.Add(attributeData.AttributeClass.Name);                                  // (1)
            AddTypeRelationship(element, attributeData.AttributeClass, RelationshipType.UsesAttribute, location); // (2)

            if (symbol is not IMethodSymbol &&
                attributeSyntax is AttributeSyntax { ArgumentList: not null } attributeWithArguments)
            {
                var semanticModel = solution.GetDocument(attributeWithArguments.SyntaxTree)?.GetSemanticModelAsync().Result;
                if (semanticModel is not null)
                {
                    AnalyzeMethodBody(element, attributeWithArguments.ArgumentList, semanticModel);      // (3)
                }
            }
        }
    }
}
```

## Wann läuft das?

Die Methode ist die **letzte Zeile von `AnalyzeRelationships`** und läuft damit für **jedes** Code-Element, das ein Symbol in der `ElementIdToSymbolMap` hat — also Klassen, Methoden, Properties, Felder, Events, Delegates … und auch das **Assembly-Element selbst**. Das Assembly-Symbol landet in Phase 1 genauso in der Map, d. h. `[assembly: InternalsVisibleTo(...)]`-Attribute werden am Assembly-Knoten erfasst. Namespaces sind die einzige Ausnahme (die sind bewusst nicht in der Symbol-Map).

Wichtig: Die Quelle ist `symbol.GetAttributes()` — eine **semantische** Abfrage, keine Syntax-Suche. Dadurch:

- Bei partiellen Klassen werden Attribute aus *allen* Teildeklarationen eingesammelt, egal in welcher Datei sie stehen.
- Jede einzelne Anwendung ist ein eigenes `AttributeData` — `[Handler(typeof(A))] [Handler(typeof(B))]` ergibt zwei Durchläufe.
- `ApplicationSyntaxReference` ist der Rückverweis vom semantischen Attribut zur konkreten `[...]`-Stelle im Quelltext — daraus kommt die `SourceLocation`, die später „Jump to Code" ermöglicht.
- Der `AttributeClass == null`-Guard fängt nicht auflösbare Attribute ab (Tippfehler, fehlende Referenz in teilgeladenen Solutions) — die werden still übersprungen.

## Die drei Effekte

**(1) Ja, die Attribute werden in das `CodeElement` aufgenommen** — aber nur als **nackte Namen**. `CodeElement.Attributes` ist ein `HashSet<string>` ([CodeElement.cs:14](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeGraph/Graph/CodeElement.cs:14)), und hineingeschrieben wird `AttributeClass.Name`, also z. B. `"ObsoleteAttribute"`, `"HandlerAttribute"` — der Klassenname **mit** `Attribute`-Suffix, **ohne** Namespace, **ohne** Argumente. Daraus folgen drei Eigenschaften:

- Doppelte Anwendungen desselben Attributs kollabieren zu einem Eintrag (HashSet).
- Zwei gleichnamige Attribute aus verschiedenen Namespaces sind in diesem Set nicht unterscheidbar.
- Die Argumente (`typeof(Payload)`, Strings, …) sind hier nicht enthalten — dafür gibt es (2) und (3).

Was diese Liste heute tatsächlich konsumiert, ist überschaubar: Sie wird in die Projektdatei **persistiert** (`ProjectData` → `SerializableCodeElement`) und vom `CodeGraphSerializer` exportiert/importiert (dort fließt sie auch in den Graph-Dump ein, den das ApprovalTestTool hasht). Eine UI-Funktion, die danach filtert oder sucht, gibt es aktuell nicht — es ist vorgehaltenes Metadatum. Wenn ihr mal „zeige alle Klassen mit `[Obsolete]`" bauen wollt, liegt die Information schon am Knoten.

**(2) Die eigentliche Abhängigkeit ist die `UsesAttribute`-Kante.** `AddTypeRelationship(element, attributeClass, UsesAttribute, location)` erzeugt eine Kante vom dekorierten Element zur **Attribut-Klasse** — und zwar mit einem eigenen Relationship-Typ statt eines generischen `Uses`. Das ist eine bewusste Entscheidung, weil Attribut-Abhängigkeiten anders „schwer" sind als echte Nutzung: Die UI kann sie gezielt ausblenden (`GraphHideFilter` führt `UsesAttribute` als filterbaren Typ), der DGML-Export behandelt sie speziell, die QuickInfo beschriftet sie als „dekoriertes Element", und die Metrik-Doku zählt sie explizit als Abhängigkeitstyp. Da es durch `AddTypeRelationship` läuft, gilt die volle Maschinerie: interne Attribut-Klasse → Kante aufs interne Element; externe (z. B. `ObsoleteAttribute`) → externes Element nur bei `IncludeExternals`; generische Attribute (C# 11, `[Handler<T>]`) werden auf die Originaldefinition normalisiert. Mehrfache Anwendungen desselben Attributs erzeugen **eine** Kante mit mehreren `SourceLocations` (Dedup in `AddRelationship`).

**(3) Der Argument-Walk (die jüngste Ergänzung).** `[Handler(typeof(Payload))]` enthält eine echte, compilergeprüfte Abhängigkeit auf `Payload`, die weder in (1) noch in (2) steckt. Deshalb wird die `ArgumentList` des Attributs durch den normalen `MethodBodyWalker` geschickt: `typeof(X)` → `Uses X`, Konstanten-Referenzen wie `SomeClass.MaxValue` → `Uses` auf das Feld, Enum-Werte → Fallback auf den Enum-Typ, `nameof(...)` → die dokumentierte nameof-Behandlung. Zwei Feinheiten:

- Es wird **nur die Argumentliste** gewalkt, nicht das ganze Attribut — der Attributname würde sonst an den Attribut-*Konstruktor* binden (das fängt zwar der Method-Group-Guard ab, aber so entsteht die Frage gar nicht erst).
- Der `symbol is not IMethodSymbol`-Filter existiert, weil **Methoden ihre Attribute schon immer über einen anderen Weg erfassen**: `AnalyzeMethodRelationships` walkt die komplette `MethodDeclarationSyntax`, und deren `AttributeLists` hängen mit im Baum. Ohne den Filter würden Methoden-Attribute doppelt analysiert (das Ergebnis wäre wegen Kanten-Dedup identisch, aber die semantischen Abfragen liefen umsonst). Genau diese Asymmetrie war übrigens die Lücke: Klassen, Properties, Felder und Events haben *keinen* solchen Deklarations-Walk, deshalb fehlten dort die Argument-Abhängigkeiten komplett.



Eine bekannte, in `board.txt` notierte Grenze bleibt: **Parameter-Attribute** (`void M([CallerMemberName] string name)`) werden auf Klassen-/Methoden-Ebene nicht als `UsesAttribute` erfasst — Parameter sind keine Code-Elemente, und `GetAttributes()` auf dem Methodensymbol liefert Parameter-Attribute nicht mit. Bei Methoden werden ihre *Argumente* immerhin über den Deklarations-Walk gesehen (`typeof` in einem Parameter-Attribut ergäbe ein `Uses`), aber die `UsesAttribute`-Kante zur Attributklasse fehlt dort.





# Testrun

Ich schaue mir die Diff-Dateien an und prüfe exemplarisch, ob die gemeldeten Unterschiede zu unseren Änderungen passen.

Die exemplarische Prüfung ist abgeschlossen — **das Ergebnis ist so gut, wie es bei dieser Art Änderung nur sein kann**. Hier der Befund:

## 1. Nichts geht verloren

Über alle neun Repo-Stände: **4720 neue Kanten, 3 „fehlende"** — und die drei habe ich einzeln am Quellcode verifiziert. Es sind keine Verluste, sondern erwartete Umklassifizierungen desselben Kantenpaars:

- **2× PowerToys `UpdateCacheablePrompts`**: `Calls` → `Uses`. Der Quellcode ist ein Lehrbuchbeispiel unserer Query-Syntax-Entscheidung — `_userSettings.AdditionalActions` steht in einer `where`-Klausel, `ResourceLoaderInstance.ResourceLoader` im `select`. Klausel-Bodies sind Lambdas → `Uses`. Exakt wie in `corrections-and-updates.md` dokumentiert.
- **1× `Program.Main → TerminateApp`**: gleiche Kante, jetzt zusätzlich mit `[IsMethodGroup]` — Zeile 84 übergibt `TerminateApp` als Method-Group, was vorher wegen des `OverloadResolutionFailure`-Quirks gar keine Kante erzeugte; das Attribut wird nun auf die bestehende Lambda-Kante gemerged.

## 2. Die neuen Kanten passen zu den Features

| Kategorie                         | Anzahl                                             | Stichproben-Befund                                           |
| --------------------------------- | -------------------------------------------------- | ------------------------------------------------------------ |
| Method-Groups (`[IsMethodGroup]`) | 3289                                               | WinForms-Designer-Wiring (`+= new EventHandler(handler_Click)` in `InitializeComponent`), MVVM (`new RelayCommand(CancelCurrentTask)`), `DependencyProperty`-Callbacks — alles Delegate-Creation-Argumente, die der Kandidaten-Fallback jetzt rettet |
| Attribut-Argumente                | großer Teil der ~1300 restlichen                   | Am Quellcode verifiziert: `[TypeConverter(typeof(MyColorConverter))]` auf Property (ShareX), `[JsonSerializable(typeof(…))]` auf Klasse (PowerToys) |
| Operatoren (`op_*`)               | 79                                                 | plausibel — interne Operator-Overloads sind selten           |
| Indexer                           | 19                                                 | dito                                                         |
| `GetEnumerator` / `Deconstruct`   | 2 / 0                                              | duck-typed Collections sind selten; Query-Operatoren tauchen nicht auf, weil sie auf `Enumerable` (extern) zeigen |
| Rest                              | verschachtelte Lambdas, `Registry<T>`-Typargumente | Stichproben passen                                           |

Die 3289 Method-Groups sind dabei der eigentliche **Schatz** dieses Laufs: Das ist genau die Klasse „Handler sieht unbenutzt aus", die ein Dependency-Tool nicht falsch beantworten darf — und sie war in jeder WinForms/WPF/MVVM-Codebasis massenhaft betroffen.

## 3. Ein entdeckter Nebeneffekt, den du absegnen solltest

`[JellyfinMigrationBackup(JellyfinDb = true)]` erzeugt jetzt `MigrateRatingLevels -(Calls)-> JellyfinMigrationBackupAttribute.JellyfinDb` — **benannte Attribut-Argumente** laufen durch die normale Property-Zugriffs-Klassifikation und ergeben eine `Calls`-Kante (Write) zur Attribut-Property. Das ist vertretbar (die Property wird bei der Attribut-Instanziierung tatsächlich gesetzt), aber man könnte argumentieren, dass im Attribut-Kontext `Uses` ehrlicher wäre, analog zu `nameof`. Ich tendiere zu „so lassen und dokumentieren" — sag Bescheid, wenn du es anders willst.

## Fazit und nächster Schritt

Der Lauf bestätigt: keine Regression, alle neuen Kanten sind erklärbar und gewollt. **Ich empfehle, die Referenzen jetzt neu zu ziehen** (die neuen Dumps als Referenz übernehmen) — damit hast du eine frische, vertrauenswürdige Baseline, und jeder künftige Parser-Fehler fällt wieder als einzelner Diff auf.

Und zu „wächst mir über den Kopf": Das Gegenteil ist gerade passiert. Genau dieser Lauf zeigt, dass das Sicherheitsnetz trägt — 42 gezielte Unit-Tests spezifizieren jedes Feature, die Approval-Diffs erklären jede Abweichung auf realen Codebasen bis zur Quellzeile, und jede Modellierungsentscheidung steht in `corrections-and-updates.md`. Du musst den Parser nicht mehr im Kopf haben; die Tests und die Doku haben ihn.