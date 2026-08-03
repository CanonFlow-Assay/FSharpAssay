# M3 — Shape and rule admission

M3 establishes `fsharp-shape/1.0.0`, classifies the complete 93-rule catalogue and adds typed baseline governance. It does not add or change any analyzer rule.

## Proposed Gate C decision

| Class | Count | Authority meaning |
|---|---:|---|
| blocking | 0 | proposed; Human Gate C pending |
| advisory | 0 | proposed; Human Gate C pending |
| experimental | 35 | implemented observations, not admitted |
| prototype | 36 | incomplete/provisional implementation |
| dummy | 22 | catalogue placeholder without executable evidence |
| deprecated | 0 | none |
| removed | 0 | none |

The machine-readable classification is [`fsassay-rule-classification-v1.json`](fsassay-rule-classification-v1.json). Every catalogue identity appears exactly once. Implemented means executable, not admitted: it describes code availability but does not establish low false-positive rates, safe remediation, framework boundaries or release authority.

## Human Gate C admission bar

A future blocking admission requires a separate reviewed change containing:

- normative rule scope and explicit non-claims;
- representative positive and negative specimens;
- deterministic tests, including hostile false-positive and boundary tests;
- reviewed framework exceptions;
- stable diagnostic identity and reliable project coverage;
- a complete rule page with remediation risks;
- deterministic policy/receipt evidence;
- fresh-consumer proof from the packaged product.

Admission changes baseline governance and must not be inferred from an existing `Implemented` status.

## Migration from M2

M2 policies used schemas `1.0.0`, `shapeContractVersion: not-established`, one experimental bucket and an intentionally empty untyped baseline placeholder. M3 policies use schemas `1.1.0`, an explicit deterministic `evaluationDate`, seven disjoint maturity classes, typed baseline records and bounded framework exceptions. Old policy and receipt payloads fail closed and must be migrated explicitly.

Existing M2 evidence remains historical evidence for M2; it is not rewritten to claim M3 semantics.

## Limits

M3 does not prove that any current rule is suitable for blocking, does not suppress findings through framework exceptions, does not make LLM judgment authoritative and does not claim the analyzer covers every Shape clause.
