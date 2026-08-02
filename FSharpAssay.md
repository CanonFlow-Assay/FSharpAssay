---FSharpAssay
title:  — Review-Integrated Implementation Plan
version: 1.1.0
status: ready-for-phase-0
date: 2026-07-20
supersedes: FS_ASSAY_IMPLEMENTATION_PLAN.md v1.0.0
source_skills_revision: arun6202/ai-skills@18c4217
review_basis: Qwen review, independently verified and selectively adopted

Enforce all fsharp idiomic from https://github.com/ArunNotFound/functional-skills

---

# `fs-assay` — F# Verification Officer

> A deterministic verification application that treats AI-generated F# as
> hostile input and produces compiler-, analyzer-, policy-, and test-backed
> evidence before code may cross a repository gate.

This document incorporates the useful findings from the Qwen review after
checking them against the actual upstream source. It is a standalone revised
plan, not a blind acceptance of the review.

The executable is `fs-assay`. Assemblies and packages use `FsAssay.*`. No LLM
participates in the verdict path.

---

## 0. Final architectural decision

Build `fs-assay` as an opinionated release-verification application using:

1. the F# compiler and FSharp.Compiler.Service for compile/type truth;
2. `FSharp.Analyzers.SDK` for public AST/TAST contexts, collectors, analyzer
   loading, ignore-range parsing, and FsAutoComplete compatibility;
3. a direct `FsAssay.Cli` orchestration path that owns project loading, tool
   failure handling, policy, tests, evidence, and verdicts;
4. Fantomas for formatting;
5. FSharpLint only for rules verified during Phase 0 to be correct and compatible;
6. custom `FsAssay.Analyzers` for skill-specific F# rules;
7. Expecto and FsCheck for examples, counterexamples, and properties;
8. `Sarif.Sdk` for SARIF construction extended with `fs-assay` evidence fields.

Do not create a custom recursive FCS walker, fork FsAutoComplete, or shell out to
the upstream analyzer CLI as the production verdict path.

### Why the separate application is necessary

The upstream `FSharp.Analyzers.Cli` is valuable for analyzer development and
parity testing, but it does not own `fs-assay` policy, compiler/test gates,
evidence, multi-TFM semantics, or the four-state verdict.

More importantly, the SDK exposes two runner methods with different trust
properties:

```text
RunAnalyzers       = ignores analyzer exceptions
RunAnalyzersSafely = returns Result<Message list, exn> per analyzer
```

`fs-assay` MUST use `RunAnalyzersSafely`. A runner that converts an analyzer
crash into zero findings is incompatible with this product's prime law.

---

## 1. Review disposition

### Adopted

| Review item | v1.1 action |
| --- | --- |
| Exact SDK/FCS pinning | Made a non-negotiable toolchain law with compatibility tests and recorded resolved versions. |
| Explicit SDK CLI relationship | `FsAssay.Cli` uses public SDK APIs directly; upstream CLI is development/parity tooling only. |
| SDK ignore ranges | Integrated into a stricter, auditable suppression gate. |
| Typed tree is optional | Added explicit `Completed / Skipped / Failed` rule evaluation and evidence. |
| `walkTast` input and ancestor-context issue | Added hybrid TAST-symbol + AST-range context indexing. |
| Generated-source policy | Added attested exclusions and anti-evasion rules. |
| Multi-target framework policy | Added all-TFM verification; did not accept first-TFM-only default. |
| Incremental agent checks | Added provisional `check` separate from authoritative full `verify`. |
| `.fsi` semantics | Added per-rule applicability and evidence requirements. |
| FSharpLint inventory | Made Phase 0 delegation conditional on corpus verification. |
| Law Zero | Promoted agent/judge separation to the first law. |
| `toolchain-lock.json` | Added as generated evidence using actual resolved versions. |
| Phase 1 ordering | Adopted: TAST rule → AST rule → project boundary → cross-file API rule. |

### Adapted

