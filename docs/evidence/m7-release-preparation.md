# M7 release-preparation inventory

M7 begins from merged main
`ccd17a1fa3fdb080f1420605b7682c740e9c2cfa`. This is a Draft-only preparation
exercise for the inherited `FsAssay.Cli` `1.0.4` identity. It publishes,
releases, tags, deploys and protects nothing.

## Read-only inventory on 2026-08-03

- `Directory.Build.props` defines `FsAssayBaselineVersion` as `1.0.4`.
- `FsAssay.Runner.fsproj` is the only packable consumer artifact and produces
  the `FsAssay.Cli` .NET 10 tool. `FsAssay.Analyzers` is bundled by project
  reference; it is not configured as an independently versioned package.
- Stable CI already performs locked restore, Release build, two 93-test entry
  points, two-root canonical package reproduction, fresh local installation,
  CLI/evidence qualification, rollback, package upload, candidate-push GitHub
  provenance attestation and immediate attestation verification.
- The candidate package is expected to fail `dotnet nuget verify --all` with
  `NU3004` because it is unsigned. GitHub provenance is not a NuGet signature.
- The target repository exposed no tags, GitHub releases, open pull requests,
  branch protection, or repository rulesets through the queried endpoints.
- The official NuGet flat-container endpoints for `fsassay.cli` and
  `fsassay.analyzers` returned HTTP `404`.
- The GitHub organization package endpoint returned HTTP `403` because the
  active credential lacks `read:packages`.

These are endpoint observations, not universal nonexistence proofs. In
particular, `404`, `403`, an empty response and an inaccessible endpoint all
leave release actions blocked. The machine inventory repeats bounded read-only
probes for each exact candidate.

Historical milestone branch heads remain in the target repository, but no tag,
release, open PR, public NuGet endpoint or release record designates any of them
as a release candidate. The exact M7 candidate is created only by the Draft PR
and its SHA-bound evidence.

## Bounded M7 implementation

The [M7 release gate](../contracts/M7-RELEASE-GATE.md) defines the fail-closed
human checklist. `eng/qualify-m7-release.sh` invokes the existing M4 package
qualification and adds only release-preparation evidence:

- exact M7 base and candidate ancestry;
- unchanged product and package identity;
- a read-only external-state observation;
- a deterministic release-preparation manifest bound to the M4 package;
- explicit `releaseEligible: false` and blocked tag, package, GitHub release,
  documentation-deployment and Human Gate D actions; and
- a no-external-action assertion over the new scripts and workflow.

The dedicated Draft workflow may attest the candidate package on branch push
and verify that attestation. It does not have a tag trigger, manual release
dispatch, package push, release creation or deployment action. Its artifact is
review evidence, not publication.

## Evidence interpretation

A green M7 workflow means the exact candidate reproduced the existing local
package and consumer evidence and kept every external action fail-closed. It
does not mean `1.0.4` is released, signed, public, generally supported, or ready
for unattended organization-wide adoption.

The FsAssay self-analysis can remain `Inconclusive` and non-authoritative because
the shipped CLI cannot ingest the stable test result. Release preparation relies
on compiler, stable tests, deterministic packaging, consumer execution,
provenance, independent testing and Human Gate D; it does not relabel missing
FsAssay evidence as `Pass`.

## Required next review

Independent testing must reproduce the exact Draft candidate without repairing
it. The judge may advise only after deterministic evidence and the tester report
exist. Human Gate D then decides whether to authorize protection and tag
preparation as a separate action. This M7 PR must remain Draft and unmerged until
that review is complete.

