# MCP Server

CSharp Code Analyst can open a local endpoint that speaks the [Model Context
Protocol](https://modelcontextprotocol.io), so an AI assistant such as Claude Code can ask questions
about the dependency graph you currently have loaded.

The point is not another search. It is that the graph knows things a coding assistant cannot easily
find out on its own: who calls a method transitively, how two classes are connected, what a change
would break. Grep does not answer those, and a language server only answers the first one, one hop at
a time.

The server answers from the graph **loaded in the application** — not from your source files. That has
consequences worth understanding before you trust an answer; see [What the assistant actually
sees](#what-the-assistant-actually-sees).

## Requirements

- The application must be **running** and have a project loaded. The endpoint lives inside it.
- The **ASP.NET Core 10 runtime** must be installed, in addition to the desktop runtime. It comes with
  the .NET SDK, so a developer machine almost always has it. Without it the application does not
  start at all.
- An MCP-capable client. The instructions below use Claude Code; the endpoint is a standard MCP
  server and any client that supports the HTTP transport works.

## Turning it on

**Home → AI access → Start MCP server.** Nothing listens until you press it, and pressing it again
stops the server and frees the graph copy it works on.

The endpoint is

```
http://127.0.0.1:5178/mcp
```

The button next to it, **Copy setup command**, puts the whole registration line on the clipboard with
the port actually in use — that is the shortest path to a working client, and it avoids the two
mistakes that are easy to make by hand (see [Choosing a scope](#choosing-a-scope)).

Two settings in `appsettings.json` **next to the executable**:

| Setting | Default | Meaning |
| --- | --- | --- |
| `McpServerAutoStart` | `false` | Start the server at application startup, for daily use. |
| `McpServerPort` | `5178` | Change it if something else holds the port. The client URL has to match. |

Both are read once at startup, so a change needs a restart. The button does not.

## Connecting Claude Code

```bash
claude mcp add --scope user --transport http csca http://127.0.0.1:5178/mcp
```

| Part | Meaning |
| --- | --- |
| `--scope user` | Register for every project on this machine. See the table below. |
| `--transport http` | The server already runs; the client only connects. The default, `stdio`, would try to *launch* a process. |
| `csca` | A name you choose. It prefixes the tool names the assistant sees: `mcp__csca__graph_info`. |
| the URL | Loopback address, the port from the setting, and the fixed path `/mcp`. |

### Choosing a scope

`claude mcp add` writes the entry into a configuration file. Which one depends on the scope, and the
default is rarely the one you want here:

| Scope | Stored in | Visible |
| --- | --- | --- |
| `local` (**default**) | `~/.claude.json`, keyed by the current working directory | Only in that one directory |
| `user` | `~/.claude.json`, global | Every project on this machine |
| `project` | `.mcp.json` in the project root | Everyone who checks out the repository |

Use **`user`** for your own machine: the server belongs to the running application, not to one
repository, so tying it to a directory only means it disappears when you work somewhere else.

Use **`project`** to give a whole team access to their own local instance. The checked-in `.mcp.json`
points at `127.0.0.1`, so every developer connects to the copy running on their own machine — the
file is shared, the server is not.

### Verifying

```bash
claude mcp list
```

A ✗ means "registered, but not answering". With this server that is the normal state whenever the
application is closed — it is not a defect. If you start the application in the middle of a session,
pick the server up with `/mcp` → **reconnect** instead of restarting the session.

To test the endpoint without involving any client:

```bash
curl -sS -X POST http://127.0.0.1:5178/mcp -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

In PowerShell use `Invoke-RestMethod` instead — PowerShell mangles the quotes inside a JSON body when
passing it to a native executable, and the server rejects the result as malformed:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:5178/mcp -Method Post -ContentType 'application/json' -Headers @{ Accept = 'application/json, text/event-stream' } -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

## Using it

### There are no magic words

You do not invoke a tool. You ask a question, and the assistant decides whether one of the tools can
answer it.

That decision is made from the tool **descriptions** and nothing else. When a session starts, the
client asks the server which tools it has and puts their names, descriptions and parameters into the
model's context. From then on the model matches your question against those descriptions — there is no
keyword list, no trigger phrase, and no configuration that maps a question to a tool.

The practical consequence: phrase the question the way the descriptions are written. They talk about
*who calls what*, *what depends on what*, *what breaks if this changes*, and *how two elements are
connected*. A question in those terms lands; "tell me about the architecture" is too vague to point
at any particular tool.

### How you see that it happened

The tool call appears in the transcript, named `mcp__<server>__<tool>` — with the default registration
that is `mcp__csca__search_elements`, `mcp__csca__find_incoming_calls`, and so on. You see the
arguments it passed and the answer it got back.

If no such line appears, the assistant answered from your source files instead. That is not
necessarily wrong, but it is a different answer: reading files finds text, the graph knows
relationships.

**One question usually produces several calls.** Element ids cannot be guessed, so almost every
answer starts with `search_elements` to turn a name into an id, and only then asks the real question.
A chain of three calls for one question is normal.

### Example questions

| Ask this | The assistant reaches for |
| --- | --- |
| "What is loaded in Code Analyst right now?" | `graph_info` |
| "Which classes depend on `CodeGraphExplorer`?" | `search_elements` → `find_incoming_relationships` |
| "Who can end up calling `MainViewModel.LoadCodeGraph`?" | `search_elements` → `find_incoming_calls` |
| "If I delete the `Importers` feature, what breaks?" | `search_elements` → `find_incoming_relationships` with `deep` |
| "How does the web graph view end up using the Roslyn parser?" | `search_elements` twice → `find_paths_between` |
| "Does anything outside the UI use `RefactoringService`?" | `search_elements` → `find_incoming_relationships` |
| "What does `GraphViewModel` depend on outside its own assembly?" | `search_elements` → `find_outgoing_relationships` with `deep` |
| "Is `FindPathsBetween` still used anywhere?" | `search_elements` → `find_incoming_calls` |
| "Find every class whose name contains 'Importer' that is not external" | `search_elements` with `importer type:class -source:extern` |

Questions that mix both worlds work well, because the assistant can use the graph *and* read your
files: *"Who calls `Parser.ParseAsync`, and does any of those callers handle the cancellation
correctly?"* — the first half is the graph, the second half is reading the code it found.

### When it does not trigger

Name the server, and it will: *"Use the csca MCP server to find out who calls this."* Or name the tool
outright: *"Call `graph_info`."*

If it still does not work, walk down this list:

1. Is the server running? The ribbon button says **Stop MCP server** when it is.
2. Is a project loaded in the application? Every tool answers "No project is loaded" otherwise —
   and that answer *does* appear in the transcript, so it is easy to spot.
3. Was the server started after the session began? Pick it up with `/mcp` → **reconnect**.

### What it cannot tell you

The graph is what the parser found. Reflection, dependency injection, and anything else resolved at
runtime leave no edge, so "nothing calls this" is a statement about the static graph, not about your
program. `find_incoming_calls` says so in its own answer for exactly that reason.

## Tools

| Tool | Answers |
| --- | --- |
| `graph_info` | What is loaded, when it was captured, how large it is, which assemblies it contains. |
| `search_elements` | Find code elements by name. The entry point — see the syntax below. |
| `describe_element` | Kind, full path, accessibility, source locations, contents, relationship counts. |
| `find_outgoing_relationships` | What this element depends on. `deep` includes its members. |
| `find_incoming_relationships` | What depends on this element — the blast radius of a change. |
| `find_incoming_calls` | Who calls a method, transitively. |
| `find_paths_between` | How two elements are connected. |

Three semantics in that list are easy to misread, so each tool states them in its own description as
well:

**`deep` means "crossing the boundary", not "everything inside".** Asking a class with `deep=true`
reports what its members depend on *outside* the class. One method calling another stays inside and is
not listed — ask about the member itself for that.

**`find_incoming_calls` follows abstractions by default.** A call to `IOrderService.Place` counts as
reaching `OrderService.Place`, which is what you want for "who can end up here". It is a heuristic: a
static graph cannot know which implementation runs. Turn it off for certainty, and accept that callers
arriving through virtual dispatch or events then go missing — an empty result is *not* proof that a
method is unused.

**`find_paths_between` reports only the shortest chains.** If a direct dependency exists, you will not
see the longer route that also connects the two. Containment is never a path, or every pair of
elements would be connected through a shared parent.

**Element ids are opaque and only valid while the server runs.** They are regenerated on every parse,
so an assistant cannot remember one across sessions. Every workflow therefore starts with
`search_elements` to obtain an id, and `graph_info` says so explicitly.

### Search syntax

The same expression language as the **Search** tab in the application, so what you type there and what
an assistant sends produce the same result.

| Pattern | Matches |
| --- | --- |
| `order` | Anywhere in the full name, case-insensitively. |
| `OS`, `OrdServ`, `OrderService` | Camel-hump matching. Any term containing an uppercase letter is split at each uppercase letter; the parts must occur in that order, each starting a word, matched **case-sensitively**. |
| `OSvc` | *Nothing* — `Svc` does not occur in `OrderService`. The parts are literal, not abbreviations. |
| `order service` | AND — both terms must match. |
| `order \| invoice` | OR. |
| `-source:extern` | Excludes. |
| `type:class` | Restricts the kind: `interface`, `struct`, `record`, `method`, `property`, `field`, `event`, `enum`, `delegate`, `namespace`, `assembly`. |
| `source:intern`, `source:extern`, `source:generated` | Restricts the origin. |

The most common surprise is the case rule: `order` is case-insensitive, `Order` is not, because the
uppercase letter switches modes.

Results are ordered by how likely they are the element you meant — an exact name match first, then a
prefix match, then the rest — with internal code before external. Long results are truncated with an
explicit count of what was left out; they are never silently cut.

## What the assistant actually sees

Three properties of the answer that are invisible in the data itself, and that `graph_info` reports
for exactly that reason:

**A snapshot, not your files.** The graph is a copy taken when the application last loaded or changed
it. Edit code in your editor and the graph does not follow. `graph_info` reports the capture time so
the assistant can weigh how much to trust it.

**Possibly a hypothetical code base.** The refactoring simulation changes the loaded graph — moving,
deleting, cutting relationships — without touching a single source file. Once you have done that, the
graph describes code that never existed. `graph_info` says so, and any answer derived from it should
repeat the warning.

This is also the most interesting thing you can do with the feature: delete a module in the
simulation, then ask the assistant what breaks.

**Only what the parser found.** External assemblies are leaf nodes; their internals are not analyzed.
Reflection, dependency injection and anything else resolved at runtime is invisible to a static
parse.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| `claude mcp list` shows ✗ | Application not running, server not started, or a different port. |
| `claude mcp list` shows nothing at all | The entry was added with the default `local` scope from a different directory. Re-add with `--scope user`. |
| `Bad Request: The POST body did not contain a valid JSON-RPC message` | PowerShell ate the quotes in the JSON body. Use `Invoke-RestMethod`, or `curl.exe --%`. |
| Application does not start at all | Missing ASP.NET Core runtime — it is required from the version that introduced this feature onwards, whether or not you use the server. Or `appsettings.json` is not in the working directory: it is read from there, not from next to the executable. |
| `McpServerPort` change has no effect | The settings are read once at startup. Restart the application; the button alone does not re-read them. |
| Every tool answers "No project is loaded" | The application is running but empty. Open a solution or a saved project. |
| Server does not start, port message | Another process holds the port. Change `McpServerPort` and the client URL together. |

## Security

The endpoint binds to **loopback only** (`127.0.0.1`). It is reachable from this machine and nowhere
else. That is deliberate: the graph contains the full structure of your source — assembly, namespace
and member names, file paths, call relationships. On a network interface that would be published.

There is no authentication. Anything running on the machine can query the endpoint while the server
is up. All tools are read-only: nothing an assistant does through MCP can change the graph, your
project file, or your source.

## How it works

The server runs inside the WPF application as a Kestrel endpoint (`CSharpCodeAnalyst.Mcp`, a
UI-free assembly, so the tools can be unit tested against a hand-built graph).

Tools never touch the live graph. The application mutates it in place during a refactoring
simulation, on the UI thread, while tool calls arrive on request threads — a query walking it at the
same time would see a half-changed structure. Instead, `CodeGraphSnapshotProvider` hands out a
**copy**. Loading a project or applying a refactoring only marks the current copy stale; the next tool
call pays for a fresh one, and only if something changed. The copy itself is taken on the UI thread,
the one moment nothing can mutate. Everything after that runs on the request thread, without a lock
anywhere.

## Status

Feature complete: the ribbon toggle, the host, the snapshot mechanism and all seven tools, with unit
tests in `Tests/UnitTests/Mcp/`.

Known limits, none of them bugs:

- The server exists only while the application runs, so there is no headless or CI use.
- The graph is a snapshot of what the application has loaded, not of your source files.
- Nothing authenticates. Loopback binding and read-only tools are what keeps that acceptable.
