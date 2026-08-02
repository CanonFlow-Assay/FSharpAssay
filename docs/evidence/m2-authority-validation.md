# M2 authority validation record

Base: `13e2314ec8676aaf224440d6a46d3196ac84d2ef`

The bounded local validation sequence is:

```bash
dotnet restore FsAssay.Stable.slnx --locked-mode
dotnet build FsAssay.Stable.slnx --configuration Release --no-restore
bash eng/run-stable-tests.sh ordinary
bash eng/run-stable-tests.sh direct

dotnet restore FsAssay.slnx --locked-mode
dotnet build FsAssay.slnx --configuration Release --no-restore
dotnet run --project FsAssay.Runner/FsAssay.Runner.fsproj \
  --configuration Release --no-build -- \
  --out-json artifacts/audit/results-first.json \
  --out-sarif artifacts/audit/results-first.sarif ./FsAssay.slnx
# Repeat with *-second names, then cmp JSON and SARIF.

dotnet pack FsAssay.Stable.slnx --configuration Release --no-build \
  --output artifacts/packages
```

The audit is expected to exit `2`, not `0`: 25 files analyze successfully and
559 observations remain visible, but Desktop and TypeGym are outside the locked
project classes and the required stable tests are deliberately `notRun` in CLI
evidence. Therefore the receipt is `Inconclusive` and `authoritative: false`.

CI runs all 85 stable tests separately but does not inject or infer that result in
the CLI receipt. Pass behavior is proved by typed reducer/serializer fixtures
with explicit nonzero test evidence. This is not consumer release authority.

Strict-validator mutation fixtures rewrite outcome/authority over `notRun` and
unsupported evidence, forge every other verdict over complete facts, and remove
or add reasons. Every mutation is rejected by receipt-to-facts reconstruction
through the production reducer. A configured wildcard baseline is rejected, and
the receipt cannot claim any applied suppression in M2.

Two independent tester failures were preserved as design evidence rather than
overwritten by later green runs:

- Candidate `43fcd55a...` accepted a one-field outcome/authority mutation because
  the then-public validator performed only structural checks. That candidate was
  disqualified; reducer reconstruction was added.
- Candidate `9d41d2044fa07ccdf48707b73abe921dcca5cf10` rejected the one-field
  mutation but accepted a coordinated replacement that removed unsupported
  project rows and required-test evidence, reconciled counts, changed the partial
  embedded requirements, erased reasons, and retained the original policy SHA.
  That candidate was disqualified; the receipt now embeds the complete strict
  policy snapshot and recomputes its canonical SHA before reducer reconstruction.

Mutation tests cover requirements, project classes, target frameworks, rule
authority sets, profiles, baseline and exceptions. The honest receipt snapshot
and hash are checked against the repository `fsassay-policy.lock.json` identity.
The public expected-policy-hash validator additionally rejects replacement of
both snapshot and hash. Without that external pin, a jointly replaced snapshot
and hash is only internally consistent: M2 provides no signature or authenticity
claim. A coherent candidate commit/approved-head/tree replacement is likewise
accepted by context-free validation and rejected by the context validator. Tests
cover honest commit and synthetic-PR contexts, including the required reviewed
head, synthetic merge and tree pins.

The full 559-finding payload is generated in ignored CI/local artifacts and is
not committed. Its increase from the inherited 436 observations is disclosed;
neither count is a success metric. The manifest binds contract/policy/schema
hashes but deliberately does not bind a self-referential candidate commit or a
runtime receipt hash.
