// Web Graph View - renders the code graph with Cytoscape.js.
// Data is pushed from C# via renderGraph(); user events go back via postToHost().

"use strict";

// ---- Diagnostics ------------------------------------------------------------
// Some layout extensions (notably ELK) run asynchronously, so a failure surfaces
// as an unhandled promise rejection AFTER the synchronous run() returned — invisible
// without this. Tag such errors clearly so they are easy to spot in DevTools (F12).
window.addEventListener("unhandledrejection", evt => {
    console.error("[WebGraph] unhandled promise rejection (async layout?):", evt.reason);
});
window.addEventListener("error", evt => {
    console.error("[WebGraph] error:", evt.message, evt.error);
});

// ---- Bridge: JS -> C# -------------------------------------------------------
// window.chrome.webview is injected by WebView2. Guard it so the page also works
// when opened in a plain browser (e.g. for quick CSS tweaks).
function postToHost(message) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);
    } else {
        console.log("[no host] " + JSON.stringify(message));
    }
}

// ---- Layout -----------------------------------------------------------------
// Cytoscape stays the renderer; each entry here is just a layout extension that
// computes node positions. The active key is chosen in the ribbon and pushed from
// C# via setLayout(); getLayout() resolves it to the config object below.
//
// fcose = fast Compound Spring Embedder. Unlike the built-in "cose" it understands
// nested (compound) nodes, so class containers don't blow up and overlap.
// dagre = Sugiyama-style layered (hierarchical) layout, good for directed graphs;
// rankDir picks the flow direction (top->bottom vs. left->right). Note: dagre has
// only limited compound support, so nesting is best preserved by fcose.
const LAYOUTS = {
    "fcose": {
        name: "fcose",
        quality: "default",
        randomize: true,
        animate: false,
        nodeDimensionsIncludeLabels: true,
        nodeSeparation: 75,
        packComponents: true,
        padding: 30,
        fit: true,
    },
    "dagre-tb": {
        name: "dagre",
        rankDir: "TB",
        nodeDimensionsIncludeLabels: true,
        nodeSep: 50,
        rankSep: 60,
        edgeSep: 10,
        animate: false,
        padding: 30,
        fit: true,
    },
    "dagre-lr": {
        name: "dagre",
        rankDir: "LR",
        nodeDimensionsIncludeLabels: true,
        nodeSep: 50,
        rankSep: 60,
        edgeSep: 10,
        animate: false,
        padding: 30,
        fit: true,
    },
    // ELK layered: like dagre's hierarchical flow but genuinely compound-aware, so our
    // nested namespace/class/method containers keep their nesting. ELK-specific options
    // go under the `elk` key; 'elk.direction' DOWN mirrors dagre TB.
    "elk-down": {
        name: "elk",
        nodeDimensionsIncludeLabels: true,
        fit: true,
        padding: 30,
        elk: {
            algorithm: "layered",
            "elk.direction": "DOWN",
            // Lay out the whole compound hierarchy as ONE layered run so the direction
            // threads through nested nodes. Without it cytoscape-elk's top-level edges
            // (whose endpoints are nested children) don't drive the layout and DOWN/RIGHT
            // come out identical.
            "elk.hierarchyHandling": "INCLUDE_CHILDREN",
            "elk.spacing.nodeNode": 50,
            "elk.layered.spacing.nodeNodeBetweenLayers": 60,
        },
    },
    "elk-right": {
        name: "elk",
        nodeDimensionsIncludeLabels: true,
        fit: true,
        padding: 30,
        elk: {
            algorithm: "layered",
            "elk.direction": "RIGHT",
            "elk.hierarchyHandling": "INCLUDE_CHILDREN",
            "elk.spacing.nodeNode": 50,
            "elk.layered.spacing.nodeNodeBetweenLayers": 60,
        },
    },
};

// The currently selected layout key; C# overrides it via setLayout() at startup.
let currentLayoutName = "fcose";

