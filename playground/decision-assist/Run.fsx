#load "Decision.fs"

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open FsAssay.Playground.DecisionAssist

// All process and file effects are here; Decision.fs only reduces typed evidence.
let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
let arguments = fsi.CommandLineArgs |> Array.skip 1
let output =
    match arguments with
    | [| "--output"; path |] -> Path.GetFullPath(path)
    | [||] -> Path.Combine(root, "artifacts", "astra-fsharp", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"))
    | _ -> failwith "usage: dotnet fsi --exec playground/decision-assist/Run.fsx [--output <new-directory>]"
if Directory.Exists output || File.Exists output then failwith "output already exists; use a fresh directory"
let jsonOptions = JsonSerializerOptions(WriteIndented = true)
let writeJson path value = File.WriteAllText(path, JsonSerializer.Serialize(value, jsonOptions))
let sha (bytes: byte array) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()
let sourcePaths = [
    "global.json"
    "toolchain-lock.json"
    "playground/m5/fsassay-policy.lock.json"
    "playground/decision-assist/Inspect.fsx"
    "playground/m5/Shape.Domain/Order.fs"
    "playground/m5/Shape.Domain/Pricing.fs"
    "playground/decision-assist/Decision.fs"
    "playground/decision-assist/Decision.Tests.fsx"
    "playground/decision-assist/Probe.fsx"
    "playground/decision-assist/Run.fsx"
    "playground/decision-assist/requirements.json"
    "playground/decision-assist/cases.json"
]
type SourceBinding = { path: string; sha256: string }
let snapshot () = sourcePaths |> List.map (fun path -> { path = path; sha256 = File.ReadAllBytes(Path.Combine(root, path)) |> sha }) |> List.toArray
let sourceBindings = snapshot ()
let snapshotSha = sourceBindings |> Array.map (fun source -> source.path + ":" + source.sha256) |> String.concat "\n" |> Encoding.UTF8.GetBytes |> sha
let fresh () = snapshot () = sourceBindings

type ProcessResult = { exitCode: int; timedOut: bool; stdout: string; stderr: string }
let invoke (workingDirectory: string) (args: string list) =
    let start = ProcessStartInfo("dotnet", WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    for arg in args do start.ArgumentList.Add arg
    start.Environment["DOTNET_NOLOGO"] <- "1"
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
    use child = new Process(StartInfo = start)
    if not (child.Start()) then failwith "unable to launch the pinned dotnet host"
    let stdout = child.StandardOutput.ReadToEndAsync()
    let stderr = child.StandardError.ReadToEndAsync()
    let completed = child.WaitForExit(60000)
    if not completed then child.Kill(true)
    child.WaitForExit()
    { exitCode = child.ExitCode; timedOut = not completed; stdout = stdout.GetAwaiter().GetResult(); stderr = stderr.GetAwaiter().GetResult() }
let sdk = invoke root [ "--version" ]
let globalDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")))
let requiredSdk = globalDocument.RootElement.GetProperty("sdk").GetProperty("version").GetString()
if sdk.exitCode <> 0 || sdk.stdout.Trim() <> requiredSdk then failwith "exact repository SDK is required"
Directory.CreateDirectory output |> ignore
writeJson (Path.Combine(output, "source-bindings.json")) sourceBindings

[<CLIMutable>]
type TestResult = { id: string; passed: bool; error: string }
[<CLIMutable>]
type TestReport = { schema: string; selection: string; total: int; passed: int; failed: int; tests: TestResult array }
[<CLIMutable>]
type MutationCase = { id: string; source: string; find: string; replace: string; requirementId: string; probeId: string; expectedBefore: string; expectedExpanded: string }
[<CLIMutable>]
type CaseManifest = { schema: string; cases: MutationCase array }
let manifest = JsonSerializer.Deserialize<CaseManifest>(File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "cases.json")))
if manifest.schema <> "fsassay-playground-mutations/0.1" || manifest.cases.Length <> 6 then failwith "invalid corpus"
if manifest.cases |> Array.map _.id |> Array.distinct |> Array.length <> manifest.cases.Length then failwith "duplicate corpus case"
[<CLIMutable>]
type Requirement = { id: string; description: string; tests: string array }
[<CLIMutable>]
type Requirements = { schema: string; scope: string; requirements: Requirement array }
let requirements = JsonSerializer.Deserialize<Requirements>(File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "requirements.json")))
if requirements.schema <> "fsassay-playground-requirements/0.1" then failwith "invalid requirement registry"
let beforeIds = set [ "QTY-positive"; "QTY-negative"; "ORDER-missing-customer"; "ORDER-below-limit"; "PRICE-standard"; "PRICE-above-threshold" ]
let expandedIds = requirements.requirements |> Array.collect _.tests |> Set.ofArray
if expandedIds.Count <> 12 || not (Set.isSubset beforeIds expandedIds) then failwith "invalid named test registry"
let allowedIds = Map [ "before", beforeIds; "expanded", expandedIds; "failed-baseline", Set.add "CONTROL-deliberately-failed-baseline" beforeIds ]
let probes = Map [ "quantity-boundary", "QTY"; "order-boundary", "ORDER"; "pricing-boundary", "PRICE" ]
let domainSources = [ "Order.fs"; "Pricing.fs" ]
let prepare name mutation =
    let directory = Path.Combine(output, name)
    Directory.CreateDirectory directory |> ignore
    for source in domainSources do
        let original = File.ReadAllText(Path.Combine(root, "playground/m5/Shape.Domain", source))
        let body =
            match mutation with
            | Some (candidate: MutationCase) when candidate.source = source ->
                if String.IsNullOrEmpty candidate.find || original.Split([| candidate.find |], StringSplitOptions.None).Length <> 2 then
                    failwith ("mutation must match exactly once: " + candidate.id)
                original.Replace(candidate.find, candidate.replace)
            | _ -> original
        File.WriteAllText(Path.Combine(directory, source), body)
    File.Copy(Path.Combine(__SOURCE_DIRECTORY__, "Probe.fsx"), Path.Combine(directory, "Probe.fsx"))
    directory
