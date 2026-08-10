# CodeGraphExplorer.CollectShortestPaths

Dieser Algorithmus findet **alle kürzesten Pfade** in einem ungewichteten, gerichteten Graphen von einer Menge von Startknoten (`sources`) zu einer Menge von Zielknoten (`targets`).

Er arbeitet in zwei Hauptphasen: Erst einer **Vorwärtssuche (BFS)** und anschließend einer **Rückwärtssammlung (Backtracking)**.

### Phase 1: Vorwärtssuche (Multi-Source BFS & DAG-Aufbau)

Ziel dieser Phase ist es, den Graphen ebenenweise (Level für Level) zu durchsuchen, bis die ersten Zielknoten erreicht werden, und dabei die Vorgänger-Beziehungen für alle gleich kurzen Pfade zu merken.

1. **Initialisierung:**
   - Alle `sources` erhalten die Distanz `0`.
   - Eine Suchtiefe `depth` zählt die Iterationen (Anzahl der Kanten/Hops).
2. **Ebenenweise Suche (`while`-Schleife):**
   - Der Algorithmus bricht ab, sobald:
     - Keine neuen Knoten mehr gefunden werden (`currentLevel.Count == 0`),
     - Mindestens ein Zielknoten erreicht wurde (`reachedTargets.Count > 0`), oder
     - Die maximale Suchtiefe (`maxLength`) erreicht ist.
   - Für jeden Knoten der aktuellen Ebene werden alle ausgehenden Kanten durchlaufen:
     - **Neuer Knoten (`!distance.TryGetValue`):** Der Knoten wird zum ersten Mal gesehen. Seine Distanz ist die aktuelle Tiefe (`depth`). Der Vorgänger wird registriert und der Knoten für die nächste Ebene vorgemerkt.
     - **Gleich langer Pfad (`knownDistance == depth`):** Der Knoten wurde auf *derselben* Ebene bereits von einem anderen Knoten aus erreicht. Der aktuelle Knoten wird als **weiterer Vorgänger** hinzugefügt. Dadurch werden alternative, gleich kurze Pfade erfasst.
     - **Knoten aus früheren Ebenen (`knownDistance < depth`):** Wird ignoriert, da bereits ein kürzerer Pfad zu diesem Knoten existiert.
3. **Zielprüfung:**
   - Am Ende jeder Ebene wird geprüft, ob Zielknoten unter den neu entdeckten Knoten sind (`reachedTargets`). Da BFS garantiert, dass die erste gefundene Ebene die kürzeste ist, stoppt die Vorwärtssuche sofort nach diesem Level.

### Phase 2: Rückwärtssammlung (Backtracking)

Sobald die Zielknoten der kürzesten Ebene bekannt sind, rekonstruiert der Algorithmus alle Pfade rückwärts bis zu den Startknoten.

1. **Queue-Initialisierung:**
   - Eine Warteschlange (`queue`) startet mit den erreichten Zielknoten (`reachedTargets`).
   - Diese Zielknoten werden direkt zu `pathIds` hinzugefügt.
2. **Rückwärtstraversierung:**
   - Ein Knoten wird aus der Queue genommen.
   - Wenn er keine Vorgänger in `predecessors` hat, handelt es sich um einen Startknoten (`source`), und der Pfad ist an dieser Stelle zu Ende.
   - Wenn er Vorgänger hat, wird für jeden Vorgänger (`parent`):
     1. Der Vorgänger-Knoten zu `pathIds` hinzugefügt.
     2. Alle Kanten zwischen `parent` und `current` zu `pathRelationships` hinzugefügt. (Es können mehrere Kanten zwischen zwei Knoten existieren, z. B. unterschiedliche Beziehungstypen).
     3. Der Vorgänger zur Queue hinzugefügt (falls er noch nicht besucht wurde), um die Suche nach vorne fortzusetzen.

### Wesentliche Besonderheiten des Algorithmus

- **Findet \*alle\* kürzesten Pfade, nicht nur einen:** Durch das Speichern von `HashSet<string>` als Vorgänger in `predecessors[next]` wird ein gerichteter azyklischer Graph (DAG) aller Pfade gleicher minimaler Länge aufgebaut.
- **Laufzeit-Effizienz:** Die BFS bricht sofort auf der ersten Ebene ab, auf der ein Zielknoten auftaucht. Es wird nicht tiefer gesucht als nötig.
- **Ungewichtete Kanten:** Funktioniert nur korrekt bei ungewichteten Graphen (jede Kante zählt als 1 Hop). Für gewichtete Kanten müsste Dijkstras Algorithmus verwendet werden.