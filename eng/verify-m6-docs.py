#!/usr/bin/env python3
"""Validate the bounded M6 static documentation without network access."""

from __future__ import annotations

import html.parser
import pathlib
import re
import sys
from urllib.parse import unquote


class DocumentParser(html.parser.HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.ids: set[str] = set()
        self.links: list[str] = []
        self.stylesheets: list[str] = []
        self.title_count = 0
        self.main_count = 0
        self.h1_count = 0

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = {name: value or "" for name, value in attrs}
        if values.get("id"):
            self.ids.add(values["id"])
        if tag == "a" and values.get("href"):
            self.links.append(values["href"])
        if tag == "link" and values.get("rel") == "stylesheet":
            self.stylesheets.append(values.get("href", ""))
        if tag == "title":
            self.title_count += 1
        if tag == "main":
            self.main_count += 1
        if tag == "h1":
            self.h1_count += 1


def fail(message: str) -> None:
    raise AssertionError(message)


def local_target(source: pathlib.Path, href: str) -> pathlib.Path | None:
    if href.startswith(("http://", "https://", "mailto:")):
        return None
    path_text = unquote(href.split("#", 1)[0])
    if not path_text:
        return source
    return (source.parent / path_text).resolve()


def validate_html(root: pathlib.Path) -> None:
    source = root / "docs-website" / "index.html"
    text = source.read_text(encoding="utf-8")
    parser = DocumentParser()
    parser.feed(text)
    parser.close()

    if parser.title_count != 1 or parser.main_count != 1 or parser.h1_count != 1:
        fail("homepage must contain exactly one title, main and h1")
    if len(parser.ids) < 8:
        fail("homepage has too few stable section anchors")
    if parser.stylesheets != ["css/style.css"]:
        fail(f"unexpected stylesheet surface: {parser.stylesheets}")

    for href in parser.links:
        if href.startswith("#"):
            anchor = href[1:]
            if anchor not in parser.ids:
                fail(f"missing homepage anchor: {href}")
            continue
        target = local_target(source, href)
        if target is not None and not target.is_file():
            fail(f"broken homepage link: {href} -> {target}")

    stale_claims = (
        "Auto-Fix Refactoring",
        "Native AOT Desktop",
        "Auto-Refactoring Engine",
        "Instantly convert hostile anti-patterns",
        "Powered by Google Antigravity",
    )
    for claim in stale_claims:
        if claim in text:
            fail(f"stale or unsupported homepage claim remains: {claim}")

    required = (
        "Evidence before verdicts",
        "Inconclusive",
        "authoritative",
        "Shape New",
        "Shape Converge",
        "Blocking</span><strong>0",
        "Advisory</span><strong>0",
        "not documented as a public NuGet release",
    )
    for phrase in required:
        if phrase not in text:
            fail(f"homepage is missing required evidence-bounded text: {phrase}")


def validate_markdown(root: pathlib.Path) -> None:
    source = root / "docs" / "ADOPTION-REFERENCE.md"
    text = source.read_text(encoding="utf-8")
    links = re.findall(r"(?<!!)\[[^]]+\]\(([^)]+)\)", text)
    for href in links:
        target = local_target(source, href)
        if target is not None and not target.is_file():
            fail(f"broken adoption-reference link: {href} -> {target}")

    required_headings = (
        "## Business value",
        "## Quick start from a reviewed local package",
        "## Shipped CLI surface",
        "## Trust and authority",
        "## Rule maturity and human review",
        "## Shape New",
        "## Shape Converge",
        "## Baselines, suppressions and exceptions",
        "## Known limits and nonclaims",
        "## Release and evidence provenance",
    )
    for heading in required_headings:
        if heading not in text:
            fail(f"adoption reference is missing section: {heading}")

    quick_start_commands = (
        "dotnet new tool-manifest",
        "dotnet tool install FsAssay.Cli --version 1.0.4 --source /absolute/path/to/local/feed",
        "dotnet tool restore",
        "dotnet tool run fsassay -- doctor",
        "dotnet tool run fsassay -- help",
        "dotnet tool run fsassay -- explain FSA-C02",
        "dotnet tool run fsassay -- --out-json artifacts/fsassay.json --out-sarif artifacts/fsassay.sarif ./MySolution.slnx",
        "dotnet tool uninstall FsAssay.Cli",
    )
    normalized = re.sub(r"\\\n\s*", "", text)
    for command in quick_start_commands:
        if command not in normalized:
            fail(f"quick-start command missing or changed: {command}")

    bounded_claims = (
        "There are no shipped `catalog`, `check` or `verify` commands.",
        "0 / 0",
        "required to keep\n  `appliedSuppressions` empty",
        "not represented as\n  a public NuGet publication",
        "85cfd65dbc6d723e2b438b700efae7754ee506b0",
    )
    for claim in bounded_claims:
        if claim not in text:
            fail(f"adoption reference is missing bounded claim: {claim}")


def main() -> int:
    root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    validate_html(root)
    validate_markdown(root)
    print("M6 static documentation validation passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
