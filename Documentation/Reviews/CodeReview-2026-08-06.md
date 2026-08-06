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

--------------------

Verstanden — Punkt 1 = die INPC-Erkennung für ViewModels mit externer Basisklasse, und die Header-Korrektur bleibt drin. Ich schaue mir zuerst an, wie der `ExternalContractStore` durch Parser, Persistenz und Analyzer fließt, um die sauberste Stelle für das neue Typ-Level-Faktum zu finden.

Build und Tests:

Punkt 1 ist umgesetzt: ViewModels mit externer INPC-Basisklasse (`ObservableObject`, `BindableBase`, …) werden jetzt als Binding-Quellen erkannt, ihre public Properties landen nicht mehr auf High. Alle 773 Tests grün.

So funktioniert es:

- **Parser** ([DeclarationAnalyzer.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeParser/Parser/DeclarationAnalyzer.cs)): Das neue `RecordIfNotifyingType` prüft pro Typ `AllInterfaces` auf `System.ComponentModel.INotifyPropertyChanged` — das Symbol kennt auch die Interfaces, die eine externe Basisklasse beisteuert, der Graph nicht.
- **Store** ([ExternalContractStore.cs](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeGraph/Declarations/ExternalContractStore.cs)): Trägt neben den Member-Contracts jetzt das Typ-Level-Set `NotifyingTypes` (ConcurrentDictionary als Set, weil Phase 2 parallel läuft). `Clear`/`LoadFrom`/`IsEmpty` decken beide Mengen ab.
- **Analyse** ([DeadCodeAnalysis.cs:284](https://claude.ai/epitaxy/CSharpCodeAnalyst.CodeGraph/Algorithms/DeadCode/DeadCodeAnalysis.cs:284)): `FindBindingSources` nutzt das Set als primäre Quelle. Der bisherige Weg (`PropertyChanged`-Contract + Spreading über `Inherits`-Kanten) bleibt als Fallback — er greift für Projektdateien, die vor diesem Feld gespeichert wurden.
- **Persistenz**: `ProjectData.NotifyingTypes` (Liste von Element-IDs); ältere Projektdateien haben schlicht keine und fallen auf den alten Weg zurück, bis neu geparst wird.

Eine Stolperfalle kam beim Testen heraus: Die In-Memory-Compilation von `ParseSourceAsync` referenzierte `System.ObjectModel.dll` nicht. `netstandard.dll` enthält für `INotifyPropertyChanged` nur einen Type-Forward — fehlt die Ziel-Assembly, bleibt der Typ unaufgelöst und fehlt *stillschweigend* in `AllInterfaces`. Die Referenz ist jetzt ergänzt (hilft auch dem Einzeldatei-Parse generell).

Tests: neue Fixture [NotifyingTypeParseTests.cs](https://claude.ai/epitaxy/Tests/UnitTests/Parser/NotifyingTypeParseTests.cs) (direkte Implementierung, interne Basis, **externe Basis** via `ObservableCollection<int>`, Negativfall) plus ein Confidence-Test in [DeadCodeConfidenceTests.cs](https://claude.ai/epitaxy/Tests/UnitTests/DeadCode/DeadCodeConfidenceTests.cs), der den Store-Eintrag ohne jedes `PropertyChanged`-Member im Graphen prüft. Doku aktualisiert in [dead-code.md](https://claude.ai/epitaxy/Documentation/dead-code.md), [dead-code-algorithm.md](https://claude.ai/epitaxy/Documentation/Design/dead-code-algorithm.md) und als neues Unterkapitel im Parser-Entscheidungsprotokoll [corrections-and-updates.md](https://claude.ai/epitaxy/Documentation/Roslyn/corrections-and-updates.md).

Die übrigen Review-Punkte (Test-Attribut-Härtung, `Finalize`, `Attributed`-Verwässerung, CHA→RTA) sind unverändert offen — sag einfach, welcher als Nächstes dran ist.



# Test Assemby poisoning

Mein Vorschlag deckt sich mit deinem — pro Typ statt pro Assembly — plus zwei Verfeinerungen, damit die Typ-Granularität in der Praxis nicht sofort Löcher hat:

**1. Test-Typen statt Test-Assemblies, aber über den Subtree bestimmt.** Die Regel „Typ trägt ein Test-Attribut" reicht nicht: xUnit hat *kein* Klassen-Attribut (nur `[Fact]`/`[Theory]` auf Methoden), und NUnit erkennt Klassen seit 2.5 auch ohne `[TestFixture]`. Deshalb: Von jedem Element mit Test-Attribut die Parent-Kette hochlaufen und **jeden Typ auf dem Weg** markieren. `IsTestCode(element)` läuft dann wie heute die Kette hoch, prüft aber gegen dieses Typ-Set statt gegen die Assembly-Wurzel. Das deckt automatisch auch unattributierte verschachtelte Klassen *innerhalb* einer Fixture ab (lokale Fakes, Testdaten-Klassen) — deren Vorfahre ist markiert. Implementierung ist fast mechanisch: `FindTestAssemblies` → `FindTestTypes`, `RootOf` entfällt, die drei Nutzer (`ComputeReferenced`, `CollectTestReferences`, `IsReported`) bleiben strukturell unverändert.

**2. Ordinal statt case-insensitive matchen.** Die Framework-Attributnamen sind exakt („Test", „Fact", „TestMethod"); das `OrdinalIgnoreCase` im `TestAttributes`-Set kauft nichts und riskiert, dass ein Domänen-Attribut `test`/`TEST` eine Klasse umklassifiziert. Kostenlose Präzision.

Die Konsequenzen, ehrlich aufgelistet:

- **Das Cross-Assembly-Poisoning ist weg** — und zwar in beide Richtungen korrekter als heute: Eine `[Test]`-Klasse in einer Produktions-Assembly macht nur noch *ihre eigenen* Referenzen zu Test-Referenzen, und Produktionscode, den nur diese eingebettete Testklasse nutzt, wird jetzt korrekt als `UsedOnlyByTests` gemeldet (heute unterdrückt die Assembly-Ausnahme das).
- **Testhelper außerhalb von Fixture-Klassen werden gemeldet** (Builder, `TestCodeGraph` & Co. — referenziert nur von Test-Typen → `UsedOnlyByTests`, Medium-Cap). Das ist der Preis, den du akzeptierst — und streng genommen ist die Aussage ja auch wahr: Löscht man die Tests, ist der Helper verwaist.
- **Produktionscode, den nur ein Helper aufruft, erscheint eine Schicht später:** Erst wird der Helper gemeldet, nach dessen Löschung der Produktionscode. Das ist exakt die Nicht-Kaskadieren-Philosophie („nochmal laufen lassen, nächste Schicht"), also konsistent mit dem Rest des Designs.
- **Innerhalb einer Fixture ändert sich nichts:** Private Helfer-Methoden und verschachtelte Klassen einer Testklasse sind über die Vorfahren-Kette Testcode und bleiben unterdrückt.

Anzupassen wären außer dem Algorithmus: die Begründungs-Doku an `FindTestAssemblies` (das dortige Plädoyer *für* Assembly-Granularität wird durch die neue Abwägung ersetzt), der Klassen-Header („Test code is decided per assembly"), der entsprechende Abschnitt in [dead-code.md](https://claude.ai/epitaxy/Documentation/dead-code.md), und neue Tests für die Typ-Granularität — auffällig ist ohnehin, dass es für `UsedOnlyByTests` bisher kaum direkte Tests gibt; die würde ich bei der Gelegenheit mit anlegen.

Wenn das so passt, setze ich es um.
