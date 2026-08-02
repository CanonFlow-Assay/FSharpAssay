# Product boundaries at M1

## Stable qualification surface

M1 qualifies the existing analyzer library and CLI runner at the inherited
`1.0.4` source identity. `FsAssay.Stable.slnx` is the bounded restore, build,
test, and pack surface. The CanonFlow plugin project is included only because it
is an existing regression fixture used by the stable test suite; it is not a
promoted plugin contract or release artifact.

The authoritative executable evidence is the 54-test Expecto suite. Both the
ordinary `dotnet test` entry point and direct executable entry point must run all
54 tests, and a deliberately empty selection must fail.

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
changes admission/authority semantics, or creates a release.

## Configuration and generated output

`.fsassayrc` is the current analyzer configuration boundary. Generated reports,
package files, browser screenshots, compiled Web output, and machine-local logs
belong under ignored `artifacts/` or other ignored build directories. They are
not source evidence and are not committed.

The full repository self-audit is evidence about the source tree, not a promise
that frozen experimental surfaces are supported. Its M1 invariant is exactly 24
files scanned, zero skipped, and zero failed evaluations; findings remain review
input rather than a success metric.
