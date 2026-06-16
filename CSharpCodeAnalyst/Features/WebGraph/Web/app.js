// Web Graph View - Phase 0 spike.
// Renders a hardcoded example graph and wires the C# <-> JS bridge skeleton.
// In Phase 1 the hardcoded elements are replaced by data pushed from C# via renderGraph().

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

// ---- Styling ----------------------------------------------------------------
// Colors loosely mirror the WPF/MSAGL scheme. Final colors come from C# later.
const KIND_COLOR = {
    Class: "#f5d76e",      // yellow
    Method: "#7fb3e8",     // blue
    Interface: "#b5e7a0",  // green
    Namespace: "#e0e0e0",  // light gray container
};

const cytoscapeStyle = [
    {
        selector: "node",
        style: {
            "label": "data(label)",
            "font-size": 11,
            "text-valign": "center",
            "text-halign": "center",
            "background-color": ele => KIND_COLOR[ele.data("kind")] || "#cccccc",
            "border-width": 1,
            "border-color": "#888888",
            "shape": "round-rectangle",
            "width": "label",
            "height": 24,
            "padding": "8px",
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
        selector: "node:selected",
        style: { "border-width": 3, "border-color": "#ff7f0e" },
    },
    // Edge styles per relationship kind.
    {
        selector: "edge",
        style: {
            "width": 1.5,
            "line-color": "#7fb3e8",
            "target-arrow-color": "#7fb3e8",
            "target-arrow-shape": "triangle",
            "curve-style": "bezier",
        },
    },
    {
        selector: "edge[kind = 'Inherits']",
        style: {
            "line-color": "#999999",
            "target-arrow-color": "#999999",
            "line-style": "dashed",
            "target-arrow-shape": "triangle-backcurve",
        },
    },
    {
        selector: "edge[kind = 'Implements']",
        style: {
            "line-color": "#999999",
            "target-arrow-color": "#999999",
            "line-style": "dotted",
        },
    },
];

// ---- Hardcoded example graph (Phase 0) --------------------------------------
// Demonstrates compound nodes (class contains methods) and typed edges.
const exampleElements = [
    // Containers
    { data: { id: "BaseDevice", label: "BaseDevice", kind: "Class" } },
    { data: { id: "Printer", label: "Printer", kind: "Class" } },

    // Methods inside BaseDevice
    { data: { id: "BaseDevice.Init", label: "Init()", kind: "Method", parent: "BaseDevice" } },
    { data: { id: "BaseDevice.Reset", label: "Reset()", kind: "Method", parent: "BaseDevice" } },

    // Methods inside Printer
    { data: { id: "Printer.Print", label: "Print()", kind: "Method", parent: "Printer" } },

    // Edges
    { data: { id: "e1", source: "Printer", target: "BaseDevice", kind: "Inherits" } },
    { data: { id: "e2", source: "Printer.Print", target: "BaseDevice.Init", kind: "Calls" } },
    { data: { id: "e3", source: "Printer.Print", target: "BaseDevice.Reset", kind: "Calls" } },
];

// ---- Cytoscape instance -----------------------------------------------------
const cy = cytoscape({
    container: document.getElementById("cy"),
    elements: exampleElements,
    style: cytoscapeStyle,
    layout: { name: "cose", padding: 20, nodeDimensionsIncludeLabels: true },
    wheelSensitivity: 0.2,
});

// ---- Bridge: C# -> JS -------------------------------------------------------
// Phase 1 entry point. C# calls this via ExecuteScriptAsync with {nodes, edges}.
window.renderGraph = function (graph) {
    const elements = [];
    for (const n of graph.nodes) {
        elements.push({
            data: { id: n.id, label: n.label, kind: n.kind, parent: n.parent || undefined },
            classes: n.external ? "external" : "",
        });
    }
    for (const e of graph.edges) {
        elements.push({ data: { id: e.id, source: e.source, target: e.target, kind: e.kind } });
    }
    cy.elements().remove();
    cy.add(elements);
    cy.layout({ name: "cose", padding: 20, nodeDimensionsIncludeLabels: true }).run();
};

// ---- User interaction -> host ----------------------------------------------
cy.on("tap", "node", evt => {
    postToHost({ type: "nodeClicked", id: evt.target.id() });
});

cy.on("dbltap", "node", evt => {
    postToHost({ type: "nodeDblClicked", id: evt.target.id() });
});

// Tell the host we are ready to receive renderGraph() calls.
postToHost({ type: "ready" });
