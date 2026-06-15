

Review mit Claude Fable.

## Gesamturteil

Die Architektur ist solide und entspricht dem üblichen Muster für solche Tools: Phase 1 sammelt Deklarationen über die Syntax, Phase 2 löst Relationships über das semantische Modell auf, Identität über einen selbstgebauten String-Key statt `SymbolEqualityComparer` (weil Symbole über Compilation-Grenzen nicht stabil sind — das Problem ist real, die Lösung pragmatisch). Der Code ist gut kommentiert, die Kommentare erklären meist *warum*, nicht *was* — das ist selten und wertvoll. Die Thread-Sicherheit in Phase 2 ist korrekt umgesetzt (alle Mutationen laufen durch `AddRelationship` unter einem globalen Lock, externe Elemente werden erst nach dem `Parallel.ForEach` in den Graphen gemerged).

Jetzt zum Kern deiner Frage.

## Abhängigkeiten, die der Parser nicht findet

Die zentrale Schwachstelle, aus der die meisten Lücken folgen: **`AnalyzeIdentifier` ignoriert `INamedTypeSymbol`** ([RelationshipAnalyzer.cs:180](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:180) behandelt nur Property/Field/Event). Jeder Typname, der nicht über einen der explizit behandelten Kontexte läuft (Lokaldeklaration, Cast, `typeof`, `is`/`as` als BinaryExpression, Object Creation), fällt durch. Konkret:

