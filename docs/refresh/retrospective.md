# FSharpAssay refresh retrospective

The approved refresh is implemented and locally qualified on code commit `b6660ac4db2ea1ce7965e5f309771603eb71d9c0`, branch `feat/csharp-parity-refresh`. The refresh was subsequently fast-forwarded into the original `FSharpAssay/main` checkout through `f979b8a`. Locked restore and Release build succeed there with zero warnings/errors, and all 120 engine tests plus 13 application tests pass. [Integration evidence](../../artifacts/refresh/integration-f979b8a/integration.json) records that checkout validation. The closing documentation commits preserve the original qualification identities; they do not relabel evidence as execution against later revisions. Exact artifacts and SHA-256 values are in [the qualification index](qualification-index.json).

## Results

| Qualification | Observed result |
| --- | --- |
| Exact SDK and Release build | SDK 10.0.301; zero warnings/errors |
| Engine tests | 120 passed, zero failed/skipped through both ordinary and direct entry points |
| Package and consumer gate | All seven gates completed; two normalized package builds match; installed DLL matches the exact candidate payload and package repository commit |
| Independent application | 13 baseline/final passing tests using real SQLite and loopback HTTP |
| Threshold regression | Fail with 11 passed and two failed tests; fresh repair returns 13 passed |
| Missing required execution | Inconclusive with 12 passed and one skipped; restored execution returns Pass |
| Wire regression | Fail with one failed contract test; fresh repair returns Pass |
| Mutation controls | Invalid mutation remains ToolFailure; an uncovered high-amount mutation survives the selected tests and remains a review item; final application source is restored |
| Existing Decision Assist | Six-case corpus completes with before/expanded selections, an invalid candidate, retained survivor and failed-baseline control |
| Existing application preview | All 52 lifecycle/adversarial checks complete; its embedded engine verdict remains separate |
| Security protocol controls | Nine controls complete, including real clean/secret/vulnerable scans, stale/unsupported/missing inputs, repeat identity and tamper refusals |
| Actual application security | Revised 17-file declared scope returns M0 Pass with zero findings, missing evidence or tool failures |
| Schema and identity checks | Twelve authority receipts and twelve SARIF reports validate; package commit and installed payload match |
| Historical source contracts | M1–M4 checks pass against the preserved original revision |
| Analysis-only self-audit | Four projects loaded/supported, 31 sources analyzed, no compiler-incomplete files; Inconclusive solely because the registered tests were not run by analysis |

The source-bound main record is [qualification.json](../../artifacts/refresh/qualification-b6660ac/qualification.json). Security controls, actual application scan and the existing preview have separate scope records in the index. Hosted CI, publication, human rule admission and production approval are not inferred from these local results.

## Approved scope reconciliation

| Approved step | Implementation and evidence |
| --- | --- |
| 1. Establish baseline | Isolated checksum-verified SDK, separate worktree, retained original restore/build and 92/93 test result; repaired repository-schema lookup; final 120-test inventory |
| 2. Harden configuration and boundaries | Strict duplicate/unknown/null/profile validation, component-aware exclusions, pure native classifier, graph-bound project selectors, namespace precedence and explicit analyzer context |
| 3. Execute required tests | Explicit check/verify/assess routes; Release build and direct MTP execution; structured TRX/count/exit validation, bounded failure detail, fresh-directory guard and source/configuration checks |
| 4. Improve reports | Shared authority-aware console/Markdown/HTML, MSBuild format, escaped SARIF URIs/nonblocking levels, staged report writes, alias/input/symlink refusal and deterministic machine reports |
| 5. Improve diagnostics | SDK/FCS/MSBuild/configuration/policy diagnostics; practical explanation and remediation boundaries; corrected partial-option explanation |
| 6. Integrate security | F# process adapter to unchanged SecurityAssay scan/review protocols; independent receipt/SBOM/SARIF; fresh isolated tools/database; actual application dependency repair |
| 7. Audit rules | Complete 93-entry inventory, security/test-style findings, candidate admission dossiers and preserved zero admissions |
| 8. Extend assessment and repair | Native additive envelope with input/producer bindings and bounded supplied assertions; retained red-to-green runs; existing requirement/baseline/freshness preview compatibility |
| 9. Exercise realistic boundaries | Portable three-project consumer, native decisions, SQLite transaction/replay, runtime SQLite version assertion, cancellation and real loopback HTTP/contract tests |
| 10. Extend investigations | Pure contract/resilience/risk observations and registered routing; unknown coverage remains unknown; real mutation corpus and uncovered survivor retained |

F# keeps its native DUs, Result/Option direction and FCS integration. C#'s Roslyn internals, nullable-reference mandate and native-union migration were not copied. Rule admission stays at zero blocking and zero advisory entries pending its independent review.

## Findings and corrections

The initial engine build passed, while one historical schema test resolved a repository file relative to a temporary consumer directory. The corrected test anchors the repository schema explicitly. The original failed log remains under `artifacts/refresh/baseline/`.

The first package qualifier expected one threshold failure, but equality and normalized-reference tests both exercise the threshold. Verification correctly returned Fail with two failures. The qualifier now requires both and checks the supplied threshold test identity; no assertion or minimum was removed. An explicit public-API guard also prevents a reused results directory from providing stale test evidence.

The initial security adapter retained a real scanner Pass, then rejected review because it supplied a relative receipt path under a different working directory. Resolving adapter paths before process calls corrected the inspection. Earlier artifacts remain separate.

A first application-preview replay refused changed producer bindings during concurrent rebuilding. Final qualification uses a fixed clean producer and completes all 52 checks. The refusal remains retained rather than being described as application success.

Most significantly, scanning the actual sample locks found High-severity `GHSA-2m69-gcr7-jv3q` in SQLitePCLRaw.lib.e_sqlite3 2.1.11. The [dependency repair](dependency-remediation.md) updates exact package/lock identities, checks the executed native SQLite version and retains the original Fail. The revised scope passes a fresh scan under unchanged scanner/database/severity pins.

## Practical limits

These are Linux local qualification results. The unchanged SecurityAssay M0 supports NuGet format-1 locks; centrally managed format-2 locks remain explicitly unsupported/Inconclusive. The portable sample owns format-1 locks without transforming the scanned bytes. Its security receipt assesses declared M0 checks, not complete application security.

Experimental rule heuristics can still overreport framework, shell or test code. Configured boundaries affect profile-aware observations and never waive compiler/project/test/integrity evidence. Automatic editor configuration discovery, broader project shapes and any blocking admission require separate qualification. Web/Desktop/MCP and external plugin contracts remain frozen surfaces.

Risk is ordinal triage, and unmeasured coverage stays unknown. A mutation survivor is not equivalence. The SQLite/HTTP sample does not establish distributed concurrency or production operational behavior. Advice does not invoke arbitrary commands or grant verification authority. Report files are replaced individually after staging, not through a multi-file filesystem transaction. Local hashes bind content; they are not signatures or independent attestations.

The branch contains local commits and candidate packages only. No source or package was published and no external message was sent.
