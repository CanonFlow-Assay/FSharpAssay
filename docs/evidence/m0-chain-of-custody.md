# M0 chain of custody

Status: factual inventory complete; source ancestry not yet established in the target at the time of this report.

This report records the untouched FsAssay prototype before migration or cleanup. It does not approve the current implementation, rule catalogue, release claims, or CI as authoritative.

## Repository identity

| Role | Repository | Default branch | Exact commit | Access observed |
| --- | --- | --- | --- | --- |
| Read-only source | `CanonFlowFoundation/FSharpAssay` | `main` | `1f25f3088a4a6fb7db980410bc5a2a767de57f2e` | Public; administrative read identity confirmed; push disabled in the migration clone |
| Target | `CanonFlow-Assay/FSharpAssay` | `main` | `8e246100cc8e1e0047bf1353c238ef4869317c04` | Public; administrative write identity confirmed |

Both repositories declare Apache-2.0. Their identities and ownership are unambiguous, so Human Gate A is not required for identity resolution.

The commits have no merge base. Neither is an ancestor of the other. Target `main` is an independent one-commit placeholder containing `.gitignore`, `LICENSE`, and a one-line `README.md`. The source contains 141 commits beginning at `a57f0c292067441d41a4a28e7b2aa14f8272dc3a`. Source ancestry has therefore not yet been established in the target.

The safe migration is a normal two-parent merge on a target feature branch, with target history as the first parent and source `1f25f308...` as the second parent. It must use an explicit unrelated-history merge, retain both roots, go through a Draft PR, and never force-push or rewrite target `main`.

## Untouched environment and baseline

The independent tester used a fresh disposable source clone at the exact source SHA. The inherited `PATH` initially omitted `dotnet`; those commands exited 127. Repeating with the already-installed SDK added to process-local `PATH` was an environment normalization, not a repository repair.

- OS: Ubuntu 24.04.4, `linux-x64`.
- SDK: .NET SDK 10.0.301, MSBuild 18.6.4.
- Runtime: .NET 10.0.9.
- `global.json`: SDK 10.0.301 with `latestFeature` roll-forward.
- Android workload was present but not required.
- NuGet access was required; no container or external application service was required for the root solution baseline.

| Exact command | Exit | Duration | Evidence |
| --- | ---: | ---: | --- |
| `dotnet --info` | 0 | 0.276 s | Expected SDK selected |
| `dotnet restore --locked-mode` | 0 | 6.429 s | Restore completed, but zero package lock files exist |
| `dotnet build --no-restore` | 0 | 24.223 s | Build completed |
| `dotnet test` | 0 | 6.343 s | **Zero tests executed**; runner reported `No test is available` |
| `dotnet build --no-restore --configuration Release` | 0 | 26.800 s | 0 warnings, 0 errors |
| `dotnet test --no-build --configuration Release --verbosity normal` | 0 | 4.714 s | **Zero tests executed** again |
| `dotnet run --project FsAssay.Tests/FsAssay.Tests.fsproj -c Release --no-build` | 0 | 77.807 s | Expecto: 54 run, 54 passed, 0 ignored, 0 failed, 0 errored |
| Existing workflow self-audit command | 1 | 58.230 s | 24 files scanned, 0 skipped, 0 failed, 436 findings |

The build baseline is healthy and the directly executed Expecto suite is healthy. Ordinary `dotnet test` is a false-green, zero-test gate and is a repository/test-adapter integration defect. It must not be described as passing tests. Successful `--locked-mode` restore does not prove locked dependencies because the repository tracks no `packages.lock.json` files.

The self-audit exit 1 follows the current CLI contract for admitted blocking findings. It is not a tool crash and is not a clean result. The independent artifact hashes were:

- `results.sarif`: `a9190b99a60babf1c44c89f71bf99fd2e378f57897978f7831b19aeb7f05addb`
- `ratecard.md`: `5c97eb66deb02f5368a7fa3804df68a7db32a71f3a7db8bb24e83908bd004415`
- `dashboard.html`: `9245c08b0cdc0507717fa9e4883d61f018d48d0794f67dee14caff8bfe605f6d`

The source tracked tree remained byte-clean after baseline execution. The self-audit produced one untracked `results.sarif` in the disposable clone; the tester preserved and disclosed it.

## Repository inventory

