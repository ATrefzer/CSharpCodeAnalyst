# C# Code Analyst – Metriken-Handbuch

*Ein Leitfaden zur strukturellen Analyse, Interpretation und Qualitätssicherung von C#-Codebasen.*

Dieses Handbuch erklärt die Metriken, die vom **C# Code Analyst** berechnet werden. Anstatt eine unüberschaubare Menge an theoretischen Kennzahlen zu liefern, konzentriert sich das Tool gezielt auf wenige, aussagekräftige Metriken, die konkrete Fragen zur Softwarearchitektur beantworten.

Der C# Code Analyst stellt vier komplementäre Analysebereiche bereit:

- **Type Dependencies:** Hilft dabei, die wichtigsten und risikoreichsten Typen innerhalb einer Solution zu identifizieren.
- **Type Cohesion:** Findet Klassen, die zu viele unabhängige Aufgaben wahrnehmen (*Low Cohesion*) und aufgeteilt werden sollten.
- **Method Complexity:** Identifiziert die größten, komplexesten und fehleranfälligsten Methoden.
- **System Metrics:** Beschreibt die gesamte Codebasis aggregiert anhand eines einzigen Werts pro Metrik.

Diese Analysen unterstützen dabei, sich schnell in einer neuen Codebasis zurechtzufinden und strukturelle Design-Schwachstellen (*Architektur-Smells*) frühzeitig aufzudecken.

## 1. Type Dependencies (Typ-Abhängigkeiten)

> **Kernfragen dieser Analyse:**
>
> - Welche Typen sollte ich mir zuerst ansehen, um das System zu verstehen?
> - Wie hoch ist das Risiko und die Auswirkung, wenn ich diesen Typ verändere?

Verfügbar über *Analyzers → Type Dependencies*. Das Ergebnis ist eine sortierbare Tabelle mit einer Zeile pro Typ:

| **#**            | **Spalte**         | **Bedeutung & Berechnungsgrundlage**                         |
| ---------------- | ------------------ | ------------------------------------------------------------ |
| **Rank**         | Position           | Rangfolge bei Sortierung nach dem *Score* (absteigend).      |
| **Type**         | Typname            | Der vollqualifizierte Name des Typs (Namespace + Klassenname). |
| **Fan-in**       | Afferente Kopplung | Anzahl anderer Typen, die direkt von diesem Typ abhängen (Direkte Tiefe = 1). |
| **Blast radius** | Auswirkungsradius  | Anzahl aller Typen, die transitiv von diesem Typ abhängen (Gesamte Kette der Code-Auswirkung). |
| **Score**        | Zentralität        | Transitive Wichtigkeit (PageRank), normiert auf einen Durchschnitt von 1.0. Zeigt, wie stark die Codebasis auf diesem Typ ruht. |
| **Fan-out**      | Efferente Kopplung | Anzahl anderer Typen, von denen dieser Typ direkt abhängt (Direkte Tiefe = 1). |

**Wichtiger Filter:** Typen außerhalb der analysierten Solution (z. B. .NET Framework-Typen wie `string` oder `object` sowie externe NuGet-Pakete) werden strikt ausgeschlossen. Andernfalls würden Basistypen wie `string` das Fan-in-Ranking dominieren.

### Das binäre Prinzip der Abhängigkeitszählung

Wenn 10 Methoden in Klasse `A` insgesamt 5 Methoden in Klasse `B` aufrufen und `A` zusätzlich auf ein Feld in `B` zugreift, zählt dies strukturell als genau **eine einzige** Typ-Abhängigkeit: `A → B`. Die Kopplung wird als Ja/Nein-Wert modelliert. Die Leitfrage lautet schlicht: *„Muss ich B verstehen, um A vollumfänglich zu verstehen?“* Wenn ja, existiert die Abhängigkeit – unabhängig davon, ob die Interaktion 5- oder 50-mal stattfindet.

Berücksichtigte Beziehungstypen sind unter anderem: `Calls`, `Creates`, `Uses`, `Inherits`, `Implements`, `Overrides`, `UsesAttribute` und `Invokes`. Die im Code-Graphen sichtbare `Handles`-Beziehung (Event-Handler) wird bewusst ignoriert, da sie in Bezug auf die strukturelle Abhängigkeit in die falsche Richtung zeigt (die tatsächliche Abhängigkeit entsteht bei der Registrierung des Events).