| Review item | Why it was changed |
| --- | --- |
| Reuse SDK SARIF writer | Its writer lives in the CLI executable rather than a public reusable reporting API. `fs-assay` uses the same `Sarif.Sdk` model and mapping principles, then adds certainty, skill references, suppressions, and evidence links through SARIF properties. |
| Populate fixes from day one | Every rule provides remediation text from day one. An SDK `Fix` is emitted only when the edit is local, mechanically safe, source-snapshot matched, and semantics-preserving. Detection does not imply a safe rewrite. |
| Vendor the analyzer SDK if it changes | Exact pins, compatibility tests, and a narrow adapter come first. Vendoring is an explicit ADR fallback only if upstream compatibility or availability becomes unacceptable. |
| Default effect catalogue | Ship only reviewed BCL and skill-relevant entries initially. Additional libraries are versioned extensions, not speculative built-ins. |

### Rejected

| Review suggestion | Reason |
| --- | --- |
| Analyze only the first TFM by default | A release verdict could miss framework-specific errors or APIs. Full `verify` analyzes every configured TFM unless the policy explicitly defines a narrower, honestly named target. |
| Require an auto-fix for `Option.get` | The correct replacement depends on domain policy: propagate `Option`, return `Result`, choose a default, or prove presence. No universal semantics-preserving edit exists. |
| Hand-write sample tool versions into the lock | The sample versions may be inconsistent. The lock is generated from the actual SDK, assembly, package, tool-manifest, and runtime resolution. |

---

## 2. Constitution

### Law Zero — Agent/judge separation

An AI agent whose code is under assay MUST NOT modify assay policy, rule
catalogue, severity, profile mapping, suppression registry, Gold fixtures,
evidence schema, toolchain lock, or CI gate in the same unreviewed change it is
trying to pass.

```text
Author(Change) ∩ UnreviewedAuthor(JudgeChange) = ∅
```

### Law One — Honest uncertainty

```text
Missing required evidence        ⇒ Inconclusive
Analyzer exception               ⇒ ToolFailure
Analyzer load failure            ⇒ ToolFailure
Incompatible runtime/toolchain   ⇒ ToolFailure
Project/TFM load failure         ⇒ ToolFailure or Inconclusive by classified cause
None of the above                ⇒ never silently Pass
```

### Law Two — Deterministic blocking

```text
DefaultBlock(f)
  ⇔ f.Certainty = Deterministic
   ∧ f.Disposition = Block
```

Contextual findings block only when a reviewed policy supplies the exact
missing context. Heuristic findings never block.

### Law Three — Reproducible verdict

```text
same(SourceSnapshot, ProjectOptions, TFMSet, Policy, RuleCatalogue, Toolchain)
⇒ same(NormalizedVerdict)
```

Clock time, hostname, process ID, checkout root, and absolute path are evidence
metadata, not finding identity.

### Law Four — Visible suppression

Every inline ignore, policy suppression, baseline entry, exclusion, and skipped
rule appears in JSON, SARIF, and the evidence bundle. Expired, malformed,
widened, unused, or unauthorized suppressions fail the suppression gate.

### Law Five — Honest non-claim

`AssayPass` proves only that enabled, mechanically specified obligations passed
under the recorded source, project options, TFM set, policy, catalogue, tests,
and toolchain. It does not prove business truth, total correctness, complete
idiomaticity, security, or performance.

### Law Six — Full release authority

An incremental or editor analysis is provisional. Only a complete `fs-assay
verify` across the declared solution/project graph and all required TFMs can
produce a release `Pass`.

---

## 3. Verdict and rule-evaluation model

```fsharp
type AssayVerdict =
    | Pass of EvidenceBundle
    | Fail of BlockingFinding list * EvidenceBundle
    | Inconclusive of MissingEvidence list * EvidenceBundle
    | ToolFailure of ToolFailure list * EvidenceBundle

type RuleCertainty =
    | Deterministic
    | Contextual
    | Heuristic

type RuleEvaluation =
    | Completed of Finding list
    | Skipped of SkipReason
    | Failed of RuleFailure

type TypedRequirement =
    | NoTypedTreeRequired
    | TypedTreeRequired
    | ProjectCheckRequired

type SignatureApplicability =
    | ImplementationOnly
    | SignatureOnly
    | Both
    | SignaturePartial of explanation: string
```

The pure rule engine returns `RuleEvaluation`; it does not directly return the
SDK's `Message list`.

- The CLI adapter retains `Skipped` and `Failed` outcomes for verdict/evidence.
- The editor adapter maps `Completed` findings into messages.
- Missing typed data in an editor may yield no visible diagnostic, but it never
  establishes a pass.