- 2,514 tracked files totaling approximately 194,969,198 bytes.
- 10 SDK-style `.fsproj` projects.
- Root `FsAssay.slnx` includes six .NET 10 projects: analyzers, CanonFlow plugin, runner, tests, desktop, and TypeGym.
- `FsAssay.Web/FsAssay.Web.sln` contains the Web client, which targets .NET 8.
- Other projects include Web tests, `InspectTAST`, and `Specimens`.
- 43 tracked `.fs` files, eight `.fsx` files, and no package lock files.
- Global warnings-as-errors is enabled. `NU1608` is explicitly treated as an error. NuGet audit is disabled.
- Unit/regression tests use Expecto with `YoloDev.Expecto.TestSdk`; Web tests use Expecto and Playwright.

Current project surface:

| Project | Purpose observed | TFM | Root solution |
| --- | --- | --- | --- |
| `FsAssay.Analyzers` | Analyzer/rule implementation | `net10.0` | Yes |
| `FsAssay.CanonFlow.Plugin` | Product-specific external analyzer plugin | `net10.0` | Yes |
| `FsAssay.Runner` | CLI/tool host | `net10.0` | Yes |
| `FsAssay.Tests` | Executable Expecto tests | `net10.0` | Yes |
| `FsAssay.Desktop` | Avalonia desktop UI | `net10.0` | Yes |
| `FsAssay.TypeGym` | Type exercise executable | `net10.0` | Yes |
| `FsAssay.Web.Tests` | Playwright Web tests | `net10.0` | No |
| `FsAssay.Web.Client` | Bolero WebAssembly client | `net8.0` | No |
| `InspectTAST` | Inspection utility | `net10.0` | No |
| `Specimens` | Specimen project | `net10.0` | No |

## Workflows and public surfaces

Three source workflows were active:

1. `CI` restores, builds, invokes `dotnet test`, packs `FsAssay.Cli`, and uploads the package. Its test step can succeed with zero tests.
2. `FsAssay Architectural Audit` builds, runs the CLI with findings tolerated as exit 1, uploads SARIF, and separately runs the executable Expecto suite.
3. `CI & Deploy Pages` builds the Web client, installs/runs Playwright, runs the executable Expecto suite, and deploys source GitHub Pages from `main`.

At the source SHA, the most recent three source runs reported success:

- CI: `https://github.com/CanonFlowFoundation/FSharpAssay/actions/runs/30451952398`
- FsAssay Architectural Audit: `https://github.com/CanonFlowFoundation/FSharpAssay/actions/runs/30451952227`
- CI & Deploy Pages: `https://github.com/CanonFlowFoundation/FSharpAssay/actions/runs/30451952505`

Source Pages is enabled at `https://canonflowfoundation.github.io/FSharpAssay/`. Target Pages is not configured. Neither repository exposed branch protection or repository rulesets during inventory. The target had no tags, releases, or open pull requests.

Public and experimental executable surfaces include the CLI, JSON, SARIF, toolchain JSON, Markdown rate card, Material HTML, suppression report, watch, diff, live server, adjudication, explicit-file mode, profiles, fix flag, MCP, documentation generation, external plugins, Desktop, Web, and TypeGym. These are inventory facts, not stability claims.

## Rule and evidence inventory

Runtime reflection over the built catalogue reports **93** rule identifiers:

- 35 `Implemented`.
- 22 `Dummy`.
- 36 `Prototype`.
- 21 production-admitted codes.

The README instead claims 91 total and 34 prototypes. That is current documentation/catalogue drift. The repository also contains 93 generated rule pages and 390 `EXPECT` annotations covering only 24 unique codes. Catalogue status alone does not demonstrate semantic correctness, precision, recall, or safe blocking authority.

The admitted set is:

`FSA2022`, `FSA2017`, `FSA-AI01`, `FSA-AI12`, `FSA-AI13`, `FSA-AI15`, `FSA-AI16`, `FSA-C02`, `FSA-C05`, `FSA-P01`, `FSA-P02`, `FSA-P03`, `FSA-P04`, `FSA-P05`, `FSA-SEC08`, `FSA-SEC11`, `FSA-SEC12`, `FSA-SEC13`, `FSA-TDD01`, `FSA-TDD02`, and `FSA-TDD03`.

Current output limitations visible from source inspection include:

