#!/usr/bin/env python3
"""Verify that a repository-local .NET tool manifest retains no installed tools."""

from __future__ import annotations

import json
import sys
from pathlib import Path


def fail(message: str) -> int:
    print(f"M6 rollback verification failed: {message}", file=sys.stderr)
    return 1


def main() -> int:
    if len(sys.argv) != 2:
        print(f"usage: {Path(sys.argv[0]).name} <dotnet-tools.json>", file=sys.stderr)
        return 64

    manifest = Path(sys.argv[1])
    if not manifest.exists():
        return 0
    if not manifest.is_file():
        return fail(f"manifest is not a regular file: {manifest}")

    try:
        payload = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        return fail(f"cannot read manifest {manifest}: {error}")

    tools = payload.get("tools")
    if not isinstance(tools, dict):
        return fail(f"manifest has no tools object: {manifest}")
    if tools:
        identities = ", ".join(sorted(str(identity) for identity in tools))
        return fail(f"manifest still contains tool identities: {identities}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
