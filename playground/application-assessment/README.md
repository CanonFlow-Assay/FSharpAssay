# Native F# application assessment preview

This separate API and coordinator join **real FsAssay receipts, compiled application tests and baseline requirements** without changing FsAssay authority. It uses the repository's exact .NET SDK 10.0.301. The existing M3/Gate C rule admission and frozen Web/Desktop/plugin surfaces are untouched.

An application can become `ReadyForReview` in this preview while its unchanged FsAssay receipt remains **Inconclusive, authoritative=false** because FsAssay 1.0.4 does not ingest separately executed tests. That explicit limitation stays visible in every envelope. ReadyForReview is not release approval, FsAssay Pass, certification or proof of correctness.

## Run the full offline consumer lifecycle

Use a clean committed candidate: FsAssay retains dirty-worktree evidence as incomplete, and the coordinator does not waive it. Build the unchanged engine once if its Release binaries are absent:

```sh
dotnet restore FsAssay.Runner/FsAssay.Runner.fsproj --locked-mode
dotnet build FsAssay.Runner/FsAssay.Runner.fsproj --no-restore -c Release
dotnet fsi --warnaserror+ --exec playground/application-assessment/Qualify.fsx artifacts/astra-fsharp-phase2/my-fresh-run
```

The output directory must be new. Toolkit restore may require the existing FSharp.Core cache; the coordinator has no live-provider or source-upload path. The application restore uses an isolated local feed containing the newly packed `FsAssay.Application 0.2.0-preview.1` and cached `FSharp.Core 10.1.201`. No package is published. The consumer has PackageReference only, no source project reference to the toolkit or engine. Qualification compares the actual loaded SDK DLL with the packed producer's DLL.

Rebuilding a development package changes its content hash. Qualification **explicitly bootstraps only a disposable copied consumer lock**, then runs locked restore and binds that prepared lock to all subsequent evidence. The committed sample lock is retained as a producer-template input; it is not silently relabeled as the rebuilt package's identity. No owner worktree lock is rewritten.

The native qualifier compiles the API/CLI, runs pure contract tests, packs locally, and executes an approval application's baseline/change lifecycle:

1. Baseline: positive amount and approval-limit requirements, five named tests.
2. Red change: register a blank-reference requirement and its sixth test; retain the real failure and registered offline advice.
3. Green change: reject blank references in the domain, preserving all old tests, minima, requirements and the contract pin; six tests pass.
4. Negative controls: changed contract, reduced selections/minima, missing named evidence, failed baseline, missing/undeclared files, duplicate JSON, ambient scan config, unbound build imports, real compiler failure, stale source/runtime, modified reports, missing compiler logs and forged readiness/engine projections.

Read `qualification.json`, baseline/red/green `assessment.json`, their archived raw `fsassay.json`, named `tests.json`, compiler/test/engine execution records and unmodified stdout/stderr logs. The deliberately failing red result is evidence, not a passing application. A running or incomplete qualifier is never complete.

## API and wire contract

`Assay.Application/Domain.fs` contains typed serialization records and native decision unions for requirements, named tests, assessment status and advice. `Assessment.evaluate` reduces typed observed facts. `Assessment.compatible` preserves application identity, the exact contract hash, old requirement descriptions/IDs, old named test selections and minima. A new requirement is additive; an intentional old-contract migration needs separate review.

`Assistance.select` can select only a registered probe for the observed failed requirement and matching source snapshot, with positive caller budget and a review-ready baseline. Incomplete evidence, a stale snapshot or a failed baseline prevents selection. This API performs no effects. Advice never constructs a verification verdict, changes code, waives tests or executes a command. The coordinator emits a deterministic registered next-investigation label; a persistent probe executor/model gateway is not implemented.