// Resolves the active layout config, falling back to fcose if the key is unknown
// (e.g. an extension whose offline lib was not bundled).
function getLayout() {
    return LAYOUTS[currentLayoutName] || LAYOUTS["fcose"];
}

// Starts the active layout, cancelling any in-flight run first. If the chosen layout
// extension is not registered (its offline lib was not bundled), Cytoscape throws when
// the layout name is unknown — we catch that and fall back to fcose so rendering never
// breaks. Returns the running layout (or null if even fcose somehow failed).
// onStop (optional) runs once the layout has positioned everything. It is attached BEFORE
// run() because discrete layouts (e.g. dagre) emit "layoutstop" synchronously inside run(),
// which a listener added afterwards would miss.
function runLayout(onStop) {
    if (currentLayout) {
        currentLayout.stop();
    }

    try {
        currentLayout = cy.layout(getLayout());
        if (onStop) {
            currentLayout.one("layoutstop", onStop);
        }
        currentLayout.run();
    } catch (err) {
        console.warn("Layout '" + currentLayoutName + "' failed, falling back to fcose:", err);
        currentLayoutName = "fcose";
        currentLayout = cy.layout(LAYOUTS["fcose"]);
        if (onStop) {
            currentLayout.one("layoutstop", onStop);
        }
        currentLayout.run();
    }

    return currentLayout;
}

// ---- Styling ----------------------------------------------------------------
// Fallback colors for standalone testing; in the app the exact color comes from C#.
const KIND_COLOR = {
    Class: "#f5d76e",
    Method: "#7fb3e8",
    Interface: "#b5e7a0",
    Namespace: "#e0e0e0",
};

const cytoscapeStyle = [
    {
        selector: "node",
        style: {
            "label": "data(label)",
            "font-size": 11,
            "text-valign": "center",
            "text-halign": "center",
            "background-color": ele => ele.data("color") || KIND_COLOR[ele.data("kind")] || "#cccccc",
            "border-width": 1,
            "border-color": "#888888",
            "shape": "round-rectangle",
            // Explicit pixel size derived from the label. The deprecated "width: label"
            // left nodes ~0-wide, so the layout had no size to separate them and stacked
            // them on one spot (which made the connecting edges zero-length / invisible).
            "width": ele => Math.max(50, (ele.data("label") || "").length * 7 + 16),
            "height": 26,
        },
    },
    {
        // While a right-click (context menu) is in progress we hide Cytoscape's gray
        // "active" press overlay — it lingers behind the opening menu and looks broken.
        selector: ".suppress-overlay",
        style: { "overlay-opacity": 0 },
    },
    {
        // Compound parent (a node that contains children) renders as a container.
        selector: ":parent",
        style: {
            "text-valign": "top",
            "background-opacity": 0.25,
            "border-color": "#666666",
            "padding": "12px",
        },
    },
    // External nodes are not styled here: C# already gives them their gray color via
    // data("color"). The "external" class is still tagged on them for future styling.
    {
        // Collapsed container drawn as a leaf: bold + thicker border signals "expandable".
        selector: "node[?collapsed]",
        style: { "font-weight": "bold", "border-width": 2, "border-color": "#555555" },
    },
    // Persistent decorations from C# PresentationState. Both red like MSAGL; the flag is
    // thicker and wins over search (it is defined last among the two equal-specificity rules).
    {
        selector: "node[?searchHighlighted]",
        style: { "border-color": "#ff0000", "border-width": 2 },
    },
    {
        selector: "node[?flagged]",
        style: { "border-color": "#ff0000", "border-width": 3 },
    },
    {
        selector: "node:selected",
        style: { "border-width": 3, "border-color": "#ff7f0e" },
    },
    {
        // Transient halo flashed on the anchor node after an explore re-layout (focusNode).
        selector: "node.focus-flash",
        style: { "overlay-color": "#ff7f0e", "overlay-opacity": 0.35, "overlay-padding": 8 },
    },
    // Edges are plain black. A bundled edge (count > 1) shows the number of underlying
    // relationships as a label, mirroring how the WPF/MSAGL view marks edge strength.
    {
        selector: "edge",
        style: {
            "width": 1.5,
            // Per-edge tint from C# (e.g. Calls = blue); default black when none.
            "line-color": ele => ele.data("color") || "#000000",
            "target-arrow-color": ele => ele.data("color") || "#000000",
            "target-arrow-shape": "triangle",
            "curve-style": "bezier",
            "label": ele => ele.data("count") > 1 ? ele.data("count") : "",
            "font-size": 10,
            "color": "#000000",
            "text-background-color": "#ffffff",
            "text-background-opacity": 1,
            "text-background-padding": 2,
        },
    },
    {
        // Structure still recedes via line style (color stays black).
        selector: "edge[kind = 'Inherits']",
        style: {
            "line-style": "dashed",
            "target-arrow-shape": "triangle-backcurve",
        },
    },
    {
        selector: "edge[kind = 'Implements']",
        style: { "line-style": "dotted" },
    },
    {
        // Flat view only: containment shown as a quiet gray edge with no arrowhead.
        selector: "edge[kind = 'Containment']",
        style: {
            "line-color": "#cfcfcf",
            "width": 1,
            "target-arrow-shape": "triangle-backcurve",
            "target-arrow-color": "#cfcfcf",
        },
    },
    {
        // Flagged edge (PresentationState): red + thicker, like MSAGL.
        selector: "edge[?flagged]",
        style: {
            "line-color": "#ff0000",
            "target-arrow-color": "#ff0000",
            "width": 3,
        },
    },
    // Hover highlighting: emphasize the relevant set (no fading of the rest for now).
    {
        selector: "node.highlighted",
        style: { "border-color": "#ff7f0e", "border-width": 3 },
    },
    {
        selector: "edge.highlighted",
        style: {
            "line-color": "#ff7f0e",
            "target-arrow-color": "#ff7f0e",
            "width": 3,
            "z-index": 20,
        },
    },
];

