# M5 adjudication

No admitted blocking finding is expected. Every observed finding remains non-authoritative.

| Rule | Count | Class | Disposition |
|---|---:|---|---|
| FSA-AI10 | 2 | prototype | Retained. Both locations describe the tested maximum-open-order business value. Rewriting it solely to reduce a prototype count would obscure the decision. |
| FSA-C16 | 3 | prototype | Retained in tests. Reflection is used only to inspect built assembly dependencies; it is not domain behavior. |
| FSA-TDD01 | 13 | experimental | Retained. The architecture heuristic reports synthetic `Architecture` locations despite direct behavior and architecture tests. It is not treated as a release defect. |
| FSA-TDD02 | 1 | experimental | Retained. The bounded example suite is not claimed to provide property-based coverage. |

Exact identities are in `expected-findings.json`; CI rejects substitution at another location.

## Limits and remaining risk

- FsAssay 1.0.4 reports the required test as `notRun`; the 15/15 `dotnet test` result is separate evidence and does not confer authority.
- All four projects and six eligible source files load in the candidate run; that still cannot compensate for required test evidence reported as `notRun`.
- The order store is an in-memory test port. There is no EF Core, messaging, concurrency, serialization, migration or round-trip evidence.
- Architecture tests cover assembly references and known project boundaries; they are not proof against reflection, process calls or future build configuration drift.
- The Converge slice proves only three frozen pricing examples and the compatibility wrapper. Count reduction is not used as proof.
- No LLM judgment is authoritative, no package is published, and no new rule, analyzer, CLI command or profile is introduced.
