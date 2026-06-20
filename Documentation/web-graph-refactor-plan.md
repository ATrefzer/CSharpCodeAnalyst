# Refactor-Plan: `GraphViewState` (geteiltes Modell) + Render-Adapter

Ziel: den heutigen `GraphViewer` (der **Zustand + MSAGL-Darstellung + Command-Routing**
in einer Klasse vermischt) in zwei Verantwortlichkeiten auftrennen:

1. **`GraphViewState`** — ein render-unabhängiges Modell, das *beide* Ansichten
   (MSAGL und Web) speisen. Reines C#, keine UI.
2. **Render-Adapter** — beobachten den State und zeichnen; übersetzen UI-Events
   zurück in State-Operationen. MSAGL-Adapter = Übergang, Web-Adapter = Zukunft.

Der Code sieht das schon vor: `GraphViewer` trägt den Kommentar
*„If ever the MSAGL is replaced this is the adapter to re-write."*

---

## 1. Endzustand

```
GraphViewState  (pure C#, no UI, UNIT-TESTBAR)
  • CodeGraph (Arbeitsgraph)
  • PresentationState (collapsed / flags / search highlights)
  • GraphHideFilter
  • Anzeige-Schalter: ShowFlat, ShowInformationFlow
  • HighlightMode
  • Command-Registry (node / edge / global)
  • Operationen: Add/RemoveFromGraph, Collapse/Expand, Flags,
    SearchHighlights, HideFilter, Sessions (Get/Load), Clear
  • Event: Changed            ← ersetzt GraphChanged
        ▲                 ▲
        │ beobachtet      │ beobachtet
   MSAGL-Adapter      Web-Adapter
   (heutiger          (WebGraphControl
    GraphViewer,       + Bridge
    abgespeckt)        + WebGraphBuilder)
```

- **GraphViewModel** redet für *Modell*-Operationen mit dem `GraphViewState`, für
  *render-spezifische* Dinge (Auswahl, Export, Tastatur) mit dem aktiven Adapter.
- **Hauptgewinn:** Jede Zustandsänderung benachrichtigt über **ein** `Changed`
  *beide* Ansichten. Damit verschwindet die ganze Fehlerklasse „Command wirkt in
  MSAGL, aber nicht im Web" (wie wir sie bei Collapse/Expand hatten). Außerdem wird
  das Modell isoliert testbar.

---

## 2. Was wandert wohin (Inventar von `GraphViewer`)

### In `GraphViewState` (render-unabhängig)
| Heute in GraphViewer | Operation(en) |
|---|---|
| `_clonedCodeGraph` | `GetGraph`, `AddToGraph`, `RemoveFromGraph` (×2) |
| `_presentationState` | `Collapse`/`Expand`/`IsCollapsed`, `ToggleFlag`/`IsFlagged`/`ClearAllFlags`, `SetSearchHighlights`/`ClearSearchHighlights` |
| `_hideFilter` | `SetHideFilter`/`GetHideFilter` |
| `_showFlatGraph`, `_flow` | `ShowFlatGraph`, `ShowInformationFlow` |
| `_highlightMode` | `SetHighlightMode`/`GetHighlightMode` + `HighlightModeChanged` |
| `_nodeCommands`/`_edgeCommands`/`_globalCommands` | `AddCommand` (×3), `Get*ContextCommands` |
| Sessions | `GetSession`, `LoadSession` (×2), `Clear` |
| `GraphChanged` | wird zu `Changed` |

### Bleibt im MSAGL-Adapter (heutiger `GraphViewer`)
`_msaglViewer`, `Bind` (`IGraphBinding`), `RefreshGraph` (MSAGL-Aufbau),
`ClickController` + `OnLeftSingleClick`/`OnLeftDoubleClick`/`OnOpenContextMenu`,
Highlight-**Rendering** (`IGraphViewerHighlighting`, `_activeHighlighting`),
`SaveToSvg`, `TryHandleKeyEvent`, `GetSelectedElementIds` (MSAGL `MarkedForDragging`),
`ShowGlobalContextMenu`, Quick-Info-Publish bei Klick, Groß-Graph-Warnung,
`RenderOption` (Layout-Richtung), `_publisher`.

### Offene Designentscheidungen
- **Auswahl (`GetSelectedElementIds`):** vorerst **pro Adapter** lassen (MSAGL:
  `MarkedForDragging`; Web: `_selectedIds` in der Bridge). Erst beim finalen Cutover
  in den State (oder eine geteilte Auswahl) heben. *Empfehlung: später.*
- **`RenderOption` (Layout-Richtung):** vorerst **pro Adapter** (MSAGL-spezifische
  Klassen; Web nutzt fcose). Optional später als neutrales Enum in den State.