// Small example so the page also renders something when opened standalone in a
// browser. In the app this is immediately replaced by renderGraph().
const exampleElements = [
    { data: { id: "BaseDevice", label: "BaseDevice", kind: "Class" } },
    { data: { id: "Printer", label: "Printer", kind: "Class" } },
    { data: { id: "BaseDevice.Init", label: "Init()", kind: "Method", parent: "BaseDevice" } },
    { data: { id: "Printer.Print", label: "Print()", kind: "Method", parent: "Printer" } },
    { data: { id: "e1", source: "Printer", target: "BaseDevice", kind: "Inherits" } },
    { data: { id: "e2", source: "Printer.Print", target: "BaseDevice.Init", kind: "Calls" } },
];

// Tracks the in-flight layout so a new render can cancel it (avoids racing fcose runs).
let currentLayout = null;

// ---- Cytoscape instance -----------------------------------------------------
// In the app we start empty so the empty-state hint shows; the example graph is only
// for standalone preview in a plain browser (no WebView2 host).
const inHost = !!(window.chrome && window.chrome.webview);

const cy = cytoscape({
    container: document.getElementById("cy"),
    elements: inHost ? [] : exampleElements,
    style: cytoscapeStyle,
    layout: getLayout(),
});

// ---- Empty-state hint -------------------------------------------------------
// Shown until the first non-empty graph is rendered, then hidden for the rest of the
// session (mirrors the WPF canvas hint, which also never reappears once a graph existed).
let hintDismissed = false;

function dismissHintIfGraph(graph) {
    if (hintDismissed || !graph.nodes || graph.nodes.length === 0) {
        return;
    }

    hintDismissed = true;
    document.getElementById("hint")?.classList.add("hidden");
}

// ---- Minimap (bird's-eye overview) -----------------------------------------
// cytoscape-navigator draws a thumbnail of the whole graph with a draggable viewport
// rectangle, so a graph panned/zoomed off-screen can be brought back. The canvas itself
// never overflows (it is viewport-sized), so a real scrollbar is impossible — this is the
// idiomatic alternative.
//
// Created lazily on the first non-empty graph (so the container already has its real size
// when the navigator measures it), then shown / hidden in step with whether the graph has
// nodes. Guarded by typeof so a missing offline lib never breaks rendering (like layouts).
let navInstance = null;

