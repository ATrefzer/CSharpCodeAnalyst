# Code Review 2026-08-22 — Primärkonstruktoren als Elemente

Aufgekommen bei der Arbeit an `MemberRole` (Konstruktorerkennung über alle Producer hinweg). Beim
Nachmessen fiel auf, dass Primärkonstruktoren gar nicht im Graphen stehen — und dass das die
Kohäsionsmetrik messbar verfälscht. Setzt den Punkt „A3" aus
[CodeReview-2026-06-12.md](CodeReview-2026-06-12.md) fort, dessen Empfehlung damals war, es bei der
Typ-Ebene zu belassen.

## Der Befund

Der Parser erzeugt für einen Primärkonstruktor kein Methodenelement (Phase 1 läuft über
`ConstructorDeclarationSyntax`), und für die positionellen Parameter eines Records keine Properties.
Damit die Abhängigkeit nicht verlorengeht, verankert `AnalyzePrimaryConstructorParameters` sie
ersatzweise am **Typelement** — das war die bewusste Entscheidung von A3, kein Versehen.

Für Zyklen und Schichten reicht das. Für die **Partitionierung** nicht:

```
class Captured(ILogger logger)              // C# 12: Parameter wird eingefangen
  Start, Stop  →  partitions = 2            ← sieht aus, als gehörte die Klasse geteilt

class Explicit { ILogger _logger; ... }     // dieselbe Klasse ausgeschrieben
  _logger, Start, Stop  →  partitions = 1   ← korrekt kohäsiv
```

Dieselbe Klasse, zwei Schreibweisen, gegenteiliges Urteil. Der eingefangene Parameter *ist* der
geteilte Zustand, aber er ist kein Knoten — also teilen `Start` und `Stop` nichts. Das ist ein
**falscher Split**, und er trifft nicht den Randfall: DI-lastiger Code besteht heute aus
`class Service(IRepo repo, ILogger log)`. Je moderner die Codebasis, desto unkohäsiver sieht sie aus.

Das Review von 2026-06-12 hat A3 gegen „Zyklen, Schichten, Partitionierung" bewertet und
geschlossen, Typ-Ebene genüge. Für die ersten beiden stimmt das; der Effekt auf die dritte war
damals nicht gemessen.

## Drei Messungen, die den Plan geformt haben

**1. Record-Properties sind *nicht* implizit deklariert.**

```
Property Id  implicit=False  syntax=[Parameter]  key=Id_Property.Order_NamedType...
```

Es sind echte `IPropertySymbol`s mit `ParameterSyntax` und stabilem `Key()`. Das Review von
2026-06-12 stufte sie als „generierte Member" ein — sind sie nicht. Phase 2 löst `order.Id` deshalb
**von allein** auf, sobald das Element existiert. Das macht diesen Teil deutlich billiger als damals
angenommen.

**2. Das Capture-Feld existiert, ist aber nicht das, worauf der Body bindet.**

```
Field <logger>P  implicit=True  syntax=[]
```

Roslyn legt es an, aber `logger` im Methodenrumpf bindet auf das **`IParameterSymbol`**, nicht
darauf. `AnalyzeIdentifier` hat für `IParameterSymbol` keinen Zweig — deshalb entsteht heute
überhaupt keine Kante, weder auf das Feld noch auf den Parameter.

**3. Phase 1 besucht `ParameterSyntax` bereits.** Sie fällt durch den `switch` in
`ProcessNodeForHierarchy`, erzeugt kein Element und rekursiert mit demselben Parent weiter. Der
Einhängepunkt ist ein `case`, kein Umbau.

## Plan

### B1 — Record: `ParameterSyntax` → Property

`case ParameterSyntax` holt `GetDeclaredSymbol(parameter)`, prüft auf `IPropertySymbol` und legt das
Element an. `CreatePropertyAccessorElements` greift dann bereits — der Kommentar dort erwähnt
Record-Positional-Properties ausdrücklich. **Phase 2 braucht keine Änderung**, der bestehende
`IPropertySymbol`-Zweig löst über den Key auf.