- **`QuickInfoFactory`:** jeder Adapter baut sie aus `state.CodeGraph` (der Web-Adapter
  macht das bereits). Keine Verlagerung nötig.
- **`IGraphViewer`:** wird zur Schnittstelle des **MSAGL-Adapters**. Der Web-Adapter
  hängt künftig direkt an `GraphViewState`, nicht mehr an `IGraphViewer`.

---

## 3. Phasen (jede Phase baut & MSAGL bleibt funktionsfähig)

### Phase R1 — `GraphViewState` einführen, `GraphViewer` delegiert
- Neue Klasse `GraphViewState` mit den Zustandsfeldern/Operationen + `Changed`.
- `GraphViewer` hält eine `GraphViewState`-Instanz und **leitet weiter**
  (`GetGraph` → `state`, `Collapse` → `state`, …). Die `IGraphViewer`-Oberfläche
  bleibt **unverändert** → `GraphViewModel`, Tests, App-Wiring unangetastet.
- `GraphViewer` abonniert `state.Changed` → `RefreshGraph`; `GraphChanged` wird zum
  Weiterreichen von `state.Changed`.
- **Verifikation:** App verhält sich identisch (MSAGL + Web laufen wie bisher; das Web
  hängt noch über `IGraphViewer.GraphChanged`, das jetzt `state.Changed` spiegelt).
- **Risiko: niedrig** (reines Verschieben + Delegation).

### Phase R2 — Web-Adapter hängt direkt am `GraphViewState`
- App erzeugt **eine** `GraphViewState` und injiziert sie in **beide** —
  `GraphViewer` (MSAGL) und `WebGraphControl`.
- `WebGraphControl`: `_viewer`-Aufrufe durch `_state`-Aufrufe ersetzen
  (`GetGraph`→`state.CodeGraph`, `GraphChanged`→`state.Changed`,
  `IsCollapsed`/`Expand`/`Collapse`→`state`, Command-Getter→`state`,
  HighlightMode→`state`). Auswahl bleibt in der Bridge.
- `WebGraphControl` braucht `IGraphViewer` nicht mehr.
- **Verifikation:** Web wird vom State getrieben; MSAGL läuft weiter.
- **Risiko: niedrig–mittel** (fokussiert in `WebGraphControl` + App-Wiring).

### Phase R3 — Command-Registry + Anzeige-Schalter in den State; ViewModel an den State
- Command-Registrierung: `GraphViewModel` registriert auf dem **State**
  (`state.AddCommand`); `GraphViewer` liest fürs Menü aus dem State.
- `ShowFlat`/`ShowInformationFlow`/`HighlightMode`-Setter im `GraphViewModel` von
  `_viewer` auf `state` umstellen.
- Der **Web-Builder** berücksichtigt jetzt `ShowFlat`/`ShowInformationFlow`
  (Paritätsgewinn — heute ignoriert das Web diese Schalter).
- **Verifikation:** beide Ansichten reagieren auf die Schalter.
- **Risiko: mittel** (berührt `GraphViewModel` + beide Adapter).

### Phase R4 — Cutover-Vorbereitung & Abriss
- Auswahl in den State (oder geteilt) heben; Toolbar-Buttons darauf umstellen.
- Dann MSAGL-Adapter, `IGraphBinding`, MSAGL-Referenzen entfernen.
- (Separat: restliche Web-Commands fertigstellen, Export, Drag-&-Drop, Tastatur.)

---

## 4. Verifikationsstrategie

- Nach **jeder** Phase: Build + Testsuite. (Hinweis: die Tests decken v.a.
  Parser/Builder ab, nicht `GraphViewer` direkt — aber `GraphViewState` wird neu
  **unit-testbar**; einfache Tests für Collapse/Expand/Add/Remove/Sessions lohnen sich.)
- Manuell nach jeder Phase: **MSAGL- und Web-Tab** funktionieren beide.
- Weil MSAGL bis zuletzt lauffähig bleibt, haben wir immer eine funktionierende
  Referenz zum Vergleichen.

---

## 5. Warum diese Reihenfolge sicher ist

R1 hält die gesamte externe Oberfläche identisch (Delegation) → nichts anderes ändert
sich. Jede spätere Phase migriert **einen** Konsumenten nach dem anderen. MSAGL bleibt
bis zum Schluss funktionsfähig. Erst wenn der Web-Weg vollständig auf dem State sitzt
und die Parität steht, fällt der MSAGL-Adapter weg.

---

## 6. Empfohlener Startpunkt

**Phase R1** — sie ist risikoarm, ändert kein Verhalten, und schafft sofort das
geteilte Modell, an das wir in R2 den Web-Adapter hängen. Danach entscheiden wir
anhand des Ergebnisses über das Tempo der weiteren Phasen.