`Workflow.assess` owns files, compiler/tests and the actual engine process. It emits `fsharp-application-assessment/0.2` with application status, unchanged engine outcome/authority, original engine receipt digest, exact named evidence, copied source snapshot, requirements, manifest, contract and producer/runtime/compiler/artifact bindings. Application statuses are `ReadyForReview`, `NeedsRepair`, `ContextRequired` and `ToolFailure`; none is an engine verdict grant.

Manifest schema: `fsharp-application/0.2` in `assay.app.json`. Requirements schema: `fsharp-application-requirements/0.1`. Test report schema: `fsharp-application-tests/0.1`. See `sample` for concrete versioned examples. Every required named test must be observed exactly once and bound to the declared requirement; any failed test, missing name, below-minimum count or exit/report disagreement prevents readiness. Duplicate/unknown JSON fields and undeclared authored inputs are rejected.

The coordinator supports the bounded sample's standalone .NET 10 executable project with an explicit file registry and self-owned build/package configuration. External project references, custom build targets/imports, symlinked scope and evidence directories nested inside input scope are unsupported. It executes the registered project's trusted build/test code; this is not a sandbox for hostile MSBuild projects or malicious test producers.

## Direct commands and retained evidence

After qualification prepares the package consumer:

```sh
dotnet playground/application-assessment/Assay.Application.Cli/bin/Release/net10.0/Assay.Application.Cli.dll assess /absolute/app/assay.app.json /absolute/FsAssay.Runner.dll /absolute/new-output --baseline /absolute/baseline/assessment.json --failure /absolute/red/assessment.json
dotnet playground/application-assessment/Assay.Application.Cli/bin/Release/net10.0/Assay.Application.Cli.dll inspect /absolute/run/assessment.json --source-root /absolute/app
```

Assessment exits: 0 review-ready, 1 application repair needed, 2 missing/unsupported/tool/context evidence, 64 invalid CLI shape. Inspection exits 0 when the retained envelope/artifacts are available and consistent, or 2 when unavailable. Inspection availability does not turn a retained NeedsRepair assessment into readiness.

A baseline's immutable archived source/manifest/requirements/contract/raw evidence is retained with the candidate. Historical baseline source need not equal changed candidate source. Without `--source-root`, inspection checks historical artifact integrity; with it, the current application source and runtime bytes must also match. Compiler, engine and toolkit producer bytes must still match in both modes. Changed/missing reports, tampered snapshots, altered execution logs and inconsistent ReadyForReview/engine projections are rejected, including a forged ready label with a recomputed local checksum.

Bindings provide local integrity and freshness, not signatures, independent attestations or proof against a malicious party who replaces all trusted producer/source/contract evidence. No universal purity, compatibility, coverage, concurrency, persistence or application correctness is claimed. Existing analyzer verdicts, rule classifications, receipt formats and prior qualification records remain unchanged.

## Preview0.2 safety contract

The new manifest explicitly registers frozenTestFiles and acceptanceTestFiles. The sample freezes OriginalTests.fs, Program.fs and its project file; AdditionalTests.fs holds new cases. Old test implementation bytes and build/lock inputs are pinned to the baseline. A repaired additive requirement requires --failure <retained-red-assessment> alongside --baseline. The retained red must fail a newly registered requirement while the old selection still passes, and additional test-source bytes must match red to green. No automatic legacy-manifest or receipt migration occurs.

Inspection now reconstructs every state, count, reason and advice from bound raw observations. It requires complete artifact/log inventories, exact current engine/SDK/toolkit producer maps and expected copied runtime files, records pre/post maps, binds original execution vectors and context, and verifies raw FsAssay source/project/cardinality/policy/error/test-selection/toolchain/candidate consistency against captured input. Empty/deleted observation maps cannot erase freshness or failures. An output path or ancestor symlink is rejected before creating evidence. These are local consistency checks; no code is loaded or executed from an untrusted receipt to validate it.

The original0.1/15993b4 package, source and qualification are preserved as historical proof; they are not relabeled as this stricter contract.
