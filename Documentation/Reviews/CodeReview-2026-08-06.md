# Code Review 2026-08-06

Review fertig — ich habe `DeadCodeAnalysis`, `DeadCodeFinding`, die Helfer (`AccessLevel`, `RelationshipTypeExtensions`), den Parser-Kontext (`DeclarationAnalyzer`, `ExternalContractStore`) und die Tests gelesen. Vorab das Gesamturteil: Der Ansatz ist solide und die zentralen Entscheidungen (Subtree-Regel, Liveness-Propagation statt Kanten-Zählung, kein Kaskadieren, zweifarbige Referenzmengen) halte ich für richtig. Viel vereinfachen würde ich nicht — die Datei ist lang, weil sie gut dokumentiert ist, nicht weil der Algorithmus kompliziert wäre. Es gibt aber ein paar echte Schwachstellen.

## False Positives (bzw. falsches High-Confidence)

**1. ViewModels mit externer Basisklasse — der wahrscheinlichste echte Treffer in der Praxis.** `FindBindingSources` ([DeadCodeAnalysis.cs:284](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeGraph/Algorithms/DeadCode/DeadCodeAnalysis.cs:284)) erkennt INPC nur über den external contract des `PropertyChanged`-Members und verteilt das über `Inherits`-Kanten nach unten. Ein ViewModel, das von `ObservableObject` (CommunityToolkit.Mvvm), `BindableBase` (Prism) oder `DependencyObject` erbt, hat *kein eigenes* `PropertyChanged`-Member — die Basisklasse ist extern und unsichtbar. Dessen gebundene public Properties bekommen dann **High** confidence, obwohl XAML sie liest. Der Kommentar (Zeile 280–282) kennt das Problem, aber angesichts der Verbreitung von CommunityToolkit ist das die gefährlichste Lücke. Der saubere Fix liegt im Parser, nicht hier: `DeclarationAnalyzer` hat das Symbol und könnte `type.AllInterfaces` auf `INotifyPropertyChanged` prüfen und das im `ExternalContractStore` (oder einem eigenen Set) ablegen — dann ist es egal, wo im Vererbungsbaum die Implementierung sitzt.

=> DONE

**2. Ein Test-Attribut in einer Produktions-Assembly vergiftet assemblyübergreifend.** `FindTestAssemblies` behauptet, Tests in der Produktions-Assembly seien „the harmless direction to fail in" (Zeile 193–194). Das stimmt nur nach innen (Findings dort werden unterdrückt). Nach außen nicht: Ist Assembly P als Test-Assembly klassifiziert, zählen *alle* ausgehenden Kanten von P nicht mehr für `FromProduction`. Ein Element in Assembly B, das nur von P benutzt wird, wird fälschlich als `UsedOnlyByTests` gemeldet, obwohl es echte Produktionsnutzung ist. Dazu kommt: Das Matching ist case-insensitive auf dem bloßen Namen — ein domäneneigenes Attribut namens `Test` (A/B-Tests, Prüfstand …) reicht, um eine ganze Produktions-Assembly umzuklassifizieren. Ordinal-case-sensitive plus ggf. Prüfung, ob das Attribut extern ist, würde das Risiko deutlich senken.

=> DONE (Jetzt pro Klasse)

**3. Finalizer.** `~C()` wird als Methode `Finalize` geparst ([HierarchyAnalyzer.cs:331](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/HierarchyAnalyzer.cs:331)), und die Override-Erfassung im `DeclarationAnalyzer` hängt an `IsOverride` — das ist für Source-Destruktoren `false`, also gibt es weder eine `Overrides`-Kante noch einen external contract. Niemand referenziert einen Finalizer je im Code → in einer `internal` Klasse ein **High**-confidence-FP. Analog zu `.cctor` gehört `Finalize` in `IsEntryPoint` ([DeadCodeAnalysis.cs:766](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeGraph/Algorithms/DeadCode/DeadCodeAnalysis.cs:766)).

=> DONE

**4. Confidence-Verwässerung durch `Attributed`.** `CallerOutsideTheGraph` enthält `Attributed`, und die Hints werden über den ganzen Subtree gesammelt. Ein ungenutztes privates Member mit `[Obsolete]`, `[DebuggerDisplay]` oder `[ExcludeFromCodeCoverage]` — eigentlich ein besonders guter Fund — landet auf **Low**. Kein FP im engeren Sinn, aber die Konfidenz misst hier nicht mehr das, was sie behauptet. Eine kleine Ignorier-Liste harmloser Attribute (die nachweislich keinen externen Aufrufer implizieren) würde die Trennschärfe der Stufen spürbar verbessern.

=> DONE

## False Negatives

**5. CHA statt RTA bei der Contract-Propagation.** `PropagateContractUsage` markiert *alle* Implementierungen eines benutzten Contract-Members als lebendig — auch die einer Klasse, die nirgends instanziiert wird (`Creates`/`Uses` fehlen komplett). Das ist klassische Class Hierarchy Analysis; die Verfeinerung Richtung Rapid Type Analysis wäre: Liveness nur zu Implementierungen propagieren, deren Typ selbst irgendwo referenziert/erzeugt wird. Konservativ falsch ist für ein Dead-Code-Tool die richtige Richtung, aber es ist der größte systematische FN-Topf neben den dokumentierten Inseln.

