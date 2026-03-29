# CLAUDE.md — Projektkontext für Claude Code

## Projekt: CSharpCodeAnalyst

- **Repository**: https://github.com/ATrefzer/CSharpCodeAnalyst
- **Autor**: Andreas Trefzer
- **Lizenz**: GPL-3.0
- **Sprache**: C#, WPF-Anwendung
- **Zweck**: Abhängigkeitsanalyse von C#-Code — Zyklen finden, visualisieren, Architekturregeln prüfen

### Was das Tool kann

- Importiert C#-Solutions (.sln) über Roslyn
- Baut einen Code-Graphen (Namespaces, Klassen, Methoden, Abhängigkeiten)
- Findet stark zusammenhängende Komponenten (Zyklen) im Abhängigkeitsgraphen
- Visualisiert Abhängigkeiten als interaktiven Graph
- Simuliert Refactorings auf dem Graphen (Element entfernen, Klasse verschieben, Kante schneiden)
- Prüft Architekturregeln (DENY, RESTRICT, ISOLATE)
- Exportiert nach DGML und PlantUML

### Projektstruktur (relevante Teile)

- `CodeParser/` — Roslyn-basierter C#-Parser, extrahiert Abhängigkeiten
- `CodeGraph/` — Graphmodell, Zyklen-Erkennung, Architekturregeln
- `CSharpCodeAnalyst/` — WPF-UI, Visualisierung, Interaktion
- `Tests/` — Unit Tests
- `TestSuite/` — Testprojekte für den Analyzer



## Aktuelles Vorhaben: MCP Server für KI-gestützte Strukturanalyse

### Motivation

Der CSharpCodeAnalyst arbeitet auf einer höheren Abstraktionsebene als Quellcode — er zeigt Abhängigkeiten, Zyklen und Strukturen. Die Idee ist, KI (Claude) auf dieser Ebene arbeiten zu lassen, um strukturelle Verbesserungen zu diskutieren und zu erarbeiten.

### Philosophie dahinter

Ich beschäftigt sich mit dem Thema Software-Wartbarkeit. Kernideen aus früheren Diskussionen:
- Funktionale Dekomposition (Parnas, Löwy) kann zu unwartbarem Code führen
- Volatilitätsbasierte Dekomposition ist die Alternative — Modulgrenzen entlang dessen, was sich unabhängig ändert
- Zyklenfreiheit auf Namespace-Ebene ist ein zentrales Qualitätskriterium
- Hierarchische Strukturen sind kognitiv leichter zu erfassen
- Navigierbarkeit im Code (Bottom-up, "Show me what calls this") ist wichtiger als Lesbarkeit im Clean-Code-Sinne

### Geplante MCP-Tools (priorisiert)

#### Phase 1: Query-Tools (Einstieg)
- `get_cycles(level)` — Zyklen auf Namespace-/Klassen-/Methoden-Ebene abrufen
- `get_dependencies(element)` — Abhängigkeiten eines Code-Elements abfragen
- `get_graph_summary()` — Überblick: Anzahl Namespaces, stärkste Kopplungen, größte Zyklen
- `get_architectural_violations()` — Definierte Regeln prüfen und Verletzungen zurückgeben

#### Phase 2: Simulated Refactoring (Vielleicht, Ziel ist Phase 3. Evt ist dieser Schritt nötig?)
- `snapshot()` — Snapshot des aktuellen Graphen
- `restore()` — Snapshot nach einem fehlgeschlagenen Refactorings wieder herstellen.
- `simulate_move(element, target_namespace)` — Klasse verschieben, neuen Zyklus-Status zurückgeben
- `simulate_remove(element)` — Element entfernen, Auswirkung auf Zyklen zeigen
- `simulate_cut_dependency(source, target)` — Kante schneiden, Ergebnis zeigen

#### Phase 3: KI-gestützte Analyse
- `suggest_architectural_rules()` — Claude schlägt Regeln basierend auf erkannter Struktur vor
- `analyze_cycle(cycle_id)` — Detailanalyse eines Zyklus mit Abhängigkeitstypen
- `suggest_cycle_resolution(cycle_id)` — Claude schlägt konkrete Auflösungsstrategien vor

### Technische Entscheidungen

1. **Export-basierter Ansatz**: Der MCP Server arbeitet auf einem bereits analysierten Graphen — kein Roslyn im MCP Server nötig. Wichtig: Zuerst prüfen, welches Speicher-/Ladeformat das WPF-Tool bereits verwendet. Falls der MCP Server das `CodeGraph`-Projekt referenziert, kann er möglicherweise dasselbe Format direkt laden. Kein neues Serialisierungsformat einführen, wenn es nicht nötig ist.
2. **C# MCP SDK**: `ModelContextProtocol` NuGet-Paket (offizielles SDK, gepflegt von Microsoft + Anthropic)
3. **Transport**: stdio (Standard für Claude Code / Claude Desktop)
4. **Neues Projekt**: `CSharpCodeAnalyst.Mcp` als Konsolenprojekt in der bestehenden Solution
5. **Referenz**: Das MCP-Projekt referenziert `CodeGraph` für das Graphmodell und die Analyse-Logik

### Nächste Schritte

1. Bestehendes Speicherformat des WPF-Tools analysieren — kann der MCP Server den Graphen direkt über die `CodeGraph`-Bibliothek laden?
2. Neues Konsolenprojekt `CSharpCodeAnalyst.Mcp` erstellen
3. NuGet-Paket `ModelContextProtocol` einbinden
4. Erstes Tool `get_cycles` implementieren
5. In Claude Code registrieren: `claude mcp add`
6. Testen und iterativ erweitern

### Hinweise für Claude Code

- Das Projekt verwendet .NET (siehe Directory.Build.props für die Version)
- Der Graph wird im `CodeGraph`-Projekt modelliert — dort liegt die Zyklen-Erkennung
- Die Klasse für stark zusammenhängende Komponenten (Tarjan's Algorithmus) ist in `CodeGraph/`
- Beim Erstellen des MCP Servers: Minimale Abhängigkeiten halten, nur `CodeGraph` referenzieren
- Vor dem Erstellen eines neuen Exportformats: Prüfen wie das WPF-Tool den Graphen heute speichert/lädt. Das bestehende Format wiederverwenden, wenn möglich. Der Graph muss enthalten: Knoten (mit Typ, Name, Namespace, Parent) und Kanten (mit Typ: Calls, Inherits, Implements, Uses, etc.)
