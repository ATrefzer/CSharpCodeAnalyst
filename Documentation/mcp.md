# MCP Server: CSharp Code Analyst

CSharp Code Analyst features an embedded endpoint speaking the [Model Context Protocol (MCP)](https://modelcontextprotocol.io). This allows AI assistants like **Claude Code** to query your project’s loaded dependency graph directly.

The graph is whatever you last loaded — a C# solution, or a C++, Python, Java, Dart or plain text import. The tools are the same for all of them; `graph_info` reports which language you are actually looking at.

---

## Requirements

Before starting, ensure you have:

* **Running Application:** The CSharp Code Analyst app must be running with a project loaded. The MCP endpoint lives inside the process.
* **MCP Client:** An MCP-compliant client. Examples below use Claude Code, but any client with HTTP transport support works.

No extra runtime is needed. The endpoint runs on `System.Net.HttpListener`, which is part of the base .NET runtime — earlier versions hosted it on Kestrel and therefore required the ASP.NET Core runtime next to the desktop one, which the application could not start without.

---

## Quick Setup Guide

### 1. Start the Server

In the CSharp Code Analyst application, navigate to:

**Home → MCP → Start MCP server**

* Nothing listens until you press it.
* Clicking the button again stops the server and frees memory.

### 2. Register with Your MCP Client

Run the command in your terminal:

```bash
claude mcp add --scope user --transport http cg http://127.0.0.1:5178/mcp

claude mcp remove cg -s user
```

> **Parameter Breakdown:**
> * `--transport http`: Mandatory. Since the server is already running inside the app, the default `stdio` transport will fail by trying to launch a new process.
> * `--scope user`: Registers the server machine-wide. The default `local` scope binds only to your current working directory. Use `--scope project` if committing a shared `.mcp.json` file to a repository.
> * `cg`: The client-side name chosen for the server, short for *code graph* (prefixes tool names in logs as `mcp__cg__<tool_name>`). Pick anything you like — but a name mentioning C# invites an assistant to skip the server when the loaded graph is a Dart or Python one.
> 
> 

### 3. Verify Connection

Run the following check in your terminal:

```bash
claude mcp list
```

* A `✗` symbol usually indicates the main application is not currently running.
* **Pro-Tip:** If you start the CSharp Code Analyst app after opening your Claude Code session, run `/mcp` → **reconnect** rather than restarting your entire terminal session.
* `/mcp` opens an interactive terminal panel, and not every surface can show one. Outside an interactive `claude` terminal — in the desktop app or an IDE integration — the command may answer *"MCP controls aren't available right now"*. That is the surface talking, not the server: it says the same thing whether the server is running or not. There the only way to pick up the connection is a new session.

---

## Configuration & Ports

To change the default port, update the `McpServerPort` setting in the `appsettings.json` file located in the application's **working directory**:

```json
{
  "ApplicationSettings": {
    "McpServerPort": 5178
  }
}
```

* This file is read **once at startup**. Restart the application to apply changes.
* Remember to update your client’s URL target to match the new port.

---

## How to Ask Questions

**There are no required trigger phrases or magic words.**

When your session begins, the assistant receives descriptions of all available graph tools. It automatically maps your natural language questions against these tool descriptions.

To help the assistant choose the right tool, phrase questions around architectural concepts: *who calls what*, *dependencies*, *blast radius*, or *connections*.

### Example Prompts & Tool Mapping

| Intent / Prompt Example | Assistant Tool Chain |
| --- | --- |
| "What is loaded in Code Analyst right now?" | `graph_info` |
| "Which classes depend on `CodeGraphExplorer`?" | `search_elements` → `find_incoming_relationships` |
| "Who can end up calling `MainViewModel.LoadCodeGraph`?" | `search_elements` → `find_incoming_calls` |
| "If I delete the `Importers` feature, what breaks?" | `search_elements` → `find_incoming_relationships` (`deep=true`) |
| "How does the web graph view end up using the Roslyn parser?" | `search_elements` (x2) → `find_paths_between` |
| "What does `GraphViewModel` depend on outside its own assembly?" | `search_elements` → `find_outgoing_relationships` (`deep=true`) |
| "Find every class named '...Importer' that is not external." | `search_elements` (`importer type:class -source:extern`) |
| "Which classes live in the `Importers` namespace?" | `search_elements` (`csharpcodeanalyst.importers type:class`) |
| "Who implements `IImporter`?" | `search_elements` → `find_inheritance` |
| "What does `DependencyRule` derive from, and what derives from it?" | `search_elements` → `find_inheritance` |

### Pro-Tips for Asking Questions

* **Combine Graph & File Reading:** The assistant can use graph context alongside standard file inspection.
*Example:* *"Who calls `Parser.ParseAsync`, and do those callers handle cancellation correctly?"* (Graph finds the callers; file reader inspects implementation).
* **Explicit Server Targeting:** If the assistant defaults to standard text file searching, nudge it explicitly:
*Example:* *"Use the cg MCP server to find out who calls this."*

---

## Available MCP Tools

| Tool Name | Purpose & Description |
| --- | --- |
| `graph_info` | Reports metadata: source languages, graph size, the element kinds actually present, included assemblies, and the source root paths are relative to. |
| `search_elements` | **Primary entry point.** Locates code elements by name using pattern matching syntax. |
| `describe_element` | Returns detailed attributes: element kind, full path, accessibility, source locations, and member counts. |
| `find_outgoing_relationships` | Analyzes dependencies required by a specific element. |
| `find_incoming_relationships` | Analyzes elements dependent on a specific target (evaluates blast radius). |
| `find_incoming_calls` | Traces transitive callers reaching a target method. |
| `find_inheritance` | Reports the hierarchy around an element in both directions and over any number of levels — base types and interfaces above, subtypes and implementations below. Works for members too (override chains). |
| `find_paths_between` | Discovers shortest dependency paths connecting two elements. |

---

## Important Tool Semantics & Rules

1. **`deep` descends into members:** By default a query answers for the element itself. `deep=true` additionally follows relationships anchored at *contained* elements, so asking a class covers what its methods depend on — or what reaches into them. Only relationships crossing the element's boundary are listed; one member calling another is internal and not shown. Ask about the member itself to see those.
2. **Abstract Call Resolution:** By default (`followAbstractions=true`), `find_incoming_calls` treats a call to `IOrderService.Place` as reaching `OrderService.Place`. This is a *heuristic* — a static graph cannot know which implementation runs, so a reported caller may never reach the method. Setting it to `false` makes every result certain, but drops callers arriving through virtual dispatch or events: an empty result is then **not** proof that a method is unused.
3. **Inheritance is transitive and two-way:** one `find_inheritance` call reports both directions, so there is no need to ask twice. It follows `Inherits`, `Implements` and `Overrides` only — a type that merely *uses* the element is not in the answer. Indentation marks the distance in levels. A type reached through two routes (a diamond) is listed once, under the first route found, and an implementation living in code the parser never saw is missing entirely: an empty downward result means "none in this code base", not "none".
4. **Shortest Paths:** `find_paths_between` yields only the shortest direct chains. Longer parallel paths or implicit containment relationships are omitted.
5. **Transient Element IDs:** Element IDs are generated dynamically per analysis session. Do not save or hardcode IDs across restarts.

---

## Element Search Syntax

The `search_elements` tool supports the exact same query syntax used by the UI's **Search** tab.

The kind names below are the graph's own vocabulary, not the source language's: every importer maps onto the same fixed set, so a Dart mixin arrives as a `class`. The list is therefore complete for every language — some kinds simply never occur in some of them. Which ones the loaded graph actually holds is reported by `graph_info`.

| Search Pattern | Matching Logic | Example |
| --- | --- | --- |
| `order` | Case-insensitive substring anywhere in full name. | Matches `Order`, `recorder`, `Border` |
| `sample.core.orders` | **Path prefix.** The full name is the whole path from the assembly down, so a lowercase prefix lists what a container holds — a type's path finds its members, and nested namespaces are included. Keep it lowercase, or the camel-hump rule below applies and the path stops matching itself. | `sample.core.orders type:class` lists the classes of that namespace |
| `OS`, `OrdServ` | **Camel-hump matching.** Any uppercase letter switches modes: the term is split at each uppercase letter, and every part must start a word, matched case-sensitively. | `OS` matches `OrderService` |
| `OSvc` | *No match.* Parts must be exact word-starting sub-strings, not arbitrary abbreviations. | Fails on `OrderService` |
| `order service` | **AND** search (all terms required). | Matches `OrderService` |
| `order \| invoice` | **OR** search (either term matched). | Matches `Order` or `Invoice` |
| `-source:extern` | **Exclude** matching items. | Drops external library symbols |
| `type:<kind>` | Filters by entity type (`class`, `interface`, `struct`, `record`, `method`, `property`, `field`, `event`, `enum`, `delegate`, `namespace`, `assembly`). | `type:class` |
| `source:<origin>` | Filters by origin (`source:intern`, `source:extern`, `source:generated`). | `source:intern` |

---

## Source File Locations

Source locations are formatted as `path:line` relative to the root reported by `graph_info`:

```text
[Class] CSharpCodeAnalyst.Analyzers.DeadCode.Analyzer  id=…  CSharpCodeAnalyst.Analyzers\DeadCode\Analyzer.cs:12

```

Common root paths are stripped for clarity. Full paths are retained only when no shared root directory exists.

---

## How Much to Trust an Answer

* **Snapshot vs. Active Files:** The graph represents a static snapshot captured when the application last loaded the project. Editing code in your IDE will not update the graph until you re-analyze the project inside CSharp Code Analyst. The server cannot tell how far the two have drifted apart, and does not pretend to — re-analyze when an answer looks out of date.
* **Static vs. Runtime Execution:** External assemblies are treated as leaf nodes (their internal structures are unanalyzed). Reflection, dependency injection runtime bindings, or dynamic invocations leave no static edges.
*(Note: A result of "Nothing calls this" reflects static graph analysis, not guaranteed runtime inactivity.)*

---

## Troubleshooting Guide

| Symptom / Issue | Primary Cause | Resolution |
| --- | --- | --- |
| `claude mcp list` shows `✗` | Application inactive or port mismatched. | Start CSharp Code Analyst, verify server state, and check port config. |
| `claude mcp list` shows nothing | Command was registered with `local` scope from another directory. | Re-add the server using `--scope user`. |
| Assistant ignores the MCP server | MCP server started *after* Claude session was initialized. | Run `/mcp` → **reconnect** in Claude Code. |
| Assistant stopped using the server after you toggled the server button | Stopping the server drops the connection; it is not re-established on its own when you start it again. | Same fix: `/mcp` → **reconnect**. |
| Assistant does not see a tool you just added, renamed or re-described | The tool list is negotiated **once per connection**, right after the handshake, and never re-read. Rebuilding and restarting the application is therefore not enough — the client still works from the list it fetched earlier. | Reconnect the client, or start a new session. |
| `/mcp` answers *"MCP controls aren't available right now"* | The command opens an interactive terminal panel, which not every surface can display (desktop app, IDE integration). Unrelated to the server — the answer is the same whether it runs or not. | Start a new session; it fetches the tool list on connect. Or use an interactive `claude` terminal, where the panel is available. |
| Tools report *"No project is loaded"* | Application is running without an open solution. | Open a solution or project file within CSharp Code Analyst. |
| Application fails to launch | Missing config file. (No longer the ASP.NET Core runtime — the server does not use it any more.) | Ensure `appsettings.json` resides in the active working directory. |
| Port changes have no effect | Config read only at application startup. | Full application restart is required after editing `appsettings.json`. |
| Port binding error | Port occupied by another process. | Change `McpServerPort` in `appsettings.json` and update the client URL. |
| `Bad Request: ... not a valid JSON-RPC message` | CLI quote mangling in PowerShell. | Use `Invoke-RestMethod` or `curl.exe --%` when manually testing HTTP payloads. |

### Checking the server without a client

Most of the confusion above comes from one question being asked of two different things: *is the server serving what I think it serves*, and *is my client connected to it*. Talking to the endpoint directly separates them — this needs no MCP client at all and works even while a session is stuck on a stale connection.

The endpoint is stateless, so `tools/list` can be called straight away, without a handshake. Note the `Accept` header: the response comes back as a server-sent event, hence the `data:` unwrapping.

```powershell
$body = '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
$r = Invoke-WebRequest -Uri 'http://127.0.0.1:5178/mcp' -Method Post -Body $body `
     -ContentType 'application/json' -Headers @{ Accept = 'application/json, text/event-stream' }
$data = ($r.Content -split "`n" | Where-Object { $_ -like 'data: *' }) -replace '^data: ', ''
($data | ConvertFrom-Json).result.tools.name
```

If the tool you just added appears here but the assistant does not see it, the server is fine and the client is holding an older connection — reconnect it.

The same shape runs a tool. This one answers what is currently loaded:

```powershell
$body = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"graph_info","arguments":{}}}'
$r = Invoke-WebRequest -Uri 'http://127.0.0.1:5178/mcp' -Method Post -Body $body `
     -ContentType 'application/json' -Headers @{ Accept = 'application/json, text/event-stream' }
$data = ($r.Content -split "`n" | Where-Object { $_ -like 'data: *' }) -replace '^data: ', ''
($data | ConvertFrom-Json).result.content[0].text
```

Worth reading the assembly list in that answer before trusting a graph question: a snapshot taken before a project existed does not contain it, and the tools cannot tell you that themselves.

`curl.exe` works too, but PowerShell mangles the quotes in the JSON body — prefix the arguments with the stop-parsing token `--%`, or stay with the cmdlet above.

---

## Security Architecture

* **Loopback Binding Only:** The endpoint binds exclusively to `127.0.0.1` (localhost). It is not exposed to remote network interfaces.
* **Authentication:** Unauthenticated on local loopback. Any process on the local machine can query the graph while active.
* **Read-Only Operations:** All MCP tools are strictly read-only. The assistant cannot alter source code, project files, or graph state via MCP.