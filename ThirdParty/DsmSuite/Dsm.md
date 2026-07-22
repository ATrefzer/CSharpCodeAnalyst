# Reading the DSM

How to read and operate the matrix on the **DSM** tab. Almost none of it is discoverable from the UI — the
legend at the bottom covers three colours and nothing else.

This file is about what you see. For how it is built and why it differs from stock DsmSuite, see
[README.md](README.md) and `Features/DsmMatrix/CodeGraphToDsmModelBuilder.cs`.

## The axes

> **Row = provider. Column = consumer.**
> A number at (row R, column C) means: **C depends on R.**

Every dependency appears **once**, so only one half of the matrix fills up: reading from a column at the
top to a row on the left.

This is the same convention NDepend uses. The difference is that NDepend also draws the mirror image in a
second colour, so each dependency shows up twice and its triangles look symmetric. Here the second
direction is left empty, and the colour it would have cost is spent on nesting depth instead.

### The order number

Every row carries a number, and every column header repeats it. It is the cross reference between a column
and its row — find the number at the top of a column, look for the same number on the left, and you have
the element that column stands for.

It is numbered straight through the tree as it currently stands, so **it changes whenever you expand or
collapse something**. Do not write it down or use it to refer to an element later.

The column header carries the element **name** next to the number as well, so a column is legible without
the cross reference. That name is what makes the header tall. The **toggle button in the top-left corner**
collapses the header back to the number alone — the same cross reference the original viewer showed — which
reclaims that vertical space when you only need to read the shape.

## The blocks on the diagonal

Expanding an element paints a square block on the diagonal covering everything inside it. This is the most
useful reading aid in the view:

> **Inside the square = internal to that assembly or namespace.
> Outside it = crosses the boundary.**

The shade tells you the nesting depth — deeper elements are darker. **There are four shades and they
repeat**: the fifth level down looks like the first. On a deeply nested tree, check where a block actually
starts and ends rather than trusting the shade alone.

## Cell colours

| Colour | Meaning |
|---|---|
| light neutral | no dependency, and not inside an expanded block |
| blue-grey ramp, 4 shades | inside an expanded block, darker = deeper |
| **warm orange** | **the two elements depend on each other — a cycle** |

Orange overwrites every other colour, so it is never hidden by a block. It is what you scan for.

Hovering or selecting a row or column darkens it into a crosshair, so you can follow a cell back to the
two elements it belongs to.

## The number in a cell

The number is the **dependency weight**: how many distinct type-to-type dependencies are aggregated under
those two elements. Above `9999` it reads `>9K`; the exact value is always in the cell's tooltip.

> **Fully expanded, every populated cell reads `1`.** One type depending on another is counted once, no
> matter how many calls or field accesses are behind it. The larger numbers you see on a collapsed row are
> the sums of the cells inside it.

So the number answers "how much of this is there", not "how strong is this one call".

## Zoomed out

Below roughly a third of full size the numbers are too small to read, so they are dropped and the cells
say only whether they are populated:

| | |
|---|---|
| empty cell | light, as always |
| **populated cell** | **filled near-black** |
| cycle | keeps its orange |

The blocks keep their normal shades, so structure and dependencies stay readable together. This is the
view for questions like "where are the dependencies at all", "is this layered or tangled", "does anything
sit far off the diagonal". Zoom back in for the numbers, or hover a cell — the tooltip carries the exact
weight at any zoom.

## The coloured bar beside the row headers

The bar at the right edge of each row header is **relative to the row you last clicked**. It answers "how
does this row relate to the thing I selected", not "what is this row".

| Colour | Meaning |
|---|---|
| Green | this row **uses** the selected element |
| Blue | this row **is used by** the selected element |
| Orange | both — **mutual, a cycle** |

It needs a **row** selection. Clicking a column header draws the crosshair but clears the bars, and with
nothing selected there are none at all.

## What is in the matrix

- **One row per type.** Methods, fields and properties are not shown; their dependencies are counted
  towards the type that contains them. Types from outside the solution are left out entirely.
- **Namespaces that hold nothing but a single other namespace are merged into one row**, so a row can read
  `CSharpCodeAnalyst.CodeGraph` rather than forcing you through two expands that show one line each. A row
  label is always the namespace path relative to the element it sits in.
- **Rows and columns are ordered so dependencies fall to one side of the diagonal.** This is what makes
  layering visible: a clean layered design comes out triangular, and anything above the diagonal is a
  dependency pointing back up.

## Reading it

| Question | What to look for |
|---|---|
| Is this layered? | Everything on one side of the diagonal. Cells on the wrong side are the back edges. |
| Where are the cycles? | Orange cells. They sit symmetrically around the diagonal. |
| Is a module self-contained? | Expand it. Numbers inside its block, few outside its rows and columns. |
| What does this element drag along? | Click its row header, then read the green and blue bars down the side. |
| Who is a bottleneck? | A row with many populated columns is used by many; a column with many populated rows uses many. |

## Controls

- **Ctrl + mouse wheel** zooms the whole matrix.
- **Plain wheel** scrolls up and down, **shift + wheel** sideways — from anywhere in the matrix, headers
  included. Worth knowing, because the scroll bars scale with the matrix and get too thin to grab once you
  zoom out. The wheel keeps working the same at any zoom.
- **Click a row header** to select it — this is what drives the indicator bars. Clicking a column header
  selects the column but clears them.
- **Click the arrow** in a row header to expand or collapse; hold **shift** to do it recursively.
- **Hover a cell** for the consumer and provider names plus the weight; **hover a row or column header**
  for the element's full name and type.
- **Toggle button in the top-left corner** collapses the column headers from name + number down to the
  number alone, and back. Use it to reclaim the tall header band when you only need the shape. It hides
  itself once you zoom far out, where the names are unreadable anyway.

The view is **read-only**. Everything the original viewer offered for editing the matrix has been removed:
what you see is a projection of the parsed code, and an edited row would no longer say anything about it.
The metrics column the original shows next to the row headers is hidden for the same kind of reason — the
application computes its own metrics, and the two answer similar-sounding questions differently.
