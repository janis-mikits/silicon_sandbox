#!/usr/bin/env python3
"""Check numerical Mac gates in a completed frozen-reference player report.

This check does not certify visual, Windows, or CI acceptance.
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
    if ("REFERENCE ARCHIVE SHA-256="
            "016e9cff9773be7cb0b931a7d7492b8a9ec856b79418e445a39ec0acf1c95c3c"
            not in report):
        raise SystemExit("Benchmark reference hash is missing or changed.")
    if ("DENSE DIAGNOSTIC SHA-256="
            "9dfc121962af93d0151c09203b82bb2dc2d4275b72145aeba2d505f7dbef9242"
            not in report):
        raise SystemExit("Dense diagnostic hash is missing or changed.")
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
        warmup = float(field(r"warmup=([0-9.]+)s", line))
        measured = float(field(r"measured=([0-9.]+)s", line))
        if warmup < 10 or measured < 60 or fps < 60 or p99 > 33.3 or edges != (
                1200 if name.startswith("active") else 0):
            raise SystemExit(f"Performance investigation required for {name}: {line}")
    if seen != expected:
        raise SystemExit(f"Missing benchmark cases: {expected - seen}")
    expected_shapes = {"sparse-standalone", "repeated-modules",
                       "dense-opaque"}
    seen_shapes = set()
    for line in report.splitlines():
        if not line.startswith("SHAPE="):
            continue
        name = line.split()[0][6:]
        if name not in expected_shapes or name in seen_shapes:
            raise SystemExit(f"Unexpected or duplicate shape view: {name}")
        seen_shapes.add(name)
        fps = float(field(r"average FPS=([0-9.]+)", line))
        p99 = float(field(r"p99 ms=([0-9.]+)", line))
        warmup = float(field(r"warmup=([0-9.]+)s", line))
        measured = float(field(r"measured=([0-9.]+)s", line))
        edges = int(field(r"edges=([0-9]+)", line))
        if (warmup < 5 or measured < 20 or fps < 60 or
                p99 > 33.3 or edges != 0):
            raise SystemExit(f"Shape-view performance investigation required: {line}")
    if seen_shapes != expected_shapes:
        raise SystemExit(f"Missing shape views: {expected_shapes - seen_shapes}")
    operations = [line for line in report.splitlines() if line.startswith("OP=")]
    if len(operations) != 10 or any(
            "result=within" not in line for line in operations):
        raise SystemExit("Edit/save/load response check missing or over budget.")
    print("Strict Mac numerical frame, shape, clock, edit, save and load gates passed.")


if __name__ == "__main__":
    main()