- A CLI rule marked `TypedTreeRequired` that receives `None` yields `Skipped`,
  and the orchestrator decides `Inconclusive` when the rule is required.

This avoids the information loss that would occur if individual rules simply
returned an empty list for every unavailable typed tree.

---

## 4. Exact SDK integration

`FsAssay.SdkAdapter` uses the public API surface:

```text
Utils.createFCS
Utils.typeCheckFile
Utils.createContext
Client<CliAnalyzerAttribute, CliContext>
Client<EditorAnalyzerAttribute, EditorContext>
Client.RunAnalyzersSafely
Ignore.getAnalyzerIgnoreRanges
SyntaxCollectorBase / ASTCollecting.walkAst
TypedTreeCollectorBase / TASTCollecting.walkTast
```

### Project loading

Project evaluation is a separate adapter. The initial spike follows the same
general approach as the upstream CLI:

```text
MSBuild registration
→ Ionide.ProjInfo WorkspaceLoader
→ FSharpProjectOptions per project and TFM
→ FCS ParseAndCheckProject
→ CliContext per source file
```

This adapter is the highest compatibility risk and receives integration tests
for project references, ordered files, linked files, conditional compilation,
and multi-targeting before broad rule implementation.

`FsAssay.Cli` does not shell out to `fsharp-analyzers` in production. The
upstream CLI remains a development oracle for parity tests on analyzer messages.

### Compatibility facade

All SDK/FCS types remain behind `FsAssay.SdkAdapter`. Rule/domain/reporting
projects do not reference concrete SDK context types.

At build time, a compatibility test compiles and exercises every required
public member. At startup, `fs-assay doctor` records assembly identities and
verifies analyzer loading. At execution, all analyzer calls use
`RunAnalyzersSafely`.

If source compatibility breaks, the product build fails before packaging. If
an installed analyzer cannot load or crashes, the run returns `ToolFailure`.

Vendoring is considered only through an ADR covering license, update ownership,
security patches, divergence cost, and exit strategy.

---

## 5. AST/TAST analysis strategy

### Untyped AST is used for

- syntax forms such as `null`, mutable bindings, loops, computation
  expressions, signatures, attributes, and module/type declarations;
- precise parent/ancestor ranges;
- source-shape rules where symbol identity is unnecessary.

### TAST/symbol analysis is used for

- distinguishing `Option.get` from a user function named `get`;
- resolving `.Result`, `.Wait`, EF Core, Argu `ParseResults`, Oracle APIs, and
  infrastructure dependencies by symbol identity;
- determining public API and cross-file use;
- project/assembly boundary rules.

### Hybrid context index

`TypedTreeCollectorBase.WalkCall` exposes resolved call symbols and ranges but
does not directly provide every source ancestor. Rules such as blocking inside
`async {}` therefore use a precomputed source context index:

```text
AST pass:
  collect computation-expression, loop, lambda, recursive-function,
  object-expression, module and declaration ranges

TAST pass:
  collect resolved calls, values and types

Join:
  call range ∈ smallest relevant AST context range
```

For `OBSOLETE_FSA`, the AST establishes that the call lies inside an async/task
computation expression; TAST establishes that the member is the actual blocking
`Result`/`Wait` or `Async.RunSynchronously` symbol. Neither text matching nor
TAST alone is enough.

Every hybrid rule documents its range-correlation law and includes nested,
overlapping, and generated-expression fixtures.

---

## 6. Multi-target framework law

For a project with target frameworks `T = {t₁ … tₙ}`:

```text
FullProjectPass(project) ⇔ ∀t ∈ RequiredTFMs(project, policy), Pass(project, t)
```

Default `RequiredTFMs` is every TFM declared by the project. Findings contain a
TFM field and are deduplicated only for presentation; raw evidence preserves
every per-TFM evaluation.

`--tfm <value>` is permitted for diagnosis or an explicitly scoped policy, but
its verdict is named `TargetedPass`, not full project `Pass`. It cannot satisfy
the release gate unless the reviewed policy defines that TFM as the complete
supported set.

Conditional-compilation symbols and project options are recorded per TFM.

---

## 7. Full verification versus incremental feedback

### Authoritative command

