# FsAssay

FsAssay is an experimental .NET 10 static analyzer for F# source. It combines
FSharp.Compiler.Service typed-tree inspection, syntax/heuristic rules, and
project-level graph analysis.

It is not a certification system and a clean result is not proof that a program
is correct, secure, pure, or production-ready.

## Evidence-bounded authority

The catalog contains 93 rule identifiers: 35 marked `Implemented`, 22 `Dummy`,
and 36 `Prototype`. Catalog status alone does not admit a rule to affect the
production verdict.

The inherited M1 catalogue recorded 21 rules as production-admitted because
each had an independently executable positive behavioral specimen:

`FSA2022`, `FSA2017`, `FSA-AI01`, `FSA-AI12`, `FSA-AI13`, `FSA-AI15`,
`FSA-AI16`, `FSA-C02`, `FSA-C05`, `FSA-P01`, `FSA-P02`, `FSA-P03`,
`FSA-P04`, `FSA-P05`, `FSA-SEC08`, `FSA-SEC11`, `FSA-SEC12`, `FSA-SEC13`,
`FSA-TDD01`, `FSA-TDD02`, and `FSA-TDD03`.

That legacy admission is historical evidence, not Human Gate C approval. The M3
candidate lock proposes **zero blocking rules**, pending Human Gate C. All current rule observations,
including the inherited set, remain historical/experimental and cannot block,
suppress, or help manufacture a `Pass`. M3 classifies the complete catalogue as
35 experimental, 36 prototype and 22 dummy rules; implemented is not admitted.

The normative [Shape New](docs/contracts/SHAPE-NEW-v1.md) and
[Shape Converge](docs/contracts/SHAPE-CONVERGE-v1.md) contracts define the
reviewed functional-core direction and bounded migration sequence. The
[M3 admission record](docs/contracts/M3-SHAPE-RULE-ADMISSION.md) explains why
no current rule meets the blocking or advisory bar.

The versioned [Authority Contract](docs/contracts/AUTHORITY-CONTRACT-v1.md)
defines exactly four top-level outcomes: `Pass`, `Fail`, `Inconclusive`, and
`ToolFailure`. `Pass` requires all policy-required evidence to be complete and
successful; absence is never success. JSON written by `--out-json` is now a
versioned authority receipt rather than the pre-M2 array of file findings.

CLI exit codes:

- `0`: complete policy-required evidence produced `Pass`
- `1`: a completed required test failed, or a future Gate-C-approved blocking rule found a violation
- `2`: policy-required evidence was incomplete, unavailable, unsupported, or intentionally `notRun`
- `3`: FsAssay could not produce trustworthy valid evidence
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

Consumer commands on the packaged CLI are:

```bash
fsassay help
fsassay doctor
fsassay explain FSA-C02
fsassay --out-json artifacts/result.json --out-sarif artifacts/result.sarif ./MyProject
fsassay --docs artifacts/rules
```

`help`, `--help`, and `-h` render the same help once and exit `0`. `doctor`
reports the installed tool identity, local SDK/runtime/FCS identity and the
offline-default posture without running analysis. `explain` reports only an
existing catalogue rule and its reviewed M3 class; it is explicitly
non-authoritative. `--docs <dir>` is the implemented catalogue surface. The
default target invocation is the implemented check/strict-authority path and
may honestly exit `2` when policy-required evidence is incomplete. There are no
separate `catalog`, `check`, or `verify` commands.

M4 qualifies installation from a local candidate feed without publishing it:

```bash
dotnet new tool-manifest
dotnet tool install FsAssay.Cli --version 1.0.4 --source /path/to/local/feed
dotnet tool run fsassay -- doctor
dotnet tool uninstall FsAssay.Cli
```

Analysis is offline by default: FsAssay has no telemetry or source-upload path.
Package restore/install may contact configured feeds unless the consumer clears
them or supplies an isolated local feed. `--serve` is the explicit localhost
listener and is outside the offline analysis proof.

`.fsassayrc` remains the scan-selection configuration and still falls back to
defaults when malformed. `fsassay-policy.lock.json` is the separate strict,
versioned authority policy. M3 deliberately has no CLI surface for ingesting
ambient test success, so the repository self-audit records its required stable
test as `notRun` and is `Inconclusive` even when CI ran tests separately.

## Product identity and boundaries

The inherited baseline identity is `1.0.4`. The read-only source baseline is
`1f25f3088a4a6fb7db980410bc5a2a767de57f2e`; M1 begins from migrated target
`main` at `f773b3090ffd86cb5600fdaf3aca20ec9cc19606`. M1 was a repository truth and
qualification repair. M2 begins at merged `main`
`13e2314ec8676aaf224440d6a46d3196ac84d2ef` and adds only the draft authority
contract and deterministic receipt. Neither milestone is a package publication
or new release claim. M3 begins at merged `main`
`8da5c3305489d0ac4d07339c400b5fdd7ebed1b1` and adds Shape, complete rule
classification and typed baseline governance without changing analyzer rules.
M4 retains `1.0.4` and does not publish it. Its candidate package includes a
README, Apache-2.0 license, repository URL/commit and SourceLink metadata. The
canonical package is qualified across independent roots and receives a GitHub
build-provenance attestation on exact candidate push. That attestation is not a
NuGet package signature: the candidate remains unsigned and `NU3004` is an
explicit limitation.

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
