# Product boundaries at M2

## Stable qualification surface

M1 qualifies the existing analyzer library and CLI runner at the inherited
`1.0.4` source identity. `FsAssay.Stable.slnx` is the bounded restore, build,
test, and pack surface. The CanonFlow plugin project is included only because it
is an existing regression fixture used by the stable test suite; it is not a
promoted plugin contract or release artifact.

The executable regression evidence is the 85-test Expecto suite. Both the
ordinary `dotnet test` entry point and direct executable entry point must run all
85 tests, and a deliberately empty selection must fail. Thirty-one M2 tests exercise
the authority reducer, missing-evidence cases, precedence, invalid evidence, and
cross-root JSON/SARIF determinism. They also prove that the public validator
rejects forged verdict/authority/reason combinations and cannot describe
configured baseline debt as an applied suppression.

CI test success is not inferred by the CLI. Until a separately reviewed consumer
evidence-ingestion surface exists, the self-audit receipt records the required
stable suite as `notRun`, returns `Inconclusive`, and sets `authoritative` to
`false`.

## Frozen experimental surfaces

The following remain source-visible but are not stable product promises:

- Desktop UI
- Web UI and Playwright exercise
- MCP transport compiled into the runner
- TypeGym
- external plugin API and sample plugin

The Web build and browser exercise remain a non-deploying qualification job.
Desktop and TypeGym remain in the full repository audit solution but outside the
stable build surface. No M1 change adds, removes, or reclassifies analyzer rules,
changes catalogue status or creates a release. M2 supersedes the inherited
verdict fold with a versioned authority contract, but approves no blocking rule;
rule admission requires separate Human Gate C review.

## Configuration and generated output

`.fsassayrc` is the scan-selection configuration. The strict
`fsassay-policy.lock.json` is the M2 authority boundary. Generated reports,
package files, browser screenshots, compiled Web output, and machine-local logs
belong under ignored `artifacts/` or other ignored build directories. They are
not source evidence and are not committed.

The full repository self-audit is evidence about the source tree, not a promise
that frozen experimental surfaces are supported. Its M2 invariant is exactly 25
files scanned, zero skipped, zero failed evaluations, 559 retained observations,
and two explicitly unsupported frozen project classes. The receipt must be
`Inconclusive` and non-authoritative. The inherited M1 count of 436 and the M2
count of 559 are observation counts, not success metrics.