```text
fs-assay verify <solution-or-project>
```

Loads the declared graph, analyzes every required file/TFM/profile, runs all
required delegated tools/tests, and creates a release evidence bundle.

### Provisional command

```text
fs-assay check <project> --files <changed-files>
```

Optimized for agent/editor feedback. It may reuse an in-process cache keyed by:

```text
project options digest × TFM × source digest × policy digest × catalogue digest
```

Cross-file rules may consult cached project results. If required context is not
available, they report `Skipped`, not success.

Incremental evidence records:

```text
analysis_scope = incremental
changed_files
context_snapshot
rules_completed
rules_skipped
not_release_authoritative = true
```

Every bounded AI repair loop ends with a full `verify` before human review.

---

## 8. Generated and excluded source

No file is excluded merely because an AI, tool, or filename claims it is
generated.

An exclusion requires:

```text
exact pattern/path
∧ reason
∧ generator identity
∧ reproducible generation command or build provenance
∧ reviewed policy entry
```

Example:

```toml
[[exclude]]
pattern = "**/*.g.fs"
reason = "generated-parser"
generator = "FsYacc"
evidence = "eng/generate-parsers.sh"
mode = "compiler-only"

[[exclude]]
pattern = "**/AssemblyInfo.fs"
reason = "build-generated"
generator = "Microsoft.NET.Sdk"
mode = "compiler-only"
```

Exclusion modes are typed:

```text
compiler-only = compiler still sees the file; custom idiom rules do not
full-analysis = no exclusion
script-profile = analyze under reviewed script rules
```

There is no unrestricted `skip`. Excluded files, reasons, provenance, matched
counts, and unexpected unmatched patterns appear in evidence. Source edited by
the AI under review cannot gain generated status in the same unreviewed change.

Type-provider behavior is not treated as a blanket generated-file exclusion.
The user's F# source using a provider remains under assay; compiler/provider
outputs are handled according to their actual project/source representation.

`.fsx` files are excluded by default from full project verification unless
explicitly included under a `script` profile. A requested script assay records
its references and compiler options.

---

## 9. Signature-file semantics

Every rule catalogue entry adds:

```text
signature_applicability
typed_requirement
missing_typed_disposition
supported_profiles
supported_tfms
```

Examples:

| Rule | `.fs` | `.fsi` | Reason |
| --- | --- | --- | --- |
| `OBSOLETE_FSA` null literal | Yes | No for expression form | A signature has no implementation expression containing a null literal; separate public-API nullability rules require typed metadata. |
| `OBSOLETE_FSA` partial access | Yes | No | Calls occur in implementation expressions. |
| `OBSOLETE_FSA` exception control flow | Yes | No for implementation form | Signatures can be checked by a separate API/exception-contract rule, not by searching for `raise`. |
| `FSA1201` forbidden core dependencies | Yes | Yes | Signatures can expose forbidden infrastructure types. |
| `FSA1402` `ParseResults` leakage | Yes | Yes | Public signatures are a primary place to enforce the boundary. |
| naming/public API conventions | Yes | Yes | `.fsi` is authoritative for exposed API where present. |

Do not infer that a signature's plain `string` should be `string option` without
a reviewed domain/API contract. That remains contextual, not a deterministic
signature rule.

---

## 10. Suppression integration

The SDK recognizes line comments using forms such as:

```text
fsharpanalyzer: ignore-line OBSOLETE_FSA
fsharpanalyzer: ignore-line-next OBSOLETE_FSA
fsharpanalyzer: ignore-region-start OBSOLETE_FSA
fsharpanalyzer: ignore-region-end
fsharpanalyzer: ignore-file OBSOLETE_FSA
```

Its `Client` filters matching messages. Therefore `fs-assay` audits
`AnalyzerIgnoreRanges` **before** message filtering and resolves every range
against the approved suppression registry.

An approved suppression requires:

```text
rule ID
∧ exact file/range or stable symbol
∧ justification
∧ owner
∧ issue/reference
∧ creation date
∧ expiry/review date
∧ policy reviewer
```

Unknown inline ignores, unmatched region ends, expired entries, unused policy
entries, file-wide ignores of blocking rules, and widened ranges become
suppression-policy findings.

CI never accepts SDK comment suppression alone. Editor suppression remains a
convenience view; the release authority is the registry-backed policy gate.

