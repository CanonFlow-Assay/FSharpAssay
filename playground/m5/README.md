# M5 evidence Playground

This is a bounded consumer exercise, not a product feature or a marketing proof. It packages the CLI from the exact candidate commit, installs that package into an isolated tool path, and analyzes a small F# `Domain -> Application -> Shell` workflow without project references to FsAssay.

The Shape New slice validates `OrderId` and `Quantity`, makes order rejection a closed union, keeps the decision pure, owns ports in Application, and maps transport input/output in Shell. The Shape Converge slice freezes the behavior of a legacy Boolean pricing entry point while delegating its decision to a typed pure core. It does not claim a whole-application convergence.

`Shape.Tests` contains 15 behavior, representation and architecture tests. The locked policy and `expected-findings.json` bind every observed finding by rule, path and fingerprint. `eng/qualify-m5-playground.sh` fails on a missing package, failed build/test, non-loaded project, tool failure, changed finding, unexpected exit code or non-deterministic JSON/SARIF.

## Run

Use SDK 10.0.301, commit the candidate, keep the worktree clean, then run:

```bash
export PATH=/root/.cache/fsassay-m5-dotnet:$PATH
export DOTNET_ROOT=/root/.cache/fsassay-m5-dotnet
bash eng/qualify-m5-playground.sh "$(git rev-parse HEAD)" artifacts/m5
```

The local package is not a public NuGet release. A successful script run is still expected to return an **Inconclusive, non-authoritative** FsAssay receipt because 1.0.4 does not ingest the separately executed test result and the test project currently has incomplete workspace evidence. Deterministic compiler and test evidence outrank any LLM judgment.
