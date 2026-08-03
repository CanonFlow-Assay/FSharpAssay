# FsAssay 1.0.4 adoption reference

FsAssay is an experimental .NET 10 static-analysis and evidence tool for F#.
It combines compiler typed-tree inspection, syntax and heuristic observations,
and project-level analysis. Its useful output is a review input: findings help a
team locate code worth examining, while the authority receipt makes missing or
failed evidence explicit.

FsAssay is not a certification system, a correctness or security proof, an
automatic functional-F# converter, or a substitute for compiler, test and human
review. Version 1.0.4 is an inherited package identity qualified from local
candidate feeds in this repository; it is not documented here as a public NuGet
release.

## Business value

FsAssay addresses three practical adoption problems:

1. **Review focus.** It records deterministic locations and fingerprints for
   selected F# design observations so reviewers can discuss exact evidence.
2. **Honest CI state.** Its JSON receipt distinguishes `Pass`, `Fail`,
   `Inconclusive` and `ToolFailure`; missing projects or tests cannot become a
   clean result merely because the finding count is zero.
3. **Bounded modernization.** The Shape contracts give teams a reviewed
   functional-core direction for new work and a behavior-preserving sequence
   for existing work without requiring a rewrite.

This value depends on project boundaries, policy, test evidence and human
adjudication. Raw finding-count reduction is not a success metric.

## Qualified product boundary

The stable qualification surface is the analyzer library and CLI runner built
by `FsAssay.Stable.slnx`. The CanonFlow plugin is compiled only as a frozen test
fixture. Desktop, Web, MCP, TypeGym and external plugin surfaces remain frozen
experimental source, not supported adoption promises. The Web workflow builds
and exercises a local site; it does not deploy this documentation or enable
GitHub Pages.

The CLI is offline by default for analysis: it has no telemetry or source-upload
path. Package restore and tool installation are separate operations and may
contact configured feeds unless consumers use an isolated local feed.

## Quick start from a reviewed local package

Prerequisites are the SDK pinned by the repository `global.json`, an absolute
path to a reviewed local feed containing `FsAssay.Cli.1.0.4.nupkg`, and an
SDK-style F# project, solution or directory. Do not replace the local feed with
nuget.org unless a separately verified public release exists.

```bash
dotnet new tool-manifest
dotnet tool install FsAssay.Cli --version 1.0.4 --source /absolute/path/to/local/feed
dotnet tool restore
dotnet tool run fsassay -- doctor
dotnet tool run fsassay -- help
dotnet tool run fsassay -- explain FSA-C02
dotnet tool run fsassay -- --out-json artifacts/fsassay.json --out-sarif artifacts/fsassay.sarif ./MySolution.slnx
```

The analysis command can exit `2` and still have produced useful evidence. Read
the JSON outcome, authority flag, reasons, project coverage, source dispositions,
required tests and tool failures before interpreting the findings.

Rollback removes only the repository-local tool registration:

```bash
dotnet tool uninstall FsAssay.Cli
```

Tool uninstall does not remove policies or evidence files created by the
consumer. Review those separately.

## Shipped CLI surface

The packaged 1.0.4 CLI has three named consumer commands:

| Command | Shipped behavior |
|---|---|
| `fsassay help` | Prints help once. `--help` and `-h` are equivalent and exit 0. |
| `fsassay doctor` | Reports tool, runtime, SDK and FCS identity plus offline-default posture; it does not analyze source. |
| `fsassay explain <RULE>` | Explains an existing catalogue identity and its M3 maturity class; the output is explicitly non-authoritative. |

Analysis is the default target invocation, not a named `check` or `verify`
command:

```bash
fsassay --out-json artifacts/result.json --out-sarif artifacts/result.sarif ./MyProject
```

`--files` narrows analysis to explicitly named files, and `--docs <dir>` emits
the implemented rule catalogue. JSON and SARIF are the qualified machine
evidence formats. The rate-card Markdown and dashboard HTML can contain absolute
checkout paths and are not qualified for cross-root byte identity.