---

## 11. Remediation and safe fixes

Each finding includes human/agent-readable remediation from Phase 1:

```fsharp
type Remediation =
    | Explain of text: string
    | SafeFix of fromRange: range * fromText: string * toText: string
```

An SDK `Fix` is emitted only when:

1. the edit is local and exact;
2. `FromText` matches the analyzed source snapshot;
3. the transformation is semantics-preserving under the rule contract;
4. applying it cannot change public API or domain failure policy;
5. the fixed file compiles and the triggering finding disappears;
6. no new blocking finding is introduced.

`Option.get`, exception-to-`Result`, primitive-to-constrained-type, class-to-
module, and public renames do not receive automatic fixes by default because
they require design intent.

FsAutoComplete already transports analyzer `Fixes` through diagnostic data and
can expose them as code actions. `fs-assay` uses that path only for `SafeFix`.
The CLI does not apply fixes in v1; a future `fix` command requires a separate
law sheet, diff preview, compilation, reassay, and human acceptance.

---

## 12. SARIF and evidence

The SDK CLI's SARIF implementation is inside its executable. `fs-assay` builds a
small reporter over `Sarif.Sdk`, following the same correct source-range and
severity mapping while adding:

- rule certainty and disposition;
- skill source/revision/section;
- profile and TFM;
- evidence pointer;
- suppression status;
- skipped/missing-context information;
- safe-fix metadata when present.

These fields use SARIF `properties`; standard consumers still receive ordinary
rule, level, message, location, help URI, and fix data.

The evidence bundle contains:

```text
assay-run.json
toolchain-lock.json
project-options/
compiler-diagnostics.json
rule-evaluations.json
analyzer-findings.json
delegated-tool-results.json
test-results/
suppression-report.json
exclusion-report.json
findings.sarif
```

### Generated toolchain lock

`toolchain-lock.json` records actual resolved values, never copied examples:

```json
{
  "schema_version": 1,
  "dotnet_sdk": "<dotnet --version>",
  "fsharp_core": "<loaded assembly identity>",
  "fsharp_compiler_service": "<loaded assembly identity>",
  "fsharp_analyzers_sdk": "<Utils.currentFSharpAnalyzersSDKVersion>",
  "fantomas": "<resolved tool-manifest version>",
  "fsharplint": "<resolved tool-manifest version or null>",
  "fs_assay": "<assembly informational version>",
  "rule_catalogue_digest": "sha256:<digest>",
  "policy_digest": "sha256:<digest>"
}
```

The run also records OS/runtime metadata, but it does not use those values in
normalized finding identity.

---

## 13. Profile rollout

To control configuration complexity, profiles are staged.

### Phase 2 profiles

| Profile | Meaning |
| --- | --- |
| `core` | Pure types/decisions/transforms; infrastructure forbidden. |
| `shell` | Effects allowed at explicit adapters; errors/cancellation/resources mapped. |
| `oracle` | Oracle-specific binding, streaming, option/null, and dependency rules. |

### Phase 3 profiles

| Profile | Meaning |
| --- | --- |
| `etl` | Bounded streams, memory, backpressure, and no per-row effects. |
| `cli` | Argu boundary; no `ParseResults` leakage. |
| `interop` | Reviewed null/cast/class/mutation exceptions at external boundaries. |
| `test` | Test-only failure helpers/builders without weakening production code. |
| `script` | Explicit `.fsx` references, environment, and limited release claim. |

`fs-assay doctor --explain-scopes` prints every file, selected profile, active
rules, TFM set, exclusions, suppressions, and the policy entry that caused each
selection. Ambiguous or multiply conflicting scopes fail policy validation.

---

## 14. FSharpLint delegation gate

FSharpLint is not trusted merely because a similarly named rule exists.

During Phase 0:

1. inventory the pinned FSharpLint rules and implementation status;
2. run them against `fs-assay` passing, failing, and near-miss fixtures;
3. measure duplicate, missing, false-positive, and source-range behavior;
4. verify compatibility with the pinned FCS/FSharp.Core toolchain;
5. delegate only rules that satisfy the corpus and maintenance criteria;
6. record the decision in `rules/delegation.json`.