### Interpretation von Fan-in und Fan-out

- **Hohes Fan-in (Fundamente):** Viele Komponenten hängen von diesem Typ ab. Er bildet ein Fundament des Systems. Änderungen hier sind hochgradig riskant (Rippeleffekt). Solche Typen sollten Sie frühzeitig analysieren und durch Unit-Tests absichern.
- **Hohes Fan-out (Orchestratoren):** Dieser Typ kennt viele andere Typen. Er agiert oft als Koordinator, Fassade oder im schlechten Fall als „Gott-Klasse“. Er eignet sich perfekt, um den dynamischen Ablauf und Kontrollfluss des Systems zu verstehen.
- **Fan-in = 0 (Einstiegspunkte / Dead Code):** Keine andere Komponente nutzt diesen Typ. Es handelt sich entweder um einen System-Einstiegspunkt (z. B. `Main`, API-Controller, Event-Handler) oder um ungenutzten Code (*Dead Code*).
- **Fan-out = 0 (Reine Blätter):** Dieser Typ benötigt keine anderen Typen der Solution. Typischerweise sind dies DTOs, Enums, reine Value Objects oder mathematische Hilfsfunktionen. Sie bilden die stabilste Ebene Ihres Graphen.

### Blast radius (Auswirkungsradius) versus Score

Der **Blast radius** ist eine flache Zählung: Jeder Typ, der den aktuellen Typ über beliebige Ecken erreichen kann, zählt als 1. Ein Typ mit einem Blast-Radius von 3 ist bei Refactorings extrem sicher; ein Typ mit einem Wert von 800 kann bei einer Änderung das gesamte System destabilisieren. Der Typ selbst zählt nicht zum eigenen Radius ($\text{Blast radius} \ge \text{Fan-in}$).

Der **Score** verfeinert dies über den PageRank-Algorithmus: Ein Typ ist nicht schon dann architektonisch wichtig, wenn ihn viele triviale Klassen nutzen (z. B. ein Standard-Logger mit hohem Fan-in, aber geringem Score), sondern wenn *wichtige und zentrale* Typen von ihm abhängen.

$$\text{PR}(v) = \frac{1 - d}{N} + d \cdot \sum_{u \to v} \frac{\text{PR}(u)}{\text{outdegree}(u)}$$

Hierbei ist $N$ die Anzahl der Typen und $d$ der Dämpfungsfaktor (0.85). Der ausgegebene Score wird so normiert, dass ein exakter Durchschnittstyp den Wert `1.0` erhält. Ein Score von `5.0` bedeutet eine 5-mal höhere Zentralität als der Durchschnitt.

> 💡 **Praxis-Leitfaden zum Lesen der Zahlen:**
>
> 1. **Nach Score sortieren:** Findet die Klassen des Systems auf denen alles aufbaut. Sie bilden das Vokabular des Systems. Das Ändern dieser Klassen ist auch mit einem höheren Risiko verbunden.
> 2. **Nach Fan-Out sortieren**: Findet die Orchestratoren. Diese Klassen können hilfreich sein zu Verstehen wie das System zusammenspielt.
> 3. **Diskrepanz nutzen:** Hohes Fan-in bei moderatem Score entlarvt reine Utility-Klassen. Moderates Fan-in bei extrem hohem Score zeigt die wahre, versteckte Architektur-Zentrale.
> 4. **Die Risikofalle:** Ein Typ mit gleichzeitig hohem Score **und** hohem Fan-out ist der gefährlichste Punkt im System (eine hochgradig vernetzte Gott-Klasse). Ein Kandidat für ein Refactoring!

## 2. Type Cohesion (Typ-Kohäsion)

> **Kernfrage dieser Analyse:**
>
> - Enthält diese Klasse mehrere unabhängige Verantwortlichkeiten (*Violation of Single Responsibility Principle*) und in wie viele Teile lässt sie sich zerlegen?

