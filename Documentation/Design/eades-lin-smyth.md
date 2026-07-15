

# Eades-Lin-Smyth

Implementierung siehe FeedbackArcAnalysis.cs.

Der Algorithmus berechnet den Aufwand, den es bedeutet den Code "layered" zu bekommen.

Er ermittelt die Rückwärtskante, die dabei stören und berechnet deren prozentualen Anteil dieser Kanten.

## Phase 1 — Adjazenz aufbauen (`Analyze`, oberer Teil)

Entfällt, da jetzt ein Typengraph extrahiert wurde, der diesen ersten Schritt ersetzt.

```
outSet[u] = { Ziele von u }     inSet[u] = { Quellen von u }
```

Aus der rohen `outgoing`-Liste werden zwei dedupliziere Nachbarschafts-Sets gebaut, Selbstkanten raus, Kanten zu Nicht-Knoten raus. `edgeCount` zählt die *distinkte* Kantenzahl (der Nenner der Metrik). Ich brauche *beide* Richtungen (`inSet` **und** `outSet`), weil das Ordering gleich In- und Out-Grade laufend aktualisieren muss.

## Phase 2 — Das Ordering (`OrderByEadesLinSmyth`)

Das ist das Herzstück. Ziel: eine lineare Reihenfolge aller Knoten, in der **möglichst wenige Kanten rückwärts** zeigen. Der Trick von Eades-Lin-Smyth ist, die Reihenfolge **von beiden Enden her** zu bauen:

- **`left`** = die Quell-Seite (wird nach vorne gefüllt)
- **`right`** = die Senken-Seite (wird nach hinten gefüllt, am Ende umgedreht)

Die Schleife macht in jeder Runde drei Dinge:

1. **Senken abschälen** (`outDeg == 0`): Ein Knoten, der auf nichts Verbleibendes mehr zeigt, kann ganz nach rechts — keine seiner Kanten kann je rückwärts werden. → `right`.
2. **Quellen abschälen** (`inDeg == 0`): Ein Knoten, auf den nichts Verbleibendes zeigt, kann ganz nach links. → `left`.
3. **Wenn keins von beiden** existiert (nur noch zyklische Knoten übrig): nimm den Knoten mit dem größten **`outDeg − inDeg`** — der „will" am ehesten Quelle sein, zeigt mehr raus als rein. → `left`.

Der Clou ist die **inkrementelle Grad-Buchhaltung** in `Remove(u)`: Entferne ich einen Knoten, sinken die Grade seiner Nachbarn. Wird dadurch ein Nachbar-`outDeg` zu 0, ist er eine *neue* Senke → in die `sinks`-Queue. So schält sich ein DAG komplett über Schritte 1+2 ab, ganz ohne Schritt 3 — und hinterlässt dann null Rückkanten.

Zwei Feinheiten, die dir beim Lesen sonst Stirnrunzeln machen:

- **„stale queue entries":** Ich pushe Knoten in `sinks`/`sources`, aber ihr Status kann sich ändern, bevor sie drankommen (oder sie wurden schon entfernt). Darum die Guards `if (!remaining.Contains(u) || outDeg[u] != 0) continue;` beim Dequeue — billiger als die Queue teuer aktuell zu halten.
- **`ThenBy(v, Ordinal)`** beim Schritt-3-Pick: reiner deterministischer Tie-Break, damit gleiche Graphen immer dieselbe Reihenfolge (und damit dieselbe Zahl) liefern — wichtig für reproduzierbare Tests und Trend-Vergleiche.

## Phase 3 — Rückkanten zählen (`Analyze`, unterer Teil)

`position[u]` = Index in der fertigen Reihenfolge. Dann simpel: jede Kante `u → v` mit `position[u] > position[v]` zeigt rückwärts = Feedback-Kante. `FeedbackDensity = feedback / edgeCount`.

**Warum ist das die Verworrenheit?** Eine Rückkante kann *nur innerhalb eines Zyklus* entstehen — zwischen zwei Knoten ohne gemeinsamen Zyklus lässt sich immer eine Vorwärts-Richtung finden (das erledigt das Senken/Quellen-Abschälen). Also ist die Menge der Rückkanten ein (approximierter) **Minimum Feedback Arc Set**: die kleinste Menge Kanten, die man kappen müsste, um das System azyklisch zu machen.

------

Kleiner Trace am 2-Zyklus `A⇄B`, damit's greifbar wird:

