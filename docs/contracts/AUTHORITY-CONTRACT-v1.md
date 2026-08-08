# FsAssay Authority Contract v1

Status: **M3 candidate under human review**. This contract describes authority
behavior at tool identity `1.0.4` and Shape identity `fsharp-shape/1.0.0`; it
does not publish a release, admit a blocking rule, add a command family, or
claim correctness.

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
outcome an authority gap. Gate C remains pending; this candidate configures zero
blocking and zero advisory rules. All 93 catalogue identities are proposed as
35 experimental, 36 prototype and 22 dummy rules; implementation status is not
admission.

## Locked inputs

`fsassay-policy.lock.json` binds the policy schema, receipt schema, authority
contract, Shape contract, deterministic evaluation date, tool identity,
profiles, seven disjoint rule maturity classes, required project
classes and target frameworks, tests, baseline identity, configured baseline
records, and reviewed exceptions. Unknown fields and unsupported versions are
diagnosed.

M3 establishes typed baseline governance, while the current zero-blocker policy
has no baseline debt and therefore uses identity `none`. Future reviewed records
bind rule ID, fingerprint, repository-relative path, symbol, owner, rationale,
disposition, dates and policy version. Exact active `accepted` records are listed
in `appliedBaselineRecords`; unmatched, expired and `resolved` records are not.
`appliedSuppressions` remains empty. Baselines cannot hide missing authority
evidence. The explicit policy date, not the wall clock, controls expiry.

This M3 candidate proposes zero blocking admissions while Gate C is pending.
The inherited catalogue and its 21
legacy admission entries are not changed, but they are non-authoritative
historical observations under this contract. A policy requesting any blocking
rule is diagnosed as incomplete instead of silently inheriting that legacy set.

## Deterministic receipt

`--out-json` emits `fsassay-authority-receipt/1.1.0`. It records:

- tool, schema, complete canonical policy snapshot and its SHA-256,
  SDK/runtime/FSharp.Compiler.Service identity;
- analyzed commit, approved PR head, tree, dirty-worktree, synthetic-merge,
  package, and repository-relative target identity as distinct fields;
- projects discovered, with an explicit supported flag, loaded/failed/skipped/unsupported disposition, project class and target frameworks;
- source disposition, including generated and policy-excluded sources;
- required tests and their exact `passed`, `failed`, `skipped`, or `notRun` evidence;
- rule evidence availability, findings, source symbols, stable fingerprints and all seven maturity classes;
- `unclassified` rule and finding classes when the policy lock is absent or invalid.
  This is explicit non-authoritative evidence and is never equivalent to `removed`;
- typed baseline configuration, actually applied baseline IDs, empty applied-suppression evidence, bounded framework exceptions,
  itemized policy/evidence errors, missing evidence, tool failures, outcome and every reason.

Arrays use stable ordering and source paths are repository-relative. SARIF uses
the same normalized findings and receipt identity. Equivalent evidence in two
different checkout roots must serialize to byte-identical JSON and SARIF.

The strict public validator validates the complete embedded policy snapshot,
canonicalizes it with the producer's policy function, and requires its computed
SHA-256 to match `policy.sha256` before reconstructing requirements and itemized
facts. It then calls the same total reducer used by the producer and requires an
exact match for outcome, authority and the complete sorted reason set. Removing
tests, project classes, target frameworks, rule classes, baseline records or
exceptions while retaining the policy hash is rejected. Changing only
`outcome`, `authoritative`, or reasons cannot turn `notRun`, unsupported,
missing, failed, blocking or tool-failure evidence into another result. `Pass`
is always authoritative; a complete conclusive `Fail` is authoritative, while a
`Fail` with concurrent incompleteness is not.

This SHA-256 is an internal semantic consistency identity, not a signature and
not proof of origin. An actor able to replace both snapshot and hash can create
a different internally consistent receipt. Consumers that know the reviewed
policy identity must use the validator overload that accepts an expected policy
SHA-256, and CI/human review must independently pin the candidate identity and
policy SHA. Gate B consumers should use the context validator to pin that policy
SHA together with the analyzed commit and tree; synthetic-merge receipts also
require the reviewed head and synthetic merge identities, while package receipts
require the package SHA-256. Signing and provenance authenticity remain outside M3.

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
excluded. CI supplies the reviewed head as `FSASSAY_APPROVED_HEAD_SHA`. For a
pull request, the runtime `GITHUB_SHA` identifies the synthesized commit that
was actually checked out; the potentially stale event `merge_commit_sha` field
is not trusted. An explicit `FSASSAY_SYNTHETIC_MERGE_SHA`, when used outside the
repository workflow, must still equal actual HEAD. These environment identities
are recorded separately and validated against actual HEAD; they never replace it.
Dirty tracked or untracked input, a non-Git target, or an unavailable commit,
tree, or reviewed head is incomplete candidate evidence: it forces
`Inconclusive` and `authoritative: false` unless a higher-precedence conclusive
failure exists. The reviewed pull-request head must be the synthetic merge's
second parent. A malformed identity, contradictory candidate kind, approved-head
mismatch, or synthetic-merge mismatch makes the evidence invalid and forces
`ToolFailure`.

## Compatibility and limitations

The pre-M2 `--out-json` payload was an array of file findings. M2 replaced it
with the versioned receipt object. M3 intentionally bumps the policy and receipt
schemas from `1.0.0` to `1.1.0`; M2 payloads fail closed and require explicit
migration. Consumers must validate `schemaVersion` before reading. `--out-sarif` remains SARIF 2.1.0 and uses relative URIs,
stable fingerprints, and run properties for outcome/policy/candidate identity.

M3 does not ingest test evidence. CI runs 92 tests, but the CLI does not infer
that ambient success and records policy-required tests as `notRun`. The full
self-audit consequently remains `Inconclusive`/non-authoritative. A reviewed
consumer evidence-ingestion surface belongs to a later milestone.

The full audit includes frozen Desktop and TypeGym projects. Their unsupported
status is explicit and prevents authority. The 563 current observations require
human adjudication and are not proof of product success. Advisory/prototype
findings must not be automatically refactored by humans or agents.

M3 project classes are conservative filename-derived categories and fail closed
to `unsupported` when unknown. Target-framework discovery reads the bounded
SDK-style project XML; conditional properties and a general multi-target
compatibility matrix are not qualified. No broader MSBuild compatibility is
claimed.
