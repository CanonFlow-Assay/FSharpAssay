# M10 — supported consumer project loading

M10 keeps M9's fail-closed authority semantics and qualifies one deliberately
small project shape: an SDK-style F# test project targeting only `net10.0`, with
an explicit source list. The fixture is loaded through Ionide/FCS and asserts
nonzero source-file evidence; it is not a claim that every F# project shape is
supported.

Project receipts now carry an explicit `supported` boolean in addition to the
status (`loaded`, `failed`, `skipped`, or `unsupported`). This distinguishes a
discovered project that is outside the locked shape from a supported project
that the workspace failed to load. Compiler-incomplete source files remain a
separate source disposition and keep authority incomplete.

## SqlHydra retest

The unchanged SqlHydra commit is `942352e4b2dae2c12f1d892d05bb5085d21ae84e`.
The legacy `src/SqlHydra.sln` discovers and workspace-loads five projects. With
no policy lock in the untouched consumer, support classification is withheld:
all five remain unsupported, with reasons preserved per project:

| Project | Target frameworks | M10 disposition | Reason |
| --- | --- | --- | --- |
| `Build/Build.fsproj` | `net10.0` | loaded, unsupported | policy unavailable; support classification withheld for class `other` |
| `SqlHydra.Cli/SqlHydra.Cli.fsproj` | `net8.0`, `net9.0`, `net10.0` | loaded, unsupported | policy unavailable; support classification withheld for class `other` and multi-target shape |
| `SqlHydra.Domain/SqlHydra.Domain.fsproj` | `netstandard2.0` | loaded, unsupported | policy unavailable; support classification withheld for class `other` and `netstandard2.0` |
| `SqlHydra.Query/SqlHydra.Query.fsproj` | `net8.0`, `net9.0`, `net10.0`, `netstandard2.0` | loaded, unsupported | policy unavailable; support classification withheld for class `other` and multi-target shape |
| `Tests/Tests.fsproj` | `net8.0`, `net9.0`, `net10.0` | loaded, unsupported | policy unavailable; support classification withheld for class `test` and multi-target shape |

This is a consumer boundary observation, not a SqlHydra defect. The receipt
records 5 discovered, 5 workspace-loaded, 0 policy-supported, 5 unsupported,
0 load failures and 33 compiler-incomplete files. It remains `Inconclusive` and
non-authoritative; all 700 findings remain unclassified and are not refactoring
instructions. Repeated JSON artifacts are byte-identical (SHA-256
`5594021e052e3d0da859a0eeee1a382366db022e309b7f64246a2425d23546ba`) and
repeated SARIF artifacts are byte-identical (SHA-256
`4498762b7be914b65bd69a6063d4afdd5e9af50732825e145169b78dc10995ab`). No
SqlHydra source or project file is changed by M10.

M10 does not add rules, change admissions, publish packages, create tags,
release, deploy Pages, or claim universal project loading. A future milestone
may qualify one additional project shape only with a new fixture and exact
unsupported/load-failure evidence.
