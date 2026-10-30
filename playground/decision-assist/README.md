# F# Decision Assist playground

Run a requirements-driven investigation over the **actual M5 F# domain source**, using native discriminated unions, private validated values, Result and decimal pricing. This is a separate extension of the existing M5 playground, inspired by CSAssay's distinction between deterministic evidence and optional advice. FSharpAssay keeps its own exact **.NET SDK 10.0.301** and pending Human Gate C; this work does not migrate it to the C# toolchain or admit any rule.

From the repository root:

```sh
dotnet fsi --warnaserror+ --exec playground/decision-assist/Decision.Tests.fsx
dotnet fsi --warnaserror+ --exec playground/decision-assist/Run.fsx
```

No restore, new package, model key or provider call is needed. The installed SDK supplies F# Interactive. The runner prints a fresh directory under ignored `artifacts/astra-fsharp/`. To choose it explicitly, append `--output /absolute/path/to/a-new-directory`; existing paths are rejected so previous evidence survives.

## What actually runs

The runner copies M5 `Order.fs` and `Pricing.fs` into isolated case directories and starts a real F# process for each test selection. It never rewrites the original M5 source. `requirements.json` registers the named quantity, order and pricing acceptance checks. The before selection deliberately has six assertions; the expanded selection preserves those assertions and adds six boundary checks. These are new comparison selections, **not** the historical 15-test M5 suite or a measured coverage report.

| Case | Before selection | Expanded selection | Advice after expansion |
| --- | --- | --- | --- |
| Accept zero quantity | Survives | Killed | NoInvestigation |
| Permit an order exactly at its limit | Survives | Killed | NoInvestigation |
| Exclude exactly 100 from preferred discount | Survives | Killed | NoInvestigation |
| Ignore missing customer | Killed | Killed | NoInvestigation |
| Replace ten-percent multiplication with division | Survives | Survives | SurvivorReview |
| Malformed quantity constructor | Invalid compilation | Invalid compilation | RepairCandidate |

The original source must pass both selections before the comparison can complete. A separate deliberate original-test failure must route to RepairBaseline, with no candidate execution. A malformed candidate cannot count as a killed mutation. Survival never establishes equivalence. The fixed corpus requires each replacement to match exactly once, preserves raw process logs and checks the named test identities, counts and process exit together.

`Decision.fs` contains the pure advice reducer. Its native union can request an eligible registered boundary investigation, ask for context, repair baseline/candidate evidence, stop for exhausted budget, retain survivor review or say no investigation is needed. It has no verification-verdict constructor and performs no effects. The 17 routing controls include missing/empty evidence, invalid compilation, failed baselines, stale source and exhausted budgets.

Advice is informational. The runner executes the explicitly declared before/expanded comparison matrix, independently of advice; it does not execute model-selected commands or expose a general probe executor. Each of the three boundary probes is registered to its requirement. No model is consulted.

## Inspect retained evidence

Use the printed output directory:

```sh
dotnet fsi --warnaserror+ --exec playground/decision-assist/Inspect.fsx artifacts/astra-fsharp/EXAMPLE/investigation.json
```

Inspection returns 0 only for a completed comparison whose source bindings, receipt checksum and retained artifact hashes still match. Missing, modified or stale evidence returns 2 and `NotAvailable`. `--source-root /absolute/path/to/checkout` can check a relocated checkout against the same repository-relative bindings. It checks local integrity; it is not a signature or independent attestation. Editing any bound driver, requirement, case, policy or source makes an earlier report unavailable for the new snapshot.

Read `investigation.json`, `source-bindings.json`, and the individual baseline/case reports and compiler logs. The aggregate `complete` means the declared comparison completed with the expected controls. It is **not** a verification Pass. The receipt explicitly records `fsassayEvaluated: false`, `verificationVerdict: NotEvaluated` and `authoritative: false`. FsAssay 1.0.4 still cannot ingest separately executed tests, and an M5 analyzer receipt remains Inconclusive under its current policy. The existing `eng/qualify-m5-playground.sh` remains the separate packaged consumer/analyzer qualification.

This bounded sample does not prove general mutation adequacy, purity, correctness, coverage, persistence, concurrency or whole-application convergence. Rule admission, frozen Web/Desktop/plugin surfaces, prior receipts and the M5 expected findings are unchanged. Later phases can add deliberately reviewed evidence ingestion and an application coordinator; neither is implied by these results.