function updateNavigator() {
    const navEl = document.getElementById("navigator");
    if (!navEl) {
        return;
    }

    if (cy.nodes().length === 0) {
        navEl.classList.add("hidden");
        return;
    }

    const wasHidden = navEl.classList.contains("hidden");
    navEl.classList.remove("hidden");

    if (!navInstance && typeof cy.navigator === "function") {
        navInstance = cy.navigator({
            container: "#navigator",
            viewLiveFramerate: 0,         // pan the main graph instantly while dragging the box
            dblClickDelay: 200,
            rerenderDelay: 100,           // throttle thumbnail redraws
            removeCustomContainer: false, // keep our #navigator div if ever destroyed
        });
    } else if (navInstance && wasHidden) {
        // Was hidden while the graph was empty; recompute sizes and refresh the thumbnail.
        navInstance.resize();
    }
}

// ---- Bridge: C# -> JS -------------------------------------------------------
// C# calls this via ExecuteScriptAsync with { nodes, edges } and an optional focusId:
// the node to re-center on once the layout settles (the anchor of an explore action), so
// a full re-layout does not leave the user lost. null = keep the default fit-to-graph.
window.renderGraph = function (graph, focusId) {
    dismissHintIfGraph(graph);

    const elements = [];
    for (const n of graph.nodes) {
        elements.push({
            // color is the exact per-type color computed by C# (ColorDefinitions); the
            // node style reads data("color") and only falls back to KIND_COLOR when absent.
            // flagged / searchHighlighted are PresentationState decorations (red borders).
            data: { id: n.id, label: n.label, kind: n.kind, color: n.color, parent: n.parent || undefined, collapsed: n.collapsed, flagged: n.flagged, searchHighlighted: n.searchHighlighted },
            classes: n.external ? "external" : "",
        });
    }
    for (const e of graph.edges) {
        // color is optional (null = default black); set per relationship type by C#.
        elements.push({ data: { id: e.id, source: e.source, target: e.target, kind: e.kind, count: e.count, color: e.color, flagged: e.flagged } });
    }

    cy.elements().remove();
    cy.add(elements);
    cy.resize();

    // Cancel a still-running layout before starting a new one (with safe fallback). When an
    // anchor is given, re-center on it once the layout settles (and the layout's fit ran),
    // so a full re-layout does not leave the user lost.
    runLayout(focusId ? () => focusNode(focusId) : undefined);

    // Show/lazily-create the minimap (or hide it when the graph is empty).
    updateNavigator();
};

// Centers and gently zooms onto a node, then flashes it so the eye finds it. Called after
// a re-layout so the user keeps track of the node an explore action started from.
function focusNode(id) {
    const node = cy.getElementById(id);
    if (!node || node.empty()) {
        return; // stale id (e.g. the node was filtered out) -> leave the fit as is
    }

    // Clamp the zoom into a readable band: at least 0.85 (so a large, fit-zoomed-out graph
    // zooms in enough to read the node) and at most 1.3 (so a tiny graph isn't over-zoomed).
    const targetZoom = Math.min(1.3, Math.max(cy.zoom(), 0.85));
    cy.animate(
        { center: { eles: node }, zoom: targetZoom },
        { duration: 350, easing: "ease-out-cubic" }
    );

    // Soft orange halo for a moment (flashClass adds the class, then removes it after Nms).
    node.flashClass("focus-flash", 800);
}

