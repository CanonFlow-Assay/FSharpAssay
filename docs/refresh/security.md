# FSharp SecurityAssay adapter

`fsassay security <request.json> <fresh-output>` calls the existing SecurityAssay `scan` and `advise` protocols through an explicit .NET host. It checks the pinned evaluator DLL, structured process exit, unchanged request/policy/catalog/toolchain bytes and SecurityAssay's own receipt inspection. It retains the original receipt, SBOM, SARIF and independent verdict inside `fsharp-security.json`. It cannot change FsAssay rule admission or its engine receipt.

The request uses absolute paths and an explicit UTC evaluation instant. Duplicate/unknown fields, producer mismatches, reused output directories and symlinked request/output paths fail closed.

```json
{
  "schema": "fsharp-security-request/0.1",
  "dotnet": "/absolute/compatible-dotnet/dotnet",
  "engine": "/absolute/SecurityAssay.dll",
  "engineSha256": "<exact 64-character SHA-256 of SecurityAssay.dll>",
  "sourceRoot": "/absolute/FSharpApplication",
  "manifest": "/absolute/inputs.json",
  "policy": "/absolute/approved-policy/m0.json",
  "toolchain": "/absolute/approved-policy/toolchain.lock.json",
  "catalog": "/absolute/asvs/catalog.json",
  "bundle": "/absolute/approved-offline-bundle",
  "asOf": "2026-11-15T17:00:00Z"
}
```

The manifest is SecurityAssay's existing `security-assay-inputs/0.1` registry of individual `source`, `config`, `nuget-lock` or `npm-lock` files. Declare F# source as `source`; there is no C#-only source extension requirement. No application package is restored or executed by this security scan.

The unchanged M0 engine supports NuGet lock **format 1**, not the format-2 central package locks in the analyzer repository. The portable refresh sample owns explicit package versions and format-1 locks. Their original bytes can be scanned directly. Unsupported format-2 locks remain Inconclusive rather than being transformed or labeled clean. Source/integrity checks in the unchanged SecurityAssay receipt reviewer can independently reject missing or changed inputs even when scanner artifacts exist.

Provisioning is a separate online operation using checksum-verified official scanner archives and a fresh database. Evaluation uses only the pinned local container image, disabled networking and declared-byte snapshots. This refresh uses a separate cache and policy copy under ignored artifacts; committed SecurityAssay policies and previous bundles are preserved. The exact SecurityAssay SDK is isolated from the F# SDK. Reproducing that historical preview toolchain does not assert current runtime support or production qualification.

For local integration controls run `dotnet fsi --warnaserror+ --exec eng/qualify-refresh-security.fsx <request-directory> <fresh-output>`. The request directory explicitly supplies `clean`, `secret`, `vulnerable`, `stale`, `missing` and `format2` request files. The qualifier retains scanner outcomes, independent inspection failures, deterministic repeats and tamper controls. A dedicated fake secret is synthetic detector evidence and never a real credential.

Scanner M0 covers declared secrets, inventory and known vulnerability matches. It does not establish authorization, injection safety, cryptographic correctness, package publisher authenticity, complete ASVS compliance or application-security certification. A local receipt digest is integrity evidence, not a signature or independent attestation.
