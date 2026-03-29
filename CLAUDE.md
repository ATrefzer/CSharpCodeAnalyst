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



## Aktuelles Vorhaben: AI Advisor für KI-gestützte Strukturanalyse

### Motivation

Der CSharpCodeAnalyst arbeitet auf einer höheren Abstraktionsebene als Quellcode — er zeigt Abhängigkeiten, Zyklen und Strukturen. Die Idee ist, KI (Claude) auf dieser Ebene arbeiten zu lassen, um strukturelle Verbesserungen vorzuschlagen. Auf dieser Ebene lassen sich Designverbesserungen besser erkennen als auf der Codeeben. 



## Soll-Ablauf

Funktionieren soll das ganze wie folgt.

- Der Benutzer importiert eine C# solution und klickt auf Cycles um sich alle Strongly Connected Components im der Solution anzeigen zu lassen. Im Code wird SCC auf Cycle Group genannt.

- Diese Anzeige erfolgt in dem Tool in einer Listendarstellung. 

- Der Benutzer klickt mit der rechten Maustaste auf einen Zeile (SCC).

- Nun wird dieser SCC in den Code Explorer übernommen, wo der Benutzer den Graphen analysieren kann.
  Soweit ist das alles bestehende Funktionalität.

- Im Ribbon neben dem Cycle Button gibt es ein weiterer Button "AI Advise" (Um das Icon kümmere ich mich)

- Klickt der Nutzer, werden zu erst im aktuellen Code Explorer Inhalt noch einmal nach Zyklengruppen gesucht. Das sollte genau eine sein. Aber der Graph kann auch auf anderem Wege aufgebaut werden und muss daher keine Zyklen enthalten. Dann erfolgt eine Meldung über Toast notification "Keine Zyklen gefunden"

- Ansonsten wird der Inhalt des Code Explorers (kleiner Ausschnitt aus dem gesamten Code Graphen) in Text exportiert. Siehe dazu CodeGraphSerializer.Serialize. GraphViewer kenn den aktuellen Inhalt des Graphen. Über GetSession kann der Graph abgefragt werden.

- Nun wird mit dieser Graphen Information ein LLM gefragt. Hier wäre ein Promt als Beispiel

- Hier wäre z.B. der immer gleichbleibende Prompt um Zyklen aufzulösen.

   

  Here is a cycle group extracted from C# source code.

  The cycle occurs on the {0} level.

  In graph theroy terms this is a strongly connected component.

   

  The graph is in following format (plain text, human readable form):

   

  CodeElementType Id [ name=Name] [ full=FullName] [ parent=ParentId] [ external] [ attr=Attr1,Attr2]

  [loc=File:Line,Col]*

  SourceId Relationship Type TargetId [ Attr1,Attr2]

  [loc=File:Line,Col]*

   

   

  Please come up with ideas on how this cycle group can be removed or at least broken down in smaller parts.

  Provide your answer as markdown.

   

  The cycle group starts here:

  

  {1}

- Hier soll folgender Ablauf implemeniert sein. Ist keine KI hinterlegt, soll eine Fehlermeldung (Toast) erscheinen, dass kein KI Endpunkt konfiguriert ist.

- Der Nutzer kann in den Settings Endpunkt und API Key eingeben. Ich möchte hier explizit nicht das Anthropic SDK verwenden, sondern einen HTTP Aufruf selbst machen. Hintergrund ist, dass ich ggf auch ein Lokales LLM verwenden will oder eine andere kompatible Endstelle. Mache eine Vorschlag wo wir diese Information sicher abspeichern. Windows Credential Manager? 

- Nun wird der Prompt ausgeführt (und die Platzhalter ausgefüllt). Die KI Antwortet im Markdown Format.

- Daher brauchen wir jetzt eine C# Bibliotk die Markdown Rendern und anzeigen kann. Ich stelle mir das so vor dann ein NICHT modales Fenster aufgeht wo der Verbesserungsvorschlag angezeigt wird. So kann man den Vorschlag neben den Graphen legen. Wenn ich die Hauptanwendung beende soll auch dieses Fenster beendet werden.

