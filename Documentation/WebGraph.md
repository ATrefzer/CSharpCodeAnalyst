[TOC]

# Plan: Web-basierte Graph-Ansicht (WebView2 + Cytoscape.js)

Lernprojekt: ein zusätzliches Tab, das den Code-Graphen mit Web-Technologie
rendert — als zweite, unabhängige Sicht **neben** der bestehenden MSAGL-Ansicht.

---

## 1. Ziel

**Ziel:** Schöneres, interaktiveres Layout als MSAGL, vor allem echte
*Compound Nodes* (Klasse als Container, Methoden als Kinder) und Edge-Styling
pro Beziehungstyp. Gleichzeitig Lernvehikel für WebView2 + JS-Graph-Libs.

---

## 2. Technologie-Stack (bewertet)

| Baustein        | Wahl                                                         | Begründung                                                   |
| --------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Browser-Host    | **WebView2** (`Microsoft.Web.WebView2` NuGet)                | Chromium-basiert, Runtime auf Win 11 vorinstalliert. Standard für WPF + Web-Embedding. |
| Graph-Rendering | **Cytoscape.js**                                             | Compound Nodes nativ (= unsere Parent/Children-Hierarchie), Edge-Styling pro Typ, Klick/Hover/Highlight eingebaut. Höchste Abstraktion = wenig selbst bauen. |
| Layout          | Start: **fcose** (Compound-freundlich, schnell) oder **dagre** (geschichtet); später **ELK** (`cytoscape-elk`) | fcose ist für Compound-Graphen gebaut und schnell → guter Default bei Performance. dagre gerichtet/geschichtet. ELK beste Schichtqualität, aber langsamer → späteres Upgrade. Siehe Abschnitt 7. |
| Expand/Collapse | **cytoscape-expand-collapse** Extension                      | Spiegelt unseren `PresentationState` (collapsed/expanded).   |

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

---

## 6. Offene Punkte / Risiken

- **Performance:** Bei großen Graphen JSON-Größe und Layout-Zeit beobachten; dagre
  ist schnell, ELK gründlicher aber langsamer.
- **`WebView2` im Output:** Native Loader-DLLs (`WebView2Loader.dll`) müssen ins
  `bin/` — das NuGet-Paket regelt das normalerweise automatisch.

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

Das ist der Kern, den man einmal verstanden haben muss. Ich erkläre es von oben nach unten.

# Umsetzung

## Die Grundidee: WebView2 ist ein Browser ohne Fensterrahmen

`WebView2` ist nichts anderes als **Microsoft Edge (Chromium), eingebettet als WPF-Control**. Die volle Browser-Engine. Alles, was im „Web View"-Tab passiert, ist exakt das, was in einem Browser-Tab passieren würde: eine HTML-Seite laden, CSS anwenden, JavaScript ausführen, rendern. Dein WPF-Fenster hat also ab jetzt eine kleine Browser-Kachel eingebaut.