**1. Indexer, Operatoren, Konversionsoperatoren, Finalizer existieren gar nicht** — das ist m.E. die größte Lücke. Der `switch` in [HierarchyAnalyzer.cs:133](https://claude.ai/epitaxy/CodeParser/Parser/HierarchyAnalyzer.cs:133) kennt `IndexerDeclarationSyntax`, `OperatorDeclarationSyntax`, `ConversionOperatorDeclarationSyntax` und `DestructorDeclarationSyntax` nicht. Folge: Diese Member werden nicht nur nicht als Elemente angelegt — **ihre Bodies werden in Phase 2 nie analysiert**, weil Phase 2 nur über die in Phase 1 gefundenen Elemente iteriert. Ein Indexer, der intern fünf Services aufruft, ist komplett unsichtbar. Eure Doku listet Indexer als „might be missed" — es ist kein „might", die Rümpfe sind garantiert unsichtbar. (Aufrufe *auf* einen Indexer, `obj[i]`, fehlen ebenfalls: `ElementAccessExpressionSyntax` wird nirgends behandelt.)

**2. Primary Constructors / positional Records.** `record RecordA(string Name, RecordB RecordB)` erzeugt **keine** Relationship RecordA→RecordB. Phase 1 sammelt nur `ConstructorDeclarationSyntax` (Kommentar dort sagt es selbst), die generierten Properties sind ebenfalls keine Elemente, und Phase 2 analysiert für den Record nur Vererbung und Attribute. Eure eigene TestSuite enthält genau diesen Fall ([RecordsAndStructs.cs:6](https://claude.ai/epitaxy/TestSuite/Regression.SpecificBugs/RecordsAndStructs/RecordsAndStructs.cs:6)), aber [RecordsAndStructsTests.cs](https://claude.ai/epitaxy/Tests/ApprovalTests/Regression/RecordsAndStructsTests.cs) prüft nur `Calls` — die fehlende Uses-Kante wird von keinem Test bemerkt. Gleiches gilt für `class Foo(Bar bar)` und die Argumente in `class Foo(Bar bar) : Base(bar)`. Bei der heutigen Verbreitung von Records ist das in echten Codebasen relevant.

**3. Pattern Matching.** `obj is Foo f` ist ein `IsPatternExpressionSyntax`, **kein** `BinaryExpressionSyntax` — der Handler in [SyntaxWalkerBase.cs:89](https://claude.ai/epitaxy/CodeParser/Parser/SyntaxWalkerBase.cs:89) greift nur beim klassischen `obj is Foo` ohne Designation. Declaration Patterns, Recursive Patterns (`is Foo { Bar: 1 }`), Switch Expressions (`x switch { Foo f => … }`) und `case Foo f:` laufen alle in den Identifier-Pfad und werden dort als `INamedTypeSymbol` verworfen. In modernem C# ist Pattern Matching oft die *einzige* Referenz auf konkrete Subtypen — gerade für ein Abhängigkeitsanalyse-Tool schmerzhaft.

**4. Konstruktor-Verkettung `: base(…)` / `: this(…)`.** `ConstructorInitializerSyntax` ist keine `InvocationExpressionSyntax` und wird nirgends behandelt. Die Argumente werden über `VisitArgument` noch erfasst, der Aufruf des Basis-/Peer-Konstruktors selbst aber nicht. Wer „Follow incoming calls" auf einem Konstruktor macht, verpasst die Ableitungen.

**5. Property-Initialisierer.** `public Foo Bar { get; } = new Foo();` — [AnalyzePropertyBody](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:1152) behandelt `ExpressionBody` und `AccessorList`, aber nicht `propertyDeclaration.Initializer`. Die `Creates`-Beziehung fehlt. (Field-Initialisierer sind dagegen korrekt abgedeckt, inklusive der durchdachten Sonderbehandlung, dass die Klasse und nicht das Feld „creates".)

**6. Typnamen in diversen Kontexten**, alle wegen Punkt oben verworfen:

- `catch (MyException ex)` — Exception-Typen sind in eurer eigenen Soll-Liste (Punkt 10 in der Doku), werden aber nicht erfasst.
- `foreach (Foo f in …)`, `out Foo f`, `using (Foo x = …)` und `for (Foo i = …;;)` — die letzten beiden, weil dort ein `VariableDeclarationSyntax` direkt im Statement hängt, nicht das behandelte `LocalDeclarationStatementSyntax`.
- `new Foo[5]` — `ArrayCreationExpressionSyntax` ist kein `BaseObjectCreationExpressionSyntax`; der Elementtyp geht verloren (der `IArrayTypeSymbol`-Zweig in `AddTypeRelationship` hilft nur, wenn der Arraytyp über Deklarationen hereinkommt).

**7. Methodengruppen außerhalb von Argumentlisten.** `AnalyzeArgument` fängt `Foo(MethodGroup)` ab, aber `Action a = MyMethod;`, `_field = MyMethod;` und `return MyMethod;` fallen durch — `AnalyzeIdentifier` ignoriert auch `IMethodSymbol`. Ebenso erzeugt das alte `evt += new EventHandler(Handler)` keine `Handles`-Kante (die rechte Seite löst zum Delegate-Konstruktor auf, nicht zum Handler).

**8. Generic Constraints** (`where T : IEntity`) — als bekannt dokumentiert, ich bestätige: nichts liest `TypeParameters[i].ConstraintTypes`. Das wäre billig nachzurüsten, da es rein auf Symbol-Ebene geht (in `AnalyzeMethodRelationships`/`AnalyzeInheritanceRelationships`).

Ein struktureller Hinweis dazu: Viele der Lücken 3–6 ließen sich **mit einer einzigen Änderung** schließen — in `AnalyzeIdentifier` (und ggf. `AnalyzeMemberAccess`) zusätzlich `INamedTypeSymbol` als `Uses` behandeln. Da die Walker ohnehin jeden Identifier besuchen und `AddRelationship` dedupliziert, würden Pattern-Typen, catch-Typen, foreach-Typen, Array-Erzeugung usw. automatisch mitkommen. Man müsste prüfen, ob das Rauschen erzeugt (z.B. die linke Seite von `Foo.StaticMember` — wobei das eigentlich eine echte Abhängigkeit ist) — aber es wäre deutlich robuster als für jeden Syntax-Kontext einen eigenen Visit-Handler nachzupflegen.

## Korrektheitsprobleme

- **`Key()` ignoriert ref/out/in** ([SymbolExtensions.cs:123](https://claude.ai/epitaxy/CodeParser/Parser/SymbolExtensions.cs:123)): `M(int)` und `M(ref int)` sind legale Overloads, bekommen aber denselben Key. Eines der beiden Elemente geht im `SymbolKeyToElementMap` verloren (die `WarnIf…`-Trace feuert). Selten, aber ein echter Bug. Nebenbei: Roslyns `GetDocumentationCommentId()` löst genau dieses Problem (kodiert `@` für ref, Arity für Generics, ist über Compilations stabil) — kombiniert mit dem Assembly-Namen könnte es euren handgebauten Key weitgehend ersetzen oder zumindest absichern.
- **Multi-Targeting killt Projekte**: Ein Projekt mit `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>` erscheint in der Solution zweimal mit gleichem Assembly-Namen — [GetValidProjects](https://claude.ai/epitaxy/CodeParser/Parser/HierarchyAnalyzer.cs:66) entfernt dann **beide** still. Besser: eines deterministisch behalten (z.B. das erste/höchste TFM) und eine sichtbare Diagnose ausgeben statt nur `Trace.WriteLine`.
- **Inkonsistente Projektfilterung**: `IsUnrecognizedProject` (.vbproj etc.) wird nur in `CollectAllFilePathInSolution` angewandt, nicht in `GetValidProjects`. Ein VB-Projekt liefert trotzdem eine Compilation → es entsteht ein leerer Assembly-Knoten im Graphen, und seine Typen landen in `AllNamedTypesInSolution`.
- Kleinkram: `ExternalCodeElementCache.GetCodeElements` lockt auf `_externalElementCache` statt auf `_lock` (real kein Race, weil es nach dem Parallel-Teil läuft, aber zwei Lock-Objekte für dieselbe Struktur sind eine Falle); der Dummy-`Execute`-FullName in `AnalyzeGlobalStatementsForAssembly` enthält das Assembly-Präfix nicht, der Klassen-FullName schon.

## Vereinfachungspotenzial

- **`AnalyzeRelationshipsSingleThreaded` ist ein Duplikat** von `…MultiThreaded` — ein `MaxDegreeOfParallelism`-Parameter (oder `ParallelOptions`) macht eine der beiden Methoden überflüssig.
- **`MethodBodyWalker` und `LambdaBodyWalker` unterscheiden sich fast nur im Relationship-Typ** (Calls/Creates vs. Uses) und der Event-Invoke-Erkennung. Ein Walker mit einem „Kontext"-Parameter wäre weniger Code und würde die bestehende Inkonsistenz beseitigen: `LambdaBodyWalker.VisitMemberAccessExpression` ruft `base.…` auf und besucht damit den `.Name`-Identifier doppelt, während `MethodBodyWalker` bewusst nur `node.Expression` besucht. Funktional gerettet wird das nur durch die Deduplizierung in `AddRelationship`.
- Die **`…Public`-Wrapper** (`AddTypeRelationshipPublic`, `AddSymbolRelationshipPublic`) im Interface sind ein Geruch — die Methoden gehören schlicht mit ordentlichem Namen ins `ISyntaxNodeHandler`, der Suffix dokumentiert nur die Entstehungsgeschichte.
- **Performance-Hotspot**: [FindTypesImplementingInterface](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:735) läuft für *jedes* Interface-Member über *alle* Typen der Solution und baut für jedes `AllInterfaces`-Element einen `Key()`-String. Das ist O(Member × Typen × Interfaces) mit String-Allokationen im innersten Loop. Eine einmal vorberechnete Map `interfaceKey → implementierende Typen` (in den `Artifacts`) würde das auf O(1) pro Lookup drücken — bei großen Solutions dürfte das messbar sein, da `Key()` generell der teuerste Baustein in Phase 2 ist.

## Fazit

Korrekt umgesetzt ist das, was abgedeckt ist — ich habe in den behandelten Pfaden keinen Fall gefunden, der *falsche* Kanten erzeugt; die Fehler sind fast alle Auslassungen. Die Prioritätenliste aus meiner Sicht: (1) Indexer/Operatoren in Phase 1 aufnehmen, weil dort ganze Methodenrümpfe unsichtbar sind, (2) Primary-Constructor-/Record-Parameter, weil allgegenwärtig, (3) `INamedTypeSymbol` im Identifier-Pfad zulassen, was Pattern Matching, catch, foreach und Array-Erzeugung in einem Schritt erledigt, (4) `: base(...)`-Verkettung und Property-Initialisierer. Die Doku `uncovered-csharp-syntax.md` würde ich danach von „might be missed"-Vermutungen auf den tatsächlich verifizierten Stand bringen — etwa die Hälfte der dort gelisteten Punkte ist nämlich entgegen der Liste abgedeckt (Expression-bodied Members, async/await, Top-Level-Statements, Default Interface Methods funktionieren alle).

Hinweis zur Verlässlichkeit: Die Befunde stammen aus Code-Lektüre, nicht aus ausgeführten Tests. Bei den als „bestätigt" markierten Lücken (Indexer, Records, Pattern, ctor-Initializer, Property-Initializer) bin ich mir sehr sicher; wenn du magst, schreibe ich dir als Nächstes gezielte TestSuite-Fälle + Approval-Tests, die jede dieser Lücken nachweisen — das wäre auch die saubere Grundlage, um sie dann einzeln zu schließen.



## Die priorisierte Liste

**Block A — fehlende Abhängigkeiten (höchster Nutzen, je 1 Commit):**

1. **A1** Indexer aufnehmen + Bodies analysieren ← *fangen wir hier an*
2. **A2** Operatoren, Konversionsoperatoren, Finalizer
3. **A3** Primary-Constructor-/Record-Parametertypen
4. A3b Selbstreferenz für Records entfernen
5. **A4** Konstruktor-Verkettung `base(...)`/`this(...)`
6. **A5** Property-Initialisierer
7. **A6** Pattern-Matching-Typen (`is Foo f`, switch, case)
8. **A7** Typen in catch/foreach/using/for/out-var
9. **A8** Array-Erzeugung `new Foo[n]`
10. **A9** Methodengruppen in Zuweisung/Return/Feld
11. **A10** Alte Delegate-Registrierung `+= new EventHandler(H)`
12. **A11** Generic Constraints `where T : IFoo`

**Block B — Korrektheit:** B1 Key() ref/out/Indexer · B2 Multi-Targeting · B3 VB/F#-Filter · B4 Lock/Dummy-Name

**Block C — Vereinfachung/Doku:** C1 Single/Multi-Threaded zusammenführen · C2 Walker zusammenführen · C3 Interface-Map · C4 Doku aktualisieren





# Bei der Umsetzung weitere Punkte aufgefallen

Bei A3 Primary Konstruktoren erkennen kam die Frage auf ob die Argumente des Primary Konstruktor auch als Property in der Hierarchie aufgenommen werden. Die Antwort ist nein. Das ist allerdings ein aufwändiger Teil, da er Phase 1 des Parsers betrifft und nicht die Beziehungsanalyse.

Ich stelle diesen Punkt hinten an.

## Haben wir die Referenzen?

**Teilweise.** Konkret für `record Order(OrderId Id, Customer Customer)`:

- ✅ **Typ-Ebene** (durch A3): `Order → OrderId` und `Order → Customer` als `Uses`. Die *Abhängigkeit* ist erfasst.
- ❌ **Member-Ebene**: Die generierten Properties `Id` und `Customer` sind **keine** Graph-Elemente. `Order.Children` ist leer.

Das hat drei Konsequenzen:

1. **Strukturansicht ist irreführend.** Im Baum/Graph erscheint `Order` als *leerer* Typ — obwohl der Record real zwei öffentliche Properties und einen Konstruktor hat. Das ist über die reine Abhängigkeitsfrage hinaus ein Darstellungsproblem.

1. **Inkonsistenz zur Langform.** Dieselbe Klasse ausgeschrieben

   ```
   record Order { public OrderId Id { get; init; } public Customer Customer { get; init; } }
   ```

   erzeugt

   ```
   Order.Id → OrderId
   ```

    (Member-Ebene) plus die navigierbaren Property-Elemente. Die Positional-Form und ihre Langform liefern also

    unterschiedliche Graphen.

1. **Verwendungen anderswo werden vergröbert.** Schreibt fremder Code `someOrder.Id`, fällt das mangels Element `Order.Id` auf den Typ `Order` zurück (`X uses Order` statt `X uses Order.Id`). Die Typ-Abhängigkeit bleibt, die Granularität geht verloren.

## Brauchen wir sie?

Das hängt vom Zweck ab — und hier ist meine Einschätzung:

**Für reine Typ-Abhängigkeitsanalyse** (Zyklen, Schichten, Partitionierung — der Kern laut README): **Nein, A3 reicht.** Die `Order → OrderId`-Kante ist genau das, was Zyklenerkennung & Co. brauchen.

**Für die Struktur-/Navigationsansicht und Konsistenz: Ja, es wäre wertvoll.** Das stärkste Argument ist Punkt 1 — ein Record als leerer Typ dargestellt ist schlicht falsch.

## Empfehlung (Opus)

Ich würde A3 **so lassen** (Typ-Ebene, minimal, erfüllt den Analyse-Kern) und das Erfassen der generierten Member als **eigene, größere Aufgabe** behandeln — denn das gehört in **Phase 1** (Property-Elemente — und ggf. ein Primary-Ctor-Methoden-Element — aus den Positional-Parametern synthetisieren), nicht in die Relationship-Phase. Wenn wir das machen, würde A3s Typ-Kante sogar redundant und könnte zugunsten der Member-Kanten entfallen (= exakt wie die Langform).

Zwei konkrete Vorschläge, über die du entscheidest:

**Nebenbefund (unabhängig von alldem):** Ich bin beim Debuggen auf vorbestehendes Rauschen gestoßen — jeder Record bekommt eine **Selbst-Kante `Order → Order`**, weil `IEquatable<Order>` implementiert wird und die generische Typargument-Behandlung das Argument `Order` als `Uses` einträgt. Das ist nicht durch A3 entstanden, aber es ist Müll im Graphen.



# Erkenntnisse aus dem Review

## A3b

```
// A type parameterized with itself (records implement IEquatable<Self>; CRTP like
// class Foo : IComparable<Foo>) would otherwise gain a meaningless self-reference.
if (typeArg is INamedTypeSymbol namedTypeArg &&
         ReferenceEquals(FindInternalCodeElement(namedTypeArg.NormalizeToOriginalDefinition()), sourceElement))
{
      continue;
}
```

Warum NormalizedToOriginalDefinition. Welche Bedeutung hast das? Und zweitens: Ich weiß, dass das schon funktioniert: Foo<Foo<Self>> würde erkannt. Wo passiert das nochmal?

Beides hängt an derselben Mechanik. Hier die Antworten mit den genauen Stellen.

### 1. Warum `NormalizeToOriginalDefinition`?

Der Grund liegt darin, **wie `FindInternalCodeElement` nachschlägt**: über `symbol.Key()` im `SymbolKeyToElementMap`. Und dieses Map enthält aus Phase 1 **nur Original-Definitionen** — konstruierte generische Typen wie `List<int>` stehen dort *nicht*, sondern nur `List<T>`. Der `Key()` einer Konstruktion unterscheidet sich vom `Key()` der Definition.

Konsequenz: Wäre das Typargument ein **konstruierter** Generic, würde `FindInternalCodeElement(typeArg)` ohne Normalisierung schlicht `null` liefern (kein Treffer) — und meine Selbst-Prüfung würde stillschweigend versagen, die Selbstkante also *nicht* erkannt. Das `NormalizeToOriginalDefinition()` führt den konstruierten Typ auf seine Definition zurück, sodass der Lookup zuverlässig das in Phase 1 angelegte Element trifft. Genau dasselbe macht `AddNamedTypeRelationship` ein paar Zeilen darüber selbst (`var normalizedSymbol = namedTypeSymbol.NormalizeToOriginalDefinition()`) und auch `TryFindInternalElementWithNormalization` — ich bin damit konsistent zum Rest der Datei.

**Ehrliche Einordnung:** Im konkreten `IEquatable<Order>`-Fall ist die Normalisierung ein **No-op**, weil `Order` nicht generisch ist und damit bereits seine eigene Definition. Sie greift erst, wenn das Typargument eine echte Konstruktion ist — z. B. `class Foo<T> : Bar<Foo<int>>`: dort wird `Foo<int>` erst durch Normalisierung auf das `Foo<T>`-Element (= Quelle) zurückgeführt und so als Selbstreferenz erkannt. Ich habe sie also aus Robustheit und Konsistenz drin, nicht weil der Record-Fall sie bräuchte.

### 2. Wo wird `Foo<Foo<Self>>` erkannt?

Mein Guard prüft nur die **direkten** Typargumente von `namedTypeSymbol`. Bei einer Verschachtelung ist das direkte Argument `Foo<Self>` — *nicht* `Self`. Dass die innere Selbstreferenz trotzdem gefangen wird, liegt an der **wechselseitigen Rekursion** zwischen den beiden Methoden:

- [RelationshipAnalyzer.cs:1008](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs) — in `AddNamedTypeRelationship` läuft die `if (namedTypeSymbol.IsGenericType)`-Schleife über die Typargumente und ruft für jedes `AddTypeRelationship(...)` auf. **Hier sitzt mein Guard.**
- [RelationshipAnalyzer.cs:907](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs) — `AddTypeRelationship` dispatcht den `case INamedTypeSymbol` *zurück* nach `AddNamedTypeRelationship`.

Das ist der Kreis: `AddNamedTypeRelationship` (Typarg-Schleife) → `AddTypeRelationship` → `AddNamedTypeRelationship` → … Auf jeder Ebene wird die Schleife — und damit der Guard — neu durchlaufen.

Für `IRgendwas<Foo<Foo<Self>>>` heißt das konkret:

| Ebene | Aufruf              | direktes Typarg  | Guard greift?                            |
| ----- | ------------------- | ---------------- | ---------------------------------------- |
| 0     | `…<Foo<Foo<Self>>>` | `Foo<Foo<Self>>` | nur falls == Quelle                      |
| 1     | `Foo<Foo<Self>>`    | `Foo<Self>`      | nur falls == Quelle                      |
| 2     | `Foo<Self>`         | `Self`           | **ja**, wenn Quelle == Self → `continue` |

Die Selbstreferenz wird also genau auf der Rekursionsebene gefangen, auf der `Self` als *direktes* Typargument auftaucht. Der Guard muss deshalb gar nicht selbst absteigen — das Absteigen erledigt die ohnehin vorhandene Rekursion, und mein `continue` verhindert nur den einen self-`Uses`-Eintrag, ohne die Rekursion in die anderen Geschwister-Argumente zu stoppen.

Ein Detail am Rande, das daraus folgt: Arrays und Pointer hängen über dieselbe Rekursion mit drin ([Zeile 902](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:902) und [912](https://claude.ai/epitaxy/CodeParser/Parser/RelationshipAnalyzer.cs:912)), d. h. `IEquatable<Self[]>` würde *nicht* gefiltert — das Argument `Self[]` ist ein `IArrayTypeSymbol`, kein `INamedTypeSymbol`, und mein `is INamedTypeSymbol`-Check greift dort nicht. Das ist ein so seltener Fall, dass ich ihn bewusst nicht abgedeckt habe; falls gewünscht, könnten wir den Guard stattdessen in `AddRelationship` als generelles „kein self-`Uses`" formulieren — das würde aber legitime Selbstkomposition (`record Node(Node Next)`) mitnehmen, weshalb ich die gezielte Variante vorgezogen habe.



# Methodengruppen

## Wie der Walker überhaupt läuft

Ein `CSharpSyntaxWalker` besucht **jeden Knoten im Syntaxbaum**, von oben nach unten. Für jeden Knotentyp gibt es ein `VisitXxx`. Die Standard-Implementierung von `VisitXxx` **steigt automatisch in alle Kindknoten ab**. Ein Override kann diesen Abstieg fortsetzen (`base.VisitXxx(node)`) oder gezielt steuern.

Entscheidend: Ein Methodenname wie `_worker.DoParallelWork` ist **nicht ein einzelner Knoten**, sondern taucht im Baum an mehreren Stellen auf, die *verschiedene* `Visit`-Methoden auslösen.

## Der konkrete Baum von `Run(_worker.DoParallelWork)`

```
InvocationExpression                         "Run(_worker.DoParallelWork)"
├─ Expression: IdentifierName "Run"
└─ ArgumentList
   └─ Argument                               ← VisitArgument  → AnalyzeArgument
      └─ MemberAccessExpression              ← VisitMemberAccessExpression → AnalyzeMemberAccess
         "_worker.DoParallelWork"
         ├─ Expression: IdentifierName "_worker"   ← VisitIdentifierName → AnalyzeIdentifier (Feld)
         └─ Name: IdentifierName "DoParallelWork"
```

Der Punkt: Der **`Argument`-Knoten** und der **`MemberAccessExpression`-Knoten** sind zwei verschiedene Knoten, aber sie beschreiben dieselbe Methodengruppe `_worker.DoParallelWork`. Der Walker besucht beide.

## Trace — vorher vs. nachher

**Vor A9:**

1. `VisitArgument` → `AnalyzeArgument`: Argumentausdruck ist `_worker.DoParallelWork` (MemberAccess, kein `()`) → `IMethodSymbol` → fügt `Uses+IsMethodGroup` hinzu. ✅
2. Abstieg → `VisitMemberAccessExpression` → `AnalyzeMemberAccess`: prüfte nur Property/Field/Event, **ignorierte Methoden** → nichts.

→ Nur `AnalyzeArgument` fing es. **Genau dafür wurde es eingeführt** — es war der *einzige* Ort, an dem Methodengruppen erfasst wurden.

**Nach A9:**

1. `VisitArgument` → `AnalyzeArgument`: → `Uses+IsMethodGroup`. ✅ *(1. Mal)*
2. Abstieg → `VisitMemberAccessExpression` → `AnalyzeMemberAccess`: neuer `IMethodSymbol`-Zweig. Guard fragt: „Ist `_worker.DoParallelWork` das *Aufrufziel* einer Invocation?" Der Parent ist `Argument`, keine `InvocationExpression` → **nein** → es ist eine Methodengruppe → `Uses+IsMethodGroup`. ✅ *(2. Mal)*

Beide schreiben dieselbe Kante. `AddRelationship` dedupliziert (gleiche Quelle, gleiches Ziel, Typ `Uses`) → im Graphen landet **eine** Kante. Deshalb bleiben die Tests grün, *obwohl* doppelt erfasst wird.

=> Also kurz und knapp: AnalyzeArgument kann komplett entfernt werden. 