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

The next clean-host run reached analysis and exposed a second harness boundary:
the independently committed consumer inherited the producer workflow's GITHUB_SHA.
The CLI correctly refused the inconsistent identity, while receipt serialization
raised an error. Consumer CLI invocations now temporarily omit only the outer CI
identity variables and restore them afterward. The consumer's actual Git HEAD and
tree still establish its identity. Production candidate validation is unchanged;
this is not permission to override a mismatched identity on the analyzed checkout.

The retained failed run is
https://github.com/CanonFlow-Assay/FSharpAssay/actions/runs/43859362178.