- Canonical JSON is an array of file findings, without a top-level four-state verdict, completeness receipt, policy identity, project-load evidence, test evidence, or authority flag.
- SARIF embeds source-organization and version strings and emits absolute `file://` paths.
- Rule documentation links are relative repository paths.
- The toolchain record contains OS, runtime version, and FCS assembly version only.
- Skipped files and failed analyses affect the process exit but are not represented in JSON/SARIF evidence.
- Project discovery and configuration parsing can fall back without a strict, recorded policy receipt.

These are preservation findings for later milestones, not M0 fixes.

## Assets and cleanup debt

The tracked tree contains substantial generated and vendored material:

- 823 tracked files under `e2e/node_modules`.
- 1,097 tracked files under `docs`.
- 475 tracked files under `public_html`.
- 936 tracked `.wasm`, `.br`, or `.gz` files.
- 262 relative paths duplicated between `docs` and `public_html/FSharpAssay`.
- Large native npm binaries, Monaco bundles/maps, WebAssembly runtime payloads, `adjudicate.log`, `out-toolchain.json`, scratch scripts, and historical review material.

This is cleanup debt, not authorization to delete useful history or evidence. M1 must classify removals and quarantine frozen surfaces before changing them.

## Claim and version drift

Observed release/product identities disagree:

- CLI banner: `0.1.0`.
- Runner tool package: `FsAssay.Cli` version `1.0.4`.
- SARIF driver and MCP server: `1.0.0`.
- CanonFlow plugin: `1.0.0`.
- Planning document: `1.1.0`, while describing capabilities not all present in the current executable.

`--help` prints help to standard output but exits 64. `-h` is interpreted as a target path and exits 3 after a project-system failure. Those observations are factual CLI defects; M0 does not repair them.

The README example uses `--json`, while the implemented long option is `--out-json`. Aspirational documents also describe `check`, `verify`, `doctor`, rule catalogue/explanation, and strict authority receipts that are not current commands. The Desktop scan/fix flow reports simulated success, the Web playground uses substring matching rather than the production analyzer path, and runtime auto-fix explicitly reports itself disabled. These experimental surfaces must not be presented as present release authority.

The source repository also contains CanonFlow-specific plugin logic. The uplift contract forbids coupling FsAssay Core to CanonFlow, GSTFlow, or ONDCFlow. The plugin remains an external project today, but its inclusion in the root solution and product workflows is a boundary fact to review in M1; no product-specific rule is admitted or added by this report.

## Migration ledger

| Item | M0 disposition | Later action |
| --- | --- | --- |
| Source Git history | Preserve exactly | Merge source commit as a parent; never squash or rewrite |
| Target placeholder root | Preserve | Keep as first-parent target ancestry |
| Apache-2.0 license | Preserve | Verify no license regression during merge |
| Analyzer and CLI code | Preserve untouched | Audit under M1/M2 before behavioral change |
| Existing 93-rule catalogue | Preserve, not endorse | Classify under M3; no new rules during uplift |
| 21 admitted rules | Preserve, not approve | Independently requalify before Gate C |
| Expecto suite | Preserve | Repair ordinary test discovery/gating in M1 |
| Existing JSON/SARIF | Preserve as baseline | Replace only under Gate-B authority contract |
| Desktop, Web, MCP, TypeGym, plugins, auto-fix | Freeze | Delete/quarantine or boundary-repair only unless separately approved |
| CanonFlow plugin | Preserve as historical external surface | Remove from core/default product boundary or quarantine in M1 |
| Generated/vendored assets | Preserve until classified | Controlled M1 gastrectomy with migration notes |
| Source Pages | Leave untouched | Target public Pages requires Gate D |
| Tags/releases/packages | None created | Gate D required before any public release action |

## M0 conclusion

The source is buildable and its directly invoked 54-test suite is green. Its ordinary test gate, dependency locking, evidence completeness, rule counts, version identity, repository hygiene, and product boundaries are not yet trustworthy enough for release authority. Zero tests or incomplete evidence must never be described as a pass.

The exact identities are resolved, so M0 may proceed without Human Gate A. The next mechanical action is the non-destructive two-parent ancestry merge on this target feature branch, followed by a Draft PR carrying this ledger. No target `main` rewrite, source write, package publication, release, tag, or Pages deployment is authorized.
