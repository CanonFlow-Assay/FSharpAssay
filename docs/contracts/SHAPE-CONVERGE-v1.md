# FSharpAssay Shape Converge contract v1

Contract identity: `fsharp-shape/1.0.0`  
Status: normative migration sequence for existing code governed by this Shape version.

Convergence is incremental and behavior-preserving. It does not require clearing advisory, experimental, prototype or dummy observations.

## Sequence

1. `SC-01` — Establish a healthy compiler and test baseline before attributing any failure to FSharpAssay.
2. `SC-02` — Record deterministic findings and incomplete evidence; do not describe missing evidence as clean.
3. `SC-03` — Fingerprint accepted existing debt with the complete reviewed baseline record.
4. `SC-04` — Select one bounded module or vertical slice.
5. `SC-05` — Freeze current behavior with tests before changing the slice.
6. `SC-06` — Move decisions and transformations toward the functional core.
7. `SC-07` — Keep database, network, clock, environment, messaging and framework effects at explicit boundaries.
8. `SC-08` — Block new debt under the applicable reviewed gate.
9. `SC-09` — Require strict Shape New for newly added or materially uplifted core modules.
10. `SC-10` — Repeat the bounded process without claiming whole-application conversion.

## Baseline laws

- A record matches only the same rule ID, stable fingerprint, repository-relative path and symbol.
- When a diagnostic has no narrower compiler symbol, the producer records the explicit stable symbol `file-scope`; it never leaves the identity blank.
- A matching `accepted` record applies through its explicit expiry date, inclusive. The policy's explicit `evaluationDate` is used; wall-clock time is not consulted.
- An unmatched or expired blocking finding is new debt and fails.
- A finding matching a `resolved` record is reappearing debt and fails.
- Baseline identity is the SHA-256 of canonical reviewed baseline content, with the identity field blanked. Changing any record or review metadata changes the identity and policy hash.
- Baseline records cannot hide missing project, compiler, test, toolchain or rule evidence.
- The receipt lists only baseline record IDs actually applied. Unmatched, invalid, expired and resolved records are never reported as applied.
- Finding-count reduction alone does not prove safe modernization.
- Behavior tests are required for any behavior-preservation claim.

## Stop conditions

Convergence stops at `ToolFailure` or `Inconclusive` when evidence is invalid or incomplete. Zero findings without complete project and rule evidence is not clean. LLM review may advise a human but cannot change these states.
