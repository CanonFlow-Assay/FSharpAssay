# Product boundaries at M3

## Stable qualification surface

M1 qualifies the existing analyzer library and CLI runner at the inherited
`1.0.4` source identity. `FsAssay.Stable.slnx` is the bounded restore, build,
test, and pack surface. The CanonFlow plugin project is included only because it
is an existing regression fixture used by the stable test suite; it is not a
promoted plugin contract or release artifact.

The executable regression evidence is the 92-test Expecto suite. Both the
ordinary `dotnet test` entry point and direct executable entry point must run all
92 tests, and a deliberately empty selection must fail. Authority tests exercise
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
verdict fold with a versioned authority contract and configured zero blocking rules;
M3 adds Shape, complete catalogue classification and typed baseline laws while
proposing zero blocking admissions. Human Gate C remains pending; rule admission
requires its separate review.

## Configuration and generated output

`.fsassayrc` is the scan-selection configuration. The strict
`fsassay-policy.lock.json` is the M3 authority boundary. Generated reports,
package files, browser screenshots, compiled Web output, and machine-local logs
belong under ignored `artifacts/` or other ignored build directories. They are
not source evidence and are not committed.

The full repository self-audit is evidence about the source tree, not a promise
that frozen experimental surfaces are supported. Its exact M3 counts are locked
by the candidate evidence manifest. The two frozen project classes remain
explicitly unsupported, so the receipt must be `Inconclusive` and
non-authoritative. The inherited M1 count of 436, M2 count of 559 and any M3
observation count are not success metrics.

Canonical JSON, SARIF and toolchain records are qualified for cross-root byte
determinism. The human-facing rate-card Markdown and dashboard HTML currently
embed absolute checkout paths: repeated runs in one root are byte-identical,
but artifacts from different roots are not. M3 records this limitation and does
not refactor the output surface.