Wichtig: Chromium läuft tatsächlich in **eigenen Prozessen** neben deiner App (genau wie Chrome jeden Tab in einen eigenen Prozess legt). Deshalb ist die Initialisierung asynchron (`EnsureCoreWebView2Async` — „starte die Browser-Engine und sag mir, wenn sie bereit ist") und deshalb braucht sie ein Verzeichnis auf der Platte. Dazu gleich mehr.

## Wie ein Browser normalerweise eine Seite lädt — und unser Sonderfall

Normaler Ablauf im Internet:

1. Du tippst `https://example.com`.
2. **DNS** übersetzt den Hostnamen `example.com` in eine IP-Adresse (einen echten Server irgendwo).
3. Der Server schickt HTML/CSS/JS zurück.

Wir wollen aber **kein Internet und keinen Server** — unsere Dateien liegen einfach in einem Ordner auf der Platte (`bin\...\Features\WebGraph\Web`). Wie bringt man einen Browser dazu, lokale Dateien zu laden? Es gibt zwei Wege:

- **`file:///C:/.../index.html`** — funktioniert, aber Browser behandeln `file://` als extrem eingeschränkten Sonderfall (viele moderne Features, Modul-Laden, Sicherheitsregeln verhalten sich seltsam). Fragil.
- **Virtual Host** (das nutzen wir) — sauberer. Erklärung jetzt.

## Was ist ein Virtual Host?

Der „Host" ist der Domain-Teil einer URL — in `https://csharp-code-analyst.local/index.html` ist das `csharp-code-analyst.local`.

„Virtual" heißt: **dieser Host ist erfunden.** Es gibt ihn nirgends im Internet, kein DNS kennt ihn, kein Server antwortet darauf. Er existiert nur *innerhalb deines WebView2*. Diese eine Zeile erzeugt ihn:

```csharp
core.SetVirtualHostNameToFolderMapping(
    "csharp-code-analyst.local", webRoot, CoreWebView2HostResourceAccessKind.Allow);
```

Das sagt WebView2: *„Immer wenn die Seite irgendetwas von `csharp-code-analyst.local` anfordert, hol die Datei stattdessen heimlich aus diesem lokalen Ordner."* Kein Netzwerk, keine DNS-Auflösung. Den Namen hätte ich frei wählen können (`app`, `meinprojekt.intern`, …). Die Endung `.local` ist nur eine Konvention für „das ist keine echte Internet-Domain", und der lange Name minimiert das Risiko, dass er je mit etwas Echtem kollidiert.

**Warum überhaupt ein Hostname statt `file://`?** Weil die Seite damit eine ganz normale, stabile, *sichere* Herkunft („Origin") bekommt. `https://` signalisiert dem Browser einen sicheren Kontext, und damit verhält sich die Seite wie eine richtige moderne Website — offline, aber mit allen Features und normalen Sicherheitsregeln. Es ist quasi ein privater Mini-Webserver, der nur Dateien von deiner Platte ausliefert.

## Wie die Web-Teile zusammenspielen (der Lade-Ablauf)

```
C#: WebView.Source = https://csharp-code-analyst.local/index.html
        │
        ▼
WebView2 sieht das Virtual-Host-Mapping → liest index.html von der Platte
        │
        ▼
Browser parst index.html von oben nach unten und findet:
   ├─ <link href="style.css">          → fordert .../style.css an   → Platte
   ├─ <script src="lib/cytoscape.min.js"> → fordert die Lib an       → Platte
   │        (jetzt existiert die globale Funktion `cytoscape`)
   └─ <script src="app.js">            → führt UNSEREN Code aus
              • cytoscape({...}) baut die Graph-Instanz
              • zeichnet den Beispielgraphen
              • postMessage({type:"ready"})  ── JS → C#
```

Der Clou: die `href`/`src` in der HTML sind **relativ** (`style.css`, nicht `https://.../style.css`). Relative Pfade werden gegen die Adresse der aktuellen Seite aufgelöst — also automatisch wieder gegen denselben Virtual Host. Du musst den Hostnamen nie wiederholen, und alles bleibt offline.

Danach ist es einfach eine laufende Web-Seite im Tab. Die Brücke in beide Richtungen (das hatten wir schon):

- **C# → JS:** `ExecuteScriptAsync("renderGraph(...)")` führt JS *in* der Seite aus.
- **JS → C#:** `window.chrome.webview.postMessage(obj)` löst in C# `WebMessageReceived` aus. `window.chrome.webview` ist ein spezielles Objekt, das WebView2 in jede gehostete Seite injiziert — der einzige „Draht" nach draußen.

## Wofür das UserData-Verzeichnis?

Ein echter Browser speichert viel pro-Benutzer-Zustand auf der Platte: Cache, Cookies, `localStorage`, GPU-Shader-Cache, Logs. WebView2 ist Chromium, braucht also dasselbe. Dieser „User Data Folder" ist der Ablageort dafür — **jede** WebView2-Instanz muss einen haben.

Wenn man keinen angibt, legt WebView2 ihn standardmäßig **neben die .exe** (`<exe>.WebView2`). Das Problem: ist deine App in `C:\Program Files\…` installiert, ist der Ordner schreibgeschützt → WebView2 startet nicht. Deshalb zeige ich explizit auf einen garantiert beschreibbaren Ort:

```csharp
var userDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "CSharpCodeAnalyst", "WebView2");   // %LOCALAPPDATA%\CSharpCodeAnalyst\WebView2
```

Praktisch ist das ein **Cache**: gefahrlos löschbar, WebView2 baut ihn neu auf. Er ist auch der Grund, warum der allererste Tab-Klick etwas länger dauert (Engine richtet sich ein) und spätere Aufrufe schneller sind. Unsere App speichert dort aktuell keine Logins o.ä. — aber Chromium will den Ordner trotzdem für Cache und internes Bookkeeping.

------

Kurzer Begriffs-Spickzettel:

| Begriff                              | In einem Satz                                                |
| ------------------------------------ | ------------------------------------------------------------ |
| **WebView2**                         | Eingebetteter Edge/Chromium-Browser als WPF-Control.         |
| **CoreWebView2**                     | Das eigentliche Engine-Objekt hinter dem Control (`WebView.CoreWebView2`). |
| **EnsureCoreWebView2Async**          | „Starte die Engine" — async, weil eigener Prozess.           |
| **Virtual Host**                     | Erfundener Domainname, der auf einen lokalen Ordner zeigt statt ins Netz. |
| **Origin**                           | Herkunft der Seite (`https://csharp-code-analyst.local`); steuert Browser-Sicherheit. |
| **User Data Folder**                 | Chromiums Cache/Zustand auf der Platte; muss beschreibbar sein. |
| **postMessage / WebMessageReceived** | Der JS→C#-Draht.                                             |
| **ExecuteScriptAsync**               | Der C#→JS-Draht.                                             |

Wenn du willst, gehe ich beim nächsten Mal genauso durch die `app.js` — also wie Cytoscape aus den `nodes`/`edges` den Graphen baut, was „Compound Nodes" intern sind und wie die Style-Selektoren (`edge[kind = 'Calls']`) funktionieren. Oder wir starten Phase 1.

Gern. Ich gehe die [app.js](https://claude.ai/epitaxy/CSharpCodeAnalyst/Features/WebGraph/Web/app.js) von oben nach unten durch. Die Datei hat vier Aufgaben: **Brücke**, **Aussehen (Style)**, **Daten**, **Verhalten**. JavaScript läuft hier wie in jedem Browser — als das Skript, das die Seite „lebendig" macht.

## 0. `"use strict";`

Ganz oben. Schaltet den „strengen Modus" von JavaScript ein — der Browser meckert dann bei typischen Fehlern (z.B. Tippfehler in Variablennamen), statt sie still zu schlucken. Reine Hygiene, keine Logik.

## 1. Die Brücke nach C#

```js
function postToHost(message) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);
    } else {
        console.log("[no host] " + JSON.stringify(message));
    }
}
```

`window` ist das globale Objekt jeder Browser-Seite. `window.chrome.webview` ist der Draht, den **WebView2** injiziert (gibt es nur, wenn die Seite *in* WebView2 läuft). Das `if` prüft genau das: Läuft die Seite in WebView2 → schick die Nachricht an C#. Läuft sie in einem normalen Browser (z.B. wenn du die Datei mal direkt aufmachst, um CSS zu testen) → schreib sie stattdessen in die Browser-Konsole. So bricht die Seite nie, egal wo sie läuft. `JSON.stringify` macht aus dem Objekt einen Text.

## 2. Das Aussehen — zwei Teile

### 2a. Die Farbtabelle

```js
const KIND_COLOR = {
    Class: "#f5d76e",      // gelb
    Method: "#7fb3e8",     // blau
    Interface: "#b5e7a0",  // grün
    Namespace: "#e0e0e0",  // hellgrau
};
```

Ein einfaches Nachschlage-Objekt: `KIND_COLOR["Method"]` liefert `"#7fb3e8"`. Der Schlüssel ist exakt der Name aus deinem `CodeElementType`-Enum — das ist Absicht, damit C# später einfach den Enum-Namen mitschicken kann.

### 2b. Das Stylesheet von Cytoscape

```js
const cytoscapeStyle = [ { selector: ..., style: {...} }, ... ];
```

Das **sieht** aus wie CSS, ist aber **Cytoscapes eigenes** Style-System (kein echtes CSS — es stylt Graph-Elemente, keine HTML-Elemente). Es ist eine Liste von Regeln. Jede Regel hat einen **Selektor** („auf welche Elemente?") und einen **Style-Block** („wie sehen sie aus?"). Wie bei CSS gewinnt bei mehreren Treffern die spätere/spezifischere Regel — deshalb steht die allgemeine `node`-Regel oben und Spezialfälle darunter.

Die Selektoren sind das eigentlich Neue für dich:

| Selektor                    | Trifft                                                       |
| --------------------------- | ------------------------------------------------------------ |
| `"node"`                    | **alle** Knoten                                              |
| `":parent"`                 | nur Knoten, die **Kinder enthalten** (= Container/Compound)  |
| `"node.external"`           | Knoten mit der **Klasse** `external` (gesetzt über `classes: "external"`) |
| `"node:selected"`           | gerade **angeklickte** Knoten                                |
| `"edge"`                    | **alle** Kanten                                              |
| `"edge[kind = 'Inherits']"` | Kanten, deren **Daten-Feld** `kind` gleich `"Inherits"` ist  |

Das `[kind = 'Calls']` ist der wichtige Trick für dein Lieblingsfeature: **Styling nach Datenfeld.** Jede Kante trägt ein Feld `kind` (kommt aus deinem `RelationshipType`). Der Selektor greift genau die Kanten eines Typs heraus und gibt ihnen Farbe/Strich. Deshalb sind Calls blau-durchgezogen und Inherits grau-gestrichelt — ohne dass du je manuell eine einzelne Kante anfasst.

Zwei Schreibweisen für Werte im Style-Block, die man auseinanderhalten muss:

```js
"label": "data(label)",                                  // (A) Datenbindung
"background-color": ele => KIND_COLOR[ele.data("kind")]  // (B) Funktion
```

- **(A) `"data(label)"`** ist Cytoscape-Kurzschrift: „nimm das Feld `label` aus den Knotendaten als Wert". Der Knotentext ist also immer `data.label`.
- **(B) Pfeilfunktion** `ele => ...`: für jeden Knoten wird diese Funktion aufgerufen, bekommt das Element `ele`, liest dessen `kind` und schlägt die Farbe in `KIND_COLOR` nach. `ele => x` ist nur eine kurze Funktion (`function(ele){ return x; }`). Brauchst du, wenn der Wert *berechnet* werden muss statt nur 1:1 aus einem Feld zu kommen.

Bei `:parent` (Container) setze ich `text-valign: top` (Beschriftung oben) und `background-opacity: 0.25` (durchscheinend), damit man die Kinder *im* Container sieht. Genau das ist der „Compound Node"-Look.

Ein paar Punkte zur Einordnung:

- **Es sieht nur aus wie CSS.** Cytoscape nennt es „style" und nutzt eine CSS-ähnliche Syntax (Selektoren + Property/Value), aber es stylt **Graph-Elemente** (Knoten/Kanten), nicht DOM-Elemente. Es wird von der Cytoscape-Engine interpretiert und auf das `<canvas>` gezeichnet — der Browser/CSS sieht davon nichts.
- **Eigene Properties.** `overlay-opacity` (sowie `overlay-color`, `overlay-padding`, `background-color` bei Knoten, `line-color`, `target-arrow-shape` bei Kanten …) sind **Cytoscape-Properties**, kein echtes CSS. Echtes CSS kennt `overlay-opacity` nicht.
- **Eigene Selektoren.** `.suppress-overlay` ist hier ein **Cytoscape-Klassenselektor** — er matcht Elemente, denen wir per `element.addClass("suppress-overlay")` (Cytoscape-API) die Klasse gegeben haben. Das ist **nicht** dieselbe „class" wie ein HTML/CSS-`class`-Attribut. Genauso sind `node`, `edge`, `:selected`, `[?flagged]`, `[kind = 'Calls']` Cytoscape-Selektoren, die auf den Graph-Daten/-Zuständen arbeiten.
- **Wo „echtes" CSS bei uns lebt:** nur in `style.css` — das stylt die HTML-Seite drumherum (`#cy`-Container, `#hint`-Overlay). Alles, was Knoten/Kanten betrifft, ist Cytoscape-Style in `app.js`.

Kurz: HTML/`#cy`/`#hint` → echtes CSS (style.css). Knoten/Kanten → Cytoscape-Style-System (app.js), das nur CSS-Syntax *nachahmt*.

## 3. Die Daten — Knoten und Kanten

```js
const exampleElements = [
    { data: { id: "BaseDevice", label: "BaseDevice", kind: "Class" } },
    { data: { id: "BaseDevice.Init", label: "Init()", kind: "Method", parent: "BaseDevice" } },
    ...
    { data: { id: "e2", source: "Printer.Print", target: "BaseDevice.Init", kind: "Calls" } },
];
```

Cytoscape kennt nur zwei Sorten Elemente, **beide** im selben Array, beide mit einem `data`-Objekt:

- **Knoten**: haben eine `id`. Optional `parent` → **das macht den Compound Node**. Sobald irgendein Knoten `parent: "BaseDevice"` hat, wird `BaseDevice` automatisch zum Container, der seine Kinder umschließt. Du baust die Hierarchie nicht explizit — du sagst nur bei jedem Kind, wer der Vater ist. **Das ist exakt deine `CodeElement.Parent`-Beziehung.** Eine Regel dabei: ein Knoten kann nur **einen** `parent` haben (das war mein Argument gegen das Duplizieren von Basisklassen-Methoden).
- **Kanten**: haben `source` und `target` (die `id`s der verbundenen Knoten) und bei uns `kind` (der Beziehungstyp). Cytoscape weiß: „hat `source`/`target` → ist eine Kante".

Die `id`s sind frei wählbare Strings. In Phase 1 setze ich dafür einfach `CodeElement.Id` ein, dann passt alles zusammen.

## 4. Die Graph-Instanz bauen

```js
const cy = cytoscape({
    container: document.getElementById("cy"),
    elements: exampleElements,
    style: cytoscapeStyle,
    layout: { name: "cose", padding: 20, nodeDimensionsIncludeLabels: true },
    wheelSensitivity: 0.2,
});
```

`cytoscape({...})` ist die Hauptfunktion aus der Lib. Sie bekommt vier Dinge und liefert das Graph-Objekt `cy` zurück (damit steuerst du danach alles):

- `container`: **wohin** gezeichnet wird. `document.getElementById("cy")` holt das `<div id="cy">` aus der `index.html`. (`document` = die geladene HTML-Seite.)
- `elements`: die Daten von oben.
- `style`: das Stylesheet von oben.
- `layout`: **welcher Algorithmus die Knoten positioniert.** `"cose"` ist eingebaut (force-directed). In Phase 1 wechseln wir auf `fcose`/`dagre`. `nodeDimensionsIncludeLabels: true` heißt „rechne die Beschriftungsgröße beim Platzieren mit ein", sonst überlappen Labels.
- `wheelSensitivity`: wie stark das Mausrad zoomt.

## 5. Der Eingang für C# (Phase 1)

```js
window.renderGraph = function (graph) {
    const elements = [];
    for (const n of graph.nodes) {
        elements.push({ data: { id: n.id, label: n.label, kind: n.kind,
                                parent: n.parent || undefined },
                        classes: n.external ? "external" : "" });
    }
    for (const e of graph.edges) {
        elements.push({ data: { id: e.id, source: e.source, target: e.target, kind: e.kind } });
    }
    cy.elements().remove();   // alten Graph wegräumen
    cy.add(elements);         // neue Elemente einfügen
    cy.layout({ name: "cose", padding: 20, nodeDimensionsIncludeLabels: true }).run();
};
```

Ich hänge die Funktion an `window`, damit sie **global** ist — nur so kann C# sie über `ExecuteScriptAsync("renderGraph(...)")` aufrufen. Sie nimmt ein Objekt `{ nodes: [...], edges: [...] }` (das schickt C# als JSON), übersetzt es in Cytoscape-Elemente, wirft den alten Graphen raus und legt neu aus. Zwei JS-Kleinigkeiten:

- `n.parent || undefined`: ist `parent` leer/`null`, nimm `undefined` → dann hat der Knoten keinen Container (Top-Level).
- `n.external ? "external" : ""`: der `? :`-Ausdruck setzt die CSS-Klasse `external` nur, wenn das Flag gesetzt ist → greift dann die graue `node.external`-Regel.

**Das ist die Naht zu Phase 1:** Diese Funktion existiert schon, sie wird im Moment nur von niemandem aufgerufen. In Phase 1 baue ich auf der C#-Seite genau das `{nodes, edges}`-JSON und rufe sie auf.

## 6. Das Verhalten — Events

```js
cy.on("tap", "node", evt => { postToHost({ type: "nodeClicked", id: evt.target.id() }); });
cy.on("dbltap", "node", evt => { postToHost({ type: "nodeDblClicked", id: evt.target.id() }); });

postToHost({ type: "ready" });
```

`cy.on("tap", "node", callback)` heißt: „**wenn** ein **Knoten** getippt/geklickt wird, ruf diese Funktion auf". `evt.target` ist der getroffene Knoten, `.id()` seine ID. Wir schicken sie via `postToHost` an C#. Doppelklick (`dbltap`) analog — das wird in Phase 3 Expand/Collapse.

Die allerletzte Zeile läuft **einmal beim Laden**: Sie meldet C# `{type:"ready"}` — „die Seite steht, du darfst jetzt `renderGraph` aufrufen". Das ist wichtig, weil das Laden asynchron ist: C# darf nicht rendern, bevor die JS-Seite bereit ist.

------

### Das Gesamtbild in einem Satz

Daten (`elements`) + Aussehen (`style`, gesteuert über Selektoren auf `kind`) gehen in `cytoscape({...})` rein; `cy` zeichnet und legt per Layout aus; Klicks fließen über `postToHost` raus zu C#, und C# kann über das globale `renderGraph` neue Daten reinschieben.

Drei Begriffe, die du jetzt „besitzt": **Element** (Knoten *oder* Kante mit `data`), **Selektor** (`edge[kind='Calls']` — wählt Elemente nach Daten aus), **Compound Node** (Container, entsteht automatisch durch `parent` beim Kind).

Wenn das sitzt, starten wir **Phase 1**: ein `WebGraphBuilder` in C#, der `GraphViewer.GetGraph()` in genau dieses `{nodes, edges}`-JSON übersetzt, plus die Verkabelung „bei Graph-Änderung → `renderGraph` aufrufen, sobald `ready` kam". Sag Bescheid, dann lege ich los.



# Web Graph View: Kontextmenü & Auswahl — Funktionsweise

Diese Doku erklärt Schritt für Schritt, wie das Kontextmenü im Web-Tab (WebView2 +
Cytoscape) funktioniert: wie der Rechtsklick von JavaScript nach C# gelangt, wie C#
das richtige Menü baut, **wie WPF weiß, wo das Menü hinkommt**, und **wie man an die
aktuelle Auswahl kommt**.

Beteiligte Dateien:

- `CSharpCodeAnalyst/Features/WebGraph/Web/app.js` — JS-Seite (Cytoscape, Events).
- `CSharpCodeAnalyst/Features/WebGraph/WebGraphControl.xaml.cs` — die Brücke (C#).
- `CSharpCodeAnalyst/Features/WebGraph/WebContextMenuFactory.cs` — baut die WPF-Menüs.
- `CSharpCodeAnalyst/Features/WebGraph/WebGraphBuilder.cs` — baut das JSON **und** die
  `WebEdgeInfo`-Metadaten pro Kante (Relationships hinter jeder Kante).
- `CSharpCodeAnalyst/Features/Graph/IGraphViewer.cs` / `GraphViewer.cs` — die Command-Listen.

---

## 1. Die Grundidee

Das Web-Tab soll **dieselben** Kontextmenüs anbieten wie die MSAGL-Ansicht — mit
denselben Aktionen und Icons. Statt das in JavaScript nachzubauen, nutzen wir die
**vorhandenen Command-Objekte** wieder (`ICodeElementContextCommand`,
`IRelationshipContextCommand`, `IGlobalCommand`). Diese sind render-unabhängig: sie
arbeiten auf `CodeElement`/`Relationship` und verändern den geteilten Graphen.

Deshalb gilt die Arbeitsteilung:

- **JavaScript** erkennt nur *wo* und *worauf* rechtsgeklickt wurde und meldet das.
- **C#** baut ein echtes **WPF-`ContextMenu`** aus den Command-Objekten und öffnet es.

Warum ein WPF-Menü und kein HTML-Menü? Weil die Commands WPF-`ImageSource`-Icons und
C#-Aktionen tragen. Ein WPF-Menü kann beides direkt verwenden; ein HTML-Menü müsste
Labels/Icons nach JS serialisieren und Klicks zurückrouten — mehr Aufwand, doppelte
Logik.

---

## 2. Der Gesamtablauf (Sequenz)

```
   Maus-Rechtsklick im Web-Tab
            │
            ▼
[JS] cy.on("cxttap", …)  ── erkennt Ziel (Knoten / Kante / Hintergrund)
            │  postToHost({ type:"contextMenu", kind, id })
            ▼
   window.chrome.webview.postMessage(...)          ← die Brücke JS → C#
            │
            ▼
[C#] CoreWebView2.WebMessageReceived  (läuft im UI-Thread)
            │  JSON → HostMessage
            ▼
[C#] ShowContextMenu(message)
            │  • Ziel auflösen (Knoten-id → CodeElement, Kanten-id → WebEdgeInfo,
            │    Hintergrund → Auswahl)
            │  • WebContextMenuFactory baut das ContextMenu aus den Command-Listen
            ▼
   menu.Placement = MousePoint;  menu.IsOpen = true;   ← WPF zeigt am Cursor
            │
            ▼  (User klickt einen Eintrag)
[C#] cmd.Invoke(...)  ── verändert den geteilten Graphen
            │
            ▼
   GraphChanged → Web-Tab (und MSAGL) rendern neu
```

Wichtig: Es fließen **keine Koordinaten** über die Brücke. Warum, steht in Abschnitt 5.

---

## 3. Schritt 1 — JavaScript erkennt den Rechtsklick

In `app.js` hängen drei Handler am Cytoscape-Event `cxttap` ("context tap" =
Rechtsklick):

```js
cy.on("cxttap", "node", evt => {
    postToHost({ type: "contextMenu", kind: "node", id: evt.target.id() });
});

cy.on("cxttap", "edge", evt => {
    postToHost({ type: "contextMenu", kind: "edge", id: evt.target.id() });
});

cy.on("cxttap", evt => {
    if (evt.target === cy) {            // cy == "der Hintergrund", kein Knoten/Kante
        postToHost({ type: "contextMenu", kind: "background" });
    }
});
```

- `evt.target` ist das getroffene Element. Bei einem Knoten dessen `id`
  (= `CodeElement.Id`). Bei einer Kante schicken wir deren `id` — C# hat die
  Beziehungen hinter jeder gezeichneten Kante beim Rendern unter dieser `id`
  abgelegt (siehe Abschnitt 6), muss also nichts rekonstruieren.
- Der dritte Handler ohne Selektor feuert bei **jedem** Rechtsklick; mit
  `evt.target === cy` filtern wir auf „leere Fläche".
- Dass der Browser kein eigenes Kontextmenü zeigt, ist bereits sichergestellt:
  in `WebGraphControl.InitializeWebViewAsync` steht
  `core.Settings.AreDefaultContextMenusEnabled = false`.

`postToHost` ist die einzige Ausgangstür nach C#:

```js
function postToHost(message) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);   // von WebView2 injiziert
    }
}
```

---

## 4. Schritt 2 — C# empfängt und verzweigt

WebView2 löst in C# das Event `CoreWebView2.WebMessageReceived` aus — **im
UI-Thread**, was wichtig ist, weil wir gleich WPF-UI (das Menü) anfassen.

```csharp
private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
{
    var message = JsonSerializer.Deserialize<HostMessage>(e.WebMessageAsJson, MessageJsonOptions);
    ...
    switch (message.Type)
    {
        case "contextMenu":
            ShowContextMenu(message);
            break;
        // … nodeClicked, edgeClicked, selectionChanged, ready, …
    }
}
```

`HostMessage` ist ein schlankes DTO mit allen möglichen Feldern
(`Type, Kind, Id, Ids`). `Id` trägt je nach Nachricht die Knoten-id **oder** die
Kanten-id. Pro Nachricht ist nur ein Teil gefüllt;
`PropertyNameCaseInsensitive = true` matcht die kleingeschriebenen JSON-Namen.

---

## 5. Schritt 3 — Wie WPF weiß, *wo* das Menü hinkommt

Das ist die Kernfrage. Antwort: **gar nicht über Koordinaten, sondern über die
Maus-Position selbst.**

```csharp
menu.PlacementTarget = WebView;          // an welches Control gekoppelt
menu.Placement = PlacementMode.MousePoint; // … aber positioniert am Mauszeiger
menu.IsOpen = true;
```

- `PlacementMode.MousePoint` weist WPF an, das Popup **am aktuellen Mauszeiger** zu
  öffnen. WPF liest dafür die Cursor-Position zum Zeitpunkt von `IsOpen = true`.
- Zu diesem Zeitpunkt steht die Maus noch **exakt auf dem Rechtsklick-Punkt** — der
  Anwender hat sie ja gerade erst geklickt und nicht bewegt. Der Weg
  JS → `postMessage` → `WebMessageReceived` → `ShowContextMenu` läuft in
  Millisekunden im selben UI-Tick-Umfeld.
- Deshalb brauchen wir **keine** Pixelkoordinaten aus JS und müssen auch **nichts
  umrechnen** (kein DPI-Scaling zwischen CSS-Pixeln und WPF-DIPs, keine
  Canvas-Offsets). Das ist der eigentliche Trick, der diese Lösung einfach macht.
- `PlacementTarget = WebView` koppelt das Menü logisch an das WebView-Control (für
  Fokus/Schließverhalten); die *Position* kommt von `MousePoint`.

### Airspace-Hinweis

Ein WPF-`ContextMenu` rendert in einem **eigenen Popup-Fenster** (separates
Win32-HWND). Dieses liegt z-mäßig über dem WebView2 (das selbst ein HWND ist).
Deshalb erscheint das Menü korrekt **über** der WebView — das bekannte
„Airspace"-Problem betrifft nur WPF-Inhalte im *selben* Fenster wie die WebView, nicht
separate Popup-Fenster.

---

## 6. Schritt 4 — Das richtige Ziel auflösen

`ShowContextMenu` verzweigt nach `Kind` und besorgt sich das jeweilige Ziel aus dem
**geteilten Modell** (`_state.CodeGraph` bzw. den Render-Metadaten `_edgeInfos`):

```csharp
var menu = message.Kind switch
{
    "node"       => BuildNodeMenu(graph, message.Id),
    "edge"       => BuildEdgeMenu(message.Id),
    "background" => WebContextMenuFactory.BuildForGlobal(
                        _state.GlobalCommands, GetSelectedElements(graph)),
    _ => null
};
if (menu is null || menu.Items.Count == 0) return;   // leeres Menü nicht zeigen
```

- **Knoten:** `graph.TryGetCodeElement(id)` liefert das `CodeElement`.
- **Kante:** Beim Rendern legt `WebGraphBuilder.Build` zu jeder gezeichneten Kante eine
  `WebEdgeInfo` (gezeichnete Quelle/Ziel + die zugrunde liegenden `Relationship`-Objekte)
  ab, gekeyt nach Kanten-id — das Web-Pendant zu MSAGLs `edge.UserData`. `BuildEdgeMenu`
  schlägt sie in `_edgeInfos[id]` nach. Eine *gebündelte* Kante (mehrere Beziehungen)
  trägt schlicht eine längere Liste; nichts wird aus der Topologie rekonstruiert.
- **Hintergrund:** die Auswahl (siehe Abschnitt 8).

Die Command-Listen kommen frisch aus dem Viewer (`GetNodeContextCommands()` etc.,
siehe Abschnitt 7) — es ist also *dieselbe* Registrierung wie für die MSAGL-Ansicht.

---

## 7. Schritt 5 — Das Menü bauen (WebContextMenuFactory)

`WebContextMenuFactory` spiegelt 1:1 die Menü-Logik aus `GraphViewer`. Für jeden
passenden Command entsteht ein `MenuItem`, dessen `Click` den Command aufruft:

```csharp
var menuItem = new MenuItem { Header = cmd.Label, Icon = CreateIcon(cmd.Icon) };
menuItem.Click += (_, _) => cmd.Invoke(element);   // dasselbe Command-Objekt!
menu.Items.Add(menuItem);
```

Drei Feinheiten, die das MSAGL-Verhalten exakt nachbilden:

- **Knotenmenü:** `SeparatorCommand`-Einträge werden zu `Separator`-Linien — aber nur,
  wenn der letzte Eintrag *kein* Separator war (kein doppelter/führender Trenner).
  Außerdem werden nur sichtbare, passende Commands gezeigt (`cmd.IsVisible &&
  cmd.CanHandle(element)`).
- **Kantenmenü:** Commands mit `SubMenuGroup` landen in einem gemeinsamen Untermenü
  (z.B. „Refactor"); der Rest direkt im Hauptmenü.
- **Globales Menü:** jeder Command entscheidet per `CanHandle(selectedElements)`
  selbst, ob er erscheint.

`CreateIcon` macht aus der WPF-`ImageSource` des Commands ein 16×16-`Image` — die
Icons sind also identisch zu denen in MSAGL.

### Der Klick führt aus — und schließt den Kreis

`cmd.Invoke(...)` ruft die Aktion auf, die bei der Registrierung in `GraphViewModel`
hinterlegt wurde (z.B. „Find outgoing calls", „Remove", „Collapse"). Diese Aktionen
verändern den **geteilten** Graphen über den Viewer. Das löst `GraphChanged` aus,
worauf das Web-Tab (und die MSAGL-Ansicht) neu rendern. Das Web-Menü „weiß" also
nichts über die Aktionen selbst — es ruft nur die bestehenden Objekte auf.

---

## 8. Wie man an die Auswahl kommt

Das **globale** Menü (Rechtsklick auf leere Fläche) operiert auf **allen ausgewählten**
Elementen. Die Auswahl entsteht und lebt so:

1. **Cytoscape** hat native Mehrfachauswahl (Klick = einer, Box-Ziehen = mehrere).

2. Bei jeder Änderung meldet JS die **komplette** Auswahl an C#:

   ```js
   cy.on("select unselect", "node", reportSelection);
   
   function reportSelection() {
       clearTimeout(selectionTimer);
       selectionTimer = setTimeout(() => {                 // Burst zusammenfassen
           const ids = cy.$("node:selected").map(n => n.id());
           postToHost({ type: "selectionChanged", ids: ids });
       }, 0);
   }
   ```

   Das `setTimeout(0)` bündelt die vielen Einzel-Events einer Box-Auswahl zu **einer**
   Nachricht pro Tick.

3. **C#** hält daraus die kanonische Auswahl:

   ```csharp
   private readonly HashSet<string> _selectedIds = [];
   
   private void UpdateSelection(List<string>? ids) {
       _selectedIds.Clear();
       if (ids is not null) _selectedIds.UnionWith(ids);
   }
   ```

   Bei jedem Re-Render wird `_selectedIds` geleert (die Auswahl überlebt einen
   Neuaufbau der Elemente nicht).

**Warum „eager" melden statt beim Rechtsklick abfragen?** Weil C# JavaScript nicht
*synchron* fragen kann — `ExecuteScriptAsync` ist asynchron. Würde C# beim Rechtsklick
erst die Auswahl anfordern, käme die Antwort zu spät fürs Menü. Indem JS die Auswahl
*vorab* bei jeder Änderung pusht, hat C# sie zum Klick-Zeitpunkt bereits parat.

Beim Bauen des globalen Menüs werden die IDs in `CodeElement`s aufgelöst:

```csharp
private List<CodeElement> GetSelectedElements(CodeGraph.Graph.CodeGraph graph) =>
    _selectedIds.Select(graph.TryGetCodeElement)
                .OfType<CodeElement>()      // entfernt evtl. veraltete IDs
                .ToList();
```

> Hinweis: Aktuell ist die Liste der globalen Commands praktisch leer — die Buttons in
> der Canvas-Werkzeugleiste haben diese Rolle übernommen. Das Hintergrundmenü zeigt
> daher meist nichts. Die Auswahl-Mechanik ist aber bewusst schon vorhanden, weil die
> Werkzeugleisten-Buttons später dieselbe `_selectedIds` lesen werden.

---

## 9. Warum Commands wiederverwendbar sind

Die Command-Schnittstellen sind render-unabhängig:

| Schnittstelle                 | operiert auf                      |
| ----------------------------- | --------------------------------- |
| `ICodeElementContextCommand`  | `CodeElement`                     |
| `IRelationshipContextCommand` | `List<Relationship>`              |
| `IGlobalCommand`              | `List<CodeElement>` (die Auswahl) |

Keine erwähnt MSAGL oder Cytoscape. Registriert werden sie in `GraphViewModel`
(`_viewer.AddCommand(...)`). Über die additiven Getter in `IGraphViewer`
(`GetNodeContextCommands()` …) liest die Web-Brücke **dieselben Instanzen**. Der
MSAGL-Menü-Code bleibt unangetastet (er ist Übergangscode und verschwindet beim
späteren Cutover). Beim Cutover wandert die Registrierung in eine geteilte
Registry/`GraphViewState`, und die Brücke konsumiert weiterhin dieselbe Schnittstelle.

---

## 10. Zusammenfassung in drei Sätzen

JavaScript meldet nur *Art* und *Ziel* des Rechtsklicks (`kind` + IDs), ohne
Koordinaten. C# löst das Ziel im geteilten Graphen auf, baut aus den vorhandenen
Command-Objekten ein WPF-`ContextMenu` und öffnet es mit `PlacementMode.MousePoint`
direkt am Mauszeiger. Ein Klick ruft das bestehende Command auf, das den geteilten
Graphen ändert — woraufhin sich beide Ansichten über `GraphChanged` aktualisieren.





| Verbleibt auf `_viewer`             | Warum                                                        |
| :---------------------------------- | :----------------------------------------------------------- |
| `GetSelectedElementIds` (3×)        | **Auswahl** — MSAGL-spezifisch (`MarkedForDragging`), kommt beim Cutover ins Modell |
| `ToggleFlag` / `ClearAllFlags` (3×) | **Flag-Dekoration** — Daten liegen in `state.PresentationState`, aber die Operation macht MSAGL-Decoration-Refresh; Web zeigt Flags noch nicht |
| `UpdateRenderOption`                | MSAGL-Layout-Richtung                                        |
| `SetQuickInfoFactory`               | MSAGL-Quick-Info                                             |
| `Layout`                            | MSAGL-Relayout                                               |
| `SaveToSvg`                         | MSAGL-Export                                                 |
| `ShowGlobalContextMenu`             | MSAGL-Hit-Testing                                            |
| `ClearQuickInfo`                    | MSAGL-„last clicked object"                                  |




# Web Graph View: Initialisierung des `WebGraphControl`

Im Fokus: die asynchrone WebView2-Initialisierung, das „ready"-Handshake zwischen C# und
JavaScript, der Platzhalter gegen den weißen Kaltstart und die HTML-Hilfe, die gezeigt
wird, solange noch kein Graph da war.

Beteiligte Dateien:
- `CSharpCodeAnalyst/Features/WebGraph/WebGraphControl.xaml(.cs)` — der WPF-Host (die Brücke).
- `CSharpCodeAnalyst/Features/WebGraph/Web/index.html` — die Seite (Layout + Hilfe-Overlay).
- `CSharpCodeAnalyst/Features/WebGraph/Web/app.js` — die JS-Seite (Cytoscape, `renderGraph`, Hint).
- `CSharpCodeAnalyst/Features/WebGraph/Web/style.css` — Styling von Canvas und Hint.
- `CSharpCodeAnalyst/App.xaml.cs`, `MainWindow.xaml(.cs)` — Verdrahtung und Tab-Reihenfolge.

Die Initialisierung hat **vier ineinandergreifende Aspekte**:

1. **Wann** initialisiert wird (Tab-Reihenfolge → Eager-Init).
2. **Wie** der WebView2 asynchron hochgefahren wird (`InitializeWebViewAsync`).
3. Das **„ready"-Handshake** (JS meldet C#, dass gerendert werden darf).
4. Die **Übergangs-/Leerzustände**: Kaltstart-Platzhalter und HTML-Hilfe.

---

## A. Wann: Tab-Reihenfolge erzwingt Eager-Init

Der WebView2 kann sich nur initialisieren, wenn das Control **im Visual Tree** liegt
(es braucht ein Fensterhandle). Ein WPF-`TabControl` realisiert aber nur den Inhalt des
**aktuell gewählten** Tabs — der Inhalt nicht gewählter Tabs ist nicht im Visual Tree.

Deshalb ist das „Web View"-`TabItem` in `MainWindow.xaml` bewusst das **erste** Tab
(Index 0) der `WorkingArea`. Da `MainViewModel.SelectedRightTabIndex` standardmäßig `0`
ist, ist das Web-Tab beim App-Start aktiv → sein Inhalt wird sofort realisiert →
`Loaded` feuert → der WebView2 initialisiert **während des Hochfahrens**, nicht erst beim
ersten manuellen Tab-Wechsel.

---

## B. Verdrahtung beim Start (vor der Init)

`App.StartUi` erzeugt **eine** `GraphViewState` und den `MessageBus` und reicht beides an
`MainWindow.SetViewer` weiter, das wiederum `WebGraphControl.SetViewer(state, publisher, subscriber)`
aufruft — **einmalig**, noch bevor das Fenster gezeigt wird:

```csharp
public void SetViewer(GraphViewState state, IPublisher publisher, ISubscriber subscriber)
{
    _state = state;
    _publisher = publisher;
    _state.Changed += OnStateChanged;              // Modelländerung -> neu rendern
    _state.HighlightModeChanged += OnHighlightModeChanged;

    // Render-only Befehle aus dem Ribbon (Layout-Splitbutton) laufen über den Bus,
    // weil sie das Modell NICHT ändern (also nicht über Changed kommen).
    subscriber.Subscribe<RelayoutGraphRequest>(_ => Dispatcher.Invoke(Relayout));
    subscriber.Subscribe<RefitGraphRequest>(_ => Dispatcher.Invoke(Refit));
}
```

Wichtig: `SetViewer` setzt nur **Referenzen und Abos**. Es startet die WebView2-Init
**nicht** — das passiert getrennt über `Loaded` (Abschnitt C). Weil `SetViewer` vor dem
Anzeigen läuft, steht `_state` garantiert bereit, bevor das erste „ready" eintrifft.

---

## C. Wie: die asynchrone WebView2-Initialisierung

### C.1 Einstieg über `Loaded` (genau einmal)

```csharp
private async void OnLoaded(object sender, RoutedEventArgs e)
{
    // Loaded kann mehrfach feuern (Tab-Wechsel). Nur einmal initialisieren.
    if (_initialized) return;
    _initialized = true;

    try
    {
        await InitializeWebViewAsync();
    }
    catch (Exception ex)
    {
        // Eine fehlende WebView2-Runtime soll nicht die ganze App mitreißen.
        Debug.WriteLine($"[WebGraph] WebView2 init failed: {ex}");
        MessageBox.Show($"WebView2 initialization failed:\n{ex.Message}", "Web Graph View",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
```

- **`async void`** ist hier korrekt: Es ist ein Event-Handler. Der `try/catch` fängt alles
  ab — eine nicht installierte WebView2-Runtime oder ein Init-Fehler führt zu einer
  Meldung statt zu einem Absturz.
- Das **`_initialized`-Flag** schützt gegen mehrfaches `Loaded` (WPF feuert es bei
  Tab-Wechseln erneut), damit der WebView2 nicht doppelt aufgesetzt wird.

### C.2 Die Schritte in `InitializeWebViewAsync`

```csharp
private async Task InitializeWebViewAsync()
{
    // (1) Schreibbarer User-Data-Ordner – funktioniert auch unter Program Files.
    var userDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CSharpCodeAnalyst", "WebView2");
    Directory.CreateDirectory(userDataFolder);

    // (2) Transparenter Default-Hintergrund: gegen das weiße Aufblitzen beim Kaltstart.
    WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

    // (3) Environment + Core asynchron erzeugen (der teure Teil, ~1-2 s beim ersten Start).
    var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
    await WebView.EnsureCoreWebView2Async(environment);

    var core = WebView.CoreWebView2;

    // (4) Entwicklung: HTML/JS nie cachen, damit Änderungen sofort greifen.
    await core.CallDevToolsProtocolMethodAsync(
        "Network.setCacheDisabled", "{\"cacheDisabled\":true}");

    // (5) Lokalen Web-Ordner (neben der Exe) unter dem virtuellen Host bereitstellen.
    var webRoot = Path.Combine(AppContext.BaseDirectory, "Features", "WebGraph", "Web");
    core.SetVirtualHostNameToFolderMapping(
        VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);

    // (6) Eingehende Nachrichten aus JS abonnieren.
    core.WebMessageReceived += OnWebMessageReceived;

    // (7) Browser-eigenes Kontextmenü aus; DevTools an (hilfreich beim Entwickeln).
    core.Settings.AreDefaultContextMenusEnabled = false;
    core.Settings.AreDevToolsEnabled = true;

    // (8) Seite laden – dies stößt das Laden von index.html + app.js an.
    WebView.Source = new Uri($"https://{VirtualHost}/index.html");
}
```

| Schritt | Zweck |
|---|---|
| (1) User-Data-Ordner | WebView2 (Chromium) legt Cache/Profil ab. Explizit nach `%LocalAppData%`, damit die App auch aus einem schreibgeschützten Pfad (z. B. `Program Files`) läuft. |
| (2) `DefaultBackgroundColor = Transparent` | Siehe Abschnitt D — verhindert den weißen Kaltstart, indem der WPF-Platzhalter durchscheint. **Muss vor** `EnsureCoreWebView2Async` gesetzt sein. |
| (3) `CreateAsync` + `EnsureCoreWebView2Async` | Der eigentliche, **asynchrone** Kaltstart: Ein Browser-Prozess wird hochgefahren. Der Teil, der beim allerersten Start ~1–2 s dauert. |
| (4) `Network.setCacheDisabled` | Reiner Entwickler-Komfort: editierte Assets werden nie aus dem Cache serviert. |
| (5) `SetVirtualHostNameToFolderMapping` | Mappt `https://csharp-code-analyst.local/` auf den lokalen Ordner `…/Features/WebGraph/Web`. So bleiben alle Ressourcen **offline** und über eine stabile, sichere Origin erreichbar (statt `file://`). Details im Phase-0-Abschnitt oben. |
| (6) `WebMessageReceived` | Der einzige Rückkanal JS → C# (siehe Kontextmenü-Doku). |
| (7) Settings | Kein Browser-Kontextmenü (wir bauen ein WPF-Menü); DevTools bleiben für die Entwicklung aktiv. |
| (8) `WebView.Source = …` | Navigiert zur Seite. Das Laden ist **wieder asynchron**: Wenn `Source` zugewiesen wird, ist die Seite noch nicht fertig — erst recht nicht das JS. |

> **Merke:** Nach `InitializeWebViewAsync` ist der WebView2 *bereit*, aber die **Seite ist
> noch nicht gerendert**. Deshalb darf C# hier noch **nicht** `renderGraph(...)` aufrufen.
> Der richtige Zeitpunkt kommt erst mit dem „ready"-Signal (Abschnitt E).

---

## D. Der Kaltstart-Platzhalter (gegen die weiße Seite)

Während des Kaltstarts (Schritt 3) und bis die Seite gemalt ist, zeigt der WebView2
ansonsten eine **weiße Fläche** (~2 s beim ersten App-Start). Das wirkt wie ein Hänger.
Lösung: ein WPF-Platzhalter.

### Warum nicht einfach ein WPF-Overlay *über* die WebView?

Wegen **Airspace**: Der WebView2 ist ein eigenes Win32-Fenster (HWND). WPF-Inhalt im
*selben* Fenster kann nicht **über** diesem HWND gezeichnet werden — ein Overlay obendrauf
wäre unsichtbar. (Nur separate Popup-Fenster wie das Kontextmenü liegen darüber.)

### Der Trick: transparenter WebView-Hintergrund + Platzhalter *dahinter*

`WebGraphControl.xaml` legt den Platzhalter **vor** die WebView (also darunter im
Z-Order), und die WebView bekommt einen **transparenten** Default-Hintergrund:

```xml
<Grid>
    <!-- Liegt hinter der WebView und scheint durch, solange die Seite noch nicht malt. -->
    <TextBlock x:Name="LoadingOverlay"
               Text="{x:Static resources:Strings.WebView_Initializing}"
               HorizontalAlignment="Center" VerticalAlignment="Center"
               FontSize="18" Foreground="#80868b" />
    <wv2:WebView2 x:Name="WebView" />
</Grid>
```

Ablauf:
1. Solange der WebView2 nichts malt (Kaltstart), ist er **durchsichtig** → der `LoadingOverlay`
   („Initializing graph view…") scheint **durch**. Kein weißes Aufblitzen.
2. Sobald `index.html` lädt, malt dessen **deckend weißer** Body über die transparente
   Fläche → der Platzhalter ist optisch verdeckt.
3. Beim „ready"-Signal wird der Platzhalter zusätzlich endgültig **collapsed** (Abschnitt E).

> Der Tab-Header und der Platzhalter-Text sind lokalisiert
> (`Strings.WebView_TabHeader`, `Strings.WebView_Initializing`).

---

## E. Das „ready"-Handshake (JS → C#) — und warum asynchron

C# kann JavaScript **nicht synchron** fragen, ob die Seite bereit ist
(`ExecuteScriptAsync` ist asynchron). Stattdessen meldet sich **JS von selbst**, wenn es
so weit ist. Ganz am Ende von `app.js` steht:

```js
// Tell the host we are ready to receive renderGraph() calls.
postToHost({ type: "ready" });
```

C# empfängt das in `OnWebMessageReceived` und reagiert genau dann mit dem ersten Rendern:

```csharp
case "ready":
    _isWebReady = true;

    // Die Seite ist gemalt; den Kaltstart-Platzhalter endgültig wegnehmen.
    LoadingOverlay.Visibility = Visibility.Collapsed;

    RenderCurrentGraph();                        // erstes (evtl. leeres) Rendern
    if (_state is not null)
        PushHighlightMode(_state.HighlightMode); // aktuellen Ribbon-Highlight-Modus nach JS spiegeln
    break;
```

- **`_isWebReady`** ist das Gate: Vor „ready" wird nichts gerendert; alle Render-/Layout-
  Pfade (`RenderCurrentGraph`, `Relayout`, `Refit`, `PushHighlightMode`) greifen erst danach.
- `RenderCurrentGraph` baut aus `_state.CodeGraph` das JSON (über `WebGraphBuilder.Build`)
  und ruft `renderGraph(json)` in JS. Ohne geladenes Projekt ist der Graph **leer** —
  und genau dann greift die HTML-Hilfe (Abschnitt F).

### Gesamtsequenz

```
App-Start
   │  Web-Tab ist Tab 0 -> Inhalt wird realisiert
   ▼
[WPF] Loaded -> OnLoaded -> InitializeWebViewAsync   (async; LoadingOverlay sichtbar)
   │   CreateAsync / EnsureCoreWebView2Async (~1-2 s)
   │   VirtualHost-Mapping, Settings, WebView.Source = index.html
   ▼
[Browser] lädt index.html + lib/* + app.js
   ▼
[JS]  cytoscape() erzeugt (im Host: leer)  ->  postToHost({type:"ready"})
   ▼
[WPF] OnWebMessageReceived "ready"
   │   _isWebReady = true
   │   LoadingOverlay.Visibility = Collapsed
   │   RenderCurrentGraph()  -> renderGraph({nodes:[],edges:[]})  (leer -> Hint bleibt)
   ▼
[JS]  renderGraph: nodes.length === 0  ->  Hint bleibt sichtbar
```

Sobald später ein Graph entsteht (Projekt laden / Elemente hinzufügen), löst
`GraphViewState.Changed` ein erneutes `RenderCurrentGraph` aus — diesmal mit Knoten, und
der Hint verschwindet (Abschnitt F).

---

## F. Die HTML-Hilfe (Leerzustand)

Solange noch **kein Graph** gezeichnet wurde, zeigt der Web-Tab eine kurze Anleitung —
das Pendant zur früheren MSAGL-Canvas-Hilfe (`IsCanvasHintsVisible`). Sie ist jetzt
**reines HTML** und lebt vollständig auf der JS-Seite (kein C#-Zustand nötig).

### Markup (`index.html`)

```html
<div id="cy"></div>

<!-- Sichtbar bis zum ersten Graphen, dann für die Sitzung ausgeblendet. -->
<div id="hint">
    <h2>Code graph view</h2>
    <p>Empty until code elements are added — load a project, or add elements from the tree or search.</p>
    <ul>
        <li><b>Double-click</b> a node — expand / collapse</li>
        <li><b>Click</b> a node or edge — show details in the Info tab</li>
        <li><b>Right-click</b> — context menu with commands</li>
        <li><b>Drag</b> the background to pan, <b>mouse wheel</b> to zoom</li>
        <li><b>Shift + drag</b> — select multiple nodes</li>
    </ul>
</div>
```

Der `#hint` liegt als CSS-Overlay über dem Canvas, ist aber **`pointer-events: none`** —
Klicks gehen also durch zum Graphen (`style.css`).

### Ein-/Ausblenden (`app.js`)

Beim Start im Host startet Cytoscape **leer** (das Demo-Beispiel nur im Standalone-Browser),
damit der Hint über leerer Fläche steht:

```js
const inHost = !!(window.chrome && window.chrome.webview);
const cy = cytoscape({
    container: document.getElementById("cy"),
    elements: inHost ? [] : exampleElements,   // im Host leer
    style: cytoscapeStyle,
    layout: LAYOUT,
});
```

Der Hint wird **einmalig und endgültig** ausgeblendet, sobald der erste Graph mit Knoten
gerendert wird:

```js
let hintDismissed = false;

function dismissHintIfGraph(graph) {
    if (hintDismissed || !graph.nodes || graph.nodes.length === 0) return;
    hintDismissed = true;
    document.getElementById("hint")?.classList.add("hidden");
}

window.renderGraph = function (graph) {
    dismissHintIfGraph(graph);
    // … Elemente bauen, cy.add, Layout …
};
```

- **Einwegschalter:** Einmal ausgeblendet, kommt der Hint in derselben Sitzung nicht
  wieder — selbst wenn der Graph später wieder geleert wird. Das spiegelt exakt das
  MSAGL-Verhalten (`IsCanvasHintsVisible` geht nie zurück auf `true`).
- **Pro Sitzung:** Bei einem App-Neustart wird die Seite frisch geladen, der Hint ist
  also wieder da, solange noch kein Graph existiert.

---

## G. Sichtbarkeit & verzögertes Rendern

Cytoscape (fcose) braucht einen **korrekt dimensionierten** Container. Wenn der Web-Tab
verborgen ist, hat der WebView keine Größe — Layouten würde ins Leere laufen. Deshalb:

- **`OnStateChanged`** (Modell hat sich geändert): Ist der Tab **sichtbar**, wird über
  einen `DispatcherTimer` (120 ms) **entprellt** — viele schnelle Änderungen führen zu
  *einem* (teuren) Re-Layout. Ist der Tab **verborgen**, wird nur `_pendingRender = true`
  gesetzt.
- **`OnIsVisibleChanged`** (Tab wird wieder sichtbar): Bei `_pendingRender` wird jetzt mit
  korrekter Größe **gerendert**; sonst nur `refitGraph()` (Größe/Ausschnitt auffrischen,
  Positionen bleiben).

Dieselbe Logik gilt für die Ribbon-Befehle: `Relayout` bei verborgenem Tab setzt
`_pendingRender`; `Refit` ist dann ein No-Op (wird beim Einblenden nachgeholt).

---

## Zusammenfassung in fünf Sätzen

Das Web-Tab ist das **erste** Tab, damit der WebView2 schon beim App-Start (statt erst
beim ersten Öffnen) **asynchron** initialisiert. Während dieses Kaltstarts überbrückt ein
WPF-Platzhalter die sonst weiße Fläche, indem der WebView2 einen **transparenten**
Hintergrund bekommt und der Platzhalter **dahinter** durchscheint (ein Overlay *darüber*
ginge wegen Airspace nicht). Weil C# JS nicht synchron fragen kann, meldet die JS-Seite per
**„ready"** zurück, dass gerendert werden darf; erst dann setzt C# `_isWebReady`, blendet
den Platzhalter aus und ruft das erste `renderGraph` auf. Solange dieser Graph **leer**
ist, zeigt eine **HTML-Hilfe** die Bedienung; sie verschwindet einmalig und für die
restliche Sitzung, sobald der erste Graph mit Knoten gerendert wurde. Alle weiteren Renders
folgen aus `GraphViewState.Changed` (entprellt, sichtbarkeitsbewusst), Layout/Refit dagegen
aus Ribbon-Befehlen über den MessageBus.

# Airspace

„Airspace" ist ein bekannter Begriff aus der WPF-/Win32-Welt. Er beschreibt eine Einschränkung beim Mischen von zwei unterschiedlichen Rendering-Technologien im **selben Fenster**.

## Die Grundregel

Ein Stück Bildschirmfläche („airspace") kann immer nur von **einer** Rendering-Technologie bespielt werden. Sie können sich nicht überlappen oder vermischen.

WPF zeichnet seine Steuerelemente (Buttons, TextBlocks, Grids …) alle zusammen auf **eine einzige** DirectX-Fläche — das gesamte WPF-Fenster ist im Grunde *ein* Win32-Fenster (HWND), in das WPF alles selbst hineinmalt.

Manche Controls sind aber **kein** WPF-Rendering, sondern haben ein **eigenes, echtes Win32-Fenster (HWND)** dahinter. Solche „HWND-Inseln" nennt man *airspace-Controls*. Klassische Beispiele:

- `WebView2` (eingebetteter Chromium-Browser),
- der alte `WindowsFormsHost`,
- `DirectX`/Video-Overlays.

## Warum das ein Problem ist

Das HWND eines solchen Controls wird vom **Betriebssystem** gezeichnet, **nachdem** WPF sein Fenster gemalt hat — und es liegt immer **obenauf**. WPF kann nicht „darüber" malen, weil das fremde HWND die Fläche selbst kontrolliert.

```
WPF-Fenster (ein HWND)
 ├─ WPF-Inhalt (Buttons, Text, Overlays …)   ← alles auf EINER WPF-Fläche
 └─ WebView2 (eigenes HWND)                   ← liegt darüber, OS-gezeichnet
        ▲
        └── WPF-Element, das ich hier drüberlegen will → unsichtbar (Airspace!)
```

In unserem konkreten Fall:

- Wollte ich einen WPF-Ladehinweis **über** die WebView legen → ginge nicht, das HWND der WebView verdeckt ihn.
- Deshalb der Trick: WebView-Hintergrund **transparent** + Platzhalter **dahinter** (in derselben WPF-Fläche, die durchscheint).



## Die Ausnahme: Popup-Fenster

Das Kontextmenü funktioniert trotzdem über der WebView — weil ein WPF-`ContextMenu`/`Popup` ein **eigenes, separates** Top-Level-HWND ist. Es liegt im Z-Order *über* dem WebView2-HWND (Geschwister-Fenster, nicht „im selben airspace"). Airspace betrifft nur WPF-Inhalt, der mit der HWND-Insel **dieselbe** Fläche im **selben** Fenster teilt.

## Kurz gesagt

> **Airspace** = die Regel, dass eine WPF-„Insel" mit eigenem Win32-Fenster (wie WebView2) eine Bildschirmfläche exklusiv belegt; normaler WPF-Inhalt im selben Fenster kann sich nicht damit überlappen (weder darüber liegen noch durchscheinen). Nur separate Popup-Fenster umgehen das.

Randnotiz: Bei *neueren* WebView2-Versionen gibt es auch einen „Composition"-Hosting-Modus (`CoreWebView2CompositionController` bzw. die `WebView2CompositionControl`), der die WebView in die WPF-Komposition einbindet und das Airspace-Problem ganz vermeidet — den nutzen wir hier aber nicht; wir lösen es über den transparenten Hintergrund.