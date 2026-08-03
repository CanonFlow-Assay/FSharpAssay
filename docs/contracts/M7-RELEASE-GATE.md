# M7 release gate for FsAssay 1.0.4

This contract governs preparation for a possible first verified FsAssay release.
It does not authorize a tag, package publication, GitHub release, documentation
deployment, or change to repository protection. Human Gate D is mandatory for
every external release action.

The release identity remains `FsAssay.Cli` `1.0.4`. M7 does not create a
separate analyzer package and does not change analyzer rules, CLI behavior,
profiles, policy semantics, evidence schemas, or package payload behavior.

## Fail-closed release decision

Release preparation and release authorization are different decisions. The M7
qualifier may establish that a candidate has reproducible local evidence while
still returning `releaseEligible: false`. Missing, inaccessible, stale, or
ambiguous external state never authorizes the next action.

The default and only M7 automation state is:

| Action gate | M7 state | Authority needed to proceed |
|---|---|---|
| Create or move a protected `v1.0.4` tag | Blocked | Separate Human Gate D approval after active tag protection is independently verified. |
| Publish `FsAssay.Cli` `1.0.4` | Blocked | Separate Human Gate D approval bound to the protected tag and exact candidate. |
| Create a GitHub release | Blocked | Separate Human Gate D approval after public package verification. |
| Deploy public documentation | Blocked | Separate Human Gate D approval and a separately reviewed deployment change. |

An HTTP `404`, permission failure, empty endpoint, or unavailable endpoint is an
observation about that request only. It is not proof that an artifact or control
does not exist elsewhere. Every such observation leaves its action gate blocked.

## Candidate gate

A candidate is reviewable only when all of the following are recorded:

1. the exact 40-character commit SHA and its ancestry from merged M6 main
   `ccd17a1fa3fdb080f1420605b7682c740e9c2cfa`;
2. a clean tracked worktree at that SHA;
3. unchanged `1.0.4` package identity and no product-source, rule, CLI, profile,
   policy, schema, or package-project change from the M7 base;
4. locked restore, Release build, and both stable test entry points with exactly
   93 passed and zero failed;
5. the M4 package manifest bound to the same candidate SHA; and
6. an M7 manifest that explicitly denies authorization for external actions.

The candidate SHA belongs in generated evidence and the Draft PR review record.
It cannot be embedded in its own tracked commit without creating circular
provenance.

## Package and consumer gate

M7 reuses the M4 qualification instead of inventing a second packaging model.
The candidate must retain:

- two independent clones and independent empty package caches;
- byte-identical canonical packages after the reviewed OPC normalization;
- exact nuspec repository commit, README, Apache-2.0 license, repository URL,
  and runner/analyzer SourceLink;
- an isolated local feed and repository-local tool manifest;
- `help`, `--help`, `-h`, `doctor`, `explain`, invalid-invocation, analysis, and
  zero-evidence behavior;
- repeated byte-identical JSON and SARIF for the qualified specimen;
- network-namespace isolation for analysis; and
- uninstall rollback restoring the original tool manifest and removing the
  local tool store.

The canonical package SHA-256, size, evidence hashes, candidate SHA, and M4
manifest SHA-256 are copied into the M7 release-preparation manifest.

## Signature and provenance gate

`dotnet nuget verify --all` currently returns `NU3004`: the local candidate is
not NuGet author- or repository-signed. That is a disclosed limitation, not a
successful signature check and not proof that publication would add a
repository signature.

On an exact candidate branch push, GitHub Actions may create and immediately
verify a build-provenance attestation for the canonical package. This
attestation is an evidence artifact only. It is not a package signature, tag,
publication, release, approval, or deployment. Pull-request runs cannot be used
as substitute attestation evidence.

Before Human Gate D can authorize publication, a reviewer must reconcile the
attested subject digest, the uploaded workflow artifact, and the deterministic
M7 manifest at the exact candidate SHA.

## External-state gate

The M7 inventory records read-only observations for:

- repository rulesets;
- the proposed Git tag endpoint;
- the GitHub release endpoint;
- repository documentation-hosting configuration;
- GitHub organization NuGet packages; and
- the official NuGet flat-container package endpoint.

The inventory records HTTP status and access interpretation without converting
absence or lack of permission into stronger claims. M7 compares the observation
before and after candidate-push attestation. A mismatch blocks the workflow; a
match only proves the recorded observations were stable during that run.

## Human Gate D checklist

Human Gate D must review the following before authorizing any external action:

- [ ] Exact candidate SHA, diff, ancestry, and all required checks are unchanged.
- [ ] Independent testing has reproduced package, install, CLI, evidence, and
      rollback behavior from a fresh checkout.
- [ ] The LLM judge report is available and remains advisory.
- [ ] The canonical package and both independent-root hashes are reconciled.
- [ ] GitHub build-provenance verification identifies the expected repository,
      workflow, candidate, and package digest.
- [ ] Unsigned local-package status and the intended public repository-signature
      behavior have been explicitly reviewed.
- [ ] An active tag ruleset preventing update and deletion of `v*` has been
      independently verified, including its bypass actors.
- [ ] The exact protected tag target has separate approval.
- [ ] Package publication has separate approval and uses only the protected tag.
- [ ] GitHub release creation has separate approval after package verification.
- [ ] Public documentation deployment, if desired, has separate approval.

Unchecked, unavailable, or inconclusive items block the corresponding action.

## Post-publication verification (not performed by M7 preparation)

If publication is later approved and completes, a fresh consumer must verify the
official package endpoint, exact version, downloaded payload hash, repository
signature status, metadata and provenance. It must install into a new local tool
manifest and run `doctor`, all help aliases, representative `explain`, analysis,
and rollback. The downloaded payload must be reconciled with the approved
workflow artifact. Only then may a public GitHub release or documentation claim
refer to `1.0.4` as released.