let runProbe directory selection =
    let reportPath = Path.Combine(directory, selection + ".json")
    let execution = invoke root [ "fsi"; "--warnaserror+"; "--exec"; Path.Combine(directory, "Probe.fsx"); selection; reportPath ]
    File.WriteAllText(Path.Combine(directory, selection + ".stdout.log"), execution.stdout)
    File.WriteAllText(Path.Combine(directory, selection + ".stderr.log"), execution.stderr)
    if execution.timedOut then Unavailable "test process timed out"
    elif not (File.Exists reportPath) then
        if execution.exitCode <> 0 && execution.stderr.Contains("error FS") then Invalid "F# compilation failed; inspect retained stderr"
        else Unavailable "no valid test report"
    else
        try
            let report = JsonSerializer.Deserialize<TestReport>(File.ReadAllText reportPath)
            let expectedIds = allowedIds[selection]
            let expected = expectedIds.Count
            let actualPassed = report.tests |> Array.filter _.passed |> Array.length
            let unique = report.tests |> Array.map _.id |> Array.distinct |> Array.length
            if report.schema <> "fsassay-playground-tests/0.1" || report.selection <> selection || report.total <> expected || report.tests.Length <> expected || unique <> expected || (report.tests |> Array.map _.id |> Set.ofArray) <> expectedIds || report.passed <> actualPassed || report.failed <> expected - actualPassed then
                Unavailable "test report identity/count mismatch"
            elif report.failed = 0 && execution.exitCode = 0 then Passed report.total
            elif report.failed > 0 && execution.exitCode = 1 then Failed report.failed
            else Unavailable "test report and process exit disagree"
        with _ -> Unavailable "malformed test report"
let outcome = function
    | Passed _ -> "Survived"
    | Failed _ -> "Killed"
    | Invalid _ -> "Invalid"
    | Unavailable _ -> "Unavailable"
let executionDescription = function
    | Passed count -> $"Passed:{count}"
    | Failed count -> $"Failed:{count}"
    | Invalid reason -> "Invalid:" + reason
    | Unavailable reason -> "Unavailable:" + reason
let baselineDirectory = prepare "baseline" None
let beforeBaseline = runProbe baselineDirectory "before"
let expandedBaseline = runProbe baselineDirectory "expanded"
let failedBaseline = runProbe baselineDirectory "failed-baseline"
let advice baseline candidate probe budget =
    Decision.decide { Baseline = baseline; Candidate = candidate; SnapshotMatches = fresh (); RemainingBudget = budget; Probe = probe } |> Decision.describe