There are no shipped `catalog`, `check` or `verify` commands. `--fix` is parsed
but reports automatic fixes disabled. `--diff` has no qualified runtime
consumer. `--serve`, MCP and plugins are outside the stable adoption boundary.

## Trust and authority

The authority contract defines four outcomes in precedence order:

| Outcome | Exit | Meaning |
|---|---:|---|
| `ToolFailure` | 3 | FsAssay could not produce trustworthy valid evidence. |
| `Fail` | 1 | A completed required test failed, or a future approved blocking rule found new/reappearing debt. |
| `Inconclusive` | 2 | Required evidence is absent, `notRun`, unloaded, incomplete, unsupported or unexpectedly zero. |
| `Pass` | 0 | Every locked policy requirement has complete, credible, nonzero evidence and succeeds. |

Invalid command lines exit `64` and are not analysis outcomes. A receipt can
retain a known failure while setting `authoritative: false` because some other
required evidence is incomplete. The authority flag is therefore separate from
the top-level outcome.

Before relying on a receipt, check at least:

- `schemaVersion`, tool version, policy hash and candidate identity;
- `outcome`, `authoritative`, every reason and every tool failure;
- discovered, loaded, failed, skipped and unsupported project counts;
- analyzed and compiler-incomplete source dispositions;
- required-test status and totals;
- rule evidence status, finding maturity and stable fingerprints;
- applied baseline records and the absence or presence of suppression claims.

When `fsassay-policy.lock.json` is absent or invalid, findings and rule outcomes
are explicitly `unclassified`, not `removed`; the receipt remains
`Inconclusive` with `authoritative: false` and exit code `2`.

Zero findings with an unloaded workspace, incomplete compiler evidence or a
required test marked `notRun` is not clean.

## Rule maturity and human review

The complete catalogue has 93 identities. Under the locked M3 classification:

| Maturity | Count | Authority meaning |
|---|---:|---|
| Blocking | 0 | No rule is admitted to fail a release. |
| Advisory | 0 | No rule is admitted as an organization-wide recommendation. |
| Experimental | 35 | Executable observation requiring more boundary and precision evidence. |
| Prototype | 36 | Provisional or incomplete implementation. |
| Dummy | 22 | Catalogue placeholder without executable authority evidence. |
| Deprecated / removed | 0 / 0 | None in the current classification. |

`Implemented` describes code availability, not admission. Experimental and
prototype findings may be useful, low-value, contextual or false positive. An
agent must not automatically refactor one simply because it exists. A future
blocking admission requires a separate human Gate C review with positive and
negative specimens, boundary tests, remediation risks and packaged-consumer
evidence.

## Shape New

[Shape New](contracts/SHAPE-NEW-v1.md) is a human-reviewed design contract for
new or materially uplifted code. Its practical direction is:

- keep domain decisions in a functional core and effects in explicit adapters;
- prefer immutable values and explicit `Result`, `Option` or native unions for
  expected outcomes;
- make meaningful states explicit and handle required decisions totally;
- prevent public construction from bypassing documented invariants;
- pass time, configuration and effects through explicit core boundaries;
- keep adapters thin and protect behavior plus architecture with deterministic
  tests.

Shape does not ban classes or all mutation, demand point-free style, prove
purity, or claim that current analyzers cover every clause.

## Shape Converge

[Shape Converge](contracts/SHAPE-CONVERGE-v1.md) is the bounded path for existing
code:

1. establish a healthy compiler and test baseline;
2. record findings and incomplete evidence honestly;
3. fingerprint reviewed existing debt;
4. select one module or vertical slice;
5. freeze behavior before changing it;
6. move decisions toward the core and effects toward boundaries;
7. apply Shape New to new or materially uplifted core work;
8. repeat without claiming a whole-application conversion.

Stop at `ToolFailure` or incomplete authority evidence. Refusing an unsafe
count-reducing change is a valid result.

## Baselines, suppressions and exceptions

