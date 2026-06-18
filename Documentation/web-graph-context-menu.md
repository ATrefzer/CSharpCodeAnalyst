# Web Graph View: Kontextmenü & Auswahl — Funktionsweise

Diese Doku erklärt Schritt für Schritt, wie das Kontextmenü im Web-Tab (WebView2 +
Cytoscape) funktioniert: wie der Rechtsklick von JavaScript nach C# gelangt, wie C#
das richtige Menü baut, **wie WPF weiß, wo das Menü hinkommt**, und **wie man an die
aktuelle Auswahl kommt**.

Beteiligte Dateien:
- `CSharpCodeAnalyst/Features/WebGraph/Web/app.js` — JS-Seite (Cytoscape, Events).
- `CSharpCodeAnalyst/Features/WebGraph/WebGraphControl.xaml.cs` — die Brücke (C#).
- `CSharpCodeAnalyst/Features/WebGraph/WebContextMenuFactory.cs` — baut die WPF-Menüs.
- `CSharpCodeAnalyst/Features/WebGraph/WebGraphBuilder.cs` — Hilfsmethode für Kanten.
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
            │  postToHost({ type:"contextMenu", kind, id | source/target })
            ▼
   window.chrome.webview.postMessage(...)          ← die Brücke JS → C#
            │
            ▼
[C#] CoreWebView2.WebMessageReceived  (läuft im UI-Thread)
            │  JSON → HostMessage
            ▼
[C#] ShowContextMenu(message)
            │  • Ziel auflösen (id → CodeElement, source/target → Relationships,
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
    postToHost({ type: "contextMenu", kind: "edge",
                 source: evt.target.data("source"), target: evt.target.data("target") });
});

cy.on("cxttap", evt => {
    if (evt.target === cy) {            // cy == "der Hintergrund", kein Knoten/Kante
        postToHost({ type: "contextMenu", kind: "background" });
    }
});
```

- `evt.target` ist das getroffene Element. Bei einem Knoten dessen `id`
  (= `CodeElement.Id`). Bei einer Kante schicken wir `source`/`target` (die IDs der
  verbundenen Knoten) — daraus rekonstruiert C# später die Beziehungen.
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
(`Type, Kind, Id, Source, Target, Ids`). Pro Nachricht ist nur ein Teil gefüllt;
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
**geteilten Graphen** (`_viewer.GetGraph()`):

```csharp
var menu = message.Kind switch
{
    "node"       => BuildNodeMenu(graph, message.Id),
    "edge"       => BuildEdgeMenu(graph, message.Source, message.Target),
    "background" => WebContextMenuFactory.BuildForGlobal(
                        _viewer.GetGlobalContextCommands(), GetSelectedElements(graph)),
    _ => null
};
if (menu is null || menu.Items.Count == 0) return;   // leeres Menü nicht zeigen
```

- **Knoten:** `graph.TryGetCodeElement(id)` liefert das `CodeElement`.
- **Kante:** Eine gezeichnete Kante ist *gebündelt* (eine pro Richtung, fasst mehrere
  Beziehungen zusammen). `WebGraphBuilder.GetBundledRelationships(graph, isCollapsed,
  source, target)` rekonstruiert daraus mit derselben Sichtbarkeits-/Rerouting-Logik
  wie beim Zeichnen die Liste der echten `Relationship`-Objekte.
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

| Schnittstelle | operiert auf |
|---|---|
| `ICodeElementContextCommand` | `CodeElement` |
| `IRelationshipContextCommand` | `List<Relationship>` |
| `IGlobalCommand` | `List<CodeElement>` (die Auswahl) |

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
