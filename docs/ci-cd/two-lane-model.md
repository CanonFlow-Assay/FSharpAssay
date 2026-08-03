# FsAssay two-lane CI model

FsAssay evidence has two distinct consumers. Mixing them creates either noisy
merge gates or false confidence.

## Observation lane

The observation lane records experimental, prototype, dummy and advisory
findings for human review. Its count is not a quality score, and a finding is
not an instruction to refactor. Its upload step always runs and retains any
JSON/SARIF that was produced, but the lane never blocks a merge. Missing output
is tool-failure evidence, not a zero-finding or clean result.

## Authority lane

The authority lane treats the JSON receipt as the only merge decision input.
The analysis exit code is captured as evidence; it is not translated into a
pass or failure by workflow shell logic. `eng/fsassay-authority-gate.sh` permits
a merge only when a supported, structurally complete receipt says both
`outcome == "Pass"` and `authoritative == true` and its candidate, policy,
project, source, test, rule and finding evidence satisfies the gate invariants.

The gate blocks valid `Fail`, `Inconclusive` and `ToolFailure` receipts. It also
rejects missing, empty, invalid, unsupported or contradictory receipts as
invalid evidence. Zero findings without complete evidence is never a pass.
A conclusive `Fail` may legitimately be authoritative or non-authoritative; it
is blocked in either case. `Inconclusive` and `ToolFailure` cannot be
authoritative.

Gate exits are:

- `0`: complete authoritative Pass; merge may proceed.
- `1`: valid non-Pass receipt; merge is blocked.
- `2`: missing, malformed, unsupported, contradictory or incomplete evidence.
- `64`: invalid gate invocation.

The gate writes the same three-line decision summary to stdout and, when set,
to an explicit second path, `FSASSAY_AUTHORITY_SUMMARY`, or
`GITHUB_STEP_SUMMARY` in that priority order. For the bounded Inconclusive
example the output is exactly:

```text
Authority: INCONCLUSIVE — merge blocked
Reasons: 2 unsupported projects, required test notRun
Observations: 563 experimental — informational
```

## Using the inactive example

`.github/examples/fsassay-two-lane.yml` is copyable documentation, not an active
workflow. Copy it under `.github/workflows/` only after reviewing paths,
required checks and policy for the consuming repository.

`FsAssay.Cli` `1.0.4` remains unpublished. The example therefore does not claim
that a public NuGet install works. Its prerequisite is a tracked repository-local
tool manifest pinning `1.0.4` and a reviewed NuGet configuration pointing only
at a local or otherwise qualified feed containing the exact reviewed package.
The feed configuration and package provenance are consumer-owned inputs.
The gate validates receipt structure and internal consistency; it does not
cryptographically authenticate the producing tool, policy or candidate. A
pinned tool manifest, reviewed policy, exact package hash and verified build
provenance remain prerequisite supply-chain controls.

The example uses the existing `dotnet tool run fsassay` analysis surface. It
does not add `check`, `verify`, a profile, a rule, or a new authority protocol.
Authority remains deterministic receipt evidence; observation remains
informational human-review material.