These mechanisms are deliberately distinct:

- A **baseline record** is reviewed policy data for a finding. It binds record
  ID, rule ID, fingerprint, repository-relative path, symbol, owner, rationale,
  disposition, dates and policy version. The baseline identity hashes canonical
  reviewed content. Only an active exact `accepted` match can classify existing
  blocking debt; `resolved`, expired or unmatched records fail if the rule is
  later admitted as blocking.
- A **Shape exception** documents a reviewed framework boundary such as hosting,
  serialization or persistence. It does not suppress a diagnostic or hide
  missing compiler, project or test evidence.
- Source suppression/profile metadata can be listed with
  `--suppressionreport-json`, but the M3 authority receipt is required to keep
  `appliedSuppressions` empty. A scan exclusion or source suppression is not a
  substitute for a reviewed baseline record.

The current repository policy has zero blocking/advisory rules and an empty
baseline identity `none`; baselines therefore do not manufacture a current
`Pass` or hide current findings.

## Known limits and nonclaims

- The CLI cannot ingest ambient test success. A policy-required test can remain
  `notRun` even when CI ran it separately, forcing non-authoritative
  `Inconclusive` evidence.
- Project classes are conservative filename-derived categories. Target
  framework discovery is limited to bounded SDK-style XML; general conditional
  and multi-target MSBuild compatibility is not qualified.
- Rules can produce false positives and false negatives. The specimen suite is
  not universal precision or recall proof.
- JSON and SARIF are qualified for deterministic evidence in the bounded lanes;
  human reports with absolute paths are not cross-root deterministic.
- GitHub artifact attestation proves the workflow-produced package subject, not
  analyzer correctness, safe remediation, business correctness or source
  security.
- The local candidate package is unsigned (`NU3004`) and is not represented as
  a public NuGet publication.
- No current capability automatically converts a codebase, proves it functional,
  or authorizes an LLM to change code without tests and human review.

## Release and evidence provenance

The package identity remains 1.0.4 throughout the evidence milestones:

| Milestone | Merged main | Bounded result |
|---|---|---|
| M0 | `f773b3090ffd86cb5600fdaf3aca20ec9cc19606` | Chain of custody and source baseline. |
| M1 | `13e2314ec8676aaf224440d6a46d3196ac84d2ef` | Stable boundary, locked restore and executable tests. |
| M2 | `8da5c3305489d0ac4d07339c400b5fdd7ebed1b1` | Four-state authority contract and deterministic receipt. |
| M3 | `36c1b9264618344878cbf9dcca11363f5ea3d59b` | Shape, complete maturity classification and typed baseline governance. |
| M4 | `04beefc9a3810ff57d975cf8cc57a99898587a73` | Reproducible local package, fresh-install/rollback evidence, SourceLink and GitHub attestation. |
| M5 | `85cfd65dbc6d723e2b438b700efae7754ee506b0` | Evidence-first Playground and independent consumer exercise. |

M4 and M5 did not tag, publish or release 1.0.4. The package README, Apache-2.0
license, repository commit and SourceLink improve provenance. The package remains
NuGet-unsigned, and GitHub attestation is not a NuGet repository signature.

## Normative and evidence links

- [Authority contract](contracts/AUTHORITY-CONTRACT-v1.md)
- [Shape New](contracts/SHAPE-NEW-v1.md)
- [Shape Converge](contracts/SHAPE-CONVERGE-v1.md)
- [Rule-admission contract](contracts/M3-SHAPE-RULE-ADMISSION.md)
- [Policy schema](contracts/fsassay-policy.schema.json)
- [Receipt schema](contracts/fsassay-authority-receipt.schema.json)
- [Product boundaries](PRODUCT-BOUNDARIES.md)
- [M4 package qualification](evidence/m4-consumer-package-qualification.md)
- [M5 independent consumer evidence](evidence/m5-independent-consumer.md)
- [Two-lane CI model](ci-cd/two-lane-model.md)

Normative contracts outrank this orientation page when wording differs.
