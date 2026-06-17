// Web Graph View - renders the code graph with Cytoscape.js.
// Data is pushed from C# via renderGraph(); user events go back via postToHost().

"use strict";

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
// fcose = fast Compound Spring Embedder. Unlike the built-in "cose" it understands
// nested (compound) nodes, so class containers don't blow up and overlap.
const LAYOUT = {
    name: "fcose",
    quality: "default",
    randomize: true,
    animate: false,
    nodeDimensionsIncludeLabels: true,
    nodeSeparation: 75,
    packComponents: true,
    padding: 30,
    fit: true,
};

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
        // Compound parent (a node that contains children) renders as a container.
        selector: ":parent",
        style: {
            "text-valign": "top",
            "background-opacity": 0.25,
            "border-color": "#666666",
            "padding": "12px",
        },
    },
    {
        selector: "node.external",
        style: { "background-color": "#9e9e9e" },
    },
    {
        // Collapsed container drawn as a leaf: bold + thicker border signals "expandable".
        selector: "node[?collapsed]",
        style: { "font-weight": "bold", "border-width": 2, "border-color": "#555555" },
    },
    {
        selector: "node:selected",
        style: { "border-width": 3, "border-color": "#ff7f0e" },
    },
    // Edges are plain black. A bundled edge (count > 1) shows the number of underlying
    // relationships as a label, mirroring how the WPF/MSAGL view marks edge strength.
    {
        selector: "edge",
        style: {
            "width": 1.5,
            "line-color": "#000000",
            "target-arrow-color": "#000000",
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
const cy = cytoscape({
    container: document.getElementById("cy"),
    elements: exampleElements,
    style: cytoscapeStyle,
    layout: LAYOUT,
});

// ---- Bridge: C# -> JS -------------------------------------------------------
// C# calls this via ExecuteScriptAsync with { nodes, edges }.
window.renderGraph = function (graph) {
    const elements = [];
    for (const n of graph.nodes) {
        elements.push({
            data: { id: n.id, label: n.label, kind: n.kind, parent: n.parent || undefined, collapsed: n.collapsed },
            classes: n.external ? "external" : "",
        });
    }
    for (const e of graph.edges) {
        elements.push({ data: { id: e.id, source: e.source, target: e.target, kind: e.kind, count: e.count } });
    }

    cy.elements().remove();
    cy.add(elements);
    cy.resize();

    // Cancel a still-running layout before starting a new one.
    if (currentLayout) {
        currentLayout.stop();
    }
    currentLayout = cy.layout(LAYOUT);
    currentLayout.run();
};

// Recompute size and re-frame without re-running the layout. C# calls this when the
// Web View tab becomes visible again, so positions the user dragged are preserved.
window.refitGraph = function () {
    cy.resize();
    cy.fit(undefined, 30);
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
    postToHost({ type: "edgeClicked", source: evt.target.data("source"), target: evt.target.data("target") });
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

// Tell the host we are ready to receive renderGraph() calls.
postToHost({ type: "ready" });