let failedBaselineAdvice, failedBaselineReason = advice failedBaseline None None 3
let controls = invoke root [ "fsi"; "--warnaserror+"; "--exec"; Path.Combine(__SOURCE_DIRECTORY__, "Decision.Tests.fsx") ]
File.WriteAllText(Path.Combine(output, "decision-tests.stdout.log"), controls.stdout)
File.WriteAllText(Path.Combine(output, "decision-tests.stderr.log"), controls.stderr)

type CaseEvidence = {
    id: string; requirementId: string; candidateSourceSha256: string
    before: string; expanded: string; beforeAdvice: string; beforeReason: string
    expandedAdvice: string; expandedReason: string; matchesExpected: bool
}
let results = manifest.cases |> Array.map (fun candidate ->
    if not (List.contains candidate.source domainSources) || not (System.Text.RegularExpressions.Regex.IsMatch(candidate.id, "^[a-z][a-z0-9-]+$")) then failwith "invalid mutation source or identifier"
    if not (requirements.requirements |> Array.exists (fun requirement -> requirement.id = candidate.requirementId)) then failwith "unknown requirement"
    if candidate.probeId <> "" && Map.tryFind candidate.probeId probes <> Some candidate.requirementId then failwith "unregistered probe or requirement mismatch"
    let directory = prepare candidate.id (Some candidate)
    let before = runProbe directory "before"
    let expanded = runProbe directory "expanded"
    let probe = if candidate.probeId = "" then None else Some { Id = candidate.probeId; RequirementId = candidate.requirementId }
    let beforeAdvice, beforeReason = advice beforeBaseline (Some before) probe 3
    let expandedAdvice, expandedReason = advice expandedBaseline (Some expanded) None 0
    { id = candidate.id; requirementId = candidate.requirementId
      candidateSourceSha256 = File.ReadAllBytes(Path.Combine(directory, candidate.source)) |> sha
      before = outcome before; expanded = outcome expanded
      beforeAdvice = beforeAdvice; beforeReason = beforeReason
      expandedAdvice = expandedAdvice; expandedReason = expandedReason
      matchesExpected = outcome before = candidate.expectedBefore && outcome expanded = candidate.expectedExpanded })
let sourceCurrent = fresh ()
let complete = beforeBaseline = Passed 6 && expandedBaseline = Passed 12 && failedBaseline = Failed 1 && failedBaselineAdvice = "RepairBaseline" && controls.exitCode = 0 && not controls.timedOut && sourceCurrent && (results |> Array.forall _.matchesExpected)
let artifacts = Directory.GetFiles(output, "*", SearchOption.AllDirectories) |> Array.sort |> Array.map (fun file -> { path = Path.GetRelativePath(output, file).Replace('\\', '/'); sha256 = File.ReadAllBytes(file) |> sha })
let summary = {| artifacts = artifacts; schema = "fsassay-playground-investigation/0.1"; complete = complete; sdk = requiredSdk; snapshotSha256 = snapshotSha; sourceCurrent = sourceCurrent
                 scope = "M5 Order.fs and Pricing.fs; fixed mutation corpus; no general mutation or purity proof"
                 baseline = {| before = executionDescription beforeBaseline; expanded = executionDescription expandedBaseline |}
                 failedBaselineControl = {| evidence = executionDescription failedBaseline; advice = failedBaselineAdvice; reason = failedBaselineReason; candidateExecuted = false |}
                 decisionTestsPassed = controls.exitCode = 0 && not controls.timedOut; cases = results
                 authority = {| fsassayEvaluated = false; authoritative = false; admittedBlockingRules = 0; humanGateC = "pending"; verificationVerdict = "NotEvaluated"; note = "Separate executed evidence is not ingested by FsAssay 1.0.4 and cannot upgrade an Inconclusive receipt." |} |}
writeJson (Path.Combine(output, "investigation.json")) summary
File.WriteAllText(Path.Combine(output, "investigation.sha256"), File.ReadAllBytes(Path.Combine(output, "investigation.json")) |> sha)
printfn "FSharp Decision Assist complete=%b; evidence=%s" complete output
if not complete then exit 1