Während die Abhängigkeitsanalyse von außen auf die Klasse blickt, analysiert die Kohäsion das *Innere*. Das Tool listet in einer sortierbaren Tabelle ausschließlich Klassen auf, die als potenzielle Split-Kandidaten gelten (LCOM4-Ansatz):

| **Spalte**     | **Bedeutung für das Refactoring**                            |
| -------------- | ------------------------------------------------------------ |
| **Class**      | Der vollqualifizierte Pfad zur analysierten Klasse.          |
| **Partitions** | In wie viele komplett isolierte Gruppen zerfallen die Member der Klasse? |
| **Members**    | Anzahl der direkten Member in dieser Klasse (Größenkontext). |
| **Largest %**  | Prozentualer Anteil der Member, die in der größten gemeinsamen Partition liegen. |

### Was ist eine Partition?

Member einer Klasse (Methoden, Felder, Properties) sind miteinander verbunden, wenn sie sich gegenseitig aufrufen oder auf dieselben internen Felder zugreifen. Member, die gemeinsam an einer Aufgabe arbeiten, bilden eine *Partition*. Besitzt eine Klasse $N \ge 2$ Partitionen, arbeitet sie an vollkommen isolierten Aufgaben und ist ein klarer Trennungskandidat.

### Vergleichsbeispiel zur Priorisierung

Zwei Klassen mit identischer Memberanzahl können eine völlig unterschiedliche Dringlichkeit aufweisen:

| **Klasse**   | **Partitions** | **Members** | **Largest %** | **Architektonische Interpretation**                          |
| ------------ | -------------- | ----------- | ------------- | ------------------------------------------------------------ |
| **Klasse X** | 2              | 19          | **53 %**      | **Hohe Priorität:** Perfekt balancierte Aufteilung (ca. 10 vs. 9 Member). Die Klasse enthält zwei völlig autarke Domänenaufgaben. |
| **Klasse Y** | 2              | 19          | **95 %**      | **Niedrige Priorität:** Ein riesiger, kohärenter Block (18 Member) und eine verirrte Hilfsmethode. Leicht zu fixen, aber kein echter Architektur-Smell. |

**Das Hub-Problem (Sonderfall):** Große MVVM-View-Models weisen oft trotz mangelnder konzeptioneller Kohäsion strukturell nur 1 Partition auf. Der Grund sind „Hubs“ – z. B. eine zentrale Methode wie `OnPropertyChanged`, die von absolut jedem Property-Setter aufgerufen wird und somit künstlich alle Member zusammenschweißt. Der Code Analyst erlaubt es daher, solche bekannten Infrastruktur-Hubs vor der Analyse mit einem simulierten Refactoring zu entfernen.

## 3. Method Complexity (Methoden-Komplexität)

> **Kernfrage dieser Analyse:**
>
> - Welche spezifischen Methoden sind so groß und verschachtelt, dass sie kaum noch wartbar oder testbar sind?

Diese Analyse arbeitet auf Methodenebene. Die Daten werden direkt beim Import des C#-Syntaxbaums ermittelt:

| **Metrik**       | **Berechnungsweise und bewerteter Aspekt**                   |
| ---------------- | ------------------------------------------------------------ |
| **Code**         | Zeilen mit logischen Tokens. Leerzeilen und reine Kommentarzeilen sind exkludiert. |
| **Statements**   | Anzahl ausführbarer Anweisungen. Formatierungsunabhängig (z. B. Expression-bodied Methods = 1). |
| **Comments / %** | Absolute Zeilenanzahl an Kommentaren (inkl. `///` XML) sowie relative Dichte. |
| **Complexity**   | Zyklomatische Komplexität nach McCabe (Anzahl linear unabhängiger Pfade). |

### Richtwerte für die Zyklomatische Komplexität

Die Komplexität berechnet sich nach dem Prinzip $1 + D$, wobei $D$ die Anzahl der Verzweigungspunkte (`if`, `while`, `for`, `catch`, `case`, `??`, `&&`, `||`) darstellt. Nutze folgende etablierte Grenzwerte zur Bewertung:

