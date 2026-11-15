# SQLite dependency remediation

The offline scan of the refresh application's actual 17 declared source/configuration/lock inputs found two High-severity observations of `GHSA-2m69-gcr7-jv3q`, one per affected Shell/Test lock. Both identified `SQLitePCLRaw.lib.e_sqlite3 2.1.11`. The original Fail receipt is retained under ignored `artifacts/refresh/security-application-010e0eb/`.

The [reviewed advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) identifies affected native packages through 2.1.11 and an SQLite remediation boundary of 3.50.2. The sample now pins [Microsoft.Data.Sqlite 10.0.15](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.15) and [SQLitePCLRaw.bundle_e_sqlite3 2.1.14](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.14), with refreshed format-1 locks. This retains the .NET 10 application target and changes no scanner threshold, database pin, required test or assertion to conceal the finding.

All 13 application tests pass. The existing persistence test also queries the actually loaded SQLite engine and requires version 3.50.2 or later. A fresh offline scan of the revised source/lock bytes produced Pass with zero findings, missing evidence or tool failures under the same approved scanner/configuration/database pins and explicit evaluation time. That result is declared-input M0 evidence, not a complete application-security claim.

The final qualification index identifies the clean code candidate and fresh package-consumer replay. Earlier failing and passing attempts retain their original identities.