**6. Der Interface-\*Typ\* stirbt nie, solange ein (toter) Implementierer existiert.** `class C : IFoo` ist eine gewöhnliche Referenzkante (Quelle ist ein Typ, `IsPolymorphicEdge` greift nicht) — die hält `IFoo` am Leben, auch wenn C selbst tot ist. Ergebnis: Man sieht `C` und `IFoo.Bar` als Findings, aber nie `IFoo` selbst. Das ist konsistent mit dem Nicht-Kaskadieren, widerspricht aber dem Satz im Header „A contract that is never called dies together with its implementations" (Zeile 22) — der gilt nur auf Member-Ebene.

## Dokumentationswiderspruch (sollte korrigiert werden)

Der Klassen-Header sagt in Zeile 23–24: externe Contracts → „the implementation is **assumed alive**". `IsReported` (Zeile 558–560) und die Tests (`Calculate_ExternalContractFromTheStore_...`) sagen das Gegenteil: Das Member wird **gemeldet**, nur mit Hint und Low confidence. Das Verhalten ist das richtige (melden statt verschweigen), der Header ist falsch. Da diese Klasse ihre Doku sehr ernst nimmt, ist das ein echter Bug.

Kleinigkeit in derselben Ecke: `external` wird aus zwei Quellen gefüllt — der Store liefert Kurznamen (`INotifyPropertyChanged.PropertyChanged`, siehe [DeclarationAnalyzer.cs:416](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/DeclarationAnalyzer.cs:416)), der Kanten-Fallback `target.FullName` (mit Namespace). Der `StartsWith("INotifyPropertyChanged.")`-Check in `FindBindingSources` funktioniert nur mit dem Kurzformat. Beim C#-Parser gewinnt der Store immer (`TryAdd`), für Importer-Graphen ohne Store läuft die Binding-Erkennung über den Fallback aber ins Leere.

=> DONE

## Vereinfachung — mein Fazit

Der Algorithmus selbst ist bereits nah am Minimum für das, was er leisten soll. Die beiden Entscheidungen, die ihn einfach *halten* — kein Kaskadieren und die Subtree-Regel statt Erreichbarkeitsanalyse — sind genau richtig und gut begründet. Was ich mechanisch aufräumen würde:

- **`CollectEdges` mit fünf Out-Parametern** ist die unschönste Stelle. Ein `private sealed record CollectedEdges(...)` als Rückgabewert (analog zum bestehenden `AnalysisContext`) würde `Calculate` deutlich entschlacken, inklusive des zweistufigen Befüllens von `external`.
- **Drei Walks über dieselben Kanten** (`Mark` ×2 plus `CollectTestReferences`). Zusammenlegen geht, kostet aber Lesbarkeit — die drei kleinen Funktionen sind jede für sich trivial. Wenn überhaupt, würde ich nur `CollectTestReferences` nachgelagert einschränken: Test-Aufrufer nur für Elemente sammeln, die nicht in `FromProduction` sind — das spart Speicher und Arbeit, weil heute Aufruferlisten für massenhaft lebendige Elemente aufgebaut werden, die nie jemand abfragt.
- `IsBindable`/`IsSerializedProperty` als benannte Einzeiler über `IsPublicPropertyOf` würde ich lassen — das ist Doku, keine Redundanz.

Wenn ich priorisieren müsste: (1) den Header-Widerspruch fixen, (2) INPC-Erkennung in den Parser ziehen (`AllInterfaces`), (3) `Finalize` als Entry Point, (4) Test-Attribut-Matching härten. Punkt 5 (RTA) wäre das nächste größere Genauigkeits-Feature, kein Bugfix. Sag Bescheid, welche davon ich umsetzen soll.

=> DONE Nach 1 war weitere Vereinfachung möglich.



# Fazit

Festgehalten — CHA bleibt, die RTA-Verfeinerung ist bewusst verworfen. Die Begründung steht jetzt an zwei Stellen, damit sie nicht verloren geht: als Absatz in der Doku von `PropagateContractUsage` in [DeadCodeAnalysis.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeGraph/Algorithms/DeadCode/DeadCodeAnalysis.cs) („deliberately every implementation … the refinement would report precisely the code it cannot see being created") und als neuer Punkt unter „Was der Algorithmus bewusst *nicht* tut" in [dead-code-algorithm.md](https://claude.ai/epitaxy/Documentation/Design/dead-code-algorithm.md), neben Kaskade und toten Zyklen — wo solche Entscheidungen in diesem Projekt hingehören. Nur Kommentar- und Markdown-Änderungen, kein Codeverhalten betroffen.

Damit ist das Review vom Anfang der Session vollständig abgearbeitet:

| Punkt                                                  | Ausgang                                                      |
| ------------------------------------------------------ | ------------------------------------------------------------ |
| Header-Widerspruch (external contract „assumed alive") | Korrigiert                                                   |
| ViewModels mit externer Basisklasse (INPC)             | `NotifyingTypes` im Parser, `FindBindingSources` komplett entfallen |
| Test-Attribut-Härtung                                  | Pro Typ statt pro Assembly, Ordinal-Matching                 |
| Finalizer / statische Konstruktoren                    | Beide gedroppt (`IsRuntimeInvoked`) statt annotiert          |
| `Attributed`-Verwässerung                              | `ToolingAttributes`-Ignorierliste                            |
| CHA→RTA                                                | Bewusst verworfen (DI-Container), dokumentiert               |

Als bekannte, bewusst offene Themen bleiben nur die von früher: Inseln/tote Zyklen und die DataTemplate-Lücke bei Bindings ohne INPC.