- `≤ 10` **Einfach & Gut:** Hervorragend lesbar, minimaler Testaufwand.
- `11 – 20` **Moderat:** Erhöhtes Risiko. Nach Möglichkeit bei der nächsten Änderung aufteilen.
- `21 – 50` **Komplex:** Hohes Fehlerrisiko, dringend strukturieren und in Hilfsmethoden auslagern.
- `> 50` **Kritisches Risiko:** Kaum noch wartbar, kaum vollständig testbar. Sofortiges Refactoring empfohlen.

## 4. System Metrics (Systemweite Kennzahlen)

> **Kernfrage dieser Analyse:**
>
> - Wie entwickelt sich die Kopplung, Zyklizität und Gesamtarchitektur des gesamten Systems über die Zeit?

### Warum Robert C. Martins Paketmetriken fehlen

Metriken wie Instabilität ($I$), Abstraktheit ($A$) oder die Distanz zur Hauptsequenz ($D$) werden hier bewusst **nicht** berechnet. In der Praxis verleiten sie zu rein kosmetischen Korrekturen (z. B. das Erstellen künstlicher Interfaces, um den Abstraktheitsgrad zu heben), ohne die echte Designqualität zu verbessern. Der Fokus liegt stattdessen auf rein verhaltensorientierten, strukturellen Graphmetriken.

### Die drei Säulen der Systembewertung

#### 1. Propagation cost (Ausbreitungskosten)

Gibt in Prozent an, wie stark sich die Änderung eines zufälligen Typs im Schnitt auf das Gesamtsystem auswirkt. Sie misst die Dichte der transitiven Erreichbarkeitsmatrix:

$$\text{Propagation Cost} = \frac{\text{Transitive Pfade } A \to B}{N \cdot (N - 1)}$$

Ein Wert von **0 %** bedeutet eine vollständige Entkopplung, während bei **100 %** jeder Typ jeden anderen Typ erreichen kann. Wichtig ist hier nicht die absolute Zahl, sondern der **Trend** über mehrere Releases hinweg: Steigt der Wert, verstrickt sich das System zunehmend.

#### 2. Cyclicity (Zyklizität)

Der prozentuale Anteil aller Typen, die sich innerhalb eines zyklischen Abhängigkeitsknäuels befinden (berechnet über stark zusammenhängende Komponenten / SCCs mittels Tarjan-Algorithmus). Während auf Namespace-Ebene Zyklen tolerierbar sein können, ist auf Modulebene ein Wert von **0 % das klare architektonische Ziel**.

#### 3. Feedback density (Feedback-Dichte)

Die Feedback-Dichte beschreibt den relativen Anteil an Abhängigkeiten, die entgegen einer optimalen, sauberen Schichtenarchitektur (*Layering*) rückwärts gerichtet sind. Sie spiegelt die relative Größe des minimalen Rückkopplungs-Bogensatzes (*Feedback Arc Set*) wider.

### Gegenüberstellung: Cyclicity vs. Feedback density

Diese beiden Werte müssen zwingend kombiniert interpretiert werden, um den Sanierungsaufwand eines Systems abzuschätzen:

| **Szenario**                                   | **Cyclicity (Betroffene Typen)**                         | **Feedback density (Rückwärts-Kanten)**                      |
| ---------------------------------------------- | -------------------------------------------------------- | ------------------------------------------------------------ |
| **Ein langer Ring** `T1 → T2 → ... → T20 → T1` | **Sehr Hoch (100%)** Alle 20 Typen sind Teil des Zyklus. | **Sehr Niedrig (5%)** Nur eine einzige Kante bricht die Schichtung. |
| **Dicht vernetztes Knäuel**                    | **Sehr Hoch** Viele Typen sind gefangen.                 | **Sehr Hoch** Extrem viele Querverbindungen blockieren das Layering. |

**Fazit für den Architekten:** Ein System mit hoher *Cyclicity*, aber niedriger *Feedback-Dichte*, ist „dünn“ verknotet. Es lässt sich durch das Aufbrechen einer einzigen strategischen Verbindung (z. B. durch ein extrahiertes Interface oder *Dependency Inversion*) sofort vollständig begradigen. Eine hohe Feedback-Dichte hingegen signalisiert ein tiefgehend verstricktes System, das erhebliche, komplexe Refactoring-Arbeit erfordert.