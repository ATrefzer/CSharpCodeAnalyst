#!/usr/bin/env python3
"""
Minimal multi-source merge for architecture evidence edges.

Loads all *.jsonl under --edges-dir (recursively), writes a dashboard markdown
and a combined graph.jsonl. Does not delete or rank sources — only aggregates.

Use as a starting point. Real hubs add tension detection between importers.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def load_jsonl(root: Path) -> list[dict[str, Any]]:
    edges: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*.jsonl")):
        if path.name in {"graph.jsonl", "merged.jsonl"}:
            continue
        for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
            line = line.strip()
            if not line:
                continue
            try:
                edges.append(json.loads(line))
            except json.JSONDecodeError:
                continue
    return edges


def write_dashboard(path: Path, edges: list[dict[str, Any]]) -> None:
    by_src = Counter(e.get("evidence_source", "?") for e in edges)
    by_kind = Counter(e.get("kind", "?") for e in edges)
    by_status = Counter(e.get("status", "?") for e in edges)
    now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%SZ")

    lines = [
        "# Architecture evidence dashboard",
        "",
        f"_Generated: {now}_",
        f"_Edges: {len(edges)}_",
        "",
        "## By evidence_source",
        "",
        "| Source | Count |",
        "|---|---|",
    ]
    for k, v in by_src.most_common():
        lines.append(f"| {k} | {v} |")
    lines += ["", "## By kind", "", "| Kind | Count |", "|---|---|"]
    for k, v in by_kind.most_common():
        lines.append(f"| {k} | {v} |")
    lines += ["", "## By status", "", "| Status | Count |", "|---|---|"]
    for k, v in by_status.most_common():
        lines.append(f"| {k} | {v} |")

    sample_deny = [e for e in edges if e.get("kind") == "layer_deny"][:15]
    sample_cycle = [e for e in edges if e.get("kind") == "cycle"][:15]
    if sample_deny:
        lines += ["", "## Sample layer_deny", ""]
        for e in sample_deny:
            hits = (e.get("meta") or {}).get("member_hits", 1)
            lines.append(f"- `{e.get('from')}` → `{e.get('to')}` (hits={hits})")
    if sample_cycle:
        lines += ["", "## Sample cycles", ""]
        for e in sample_cycle:
            lines.append(f"- `{e.get('from')}` → `{e.get('to')}` — {e.get('note', '')}")

    lines += [
        "",
        "## Doctrine",
        "",
        "- C# Code Analyst: layers + cycles + metrics.",
        "- Peer importers: bindings, shell routes, DI, etc.",
        "- Merges never silently overwrite sources; conflict = tension (implement per team).",
        "",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines), encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--edges-dir", required=True, help="Directory containing *.jsonl edge files")
    p.add_argument("--dashboard-out", required=True, help="Markdown dashboard path")
    p.add_argument(
        "--merged-out",
        default="",
        help="Optional path for combined graph.jsonl (default: <edges-dir>/graph.jsonl)",
    )
    args = p.parse_args(argv)

    root = Path(args.edges_dir)
    if not root.is_dir():
        print(f"missing edges-dir: {root}", file=sys.stderr)
        return 2

    edges = load_jsonl(root)
    merged = Path(args.merged_out) if args.merged_out else root / "graph.jsonl"
    merged.parent.mkdir(parents=True, exist_ok=True)
    with merged.open("w", encoding="utf-8") as f:
        for e in edges:
            f.write(json.dumps(e, ensure_ascii=False) + "\n")

    write_dashboard(Path(args.dashboard_out), edges)
    print(f"edges={len(edges)} dashboard={args.dashboard_out} merged={merged}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