Räumt nebenbei auf, was 2026-06-12 als „Strukturansicht ist irreführend" notiert hat: ein Record ist
heute ein leerer Typ im Baum.

### A — Primärkonstruktor als Element

Fall in `ProcessNodeForHierarchy` über `INamedTypeSymbol.InstanceConstructors`, gefiltert auf
`!IsImplicitlyDeclared` und „declaring syntax ist die Typdeklaration" — dieselbe Erkennung, die
`IsExplicitConstructor` schon hat. Key und `MemberRole = Constructor` fallen aus dem bestehenden Weg
heraus.

**Die Folgewirkung ist der eigentliche Inhalt:** `IsExplicitConstructor()` liefert für
Primärkonstruktoren `false` und ist das Tor an **vier** Aufrufstellen (`SyntaxNodeAnalyzer` 173/207/454,
`DeclarationAnalyzer` 141). Sobald ein Element existiert, gehört das Tor geöffnet — `new Service(x)`
bekommt dann `Calls Service..ctor` zusätzlich zum `Creates Service`. Gewollt: es macht Kurz- und
Langform gleich. Danach ist der Name `IsExplicitConstructor` irreführend („explizit" hieß dort „hat
eine eigene Deklarationssyntax") und gehört umbenannt oder aufgelöst.

### C — Übergangslösung entfernen

`AnalyzePrimaryConstructorParameters` und `AnalyzePrimaryConstructorBaseArguments` hängen von Typ- auf
Konstruktorelement um. Genau das hat 2026-06-12 vorhergesagt: „würde A3s Typ-Kante sogar redundant".
Für Zyklen und Schichten folgenlos, weil Member-Kanten ohnehin auf den Typ gehoben werden.

### B2 — Klasse/Struct: eingefangener Parameter → Field

Zwei Hälften, die zusammengehören:

- *Phase 1:* Element vom Typ `Field`, Name `logger`, **gekeyt auf das `IParameterSymbol`** — nicht auf
  `<logger>P`, denn darauf bindet nie etwas. Nur anlegen, wenn der Parameter wirklich eingefangen ist,
  sonst sammelt jede Klasse mit Primärkonstruktor Felder an, die es nicht gibt.
- *Phase 2:* neuer `IParameterSymbol`-Zweig in `AnalyzeIdentifier` **und** `AnalyzeMemberAccess`, der
  nur für Primärkonstruktor-Parameter feuert.

**B2 ist unteilbar.** Ohne die zweite Hälfte referenziert niemand das neue Feld, es landet als
„detached state" in der Kohäsionsauswertung, und der falsche Split bleibt — Phase 1 allein macht das
Ergebnis also nicht besser, sondern nur den Graphen größer.

Offene Frage: woran „wirklich eingefangen" zuverlässig erkannt wird. Die Anwesenheit von `<name>P` in
`type.GetMembers()` ist ein Kandidat, aber ein Compiler-Implementierungsdetail. Beim Umsetzen gegen
beide Fälle absichern.

## Reihenfolge

**B1 → A → C → B2.** B1 ist isoliert, braucht keine Phase-2-Änderung und liefert sofort sichtbaren
Nutzen. A und C gehören zusammen: C geht ohne A nicht, und A ohne C hinterließe zwei konkurrierende
Verankerungen. B2 ist das aufwändigste und das einzige mit einer echten offenen Frage — zuletzt.

## Kosten und Risiken

| | |
| --- | --- |
| Approval-Tests | Jeder Record und jede Klasse mit Primärkonstruktor in `TestSuite/` bekommt neue Elemente und Kanten. Größter Einzelposten. |
| Neue Kanten | `Calls X..ctor` an vier Stellen zusätzlich. Kein Verlust, aber dichterer Graph. |
| `SplitPropertyAccessors` | B1 erzeugt bei aktivierter Option pro Record-Parameter zwei weitere Elemente. In beiden Konfigurationen testen. |
| Kohäsionszahlen | Ändern sich für betroffene Klassen — in die richtige Richtung, aber sie ändern sich. |
| Persistenz | Nichts zu tun. Keine neuen Felder, nur mehr Elemente. |

## Abnahme

Gegen die Ausgangsmessung: `class Captured(ILogger logger)` mit zwei Methoden, die den Parameter
benutzen, muss **1 Partition** ergeben wie die ausgeschriebene Variante — nicht 2.

---

# Umsetzung

Reihenfolge wie geplant: B1 → A → C → B2. 979 Tests grün, Solution baut sauber. Zwei Dinge kamen beim
Umsetzen anders als im Plan:

**Der Body-Walk war ein Loch im Plan.** `AnalyzeMethodRelationships` walkt jede
`DeclaringSyntaxReference` als Methodenrumpf — beim Primärkonstruktor ist das die **ganze
Typdeklaration**. Ungebremst hätte Schritt A jeden Methodenrumpf des Typs dem Konstruktor
zugeschrieben. Der Walk ist jetzt auf das eingeschränkt, was der Konstruktor wirklich ausführt: die
Argumentliste der Base-Klausel. Field- und Property-Initializer hängen ohnehin an den Membern selbst
(`FieldInitializerParseTests`).

**Schritt C fiel teilweise von allein weg.** Sobald der Konstruktor ein Element ist, erzeugt der normale
Methodenpfad die `Uses`-Kanten für die Parametertypen — `AnalyzePrimaryConstructorParameters` war danach
ersatzlos löschbar, nicht nur umzuhängen. Ebenso `IsExplicitConstructor`: an allen vier Aufrufstellen
stand direkt daneben `FindInternalCodeElement(...) is not null`, und das ist die ehrliche Frage. Der
Helper hieß „explizit" und meinte „hat eine eigene Deklarationssyntax" — das Konzept existiert nicht
mehr.

**Die offene Frage aus B2 ist beantwortet.** „Wirklich eingefangen" ist am Compiler ablesbar:

| | Feld im Typ | |
| --- | --- | --- |
| in Methode benutzt | `<logger>P`, implicit, `AssociatedSymbol = null` | eingefangen |
| nie benutzt | – | kein Feld |
| nur im Field-Initializer | nur das echte `_l` | kein Capture nötig |
| Record-Parameter | `<Logger>k__BackingField`, `AssociatedSymbol = Logger` | Property-Backing |

`AssociatedSymbol` trennt das Capture-Feld sauber vom Property-Backing, der Rest ist der Name. Bricht
Roslyn das Schema, entfällt das Element wieder — es degradiert auf das heutige Verhalten statt zu
crashen.

**Nur ein Zweig in Phase 2 statt zwei.** `VisitMemberAccessExpression` besucht `node.Expression`
explizit, das linke `logger` in `logger.Log()` läuft also durch `AnalyzeIdentifier`. `AnalyzeMemberAccess`
sieht nur die rechte Seite, und die kann nie ein Primärkonstruktor-Parameter sein.

## Abnahme

Erfüllt. `class Captured(ILogger logger)` mit zwei Methoden ergibt **1 Partition** wie die ausgeschriebene
Variante — festgehalten in
`CapturedPrimaryConstructorParameterParseTests.TheCSharp12FormAndTheWrittenOutForm_AgreeOnCohesion`.

## Was tatsächlich an Tests bewegt wurde

Deutlich weniger als befürchtet — **kein einziger Approval-Test** musste angefasst werden. Bewegt haben
sich genau die drei Stellen, die das alte Verhalten festhielten:

- `PrimaryConstructorBaseArgumentsParseTests` — die Fixture dokumentierte eine Asymmetrie zwischen Kurz-
  und Langform. Die gibt es nicht mehr; sie prüft jetzt, dass beide gleich sind.
- `RecordsAndStructsParseTests.PrimaryConstructorParameterTypes_AreRecordedAsUses` — Kanten sitzen auf
  dem Konstruktor und der Property statt auf dem Typ.
- `PositionalRecordMemberParseTests.AClassPrimaryConstructorParameter_DeclaresNoMember` — aus B1, durch
  B2 überholt: der Parameter deklariert keine *Property*, wird als eingefangener aber ein *Field*.

Modellierungsentscheidungen sind in `Documentation/Roslyn/corrections-and-updates.md` festgehalten.
