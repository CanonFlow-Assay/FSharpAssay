# FSharpAssay Shape New contract v1

Contract identity: `fsharp-shape/1.0.0`  
Status: normative for new code governed by a policy that names this identity.

Shape is a human-reviewed design contract. It is not a promise that the current analyzers can prove every clause. A rule's implementation status is separate from its admission class.

## Clauses

- `SN-CORE` — Keep domain decisions in a functional core. Locate hosting, persistence, serialization, UI, network, clock and other effects in an imperative shell or explicit adapter.
- `SN-IMM` — Prefer immutable domain values and transformations. Mutation requires a named boundary reason and behavioral tests.
- `SN-OUT` — Represent expected business outcomes explicitly with native F# discriminated unions, `Result` or `Option`; do not encode an expected branch as an exception or magic null.
- `SN-STATE` — Use a discriminated union when the alternatives are meaningful domain states. Make illegal state combinations unrepresentable where the public contract permits.
- `SN-TOTAL` — Handle every case when a domain decision must be total. A deliberate catch-all is acceptable only when its behavior is part of the reviewed contract.
- `SN-VALID` — Public construction produces a valid value or an explicit failure. Default values and public constructors must not bypass a documented invariant.
- `SN-DEPS` — Pass time, configuration and effectful dependencies explicitly at the core boundary. The shell may acquire framework services and adapt them to core-owned functions or ports.
- `SN-ADAPTER` — Keep adapters thin: translate representations, invoke the core and translate the result. Business branching belongs in the core.
- `SN-TEST` — Protect business behavior and architecture boundaries with deterministic tests. Missing compiler, project, test or rule evidence is never a pass.

## Reviewed framework exceptions

Only `hosting`, `serialization`, `persistence`, `ui`, `dependency-injection` and `interoperability` are recognized exception categories. Each exception is bound to an ID, repository-relative path, symbol, owner, reason, creation date, optional expiry and one or more Shape clause IDs.

An exception documents a reviewed framework boundary. It does not suppress compiler errors, project-load failure, missing tests, missing rule outcomes or other authority evidence. It also does not suppress an analyzer finding: admitted blocking debt requires its own exact baseline record.

## Non-claims

Shape does not require point-free style, ban classes, ban objects, forbid all mutation, prove functional correctness, prove security, convert existing code automatically or make an LLM judgment authoritative. An ordinary clear F# implementation is preferred to artificial purity.

