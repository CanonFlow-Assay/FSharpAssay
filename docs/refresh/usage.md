# FSharpAssay verification and repair

The refresh retains the exact .NET SDK 11.0.100, native F# types, four external verdicts and zero admitted rules. Ordinary target invocation and `check` analyze without executing required tests. `verify` and `assess` explicitly build and run policy-registered test executables, validate their structured TRX results and retain execution logs. Tested producers use Expecto with Microsoft Testing Platform and its TRX extension. Unsupported report producers cannot Pass.

```sh
fsassay doctor
fsassay explain FSA-C02
fsassay check --out-json artifacts/check.json MyApplication.slnx
fsassay verify --out-json artifacts/verified.json --out-sarif artifacts/verified.sarif \
  --assessment-json artifacts/assessment.json --ratecard-md artifacts/report.md \
  --material-html artifacts/report.html --test-artifacts artifacts/fresh-tests MyApplication.slnx
fsassay assess --assessment-json artifacts/assessment.json MyApplication.slnx
fsassay verify --format msbuild MyApplication.slnx
```

`--out-json` retains authority receipt schema `fsassay-authority-receipt/1.1.0`; the additive `--assessment-json` envelope uses `fsassay-assessment/0.1` and embeds the unchanged receipt with input/producer bindings and bounded test failure detail. `assess` performs the same verification as `verify`. Invocation context belongs only to readable reports so canonical JSON/SARIF remain independent of output destinations. Arguments are data, not a shell command to paste.

Mixed passed/skipped test runs retain all observed counts and remain Inconclusive. Missing/malformed reports, contradictory counts/exits, timeouts and build failures remain ToolFailure. Below-minimum evidence cannot Pass. Test results are produced in a new evidence directory; the verifier never ingests an ambient success report. It executes trusted project build/test code and does not sandbox hostile projects. The supported test route builds Release without restore and uses the registered executable's direct MTP entry point through `dotnet run`.

Markdown and HTML flags now produce authority reports instead of numeric quality grades. All requested reports are staged before replacement. Targets must be distinct, cannot overwrite declared inputs and cannot traverse symlinked output paths. Files are individually replaced; a filesystem failure during replacement is not a multi-file transaction and returns ToolFailure. Older existing files from a failed staging attempt remain historical, and the failed invocation cannot authorize their reuse.

## Scan configuration and boundaries

Malformed, duplicate, unknown or null `.fsassayrc` values fail closed. Unknown CLI profiles are invalid evidence. Exclusions match path components and bounded globs, so `bin` cannot accidentally exclude `Combine`. A missing configuration uses explicit defaults; its absence is distinct from malformed content.

```json
{
  "profile": "core",
  "exclude": ["**/obj/**", "**/bin/**"],
  "coreProjects": ["Domain/Domain.fsproj"],
  "shellProjects": ["Adapters/Adapters.fsproj"],
  "coreNamespaces": ["Orders"],
  "shellNamespaces": ["Orders.Persistence"]
}
```

Projects are literal paths relative to the configuration directory. Normalized contradictory selectors are invalid. A selector must identify an already evaluated project; it cannot load extra code. Shell project wins, then shell namespace, core namespace, core project, default. Namespaces match compiler symbol identities and descendants with ordinal case. Projects use the host path comparison. Default retains core analysis behavior for observations. Explicit configured entity scope overrides source profile attributes; it does not change rule admission or suppress compiler/project/test/integrity evidence. Editor/SDK callers can reuse the pure classifier and the explicit analyzer API; automatic editor configuration discovery is not qualified.

## Repair procedure

1. Retain the failed receipt, test logs and process exit. Distinguish Fail, Inconclusive and ToolFailure.
2. Read the exact failed requirement, named test, supplied assertion or missing evidence item. Inspect callers and both sides of a threshold or state transition.
3. Repair the smallest justified source change. Preserve policy, original tests, minima and expected contracts. A new requirement uses an explicit retained red-to-green lifecycle in the application preview.
4. Rerun full configured verification with fresh evidence paths. Do not reuse a receipt after source, configuration, runtime or producer changes.
5. Review experimental findings and mutation survivors separately. Passing configured tests can coexist with an uncovered defect.

Failure details contain at most 20 names/messages, with names bounded to 1,024 UTF-16 units and messages to 4,096 without cutting a surrogate pair. Truncation is explicit. No console output, stack trace or guessed expected/actual value becomes assertion evidence. Keep credentials out of test messages.

The existing application preview retains its separate `ReadyForReview` state and uses ordinary engine analysis, so its embedded engine receipt remains Inconclusive when required tests are not run by the engine. This refresh does not reinterpret historical preview receipts or grant release approval.

Primary references: [Microsoft Testing Platform TRX reporting](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-platform-extensions-test-reports) and [F# compiler diagnostic FS0025](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-messages/fs0025).
