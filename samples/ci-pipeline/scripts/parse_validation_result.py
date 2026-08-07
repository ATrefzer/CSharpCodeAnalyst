#!/usr/bin/env python3
"""
Parse C# Code Analyst headless validation text into multi-source pipeline edges.

Input is produced by:
  CSharpCodeAnalyst.exe -validate -sln:... -rules:... -out:validation-result.txt

Output:
  - JSONL edges (one per line) matching samples/ci-pipeline/schema/edge.schema.json
  - handoff summary JSON for hub dashboards

This is tolerant text scraping until a native -out-json export exists upstream.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter
from pathlib import Path
from typing import Any, Iterable


EDGE_SOURCE = "codeanalyst"


def edge_id(kind: str, frm: str, to: str, path: str, status: str) -> str:
    raw = f"{kind}|{frm}|{to}|{EDGE_SOURCE}|{path}|{status}"
    return hashlib.sha1(raw.encode("utf-8")).hexdigest()[:16]


def typify(path: str) -> str:
    """Lift member paths to a type-ish path (heuristic for dashboards)."""
    segs = path.split(".")
    cut = len(segs)
    for i, seg in enumerate(segs):
        if (
            seg.startswith("set_")
            or seg.startswith("get_")
            or "(" in seg
            or seg in {".ctor", "ctor"}
            or seg.endswith("Async") and i + 1 == len(segs)
        ):
            cut = i
            break
    return ".".join(segs[: max(cut, 1)])


def strip_global_noise(path: str) -> str:
    # Graph paths often look like: Assembly.global.Namespace.Type.Member
    return path.replace(".global.", ".")


_ARROW = re.compile(r"^(.+?) -> (.+)$")
_CYCLE_HDR = re.compile(r"dependency cycle '([^']+)' between (\d+)", re.I)
_METRIC = re.compile(r"MAX[A-Z]+|LOC|lines", re.I)


def parse_validation(text: str, source_path: str) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    parts = re.split(r"- Rule Type:\s*", text)
    edges: list[dict[str, Any]] = []
    deny = 0
    cycles = 0
    metrics = 0
    rule_types: Counter[str] = Counter()

    for part in parts[1:]:
        lines = [ln.rstrip() for ln in part.strip().splitlines() if ln.strip()]
        if not lines:
            continue
        kind = lines[0].strip().upper()
        rule_types[kind] += 1

        if kind == "DENY":
            for line in lines[1:]:
                m = _ARROW.match(line.strip())
                if not m:
                    continue
                frm = typify(strip_global_noise(m.group(1).strip()))
                to = typify(strip_global_noise(m.group(2).strip()))
                status = "DENY"
                ekind = "layer_deny"
                edges.append(
                    {
                        "id": edge_id(ekind, frm, to, source_path, status),
                        "kind": ekind,
                        "from": frm,
                        "to": to,
                        "evidence_source": EDGE_SOURCE,
                        "status": status,
                        "confidence": 0.95,
                        "path": source_path,
                        "note": "DENY rule violation",
                    }
                )
                deny += 1

        elif kind in {"NOCYCLES", "CYCLE", "CYCLES"}:
            name: str | None = None
            elems: list[str] = []

            def flush() -> None:
                nonlocal cycles
                if not name or len(elems) < 2:
                    return
                for i, a in enumerate(elems):
                    b = elems[(i + 1) % len(elems)]
                    frm = typify(strip_global_noise(a))
                    to = typify(strip_global_noise(b))
                    status = "WARN"
                    ekind = "cycle"
                    edges.append(
                        {
                            "id": edge_id(ekind, frm, to, source_path, status),
                            "kind": ekind,
                            "from": frm,
                            "to": to,
                            "evidence_source": EDGE_SOURCE,
                            "status": status,
                            "confidence": 0.9,
                            "path": source_path,
                            "note": f"cycle group '{name}'",
                            "meta": {"cycle_name": name, "elements": len(elems)},
                        }
                    )
                    cycles += 1

            for line in lines[1:]:
                m = _CYCLE_HDR.search(line)
                if m:
                    flush()
                    name = m.group(1)
                    elems = []
                    continue
                # Element lines are typically fully qualified graph paths without " -> "
                if " -> " not in line and line.count(".") >= 1 and not line.startswith("Found"):
                    elems.append(line.strip())
            flush()

        else:
            # Metric / other rule blocks — keep a compact summary edge per block
            header = " ".join(lines[:3])[:240]
            if _METRIC.search(kind) or _METRIC.search(header):
                frm = f"rule:{kind}"
                to = header
                status = "WARN"
                ekind = "metric"
                edges.append(
                    {
                        "id": edge_id(ekind, frm, to, source_path, status),
                        "kind": ekind,
                        "from": frm,
                        "to": to[:120],
                        "evidence_source": EDGE_SOURCE,
                        "status": status,
                        "confidence": 0.8,
                        "path": source_path,
                        "note": header,
                    }
                )
                metrics += 1

    # De-dupe type-level DENY pairs (member expansion is noisy for dashboards)
    dedup: dict[tuple[str, str, str, str], dict[str, Any]] = {}
    collapsed = 0
    for e in edges:
        if e["kind"] != "layer_deny":
            key = (e["kind"], e["from"], e["to"], e.get("note", ""))
            dedup[key] = e
            continue
        key = (e["kind"], e["from"], e["to"], "DENY")
        if key in dedup:
            collapsed += 1
            prev = dedup[key]
            prev["meta"] = prev.get("meta") or {}
            prev["meta"]["member_hits"] = int(prev["meta"].get("member_hits", 1)) + 1
        else:
            e = dict(e)
            e["meta"] = {"member_hits": 1}
            dedup[key] = e

    out_edges = list(dedup.values())
    handoff = {
        "source": EDGE_SOURCE,
        "status": "ok" if out_edges or "No violation" in text or not parts[1:] else "ok",
        "deny_edges": deny,
        "deny_type_pairs": sum(1 for e in out_edges if e["kind"] == "layer_deny"),
        "cycle_edges": cycles,
        "metric_edges": metrics,
        "edge_count": len(out_edges),
        "collapsed_member_denies": collapsed,
        "rule_types": dict(rule_types),
        "input": source_path,
    }
    return out_edges, handoff


def write_jsonl(path: Path, rows: Iterable[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--input", required=True, help="validation-result.txt from -out:")
    p.add_argument("--edges-out", required=True, help="JSONL edge output path")
    p.add_argument("--handoff-out", required=True, help="handoff summary JSON path")
    args = p.parse_args(argv)

    src = Path(args.input)
    if not src.is_file():
        print(f"missing input: {src}", file=sys.stderr)
        return 2

    text = src.read_text(encoding="utf-8", errors="replace")
    edges, handoff = parse_validation(text, str(src).replace("\\", "/"))
    write_jsonl(Path(args.edges_out), edges)
    Path(args.handoff_out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.handoff_out).write_text(json.dumps(handoff, indent=2), encoding="utf-8")
    print(json.dumps(handoff, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
