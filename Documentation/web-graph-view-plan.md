# Plan: Web-basierte Graph-Ansicht (WebView2 + Cytoscape.js)

Lernprojekt: ein zusätzliches Tab, das den Code-Graphen mit Web-Technologie
rendert — als zweite, unabhängige Sicht **neben** der bestehenden MSAGL-Ansicht.

Status: Planung. Noch kein Code geschrieben.

---

## 1. Ziel und Abgrenzung

- **Ziel:** Schöneres, interaktiveres Layout als MSAGL, vor allem echte
  *Compound Nodes* (Klasse als Container, Methoden als Kinder) und Edge-Styling
  pro Beziehungstyp. Gleichzeitig Lernvehikel für WebView2 + JS-Graph-Libs.
- **Was es NICHT ist:** Kein Ersatz der MSAGL-Ansicht. Die bleibt unangetastet.
  Das Web-Tab implementiert bewusst **nicht** `IGraphViewer`, damit der Lerncode
  isoliert bleibt und nichts an der bestehenden Seite bricht.

### Getroffene Entscheidungen

| Frage | Entscheidung |
|---|---|
| Datenquelle | **Spiegel des CodeExplorers** — das Web-Tab zeigt denselben Ausschnitt wie die MSAGL-Ansicht (`GraphViewer.GetGraph()`). Eigenständige Sicht ggf. später. |
| Offline | **Strikt offline** — alle JS-Libs lokal gebündelt, keine CDN-Zugriffe. |
| Reihenfolge | Erst diese Doku, dann Phase 0 (Spike). |

---

## 2. Technologie-Stack (bewertet)

| Baustein | Wahl | Begründung |
|---|---|---|
| Browser-Host | **WebView2** (`Microsoft.Web.WebView2` NuGet) | Chromium-basiert, Runtime auf Win 11 vorinstalliert. Standard für WPF + Web-Embedding. |
| Graph-Rendering | **Cytoscape.js** | Compound Nodes nativ (= unsere Parent/Children-Hierarchie), Edge-Styling pro Typ, Klick/Hover/Highlight eingebaut. Höchste Abstraktion = wenig selbst bauen. |
| Layout | Start: **fcose** (Compound-freundlich, schnell) oder **dagre** (geschichtet); später **ELK** (`cytoscape-elk`) | fcose ist für Compound-Graphen gebaut und schnell → guter Default bei Performance. dagre gerichtet/geschichtet. ELK beste Schichtqualität, aber langsamer → späteres Upgrade. Siehe Abschnitt 7. |
| Expand/Collapse | **cytoscape-expand-collapse** Extension | Spiegelt unseren `PresentationState` (collapsed/expanded). |

Verworfen: **D3.js** (zu roh für den Einstieg — Layout/Hit-Testing/Collapse
selbst), **Sigma.js/graphology** (für 10k+ Knoten gedacht; unsere Graphen liegen
bei ~200, vgl. `AppSettings.WarningCodeElementLimit`), **Graphviz-wasm** (schöne
statische Layouts, aber schwächer bei Interaktion).

---

## 3. Architektur-Überblick

```
┌─────────────────────────────────────────────────────────────┐
│  WPF (C#)                                                     │
│                                                              │
│  MainWindow.xaml  ──► neues TabItem "Web View"               │
│        │                                                     │
│        ▼                                                     │
│  WebGraphControl (UserControl mit WebView2)                  │
│        │                                                     │
│        ▼                                                     │
│  WebGraphViewModel / WebGraphBridge                          │
│     • liest CodeGraph (Spiegel von GraphViewer.GetGraph())   │
│     • WebGraphBuilder: CodeGraph ──► JSON (nodes/edges)      │
│     • ExecuteScriptAsync(...)  ──────────────┐  C# → JS      │
│     • WebMessageReceived       ◄─────────────┼─ JS → C#      │
│     • übersetzt JS-Events in MessageBus-Msgs │              │
└──────────────────────────────────────────────┼─────────────┘
                                                │ JSON-Protokoll
┌───────────────────────────────────────────────┼─────────────┐
│  WebView2 (HTML/JS, lokal/offline)            ▼             │
│  index.html + cytoscape.min.js + layout-ext + app.js        │
│     • renderGraph(json)  ──► Cytoscape rendert              │
│     • on('tap', ...) ──► window.chrome.webview.postMessage  │
└─────────────────────────────────────────────────────────────┘
```

### Warum das in die Codebasis passt

- **Domänenmodell ist ideal:** `CodeGraph` (`CodeGraph/Graph/CodeGraph.cs`) ist
  ein `Dictionary<string, CodeElement>`. Jedes `CodeElement` hat `Id`, `Name`,
  `ElementType`, **`Parent`/`Children`** und `Relationships`. Die Parent/Children-
  Hierarchie wird in Cytoscape direkt zu Compound Nodes.
- **Vorlage existiert:** `MsaglBuilderBase` / `MsaglHierarchicalBuilder`
  (`CSharpCodeAnalyst/Features/Graph/`) wandeln `CodeGraph` + `PresentationState`
  + `GraphHideFilter` in MSAGL-Objekte. Der `WebGraphBuilder` ist das Pendant,
  das stattdessen JSON erzeugt.
