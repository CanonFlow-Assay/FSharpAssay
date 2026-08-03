# M3 Shape and rule-admission inventory

Inventory base: `8da5c3305489d0ac4d07339c400b5fdd7ebed1b1`  
Inventory date: `2026-08-03`  
Scope: tracked FSharpAssay source and public documentation before M3 edits.

## Pre-edit facts

- No normative Shape New or Shape Converge contract existed.
- The policy explicitly used `shapeContractVersion: not-established`.
- Baseline configuration contained only `identity: none` and an empty untyped `approvedFindings` array. There was no per-finding owner, rationale, disposition, date, expiry, symbol, application evidence or reappearance law.
- The catalogue contained exactly 93 identities: 35 `Implemented`, 36 `Prototype` and 22 `Dummy`.
- All 93 rule pages existed, but they were generated catalogue summaries. They did not collectively provide the normative scope, non-claims, remediation-risk analysis, framework exceptions and fresh-consumer package proof required for blocking admission.
- Positive executable specimens covered the inherited 21-rule admission set. Negative/hostile coverage was materially smaller and not a per-rule admission matrix.
- There was no durable per-rule evidence record proving complete positive, negative, hostile, boundary and package-consumer qualification.

The repository was not selected for convenient violations. This is an inventory of its own existing catalogue and qualification evidence.

## Gate C decision

No rule meets the complete admission bar at this milestone. Therefore:

- blocking: 0;
- advisory: 0;
- experimental: all 35 currently implemented rules;
- prototype: all 36 prototype rules;
- dummy: all 22 dummy rules;
- deprecated: 0;
- removed: 0.

The exact identities are in [`../contracts/fsassay-rule-classification-v1.json`](../contracts/fsassay-rule-classification-v1.json). This decision does not demote executable code or delete legacy evidence. It prevents “implemented” or a legacy production set from being mistaken for current blocker authority.

## Gaps retained intentionally

- No rule is newly admitted, so no blocker package-consumer proof is claimed.
- Existing rule pages are not rewritten to manufacture complete admission dossiers.
- The 563 self-audit observations require human adjudication; their presence is not a product-success metric.
- The self-audit remains `Inconclusive` and non-authoritative because required tests are recorded as `notRun` and two frozen project classes are unsupported.
- Shape clauses exceed current analyzer coverage. They are normative design/review guidance, not a claim of automated enforcement.
- LLM judgment is advisory only and cannot override deterministic compiler, test, policy or receipt evidence.