// Update flag / search decorations on the EXISTING elements (no re-layout). C# pushes this
// whenever the PresentationState decorations change; styling reacts to the data flags.
window.setDecorations = function (dec) {
    const flaggedNodes = dec.flaggedNodes || [];
    const searchNodes = dec.searchNodes || [];
    const flaggedEdges = dec.flaggedEdges || [];

    cy.batch(() => {
        cy.nodes().forEach(n => {
            n.data("flagged", false);
            n.data("searchHighlighted", false);
        });
        cy.edges().forEach(e => e.data("flagged", false));

        // getElementById on a missing id yields an empty collection -> .data() is a no-op.
        flaggedNodes.forEach(id => cy.getElementById(id).data("flagged", true));
        searchNodes.forEach(id => cy.getElementById(id).data("searchHighlighted", true));
        flaggedEdges.forEach(id => cy.getElementById(id).data("flagged", true));
    });
};

// Recompute size and re-frame without re-running the layout. C# calls this when the
// Web View tab becomes visible again, so positions the user dragged are preserved.
window.refitGraph = function () {
    cy.resize();
    cy.fit(undefined, 30);
};

// Re-run the layout on the current elements (full reposition). The ribbon "Layout"
// button calls this; unlike renderGraph it keeps the element set (and thus the
// selection) and just recomputes positions.
window.relayoutGraph = function () {
    cy.resize();
    runLayout();
};

// Switch the layout algorithm and re-run it on the current elements. C# pushes the
// key (e.g. "dagre-tb") from the ribbon ComboBox and at startup. Unknown keys fall
// back to fcose (see getLayout), so a missing offline lib never breaks rendering.
window.setLayout = function (name) {
    currentLayoutName = name;
    relayoutGraph();
};

// ---- Image export (C# reads the return value via ExecuteScriptAsync) ---------
// Whole graph on a white background. PNG is built in; SVG needs the cytoscape-svg extension.
window.exportPngBase64 = function () {
    // Raw base64, no "data:image/png;base64," prefix.
    return cy.png({ output: "base64", full: true, bg: "#ffffff" });
};

window.exportSvg = function () {
    if (typeof cy.svg !== "function") {
        return "";   // extension missing -> C# reports it could not export
    }
    return cy.svg({ full: true, bg: "#ffffff" });
};

// ---- Hover highlighting (local; mode comes from the ribbon via C#) ----------
// Mirrors the MSAGL highlight strategies, computed on Cytoscape's own (already
// bundled / collapsed) topology. C# only tells us which mode is active.
// Note: we only emphasize the relevant set; fading the rest is deferred until we
// have a precise spec (fading compound nodes made inner-node labels unreadable).
let highlightMode = "EdgeHovered";

window.setHighlightMode = function (mode) {
    highlightMode = mode;
    clearHighlight();
};

function clearHighlight() {
    cy.elements().removeClass("highlighted");
}

function applyHighlight(target) {
    let hl = cy.collection();

    if (highlightMode === "EdgeHovered") {
        if (target.isEdge()) {
            hl = target.union(target.connectedNodes());
        }
    } else if (highlightMode === "OutgoingEdgesChildrenAndSelf") {
        if (target.isNode()) {
            // The node plus its (visible) nested children, and their outgoing edges.
            const sources = target.descendants().union(target);
            const edges = sources.connectedEdges("edge").filter(e => sources.contains(e.source()));
            hl = edges.union(edges.connectedNodes());
        }
    } else if (highlightMode === "ShortestNonSelfCircuit") {
        if (target.isNode()) {
            hl = shortestNonSelfCircuit(target);
        }
    }

    clearHighlight();
    hl.addClass("highlighted");
}

// Shortest cycle through `node` back to itself: leave via one outgoing edge, then take
// the shortest directed path from that neighbor back to the node; pick the smallest over
// all outgoing edges. Mirrors the MSAGL HighlightShortestNonSelfCircuit strategy.
function shortestNonSelfCircuit(node) {
    let best = cy.collection();
    let bestLength = Infinity;

    node.outgoers("edge").forEach(startEdge => {
        const neighbor = startEdge.target();
        if (neighbor.same(node)) {
            return; // ignore self edges; we want a real circuit
        }

        // dijkstra with default weight 1 == BFS shortest path (in edge count).
        const search = cy.elements().dijkstra({ root: neighbor, directed: true });
        const backDistance = search.distanceTo(node);
        if (backDistance === Infinity) {
            return; // no way back through this neighbor
        }

        const totalLength = backDistance + 1; // + the start edge
        if (totalLength < bestLength) {
            bestLength = totalLength;
            // pathTo returns the nodes and edges of the way back; add the start edge.
            best = search.pathTo(node).union(startEdge);
        }
    });

    return best;
}

