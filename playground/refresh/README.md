# FSharpAssay packaged application verification

This independent .NET 10 F# consumer demonstrates pure approval decisions, SQLite transactions, idempotent response replay and a bounded loopback HTTP exchange. Thirteen executable tests cover exact thresholds, validation, architecture, persistence, cancellation and wire contracts. Package versions and NuGet format-1 locks are explicit so the same declared locks can be assessed by SecurityAssay M0 without transforming their bytes.

Run `bash eng/qualify-refresh.sh <fresh-output>` from the repository with the exact SDK. The qualifier packs and installs the CLI from a local-only feed, copies this application into its own Git repository, restores its committed locks and executes check/verify/repair workflows through the installed tool. There is no source project reference to the analyzer or runner.

Qualification retains threshold Fail and repair, skipped required execution and repair, a wire-name regression and repair, an invalid mutation, and an unresolved high-amount mutation that survives the selected tests. A test Pass is bounded evidence, so the survivor remains a review item rather than an equivalence claim. The existing six-case Decision Assist corpus supplies the failed-baseline and expanded-selection controls.

The sample is a qualification fixture, not a deployed application. HTTP serves one loopback request; SQLite tests exercise local transaction/replay behavior. It does not establish distributed concurrency safety, production authentication or operational readiness.

SQLite transaction behavior follows the [Microsoft.Data.Sqlite transaction API](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions). HTTP uses the [HttpListener API](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-net-httplistener).

The initial dependency scan rejected SQLitePCLRaw.lib.e_sqlite3 2.1.11 for [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q). The sample now pins Microsoft.Data.Sqlite 10.0.15 and SQLitePCLRaw.bundle_e_sqlite3 2.1.14. Its persistence test queries the actually loaded SQLite engine and requires at least 3.50.2, the advisory remediation boundary. The failed scan remains retained separately from fresh scans of the revised lock bytes.
