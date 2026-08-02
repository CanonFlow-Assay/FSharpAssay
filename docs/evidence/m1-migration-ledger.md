# M1 product-truth migration ledger

## Provenance

- Read-only source baseline: `1f25f3088a4a6fb7db980410bc5a2a767de57f2e`
- Migrated target `main` base: `f773b3090ffd86cb5600fdaf3aca20ec9cc19606`
- Base tree: `417d216dd00d7d8f082627f1f859f413044a8c6d`
- Analyzer source baseline tree: `bbd34a4fe89f692347f6f5706258252ffa69c32e`
- Inherited product identity: `1.0.4`
- Release action: none

M1 does not change analyzer source, rule definitions, rule admission, authority
semantics, or CLI commands. It repairs repository claims, test execution,
dependency locking, workflow boundaries, and tracked-output hygiene.
The only new path below `FsAssay.Analyzers/` is its dependency lock file; every
pre-existing analyzer path is byte-identical to the target base.

The stable tests transitively build the external CanonFlow plugin because the
existing suite uses it as frozen regression evidence. That build does not admit
the plugin into core authority or promote the plugin contract.

## Acceptance ledger

| Concern | Before M1 | M1 invariant |
|---|---|---|
| Stable tests | `dotnet test` returned success without executing tests | ordinary and direct entry points each execute exactly 54; zero tests fail |
| Restore | package graph was not truthfully locked | maintained stable, audit, and Web projects carry `packages.lock.json`; CI uses `--locked-mode`; drift is rejected |
| Deployment | inherited workflow held deployment permissions and actions | workflow files contain no deployment tokens, actions, environments, or permissions |
| Stable boundary | full experimental tree was treated as one product surface | analyzer and runner are bounded by `FsAssay.Stable.slnx`; frozen surfaces are documented separately |
| Repository audit | duplicated test responsibility and root outputs | audit owns only the 24-file self-scan and writes under ignored `artifacts/audit/` |
| Claims | README reported 91 rules and an invalid JSON switch | README and executable assertions bind 93 total: 35 implemented, 22 dummy, 36 prototype; JSON uses `--out-json` |
| Identity | package, console, SARIF, and MCP displayed conflicting versions | existing runner surfaces derive the inherited `1.0.4` identity from one MSBuild property |
| Tracked debt | compiled site copies, vendored dependencies, logs, screenshots, scratch scripts, and the deployment workflow were committed | 2,296 exact paths are deleted and recoverable from the target base commit |

## Deletion evidence and recovery

[`m1-deletions.tsv`](m1-deletions.tsv) is the normative deletion manifest. It is
sorted by path and records every base blob, byte size, category, and exact Git
recovery command. The manifest has 2,296 entries totaling 193,488,195 bytes.
Its SHA-256 is
`9995b05c921816bf431727560f454c04972d8b64f8eb1f12602ec2224e6df22d`.

Categories:

| Category | Paths | Bytes |
|---|---:|---:|
| committed-run-log | 1 | 195,840 |
| compiled-docs-web-payload | 989 | 77,576,375 |
| compiled-public-html-copy | 475 | 60,578,901 |
| deployment-workflow-replaced | 1 | 2,288 |
| empty-scratch | 1 | 0 |
| failed-run-screenshot | 1 | 13,716 |
| machine-host-output | 1 | 74 |
| obsolete-scratch-script | 4 | 9,508 |
| vendored-node-dependencies | 823 | 55,111,493 |

Any item can be inspected without changing the worktree:

```bash
git show f773b3090ffd86cb5600fdaf3aca20ec9cc19606:<path>
```

It can be restored deliberately with:

```bash
git show f773b3090ffd86cb5600fdaf3aca20ec9cc19606:<path> > <path>
```

The generated directories and machine-local outputs are now ignored so normal
qualification cannot silently reintroduce them.

## Deterministic verification

`eng/verify-m1-claims.sh` binds the base and analyzer source baseline, permits
only the required analyzer lock-file addition below that directory, checks every
deletion entry against Git object identity and byte size, compares the manifest
to the candidate deletion diff, validates lock-file coverage, rejects tracked
generated debt, and rejects deployment capability in workflow files.

`eng/run-stable-tests.sh`, `eng/assert-zero-test-fails.sh`, and
`eng/assert-locked-restore.sh` keep all runtime evidence below ignored
`artifacts/`. CI artifacts are supporting review material; the committed
manifest and scripts are the reproducible contract.

## Limitations

- The `1.0.4` value is an inherited source identity, not a publication claim.
- Desktop, Web, MCP, TypeGym, and plugin surfaces remain frozen experimental
  code; M1 neither deletes nor stabilizes them.
- Browser qualification remains environment-sensitive and is explicitly
  non-deploying.
- The repository self-audit may report known findings. M1 binds completeness
  (24 scanned, zero skipped, zero failed), not a zero-finding outcome.
- M1 intentionally does not repair analyzer behavior, change rule counts or
  admission, add commands, or begin later milestones.