- **Tab-Einstieg trivial:** `MainWindow.xaml` Zeile ~392, `TabControl
  x:Name="WorkingArea"`. Neues `<TabItem Header="Web View">` dort einfügen.
- **Message-Bus vorhanden:** `App.StartUi` verdrahtet alles manuell über
  `MessageBus` (z.B. `QuickInfoUpdateRequest`, `LocateInTreeRequest`). Die
  Brücke **erfindet keine neue Interaktion**, sondern übersetzt JS-Events in
  genau diese Messages — der Rest der App reagiert wie gewohnt.

---

## 4. Die Brücke C# ↔ Browser

Zwei Kanäle, mehr braucht es zum Start nicht:

1. **C# → JS:** `webView.CoreWebView2.ExecuteScriptAsync("renderGraph(<json>)")`
2. **JS → C#:** in JS `window.chrome.webview.postMessage({...})`,
   in C# das Event `CoreWebView2.WebMessageReceived`.

Kein COM, kein `AddHostObjectToScript` nötig.

### JSON-Protokoll (Vorschlag, in beide Richtungen)

**C# → JS**
```jsonc
{ "type": "setGraph",
  "nodes": [ { "id": "...", "label": "Name", "kind": "Class", "parent": "<parentId|null>", "external": false } ],
  "edges": [ { "id": "...", "source": "...", "target": "...", "kind": "Calls", "count": 1 } ] }

{ "type": "highlight", "ids": ["...", "..."] }   // z.B. Call-Path
{ "type": "clearHighlight" }
```

**JS → C#**
```jsonc
{ "type": "nodeClicked",    "id": "..." }                 // → QuickInfoUpdateRequest publizieren
{ "type": "nodeDblClicked", "id": "..." }                 // → Collapse/Expand
{ "type": "contextMenu",    "id": "...", "x": 0, "y": 0 } // → Kontextmenü
{ "type": "ready" }                                        // JS-Seite ist initialisiert
```

### Mapping der Daten

- `node.id`     = `CodeElement.Id`
- `node.label`  = `CodeElement.Name`
- `node.kind`   = `CodeElement.ElementType` (Enum-Name) → Farbe in CSS/JS
- `node.parent` = `CodeElement.Parent?.Id` → Cytoscape baut daraus den Container
- `node.external` = `CodeElement.IsExternal` → grau
- `edge.kind`   = `RelationshipType` (Calls, Inherits, Implements, …) → Farbe/Strich
  (gestrichelt für Implements, gebündelt = dicker, analog `MsaglBuilderBase`)

Farben/Stile am besten 1:1 aus den bestehenden Definitionen ableiten
(`CodeGraph/Colors/ColorDefinitions`, `MsaglBuilderBase.CreateEdgeAttr`), damit
beide Ansichten konsistent aussehen.

### Asset-Hosting (offline)

- HTML/JS/CSS + gebündelte Libs liegen in einem Ordner, der per
  `CopyToOutputDirectory` ins `bin/` kopiert wird (z.B.
  `CSharpCodeAnalyst/Features/WebGraph/Web/`).
- Ausliefern über `CoreWebView2.SetVirtualHostNameToFolderMapping(
  "app", <ordner>, CoreWebView2HostResourceAccessKind.Allow)` und Navigation auf
  `https://app/index.html`.
- **Keine CDN-URLs** — Cytoscape, Layout-Extension und expand-collapse als lokale
  `.min.js` ablegen.
- Lernvorteil: HTML/JS editieren und WebView neu laden, ohne C# neu zu bauen.

---

## 5. Phasenplan

Jede Phase ist für sich lauffähig und liefert ein sichtbares Ergebnis.

### Phase 0 — Spike (Plumbing beweisen)
- `Microsoft.Web.WebView2` NuGet hinzufügen.
- Neues `WebGraphControl` (UserControl mit `<wv2:WebView2>`), neues TabItem
  „Web View" in `MainWindow.xaml`.
- `EnsureCoreWebView2Async`, Virtual-Host-Mapping, statische `index.html` mit
  **hartkodiertem** Cytoscape-Beispielgraph (3–4 Knoten), Libs lokal.
- **Ziel:** WebView2 startet, Libs laden offline, Graph erscheint.
- *Akzeptanz:* Tab zeigt einen Cytoscape-Beispielgraphen.

### Phase 1 — Echten Graph rendern (read-only)
- `WebGraphBuilder`: `CodeGraph` → JSON (Schema oben). Compound Nodes über
  `parent`, Farben nach `ElementType`, Kantenstil nach `RelationshipType`.
- Datenquelle = Spiegel: bei `GraphChanged` / nach Layout den aktuellen
  `GraphViewer.GetGraph()` serialisieren und via `setGraph` an JS schicken.
- Layout dagre/klay wählen.
- *Akzeptanz:* Web-Tab zeigt denselben Graphen wie das MSAGL-Tab, mit
  Containern und typ-abhängigen Kanten.

