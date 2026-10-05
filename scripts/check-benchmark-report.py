#!/usr/bin/env python3
"""Check numerical Mac gates in a completed frozen-reference player report.

This check does not certify visual, render-region, Windows, or CI acceptance.
"""

import re
import sys
from pathlib import Path


def field(pattern: str, line: str) -> str:
    match = re.search(pattern, line)
    if match is None:
        raise SystemExit(f"Missing benchmark field {pattern}: {line}")
    return match.group(1)


def main() -> None:
    if len(sys.argv) != 3 or sys.argv[2] not in {"0", "1"}:
        raise SystemExit("Usage: check-benchmark-report.py REPORT 0|1")
    report = Path(sys.argv[1]).read_text()
    if "REFERENCE MEASUREMENT COMPLETE" not in report.splitlines():
        raise SystemExit("Benchmark report is incomplete.")
    if sys.argv[2] == "1":
        print("Native diagnostic only; strict performance gates are not certified.")
        return
    if "DIAGNOSTIC ONLY:" in report or "Resolution=1920x1080" not in report:
        raise SystemExit("Strict benchmark did not use the required 1920x1080 resolution.")
    expected = {"idle/stationary", "active/stationary",
                "idle/flying", "active/flying"}
    seen = set()
    for line in report.splitlines():
        if not line.startswith("CASE="):
            continue
        name = line.split()[0][5:]
        if name not in expected or name in seen:
            raise SystemExit(f"Unexpected or duplicate benchmark case: {name}")
        seen.add(name)
        fps = float(field(r"average FPS=([0-9.]+)", line))
        p99 = float(field(r"p99 ms=([0-9.]+)", line))
        edges = int(field(r"edges=([0-9]+)", line))
        if fps < 60 or p99 > 33.3 or edges != (
                1200 if name.startswith("active") else 0):
            raise SystemExit(f"Performance investigation required for {name}: {line}")
    if seen != expected:
        raise SystemExit(f"Missing benchmark cases: {expected - seen}")
    operations = [line for line in report.splitlines() if line.startswith("OP=")]
    if len(operations) != 10 or any(
            "result=within" not in line for line in operations):
        raise SystemExit("Edit/save/load response check missing or over budget.")
    print("Strict Mac numerical frame, clock, edit, save and load gates passed.")


if __name__ == "__main__":
    main()