cy.on("mouseover", "node, edge", evt => applyHighlight(evt.target));
cy.on("mouseout", "node, edge", clearHighlight);

// ---- User interaction -> host ----------------------------------------------
cy.on("tap", "node", evt => {
    postToHost({ type: "nodeClicked", id: evt.target.id() });
});

cy.on("tap", "edge", evt => {
    // The edge id is enough: C# has the relationships behind it keyed by id.
    postToHost({ type: "edgeClicked", id: evt.target.id() });
});

cy.on("dbltap", "node", evt => {
    postToHost({ type: "nodeDblClicked", id: evt.target.id() });
});

// A tap on empty canvas (target is the core, not a node/edge) clears the Info panel.
cy.on("tap", evt => {
    if (evt.target === cy) {
        postToHost({ type: "backgroundClicked" });
    }
});

// ---- Context menu (right-click) -> host -------------------------------------
// C# builds the actual WPF menu at the cursor from the existing command objects.

// Suppress the gray "active" press overlay for the duration of a right-click only
// (left-click keeps its normal feedback). cxttapstart fires on right mouse-down,
// cxttapend on release; after release there is no active state anyway.
cy.on("cxttapstart", "node, edge", evt => evt.target.addClass("suppress-overlay"));
cy.on("cxttapend", "node, edge", evt => evt.target.removeClass("suppress-overlay"));

cy.on("cxttap", "node", evt => {
    postToHost({ type: "contextMenu", kind: "node", id: evt.target.id() });
});

cy.on("cxttap", "edge", evt => {
    postToHost({ type: "contextMenu", kind: "edge", id: evt.target.id() });
});

cy.on("cxttap", evt => {
    if (evt.target === cy) {
        postToHost({ type: "contextMenu", kind: "background" });
    }
});

// ---- Selection -> host ------------------------------------------------------
// Native Cytoscape multi-selection (click selects one, box-drag selects many).
// We report the full selected node set; C# keeps it as the canonical selection,
// which the context menu (and later the toolbar buttons) will consume.
let selectionTimer = null;

function reportSelection() {
    // Coalesce the burst of per-element events from a box selection into one message.
    clearTimeout(selectionTimer);
    selectionTimer = setTimeout(() => {
        const ids = cy.$("node:selected").map(n => n.id());
        postToHost({ type: "selectionChanged", ids: ids });
    }, 0);
}

cy.on("select unselect", "node", reportSelection);

// ---- Keyboard: expand the selection by following relationships --------------
// Arrow Up/Down follow all outgoing/incoming relationships of every selected node;
// Page Up/Down do the "deep" variants (whole subtree). C# runs the exploration on
// the canonical selection it already holds, so we only forward the requested action.
const EXPLORE_KEYS = {
    "ArrowUp": "outgoingRelationships",
    "ArrowDown": "incomingRelationships",
    "PageUp": "outgoingDeep",
    "PageDown": "incomingDeep",
};

document.addEventListener("keydown", evt => {
    const action = EXPLORE_KEYS[evt.key];
    const isDelete = evt.key === "Delete";
    if (!action && !isDelete) {
        return;
    }

    // Nothing selected -> let the key do its normal thing (and avoid a no-op round-trip).
    if (cy.$("node:selected").length === 0) {
        return;
    }

    evt.preventDefault();
    if (isDelete) {
        // Remove the selected elements (with children). Handled in JS like the explore
        // shortcuts so it also works while the canvas has keyboard focus.
        postToHost({ type: "deleteSelected" });
    } else {
        postToHost({ type: "exploreSelected", action: action });
    }
});

// Tell the host we are ready to receive renderGraph() calls.
postToHost({ type: "ready" });
