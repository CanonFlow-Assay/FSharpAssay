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
535 observations remain visible, but Desktop and TypeGym are outside the locked
project classes and the required stable tests are deliberately `notRun` in CLI
evidence. Therefore the receipt is `Inconclusive` and `authoritative: false`.

CI runs all 83 stable tests separately but does not inject or infer that result in
the CLI receipt. Pass behavior is proved by typed reducer/serializer fixtures
with explicit nonzero test evidence. This is not consumer release authority.

The full 535-finding payload is generated in ignored CI/local artifacts and is
not committed. Its increase from the inherited 436 observations is disclosed;
neither count is a success metric. The manifest binds contract/policy/schema
hashes but deliberately does not bind a self-referential candidate commit or a
runtime receipt hash.