### Phase 2 — JS → C# (eine Richtung)
- Klick auf Node → `postMessage({type:"nodeClicked"})` →
  `WebMessageReceived` → `MessageBus.Publish(new QuickInfoUpdateRequest(...))`.
- *Akzeptanz:* Klick im Web-Tab füllt das bestehende Info-Panel — ohne neuen
  Info-Code.

### Phase 3 — Zwei Richtungen / echte Interaktion
- Doppelklick → Expand/Collapse (expand-collapse Extension, gespiegelt auf
  `PresentationState`).
- Call-Path-Highlight: Node anklicken → transitive `Calls`-Nachbarschaft
  hervorheben, Rest ausgrauen (`highlight`-Message).
- Kontextmenü (zunächst WPF-Popup, gespeist aus `contextMenu`-Message).

### Phase 4 — Politur
- ELK-Layout statt dagre/klay.
- Edge-Bundling, sanfte Animationen, Whitespace-Tuning.

---

## 6. Offene Punkte / Risiken

- **WebView2-Init ist asynchron:** Vor dem ersten `ExecuteScriptAsync` muss
  `EnsureCoreWebView2Async()` abgeschlossen und die JS-Seite `ready` gemeldet
  haben. Reihenfolge sauber behandeln (Queue für frühe `setGraph`-Aufrufe).
- **Theming:** Farben aus `ColorDefinitions` an JS durchreichen, damit Light/Dark
  und ElementType-Farben konsistent bleiben.
- **Performance:** Bei großen Graphen JSON-Größe und Layout-Zeit beobachten; dagre
  ist schnell, ELK gründlicher aber langsamer.
- **Lizenz/Offline:** Lib-Versionen + Lizenzen in `ThirdPartyNotices` ergänzen
  (das Projekt pflegt das bereits).
- **`WebView2` im Output:** Native Loader-DLLs (`WebView2Loader.dll`) müssen ins
  `bin/` — das NuGet-Paket regelt das normalerweise automatisch.

---

## 7. Basisklassen, Containment & Performance (Designentscheidungen)

### Containment vs. Vererbung = zwei orthogonale Achsen
- **Containment** (Parent/Children) → Compound-Verschachtelung. Mehrstufig:
  Namespace > Klasse > innere Klasse > Methode. **Verschachtelte Klassen werden
  geparst** (`HierarchyAnalyzer.ProcessNodeForHierarchy` rekursiert über
  `node.ChildNodes()`), tauchen also als Kinder auf.
- **Vererbung** (`RelationshipType.Inherits`) → Kante. Eine Basisklasse ist nie
  Containment-Parent ihrer Ableitung. Deshalb **keine Kollision** zwischen
  Containment-Verschachtelung und Basisklassen-Darstellung.

### Basisklassen-Methoden: NICHT duplizieren (für v1)
Entscheidung: Basisklasse bleibt eine eigene Box, Aufrufe gehen als Kante dorthin.
Begründung:
- **Performance:** Duplizieren erhöht die Knotenzahl (Basisklasse × Ableitungen)
  — genau falsch bei 500+ Knoten.
- **Cytoscape erlaubt nur einen Parent pro Knoten** — eine Methode kann nicht Kind
  zweier Ableitungen sein. Duplizieren erzwingt synthetische IDs
  (`methodId@derivedId`) und verkompliziert die Brücke (Rückmapping auf echtes
  `CodeElement`).
- Compound Nodes machen die Basisklasse ohnehin zur klar beschrifteten eigenen Box.

Stattdessen gegen „Pfeil führt in die Basisklasse" (billig, ohne neue Knoten):
- **Edge-Styling nach Typ** (Vererbung gestrichelt/grau, Calls kräftig/farbig).
- **Toggle „Strukturkanten ausblenden"**.
- **Call-Path-Highlight** (nur `Calls`-Nachbarschaft).

Basisklasse **nicht** in die Ableitung verschachteln — stattdessen geschichtetes
Layout, das Vererbungskanten in eine Richtung laufen lässt. Duplizieren bleibt ein
mögliches Phase-4-Experiment, falls danach noch nötig.

### Performance / mögliche MSAGL-Ablösung (Zukunft)
- Cytoscape rendert auf **Canvas** → flüssige Interaktion bei einigen Tausend
  Elementen.
- Flaschenhals bei 500+ Knoten ist meist das **Layout**, nicht das Rendern.
  Layout-Wahl entscheidend:
  - **fcose** — für Compound-Graphen, schnell, gute Qualität → Favorit für
    Compound + Performance.
  - **dagre** — schnell/geschichtet, schwächer bei tiefen Compounds.
  - **ELK** — beste Qualität, langsamste → opt-in.
- **Collapse/Expand bleibt der Haupthebel** für Performance und Übersicht
  (`PresentationState` existiert bereits).
- **Validierung:** In Phase 1 mit realistischem ~500-Knoten-Graphen testen, bevor
  über MSAGL-Ablösung entschieden wird.

## 8. Nächster Schritt

Phase 0 (Spike): WebView2-Paket + Tab + statischer Cytoscape-Graph offline.
```
