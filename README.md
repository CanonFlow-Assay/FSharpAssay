# FsAssay

FsAssay is an experimental .NET 10 static analyzer for F# source. It combines
FSharp.Compiler.Service typed-tree inspection, syntax/heuristic rules, and
project-level graph analysis.

It is not a certification system and a clean result is not proof that a program
is correct, secure, pure, or production-ready.

## Evidence-bounded verdicts

The catalog currently contains 91 rule identifiers: 35 marked `Implemented`,
22 `Dummy`, and 34 `Prototype`. Catalog status alone does not admit a rule to
affect the production verdict.

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

## Build and verify

The repository expects the SDK selected by `global.json`.

```bash
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test
```

Run the CLI against a project or directory:

```bash
dotnet run --project FsAssay.Runner/FsAssay.Runner.fsproj -- ./MyProject
```

Run only explicitly declared files:

```bash
fsassay ./MyProject --files ./MyProject/A.fs,./MyProject/B.fs --json result.json
```

The `.fsassayrc` format supports `profile` and `exclude`. Unknown or malformed
configuration currently falls back to defaults, so callers that require a
strict policy should pin the packaged tool and validate their configuration
before invocation.

## Scope

FsAssay can surface patterns worth review. Some rules are heuristic and may
produce false positives or false negatives. The behavioral suite establishes
specific executable examples; it does not establish universal precision or
recall.

Licensed under Apache-2.0. See `LICENSE`.
