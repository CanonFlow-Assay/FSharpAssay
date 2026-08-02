# M2 receipt migration

- Validate `schemaVersion == "fsassay-authority-receipt/1.0.0"`.
- Read `outcome` and `authoritative` separately. `Fail` can be non-authoritative
  when a conclusive failure coexists with missing evidence.
- Never infer `Pass` from `findings.length == 0`.
- Treat every `reasons`, `missingEvidence`, and `toolFailures` entry as evidence.
- Read findings from `findings`; the top-level pre-M2 file array no longer exists.
- Use repository-relative `path`/SARIF URIs and `fingerprint` for stable matching.
- Do not equate `candidate.commitSha`, `approvedHeadSha`, `treeSha`,
  `syntheticMergeSha`, or package SHA-256. Each identifies a different
  qualification object.

Rollback means using a pinned pre-M2 tool and its old evidence parser. Mixing an
old parser with the M2 schema is unsupported and must fail closed.