For every delegated rule, `fs-assay` owns the stable public rule mapping and
evidence. If FSharpLint becomes incompatible, the rule is either implemented as
a custom analyzer, temporarily marked unavailable causing `Inconclusive` when
required, or removed through a reviewed catalogue version. It is never silently
skipped.

Fantomas remains the formatting authority; `fs-assay` does not duplicate its
layout engine.

---

## 15. Effect/source catalogue

Contextual rules use a versioned catalogue rather than parameter-name guesses.

The initial built-in catalogue covers only reviewed skill-relevant symbols:

- BCL `System.IO`, `System.Net.Http`, threading/task and channel primitives;
- ADO.NET abstractions needed to classify database effects;
- `Oracle.ManagedDataAccess` APIs used by the Oracle skills;
- Dapper/Donald APIs explicitly used in the skills;
- Argu and `ParseResults<_>`;
- known F# async/task/collection primitives.

Project extensions are allowed through reviewed policy. A new library is
`UnknownEffect` until classified; the result is advisory or `Inconclusive`
according to the rule, never a guessed safe/unsafe classification.

The AI under assay cannot extend this catalogue in its own unreviewed repair.

---

## 16. Revised implementation phases

### Phase 0 — Constitution, compatibility, and corpus

- Create `docs/LAWS.md` with Laws Zero through Six.
- Pin exact package/tool versions; generate the first toolchain lock.
- Add SDK surface compatibility tests for contexts, collectors, messages,
  fixes, ignore ranges, public utility functions, and safe runner behavior.
- Prove that production orchestration uses `RunAnalyzersSafely`.
- Spike MSBuild/ProjInfo project loading across a small referenced solution and a
  multi-TFM project.
- Build passing/failing/near-miss/boundary fixtures from the F# skill corpus.
- Inventory Fantomas/FSharpLint overlap and write delegation decisions.
- Define per-rule `.fsi`, typed-tree, profile, and TFM metadata.

**Gate:** no MVP rule is accepted without source traceability, a falsifier,
mechanism, signature applicability, typed requirement, and independent fixtures.

### Phase 1 — Vertical trust slice

Implement in this order:

1. `OBSOLETE_FSA` — `Option.get`/`.Value`: validates TAST and symbol identity;
2. `OBSOLETE_FSA` — null literal: validates AST collection;
3. `FSA1301` — EF Core in Oracle/core scope: validates project/profile symbols;
4. `FSA1402` — Argu `ParseResults` leakage: validates cross-file public API.

Also implement:

- `Completed / Skipped / Failed` rule outcomes;
- `Pass / Fail / Inconclusive / ToolFailure` verdicts and exit codes;
- canonical JSON and minimal SARIF;
- exact range and same-input determinism tests;
- analyzer crash, load failure, missing TAST, and project-load tests;
- CLI/upstream-runner parity tests for successful analyzer messages.

**Gate:** no analyzer exception, missing typed tree, or failed load can yield
zero-findings success.

### Phase 2 — Verification MVP

- Add remaining Tier A deterministic rules.
- Add compiler, Fantomas, conditional FSharpLint, suppression, and evidence gates.
- Add `core`, `shell`, and `oracle` profiles.
- Add all-TFM verification.
- Add generated/excluded-source auditing.
- Package `FsAssay.Analyzers` and the `fs-assay` dotnet tool.

**Gate:** one full command produces complete evidence and a reproducible verdict
for a real multi-project F# solution.

### Phase 3 — Contextual depth

- Add `etl`, `cli`, `interop`, `test`, and `script` profiles.
- Add the reviewed effect/source catalogue.
- Implement concurrency/ETL/Oracle contextual rules with policy evidence.
- Add the hybrid AST-context/TAST-symbol index.
- Add cancellation, boundedness, per-row I/O, materialization, and buffering
  fixture families.

**Gate:** every blocking contextual finding names the precise policy fact that
made it decidable; absent context produces `Skipped`/`Inconclusive`.

### Phase 4 — Fast feedback and editor

- Export `EditorAnalyzer` adapters from the shared pure rule engine.
- Map only mechanically safe remediation to SDK `Fix`.
- Verify CLI/editor agreement wherever editor context is sufficient.
- Add `fs-assay check --files` and cache invalidation tests.
- Add agent post-edit hooks and CI SARIF examples.

