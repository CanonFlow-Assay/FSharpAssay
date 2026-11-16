# Clean-host consumer restore

The alias regression commit passed all 121 tests through both CI entry points, but
the packaged-consumer qualification failed at application restore on a clean host.
The qualification directory's tool-only NuGet feed was inherited by the copied
SQLite application. Developer cache contents had concealed the missing feed.

The qualification now writes an application-local NuGet.Config with the official
NuGet source before committing its consumer baseline. Tool installation still uses
the candidate archive-only feed. Existing package pins, locked restore, test counts,
rule admissions and expected authority outcomes are unchanged.

The failed run and its retained restore log are available at
https://github.com/CanonFlow-Assay/FSharpAssay/actions/runs/43857853899.