```
Start: outDeg{A:1,B:1}, inDeg{A:1,B:1}  → keine Senke, keine Quelle
Schritt 3: outDeg−inDeg beide 0 → Tie-Break "A" → left=[A], Remove(A)
  Remove(A) senkt B: outDeg[B] 1→0  → B wird Senke
Runde 2: sinks=[B] → right=[B], Remove(B)
Order = left + reverse(right) = [A, B]
Kanten: A→B  (pos 0<1, vorwärts) | B→A (pos 1>0, RÜCKWÄRTS)
→ 1 Feedback / 2 Kanten = 0.5
```

## Warum haben Sinks Vorrang (im Code)?

Erst die überraschende Antwort: **für die Metrik ist es egal.** Das Entfernen einer reinen Source *oder* einer reinen Sink erzeugt garantiert **keine** Rückkante:

- Eine **Sink** (outDeg 0 unter den Verbliebenen) hat keine Ausgangskante nach „rechts" → nichts kann rückwärts zeigen. Und keine schon entfernte Source zeigt in sie hinein (sonst wäre jene Source damals nicht inDeg 0 gewesen).
- Symmetrisch für eine **Source**.

Weil beide „gratis" sind, ändert die Reihenfolge Sink-zuerst vs. Source-zuerst nur das *Interleaving* der Ordnung, nicht die Anzahl der Feedback-Kanten. Die Zahl bleibt identisch.

**Wo es eine echte Entscheidung ist:** wenn ein Knoten *gleichzeitig* beides ist. Das ist genau dann der Fall, wenn `outDeg == 0 && inDeg == 0` — ein **isolierter Knoten**. Und den routen die Guards konsequent zur Sink-Seite:

```csharp
var sources = ... remaining.Where(v => outDeg[v] != 0 && inDeg[v] == 0);  // outDeg != 0 schließt Isolierte aus
...
if (--inDeg[succ] == 0 && outDeg[succ] > 0)   // dito im Remove
```

„Sinks Vorrang" heißt also konkret: **Gleichstände (isolierte Knoten) landen rechts, nicht links.** Eine deterministische, willkürlich-aber-konsistente Wahl — auf die Metrik ohne Einfluss.

## Wie entstehen die stale Einträge?

Deine Prämisse stimmt: ein Knoten wird nur eingequeued, *wenn er in dem Moment* Source oder Sink ist. Der Haken: **ein Knoten kann nacheinander beides werden.**

Eine Source hat inDeg 0, aber noch outDeg > 0. Wenn nun ihre Ausgangs-Nachbarn nach und nach entfernt werden, sinkt ihr outDeg — bis auf 0. Dann **wird die Source zur Sink** und wird ein **zweites Mal** eingequeued, diesmal in `sinks`. Jetzt steht derselbe Knoten in *beiden* Queues. Die erste Verarbeitung entfernt ihn (`remaining.Remove`), die zweite trifft auf einen Geist.

Minimales Beispiel — nur `X → Y`:

```
Start:  outDeg{X:1, Y:0}  inDeg{X:0, Y:1}
        sinks=[Y]   sources=[X]        // X ist Source, Y ist Sink

Iter 1, sinks-Loop:
  dequeue Y  → gültig → right=[Y], Remove(Y)
     Remove(Y): X ∈ inSet[Y], --outDeg[X] = 0  → X wird Sink → sinks=[X]
  dequeue X  → gültig (outDeg 0) → right=[Y,X], Remove(X)

Iter 1, sources-Loop:
  dequeue X  → !remaining.Contains(X)  →  STALE, skip   ← der ursprüngliche Source-Eintrag
```

X wurde als Source vorgemerkt, aber als Sink entfernt — sein Source-Eintrag ist damit veraltet.

Das ist der Grund für die **zwei** Guard-Varianten:

- Sinks-Loop: `!remaining.Contains(u) || outDeg[u] != 0` — „schon weg" **oder** „ist inzwischen keine Sink mehr" (outDeg wieder > 0 kann nicht passieren, aber `!remaining` schon, wie oben).
- Sources-Loop: `!remaining.Contains(u) || inDeg[u] != 0 || outDeg[u] == 0` — der letzte Term `outDeg == 0` ist genau der Fall „ist zur Sink mutiert" (Kommentar: *„or it turned into a sink meanwhile"*).

## Das Entwurfsprinzip dahinter