**Gate:** incremental/editor output is clearly provisional and never reused as
a release `Pass`; every repair loop finishes with full `verify`.

### Phase 5 — Hardening

- Benchmark cold/warm, incremental/full, single/multi-project, and multi-TFM runs.
- Test linked files, conditional compilation, `.fsi`, `.fsx`, generated sources,
  broken projects, incompatible analyzer assemblies, and path normalization.
- Fault-inject analyzer exceptions, truncated evidence writes, cancelled runs,
  and corrupt policies.
- Publish compatibility, migration, performance, rollback, and support matrices.

**Gate:** reproducible packages and evidence survive fault injection without a
false `Pass`.

---

## 17. Updated CLI contract

```text
fs-assay verify <solution-or-project>
    [--policy <fs-assay.toml>]
    [--format human|json|sarif]
    [--evidence-dir <path>]
    [--tfm <diagnostic-target>]

fs-assay check <project> --files <path>...
fs-assay rules list [--profile <name>]
fs-assay rules explain <rule-id>
fs-assay doctor
fs-assay doctor --explain-scopes
fs-assay baseline inspect <baseline>
```

No global `--strict` switch exists in v1. Certainty and disposition come from a
reviewed, versioned policy; heuristics cannot be promoted accidentally at the
command line.

| Exit | Verdict |
| --- | --- |
| `0` | Full `Pass`, or explicitly labelled provisional check success |
| `1` | `Fail` |
| `2` | `Inconclusive` |
| `3` | `ToolFailure` |
| `64` | Invalid usage |

Machine output always includes `analysis_scope` so an incremental exit `0`
cannot be mistaken for release evidence.

---

## 18. Release definition

`fs-assay` v1.0 may ship only when:

- exact SDK/FCS/FSharp.Core/tool versions are pinned and recorded from actual resolution;
- `RunAnalyzersSafely` is proven on the production path;
- analyzer load/crash/missing-context cases cannot disappear into `Pass`;
- every active rule declares certainty, disposition, mechanism, profile, TFM,
  `.fsi` applicability, typed requirement, skill source, falsifier, and fixtures;
- all required TFMs are analyzed for a full verdict;
- exclusions and inline ignores are policy-authorized and evidence-visible;
- only mechanically safe edits appear as automatic fixes;
- FSharpLint delegation is corpus-verified and has a fallback status;
- incremental and editor results are visibly non-authoritative;
- SARIF, JSON, and human output agree on normalized rule/path/range/TFM/disposition;
- the AI author cannot alter its judge in the same unreviewed change;
- a stranger can determine what was checked, skipped, suppressed, excluded,
  failed, and not proven from the evidence bundle alone.

---

## 19. Verified upstream basis

Checked locally on 2026-07-20:

| Repository | Revision/tag | Relevant verified fact |
| --- | --- | --- |
| [`arun6202/ai-skills`](https://github.com/arun6202/ai-skills/tree/main) | `18c4217` | Source F# creed, Oracle, ETL, concurrency, boundedness, domain, and CLI rules. |
| [`ionide/FSharp.Analyzers.SDK`](https://github.com/ionide/FSharp.Analyzers.SDK) | `v0.37.2`, commit `f323144` | Public AST/TAST collectors, CLI/editor contexts, optional typed tree, fixes, ignore ranges, utilities, safe/unsafe runners. |
| [`ionide/FsAutoComplete`](https://github.com/ionide/FsAutoComplete) | `8d22509` | Analyzer SDK diagnostics and fixes are mapped into LSP diagnostics/code actions. |
| [`fsharp/fsharp-compiler-docs`](https://github.com/fsharp/fsharp-compiler-docs) | `156d434` | FCS project/parse/check and syntax model documentation. |
| [`fsprojects/FSharpLint`](https://github.com/fsprojects/FSharpLint) | `4a98e8c` | Typed/untyped lint platform; actual delegation remains corpus-gated. |

### Critical verified upstream behavior

The normal SDK `Client.RunAnalyzers` documentation states that it ignores
analyzer errors, and its implementation converts exceptions into empty results.
`Client.RunAnalyzersSafely` instead returns a per-analyzer `Result`. This is the
decisive reason `fs-assay` owns its production orchestration path and treats
safe-runner use as part of the trusted computing base.

