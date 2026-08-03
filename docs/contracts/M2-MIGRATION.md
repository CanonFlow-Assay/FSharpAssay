# M2 receipt migration

- Validate `schemaVersion == "fsassay-authority-receipt/1.0.0"`.
- Read `outcome` and `authoritative` separately. `Fail` can be non-authoritative
  when a conclusive failure coexists with missing evidence.
- Never infer `Pass` from `findings.length == 0`.
- Treat every `reasons`, `policyErrors`, `evidenceErrors`, `missingEvidence`, and
  `toolFailures` entry as evidence, and require public semantic validation rather
  than trusting those top-level labels.
- Read configured baseline records and reviewed exceptions from the complete
  `policy.snapshot`; `appliedSuppressions` is separately itemized and empty in M2.
- Require the public validator to recompute the canonical snapshot hash. When a
  reviewed policy SHA is known, use the expected-policy-hash validator: snapshot
  plus hash can otherwise be replaced together because M2 does not sign receipts.
- At Human Gate B, use the expected-context validator to pin policy SHA, analyzed
  commit and tree. Also pin reviewed head/synthetic merge or package SHA when the
  receipt kind requires those identities. Context-free validation is consistency
  checking only.
- Read findings from `findings`; the top-level pre-M2 file array no longer exists.
- Use repository-relative `path`/SARIF URIs and `fingerprint` for stable matching.
- Do not equate `candidate.commitSha`, `approvedHeadSha`, `treeSha`,
  `syntheticMergeSha`, or package SHA-256. Each identifies a different
  qualification object.

Rollback means using a pinned pre-M2 tool and its old evidence parser. Mixing an
old parser with the M2 schema is unsupported and must fail closed.