`remaining` + `outDeg`/`inDeg` sind die **einzige Wahrheit**. Die Queues sind ein **append-only Merkzettel** — wir löschen einen veralteten Eintrag nie aktiv heraus, sondern **validieren faul beim Dequeue**. Der Grund ist Performance: einen bestimmten Knoten aus der Mitte einer `Queue<T>` zu entfernen wäre O(n); ein Doppel-Eintrag plus O(1)-Guard beim Dequeue ist billiger. Das ist dasselbe „lazy deletion"-Muster, das man bei Dijkstra mit `PriorityQueue` sieht (Knoten mit veralteter Distanz einfach überspringen).

Kurz: nicht die Queue hält den Zustand aktuell, sondern der Guard fragt beim Herausnehmen „bist du *jetzt* noch das, als was ich dich gemerkt habe?".

## Optimierung

Frage: Einfach "remaining" nach jedem durchlauf neu scannen? Starten mit Vorsortierung? Lesbarkeit vs. Performance

Guter Instinkt — und für den *naiven* Rescan wäre es tatsächlich eine echte Verbesserung des Konstanten-Faktors. Aber es hat einen systematischen blinden Fleck, und der zeigt genau, was die Queue eigentlich leistet.

**Das Kernproblem: ein Pre-Sort ordnet nach dem Grad zum Zeitpunkt 0 — „ready" ist aber der Grad \*jetzt\*.** Die Grade ändern sich beim Abschälen, die Sortierung ist eine eingefrorene Momentaufnahme. Für die *erste* Schicht Sources/Sinks passt sie, danach driftet sie weg.

Wo das kippt, sind **Hubs** — und davon sind reale Abhängigkeitsgraphen voll (ein Logger, eine Basisklasse, ein zentrales Interface). Konkret:

> Knoten `H` hat `inDeg 1000` (1000 Typen zeigen auf ihn) und `H → X` (`outDeg 1`). Sortierst du aufsteigend nach `inDeg`, um Sources vorne zu finden, liegt `H` ganz **hinten**.

Jetzt werden die 1000 Vorgänger nach und nach entfernt, `H`s `inDeg` sinkt. In dem Moment, in dem `H` `inDeg 0` erreicht und *ready* wird, ist es **immer noch hinten** in der statischen Sortierung. Ein Front-Scan läuft die ganze verbliebene Liste ab, um `H` zu finden. Der Pre-Sort hat genau dort *nicht* geholfen, wo es teuer wurde — weil `H`s statische Position (hinten, wegen `inDeg 1000`) seiner dynamischen Readiness (jetzt bereit) widerspricht.

Und jetzt der Punkt, der alles zusammenzieht:

> **Die Queue \*ist\* dein Pre-Sort — nur inkrementell selbst-pflegend.**

Wenn `H`s `inDeg` auf 0 fällt, hängt `Remove` es sofort vorne in die Queue (`sinks.Enqueue`/`sources.Enqueue`), O(1), egal wo ein statischer Sort es einsortiert hätte. Die Queue sortiert nach „ist gerade bereit geworden" statt nach „war anfangs bereit". Ein einmaliger Sort fängt nur die erste Schicht ein; die Queue fängt jede Schicht ein.

Deine Aussage stimmt also für zwei Ebenen unterschiedlich:

- **vs. naiver Rescan:** Pre-Sort ist besser (Sources anfangs vorne) — ja, klar schneller im Schnitt, weiterhin O(V²) worst case.
- **vs. die Queue-Variante, die wir haben:** Pre-Sort ist ein *Rückschritt* — er approximiert statisch, was die Queue dynamisch schon exakt und in O(V+E) macht. Und bei Hubs versagt die Approximation genau dort, wo der Graph teuer ist.

Wo dein Gedanke *wirklich* trägt, ist der **Pick-Schritt** (die verbliebene O(V²)-Stelle, die die Queue nicht optimiert). Auch dort will man „den mit größtem `outDeg − inDeg` sofort vorne haben" — aber aus demselben Grund (Grade ändern sich) hilft ein statischer Sort nicht, sondern nur die *lebende* Version: eine **Bucket-Queue / Heap mit decrease-key** nach `outDeg − inDeg`. Das ist dein Pre-Sort-Gedanke, konsequent inkrementell zu Ende gedacht — und exakt das, was ein „vollständiges" ELS baut.

Kurz: Die Idee „halte sie geordnet, damit die Bereiten vorne stehen" ist goldrichtig — die Queue ist diese Idee, nur dass sie sich bei jeder Grad-Änderung selbst nachzieht statt einmal.