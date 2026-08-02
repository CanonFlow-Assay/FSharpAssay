# FsAssay

FsAssay is an experimental .NET 10 static analyzer for F# source. It combines
FSharp.Compiler.Service typed-tree inspection, syntax/heuristic rules, and
project-level graph analysis.

It is not a certification system and a clean result is not proof that a program
is correct, secure, pure, or production-ready.

## Evidence-bounded verdicts

The catalog contains 93 rule identifiers: 35 marked `Implemented`, 22 `Dummy`,
and 36 `Prototype`. Catalog status alone does not admit a rule to affect the
production verdict.

The production boundary admits these 21 implemented rules because each has an
independently executable positive behavioral specimen:

`FSA2022`, `FSA2017`, `FSA-AI01`, `FSA-AI12`, `FSA-AI13`, `FSA-AI15`,
`FSA-AI16`, `FSA-C02`, `FSA-C05`, `FSA-P01`, `FSA-P02`, `FSA-P03`,
`FSA-P04`, `FSA-P05`, `FSA-SEC08`, `FSA-SEC11`, `FSA-SEC12`, `FSA-SEC13`,
`FSA-TDD01`, `FSA-TDD02`, and `FSA-TDD03`.

Findings from catalog rules outside that set are informational/inconclusive and
cannot create a blocking production verdict. External plugin findings are
reported separately and remain governed by the plugin's declared severity.

CLI exit codes:

- `0`: admitted analysis completed without a blocking finding
- `1`: an admitted critical or major rule found a blocking issue
- `2`: required evidence was missing or only non-admitted findings were present
- `3`: the tool could not complete the requested evaluation
- `64`: the command line was invalid

## Build and verify

The repository expects the SDK selected by `global.json`. Stable qualification
uses the bounded solution and locked package graph:

```bash
dotnet restore FsAssay.Stable.slnx --locked-mode
dotnet build FsAssay.Stable.slnx --no-restore --configuration Release
bash eng/run-stable-tests.sh ordinary
```

Run the CLI against a project or directory:

```bash
dotnet run --project FsAssay.Runner/FsAssay.Runner.fsproj -- ./MyProject
```

Run only explicitly declared files and write canonical JSON:

```bash
fsassay ./MyProject --files ./MyProject/A.fs,./MyProject/B.fs --out-json artifacts/result.json
```

The `.fsassayrc` format supports `profile` and `exclude`. Unknown or malformed
configuration currently falls back to defaults, so callers that require a
strict policy should pin the packaged tool and validate their configuration
before invocation.

## Product identity and boundaries

The inherited baseline identity is `1.0.4`. The read-only source baseline is
`1f25f3088a4a6fb7db980410bc5a2a767de57f2e`; M1 begins from migrated target
`main` at `f773b3090ffd86cb5600fdaf3aca20ec9cc19606`. M1 is a repository truth and
qualification repair; it is not a package publication or new release claim.

The analyzer and CLI runner are the stable qualification surface. The stable
tests transitively build the external CanonFlow plugin solely as frozen
regression evidence; that plugin is not admitted core authority. Desktop, Web,
MCP, TypeGym, and external plugin surfaces are frozen experimental work and are
not promoted by M1. See [product boundaries](docs/PRODUCT-BOUNDARIES.md).

## Scope

FsAssay can surface patterns worth review. Some rules are heuristic and may
produce false positives or false negatives. The behavioral suite establishes
specific executable examples; it does not establish universal precision or
recall.

Licensed under Apache-2.0. See `LICENSE`.
