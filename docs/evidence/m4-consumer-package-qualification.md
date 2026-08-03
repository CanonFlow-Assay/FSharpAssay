# M4 consumer and package qualification

M4 starts from GitHub-verified signed `main` commit
`36c1b9264618344878cbf9dcca11363f5ea3d59b`. It changes no analyzer rule,
diagnostic identity, maturity classification, authority reducer or evidence
schema. It retains inherited product identity `1.0.4` and publishes nothing.

## Read-only inventory before implementation

The shipped CLI was a single default target invocation with JSON, SARIF,
toolchain, rate-card, HTML, suppression-report, watch, diff, local-server,
adjudication, explicit-file, profile, disabled-fix, stdio MCP, documentation and
plugin options. `--diff` was parsed without a runtime consumer and `--fix`
reported itself disabled. `doctor`, `help`, `explain`, `catalog`, `check` and
`verify` commands were absent. `--help` rendered once to stdout but exited `64`;
`-h` and an unknown option-like token were treated as target paths and exited
`2`. Malformed known options rendered on stdout and exited `64`.

The only qualified consumer artifact was the `FsAssay.Cli` `1.0.4` .NET 10
tool. Its NuGet metadata contained authors, description, package type and commit
but no README, license metadata/content, project/repository URL or verified
SourceLink. It bundled its dependencies and declared no nuspec dependency
groups. CI packed and uploaded it but did not fresh-install, uninstall, hash,
attest or compare independent roots. `dotnet nuget verify --all` returned
`NU3004` because the package was unsigned.

Untouched packages from independent clones and independent empty NuGet caches
were not byte reproducible:

- root A: `c4453d06865ded5951ae3daaddb9741ad244706eaa984d229664d016d6db7d79`;
- root B: `8a06fdf1d6dc0d5a63825a293049be692f90d531d9123bed40e0721062cab1e5`.

Checkout paths occurred in runner/analyzer DLL and PDB bytes. NuGet pack also
generated a random OPC core-properties filename/relationship identifier and
used build/pack timestamps. The extracted dependency payload otherwise
matched.

Syscall tracing of default analysis observed no `AF_INET` or `AF_INET6`
activity. It observed only local .NET diagnostic and syslog Unix sockets. The
runner contains an explicit `--serve` localhost listener; MCP uses stdio.
Restore and install are separate package-management operations and can use
configured feeds.

The base merge commit was GitHub-signature verified. The package recorded its
commit but had no NuGet signature, GitHub artifact attestation or deterministic
hash manifest. Public documentation correctly called the tool experimental and
the version inherited/not published, but lacked a complete consumer
installation and rollback path. Aspirational documents were not shipped CLI
contracts.

## Bounded implementation

- `help`, `--help` and `-h` render identical stdout once and exit `0`.
- `doctor` performs no analysis or network operation and reports tool/runtime,
  SDK, FCS and offline-default posture.
- `explain <RULE>` derives its message, severity and implementation status from
  the existing catalogue and maps the exact M3 35 experimental / 36 prototype /
  22 dummy partition. It states that the output is non-authoritative.
- Unknown rules/options and malformed invocations write to stderr and exit
  `64`.
- No named catalogue/check/verify alias was invented. `--docs` remains the
  catalogue surface; default analysis plus JSON/SARIF remains the strict
  four-state path.
- The package adds current description, README, Apache-2.0 license,
  project/repository URL, exact commit, deterministic paths and verified
  SourceLink metadata.
- The final nupkg canonicalizer fixes OPC identities, entry order and timestamps
  after normal SDK pack. It does not sign, publish or change payload semantics.
- Qualification uses two independent clones/caches, a repository-local tool
  manifest, a NuGet configuration with `<clear/>` and one local feed, a disabled
  network namespace, install/run/uninstall rollback and repeated JSON/SARIF.
- Failure injection rejects wrong hashes/provenance, incomplete packages,
  invalid invocations, zero evidence and lock drift.
- Candidate-push CI attests the exact package hash with GitHub build provenance
  and verifies it. This is distinct from a NuGet author/repository signature.

The generated `artifacts/m4/package-manifest.json` is the deterministic machine
receipt. It binds the exact candidate SHA, package hash/size/metadata, raw and
canonical reproduction result, stable test minimum, installed evidence hashes,
offline proof, rollback and adversarial results. CI publishes the package,
manifest, command streams, traces and logs as review artifacts.

## Limitations and nonclaims

M4 does not admit a blocking or advisory rule. Finding counts are observations,
not success metrics. Default analysis cannot infer ambient test success and can
remain `Inconclusive`/non-authoritative. The package is not NuGet-signed and is
not published. GitHub provenance proves the workflow-produced artifact subject;
it does not prove analyzer correctness, business correctness, source security or
safe automatic remediation. Frozen Desktop, Web, MCP, TypeGym, plugin and fix
surfaces are not promoted.
