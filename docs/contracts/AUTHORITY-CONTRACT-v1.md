# FsAssay Authority Contract v1

Status: **draft for Human Gate B**. This contract describes M2 behavior at tool
identity `1.0.4`; it does not publish a release, Shape contract, rule approval,
new command family, or correctness guarantee.

## Outcomes and precedence

The receipt has exactly four top-level outcomes:

| Precedence | Outcome | Required condition |
|---:|---|---|
| 1 | `ToolFailure` | FsAssay cannot produce trustworthy valid evidence, including an internal crash or contradictory receipt facts. |
| 2 | `Fail` | A completed required test failed, or a future Human Gate C approved blocking rule produced a finding. |
| 3 | `Inconclusive` | Required evidence is absent, `notRun`, unloaded, compiler/workspace-incomplete, unexpectedly zero, skipped, unsupported, or diagnosed policy input is invalid. |
| 4 | `Pass` | Every policy requirement has complete, credible, nonzero evidence and succeeds. |

All applicable reasons are retained even when a higher-precedence outcome wins.
A known required failure remains `Fail` when separate evidence is incomplete,
but `authoritative` is `false`. Gate B deliberately rejects the alternative
`ToolFailure > Inconclusive > Fail > Pass`, because it would hide a conclusive
required failure behind an unrelated evidence gap.

`authoritative` is true only when the receipt itself is valid and no
incompleteness reason exists. Zero findings, an unloaded workspace, a dummy or
prototype rule, or a missing test can never create authority.

The policy distinguishes observationally requested rules from
authority-required rules. `completed`, `incomplete`, and `unavailable` report
what execution evidence exists. Advisory/experimental status never helps create
`Pass`, never blocks it, and never adds an authority reason. Only a separately
Gate-C-approved blocking rule could make its missing/incomplete/unavailable
outcome an authority gap. M2 has no such rule.

## Locked inputs

`fsassay-policy.lock.json` binds the policy schema, receipt schema, authority
contract, explicitly absent Shape contract, tool identity, profiles, Gate-C
approved blocking rules, advisory/experimental observations, required project
classes and target frameworks, tests, baseline identity, suppressions, and
reviewed exceptions. Unknown fields and unsupported versions are diagnosed.

M2 has no Gate-C-approved blocking rules. The inherited catalogue and its 21
legacy admission entries are not changed, but they are non-authoritative
historical observations under this contract. A policy requesting any blocking
rule is diagnosed as incomplete instead of silently inheriting that legacy set.

## Deterministic receipt

`--out-json` emits `fsassay-authority-receipt/1.0.0`. It records:

- tool, schema, policy hash, SDK/runtime/FSharp.Compiler.Service identity;
- analyzed commit, approved PR head, tree, dirty-worktree, synthetic-merge,
  package, and repository-relative target identity as distinct fields;
- projects discovered, loaded, failed, skipped and unsupported, with project class and target frameworks;
- source disposition, including generated and policy-excluded sources;
- required tests and their exact `passed`, `failed`, `skipped`, or `notRun` evidence;
- rule evidence availability, findings, stable fingerprints, authority class;
- baseline, suppressions, exceptions, missing evidence, tool failures, outcome and every reason.

Arrays use stable ordering and source paths are repository-relative. SARIF uses
the same normalized findings and receipt identity. Equivalent evidence in two
different checkout roots must serialize to byte-identical JSON and SARIF.

Every non-`Pass` reason is also a SARIF `toolExecutionNotification`. SARIF run
properties repeat the receipt outcome, authority flag, exact counts, finding
count, policy identity and candidate identities. `executionSuccessful` is false
only for `ToolFailure`; a `Fail` is a successfully produced conclusive result.
Notification count/reason identity and finding count must reconcile with JSON.
SARIF `executionSuccessful: true` on `Inconclusive` means the FsAssay process
successfully produced evidence; it does not mean the receipt is authoritative.

Candidate identity is runtime evidence. No committed artifact embeds its own
future commit SHA. A branch commit, tree, dirty worktree, GitHub synthetic merge,
and package payload are not interchangeable identities.

Actual HEAD and tree always come from Git. Dirty detection includes every tracked
change plus untracked authority inputs: F# source/signature/script, project and
solution files, MSBuild props/targets, `global.json`, NuGet configuration,
package locks, and FsAssay policy/configuration JSON. Generated non-input paths
such as `artifacts/`, build outputs, and the workspace-local `.dotnet/` SDK are
excluded. CI supplies the
reviewed head as `FSASSAY_APPROVED_HEAD_SHA` and, for pull requests, the merge
object as `FSASSAY_SYNTHETIC_MERGE_SHA`. These environment identities are
recorded separately and validated against actual HEAD; they never replace it.
Dirty tracked or untracked input, a non-Git target, or an unavailable commit,
tree, or reviewed head is incomplete candidate evidence: it forces
`Inconclusive` and `authoritative: false` unless a higher-precedence conclusive
failure exists. The reviewed pull-request head must be the synthetic merge's
second parent. A malformed identity, contradictory candidate kind, approved-head
mismatch, or synthetic-merge mismatch makes the evidence invalid and forces
`ToolFailure`.

## Compatibility and limitations

The pre-M2 `--out-json` payload was an array of file findings. M2 replaces it
with the versioned receipt object; consumers must validate `schemaVersion`
before reading it. `--out-sarif` remains SARIF 2.1.0 but now uses relative URIs,
stable fingerprints, and run properties for outcome/policy/candidate identity.

M2 does not ingest test evidence. CI may run 83 tests, but the CLI does not infer
that ambient success and records policy-required tests as `notRun`. The full
self-audit consequently remains `Inconclusive`/non-authoritative. A reviewed
consumer evidence-ingestion surface belongs to a later milestone.

The full audit includes frozen Desktop and TypeGym projects. Their unsupported
status is explicit and prevents authority. The 545 current observations require
human adjudication and are not proof of product success. Advisory/prototype
findings must not be automatically refactored by humans or agents.

M2 project classes are conservative filename-derived categories and fail closed
to `unsupported` when unknown. Target-framework discovery reads the bounded
SDK-style project XML; conditional properties and a general multi-target
compatibility matrix are not qualified. No broader MSBuild compatibility is
claimed.
