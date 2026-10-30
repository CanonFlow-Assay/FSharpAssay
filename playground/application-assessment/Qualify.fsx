#load "Assay.Application/Domain.fs"
open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json
open System.Text.Json.Nodes
open FsAssay.Application

let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
let args = fsi.CommandLineArgs |> Array.skip 1
if args = [|"--check"|] then exit 0
let output = match args with [| path |] -> Path.GetFullPath path | _ -> failwith "usage: dotnet fsi --exec playground/application-assessment/Qualify.fsx <fresh-output>"
if Directory.Exists output || File.Exists output then failwith "existing output is never overwritten"
Directory.CreateDirectory output |> ignore
let toolkit = __SOURCE_DIRECTORY__
let cli = Path.Combine(toolkit, "Assay.Application.Cli/bin/Release/net10.0/Assay.Application.Cli.dll")
let engine = Path.Combine(root, "FsAssay.Runner/bin/Release/net10.0/FsAssay.Runner.dll")
if not (File.Exists engine) then failwith "build unchanged FsAssay.Runner Release first"
let feed, packages = Path.Combine(output, "feed"), Path.Combine(output, "consumer-packages")
Directory.CreateDirectory feed |> ignore
let results = ResizeArray<obj>()
let hash file = File.ReadAllBytes file |> SHA256.HashData |> Convert.ToHexString
let run (cwd: string) (name: string) (arguments: string list) =
    let start = ProcessStartInfo("dotnet", WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    for arg in arguments do start.ArgumentList.Add arg
    start.Environment["FSASSAY_APPLICATION_FEED"] <- feed
    start.Environment["NUGET_PACKAGES"] <- if name.StartsWith("toolkit-") then Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages") else packages
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
    use child = new Process(StartInfo = start)
    child.Start() |> ignore
    let stdout, stderr = child.StandardOutput.ReadToEndAsync(), child.StandardError.ReadToEndAsync()
    let complete = child.WaitForExit 180000
    if not complete then child.Kill true
    child.WaitForExit()
    File.WriteAllText(Path.Combine(output, name + ".stdout.log"), stdout.GetAwaiter().GetResult())
    File.WriteAllText(Path.Combine(output, name + ".stderr.log"), stderr.GetAwaiter().GetResult())
    results.Add(box {| name = name; exitCode = child.ExitCode; timedOut = not complete |})
    if not complete then failwith (name + " timed out")
    child.ExitCode
let require condition label = if not condition then failwith label
let write file value = File.WriteAllText(file, JsonSerializer.Serialize(value, JsonSerializerOptions(WriteIndented = true)))
let read<'a> file = JsonSerializer.Deserialize<'a>(File.ReadAllText file)
let replace (file: string) (before: string) (after: string) =
    let text = File.ReadAllText file
    require (text.Split([|before|], StringSplitOptions.None).Length = 2) ("replacement anchor: " + file)
    File.WriteAllText(file, text.Replace(before, after))
let cloneSources source name =
    let directory = Path.Combine(output, name)
    Directory.CreateDirectory directory |> ignore
    for file in Directory.GetFiles source do File.Copy(file, Path.Combine(directory, Path.GetFileName file))
    directory
let restore app name = require (run app (name + "-restore") ["restore"; "Approval.App.fsproj"; "--locked-mode"; "--no-cache"] = 0) (name + " restore")
let mutable retainedFailure: string option = None
let assess app name baseline expected =
    let destination = Path.Combine(output, name)
    let arguments = [ cli; "assess"; Path.Combine(app, "assay.app.json"); engine; destination ] @ (baseline |> Option.map (fun file -> ["--baseline";file]) |> Option.defaultValue [])
    let arguments = arguments @ (if baseline.IsSome then retainedFailure |> Option.map (fun file -> ["--failure";file]) |> Option.defaultValue [] else [])
    let code = run root name arguments
    use document = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "assessment.json")))
    let status = document.RootElement.GetProperty("status").GetString()
    require (status = expected) (name + " unexpected status: " + status)
    require (document.RootElement.GetProperty("engineOutcome").GetString() = "Inconclusive" && not (document.RootElement.GetProperty("engineAuthoritative").GetBoolean())) "external tests changed engine authority"
    require (code = (if expected = "ReadyForReview" then 0 elif expected = "NeedsRepair" then 1 else 2)) "assessment exit/status mismatch"
    Path.Combine(destination, "assessment.json")

let inspect runFile rootOption name expected =
    let arguments = [cli; "inspect"; runFile] @ (rootOption |> Option.map (fun root -> ["--source-root";root]) |> Option.defaultValue [])
    require (run root name arguments = expected) (name + " inspection")
let rec toolkitFiles directory = [|
    for entry in Directory.EnumerateFileSystemEntries directory do
        if not (["bin";"obj"] |> List.contains (Path.GetFileName entry)) then
            if Directory.Exists entry then yield! toolkitFiles entry else yield entry
|]
let toolkitSnapshot () = toolkitFiles toolkit |> Array.sort |> Array.map (fun file -> {|path=Path.GetRelativePath(toolkit,file);sha256=hash file|})
let sourceInputs = toolkitSnapshot ()
for lock in ["Assay.Application/packages.lock.json";"Assay.Application.Cli/packages.lock.json";"sample/packages.lock.json"] do require (File.Exists(Path.Combine(toolkit,lock))) ("missing committed lock: " + lock)
let mutable completed = false
try
    require (run root "toolkit-restore" ["restore"; Path.Combine(toolkit, "Assay.Application.Cli/Assay.Application.Cli.fsproj"); "--locked-mode"; "--ignore-failed-sources"] = 0) "toolkit restore"
    require (run root "toolkit-build" ["build"; Path.Combine(toolkit, "Assay.Application.Cli/Assay.Application.Cli.fsproj"); "--no-restore"; "-c"; "Release"] = 0) "toolkit build"
    require (run root "contract-tests" ["fsi"; "--warnaserror+"; "--exec"; Path.Combine(toolkit, "Contract.Tests.fsx")] = 0) "pure contract controls"
    require (run root "toolkit-pack" ["pack"; Path.Combine(toolkit, "Assay.Application/Assay.Application.fsproj"); "--no-build"; "--no-restore"; "-c"; "Release"; "-o"; feed] = 0) "local package"
    let core = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages/fsharp.core/10.1.201/fsharp.core.10.1.201.nupkg")
    File.Copy(core, Path.Combine(feed, Path.GetFileName core))
    let baselineApp = cloneSources (Path.Combine(toolkit, "sample")) "baseline-app"
    // Rebuilt local preview packages have new bytes: bootstrap only the disposable consumer lock.
    require (run baselineApp "consumer-lock-bootstrap" ["restore"; "Approval.App.fsproj"; "--force-evaluate"; "--no-cache"; "-p:RestoreLockedMode=false"] = 0) "explicit consumer lock bootstrap"
    restore baselineApp "baseline"
    let baseline = assess baselineApp "baseline" None "ReadyForReview"
    inspect baseline (Some baselineApp) "baseline-current" 0
    let sdkConsumer = Path.Combine(baselineApp, "bin/Release/net10.0/Assay.Application.dll")
    require (hash sdkConsumer = hash (Path.Combine(toolkit, "Assay.Application/bin/Release/net10.0/Assay.Application.dll"))) "consumer did not load the packed SDK bytes"
    let redApp = cloneSources baselineApp "red-app"
    let manifest = read<Manifest>(Path.Combine(redApp, "assay.app.json"))
    write (Path.Combine(redApp, "assay.app.json")) {manifest with minimumPassed = 6}
    let requirements = read<Requirements>(Path.Combine(redApp, "requirements.json"))
    let added = {id="REFERENCE"; description="Blank references are rejected before approval.";testIds=[|"reference-blank"|];minimumPassed=1}
    write (Path.Combine(redApp, "requirements.json")) {requirements with requirements=Array.append requirements.requirements [|added|]}
    replace (Path.Combine(redApp, "AdditionalTests.fs")) "let cases : (string * string * (unit -> bool)) list = []" "let cases : (string * string * (unit -> bool)) list = [\n    \"reference-blank\", \"REFERENCE\", fun () -> decide { Reference = \" \"; AmountCents = 1250L } = Decision.Rejected Fault.MissingReference\n]"
    restore redApp "red"
    let red = assess redApp "red" (Some baseline) "NeedsRepair"
    retainedFailure <- Some red
    let greenApp = cloneSources redApp "green-app"
    replace (Path.Combine(greenApp, "Domain.fs")) "    if request.AmountCents <= 0L then" "    if System.String.IsNullOrWhiteSpace request.Reference then Decision.Rejected Fault.MissingReference\n    elif request.AmountCents <= 0L then"
    restore greenApp "green"
    let green = assess greenApp "green" (Some baseline) "ReadyForReview"
    inspect green (Some greenApp) "green-current" 0
    let weakOriginal = cloneSources greenApp "weakened-original-app"
    replace (Path.Combine(weakOriginal,"OriginalTests.fs")) "decide { Reference = \"invoice-1\"; AmountCents = 1L } = Decision.Approved(\"invoice-1\", 1L)" "true"
    restore weakOriginal "weakened-original"
    assess weakOriginal "weakened-original" (Some baseline) "ContextRequired" |> ignore
    let changedAcceptance = cloneSources greenApp "changed-acceptance-app"
    replace (Path.Combine(changedAcceptance,"AdditionalTests.fs")) "AmountCents = 1250L" "AmountCents = 1251L"
    restore changedAcceptance "changed-acceptance"
    assess changedAcceptance "changed-acceptance" (Some baseline) "ContextRequired" |> ignore
    let noFailureOutput = Path.Combine(output,"missing-red")
    require (run root "missing-red" [cli;"assess";Path.Combine(greenApp,"assay.app.json");engine;noFailureOutput;"--baseline";baseline] = 2) "green without retained red accepted"
    let alias = Path.Combine(output,"output-alias")
    Directory.CreateSymbolicLink(alias,greenApp) |> ignore
    let aliasOutput = Path.Combine(alias,"unsafe-evidence")
    require (run root "symlink-output" [cli;"assess";Path.Combine(greenApp,"assay.app.json");engine;aliasOutput] = 2 && not (Directory.Exists(Path.Combine(greenApp,"unsafe-evidence")))) "symlink output wrote into inputs"
    Directory.Delete alias
    let changedContract = cloneSources greenApp "changed-contract-app"
    File.AppendAllText(Path.Combine(changedContract, "contract.json"), " ")
    restore changedContract "changed-contract"
    assess changedContract "changed-contract" (Some baseline) "ContextRequired" |> ignore
    let reduced = cloneSources greenApp "reduced-tests-app"
    let requirements = read<Requirements>(Path.Combine(reduced, "requirements.json"))
    write (Path.Combine(reduced, "requirements.json")) {requirements with requirements=requirements.requirements |> Array.map(fun requirement -> if requirement.id="AMOUNT" then {requirement with minimumPassed=1;testIds=[|"amount-positive"|]} else requirement)}
    restore reduced "reduced-tests"
    assess reduced "reduced-tests" (Some baseline) "ContextRequired" |> ignore
    let missingNamed = cloneSources greenApp "missing-named-app"
    let requirements = read<Requirements>(Path.Combine(missingNamed, "requirements.json"))
    write (Path.Combine(missingNamed, "requirements.json")) {requirements with requirements=requirements.requirements |> Array.map(fun requirement -> if requirement.id="REFERENCE" then {requirement with testIds=[|"reference-blank";"reference-never-run"|]} else requirement)}
    restore missingNamed "missing-named"
    assess missingNamed "missing-named" (Some baseline) "ContextRequired" |> ignore
    assess greenApp "failed-baseline" (Some red) "ContextRequired" |> ignore
    let missingFile = cloneSources greenApp "missing-file-app"
    File.Delete(Path.Combine(missingFile, "Domain.fs"))
    require (run root "missing-source" [cli;"assess";Path.Combine(missingFile,"assay.app.json");engine;Path.Combine(output,"missing-source")] = 2) "missing source accepted"
    let malformed = cloneSources greenApp "malformed-app"
    replace (Path.Combine(malformed, "assay.app.json")) "\"schema\":" "\"schema\": \"duplicate\", \"schema\":"
    require (run root "duplicate-json" [cli;"assess";Path.Combine(malformed,"assay.app.json");engine;Path.Combine(output,"duplicate-json")] = 2) "duplicate JSON accepted"
    let undeclared = cloneSources greenApp "undeclared-app"
    File.WriteAllText(Path.Combine(undeclared,"Hidden.fs"), "module Hidden\nlet value=1\n")
    require (run root "undeclared-source" [cli;"assess";Path.Combine(undeclared,"assay.app.json");engine;Path.Combine(output,"undeclared-source")] = 2) "undeclared source accepted"
    let hiddenConfig = cloneSources greenApp "undeclared-config-app"
    File.WriteAllText(Path.Combine(hiddenConfig,".fsassayrc"), "{}")
    require (run root "undeclared-scan-config" [cli;"assess";Path.Combine(hiddenConfig,"assay.app.json");engine;Path.Combine(output,"undeclared-scan-config")] = 2) "ambient scan config accepted"
    let customBuild = cloneSources greenApp "custom-build-app"
    File.WriteAllText(Path.Combine(customBuild,"Directory.Build.targets"), "<Project><Import Project=\"/tmp/unbound.targets\"/></Project>")
    require (run root "unbound-build-import" [cli;"assess";Path.Combine(customBuild,"assay.app.json");engine;Path.Combine(output,"unbound-build-import")] = 2) "unbound build import accepted"
    let broken = cloneSources greenApp "broken-compiler-app"
    File.AppendAllText(Path.Combine(broken,"Domain.fs"), "\nlet broken = (\n")
    restore broken "broken-compiler"
    let brokenOutput = Path.Combine(output,"broken-compiler")
    let brokenExit = run root "broken-compiler" [cli;"assess";Path.Combine(broken,"assay.app.json");engine;brokenOutput]
    require (brokenExit <> 0) "compiler failure became ready"
    use brokenDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(brokenOutput,"assessment.json")))
    require (brokenDocument.RootElement.GetProperty("status").GetString() <> "ReadyForReview") "compiler failure acquired review readiness"
    let domain = Path.Combine(greenApp, "Domain.fs")
    let original = File.ReadAllBytes domain
    File.AppendAllText(domain, "\n// stale source control\n")
    inspect green (Some greenApp) "stale-source" 2
    File.WriteAllBytes(domain, original)
    let runtime = Path.Combine(greenApp, "bin/Release/net10.0/Approval.App.dll")
    let originalRuntime = File.ReadAllBytes runtime
    File.AppendAllText(runtime, "tampered runtime")
    inspect green (Some greenApp) "stale-runtime" 2
    File.WriteAllBytes(runtime, originalRuntime)
    let copied = Path.Combine(output,"tampered-evidence")
    let rec copyTree source destination =
        Directory.CreateDirectory destination |> ignore
        for file in Directory.GetFiles source do File.Copy(file,Path.Combine(destination,Path.GetFileName file))
        for directory in Directory.GetDirectories source do copyTree directory (Path.Combine(destination,Path.GetFileName directory))
    copyTree (Path.GetDirectoryName green) copied
    File.AppendAllText(Path.Combine(copied,"tests.json"), " ")
    inspect (Path.Combine(copied,"assessment.json")) None "tampered-report" 2
    File.Copy(Path.Combine(Path.GetDirectoryName green,"tests.json"),Path.Combine(copied,"tests.json"),true)
    File.Delete(Path.Combine(copied,"compiler.stdout.log"))
    inspect (Path.Combine(copied,"assessment.json")) None "missing-compiler-log" 2
    let forged = Path.Combine(output,"forged-ready")
    copyTree (Path.GetDirectoryName red) forged
    let forgedRun = Path.Combine(forged,"assessment.json")
    replace forgedRun "\"status\": \"NeedsRepair\"" "\"status\": \"ReadyForReview\""
    File.WriteAllText(Path.Combine(forged,"assessment.sha256"),hash forgedRun |> fun value -> value.ToLowerInvariant())
    inspect forgedRun None "forged-ready" 2
    let forgedEngine = Path.Combine(output,"forged-engine")
    copyTree (Path.GetDirectoryName green) forgedEngine
    let engineFile = Path.Combine(forgedEngine,"fsassay.json")
    let raw = JsonNode.Parse(File.ReadAllText engineFile).AsObject()
    raw["outcome"] <- JsonValue.Create("Fail")
    File.WriteAllText(engineFile,raw.ToJsonString(JsonSerializerOptions(WriteIndented=true)))
    let forgedFile = Path.Combine(forgedEngine,"assessment.json")
    let projection = JsonNode.Parse(File.ReadAllText forgedFile).AsObject()
    let digest = (hash engineFile).ToLowerInvariant()
    projection["engineReceiptSha256"] <- JsonValue.Create(digest)
    for binding in projection["artifacts"].AsArray() do
        if binding["path"].GetValue<string>() = "fsassay.json" then binding["sha256"] <- JsonValue.Create(digest)
    File.WriteAllText(forgedFile,projection.ToJsonString(JsonSerializerOptions(WriteIndented=true)))
    File.WriteAllText(Path.Combine(forgedEngine,"assessment.sha256"),(hash forgedFile).ToLowerInvariant())
    inspect forgedFile None "forged-engine-projection" 2
    let inspectForgery (name: string) (source: string) (change: string -> JsonObject -> unit) =
        let directory = Path.Combine(output,"reader-control-"+name)
        copyTree (Path.GetDirectoryName source) directory
        let file = Path.Combine(directory,"assessment.json")
        let record = JsonNode.Parse(File.ReadAllText file).AsObject()
        change directory record
        File.WriteAllText(file,record.ToJsonString(JsonSerializerOptions(WriteIndented=true)))
        File.WriteAllText(Path.Combine(directory,"assessment.sha256"),(hash file).ToLowerInvariant())
        inspect file None ("reader-"+name) 2
    inspectForgery "empty-producers" green (fun _ record -> record["producers"] <- JsonArray())
    inspectForgery "missing-fsc" green (fun _ record ->
        let entries = record["producers"].AsArray()
        for index in [entries.Count-1 .. -1 .. 0] do
            let entry = entries[index]
            let path = entry["path"].GetValue<string>()
            if path.EndsWith("/fsc.dll") then entries.RemoveAt index)
    inspectForgery "missing-app-runtime" green (fun _ record ->
        let entries = record["runtime"].AsArray()
        for index in [entries.Count-1 .. -1 .. 0] do
            let entry = entries[index]
            let path = entry["path"].GetValue<string>()
            if path.EndsWith("/Approval.App.dll") then entries.RemoveAt index)
    inspectForgery "missing-unregistered-log" green (fun directory record ->
        File.Delete(Path.Combine(directory,"compiler.stdout.log"))
        let entries = record["artifacts"].AsArray()
        for index in [entries.Count-1 .. -1 .. 0] do
            let entry = entries[index]
            if entry["path"].GetValue<string>()="compiler.stdout.log" then entries.RemoveAt index)
    inspectForgery "contradictory-current" green (fun _ record -> record["sourceCurrent"] <- JsonValue.Create(false); record["advice"] <- JsonValue.Create("ContextRequired"))
    inspectForgery "erased-baseline" green (fun _ record -> record["baselineSnapshot"] <- JsonValue.Create(""); record["hasBaseline"] <- JsonValue.Create(false))
    inspectForgery "red-diagnostic-collapse" red (fun _ record -> record["passed"] <- JsonValue.Create(600); record["failed"] <- JsonValue.Create(0); record["reasons"] <- JsonArray(); record["advice"] <- JsonValue.Create("NoInvestigation"))
    let engineForgery name change = inspectForgery name green (fun directory record ->
        let rawFile = Path.Combine(directory,"fsassay.json")
        let raw = JsonNode.Parse(File.ReadAllText rawFile).AsObject()
        change raw
        File.WriteAllText(rawFile,raw.ToJsonString(JsonSerializerOptions(WriteIndented=true)))
        let digest = (hash rawFile).ToLowerInvariant()
        record["engineReceiptSha256"] <- JsonValue.Create(digest)
        for binding in record["artifacts"].AsArray() do if binding["path"].GetValue<string>()="fsassay.json" then binding["sha256"] <- JsonValue.Create(digest))
    engineForgery "raw-empty-source" (fun raw -> raw["sources"] <- JsonArray(); raw["projects"] <- JsonArray())
    engineForgery "raw-policy-errors" (fun raw -> let section = raw["policy"] in section["status"] <- JsonValue.Create("failed"); raw["policyErrors"] <- JsonArray(JsonValue.Create("invalid-policy")))
    engineForgery "raw-wrong-sdk" (fun raw -> let section = raw["toolchain"] in section["sdkVersion"] <- JsonValue.Create("99.0.0"))
    engineForgery "raw-wrong-target" (fun raw -> let section = raw["candidate"] in section["repositoryRelativeTarget"] <- JsonValue.Create("Other.fsproj"))
    engineForgery "raw-wrong-test-id" (fun raw -> let test = raw["tests"].AsArray()[0] in test["id"] <- JsonValue.Create("unregistered-test"))
    inspect green (Some greenApp) "restored-current" 0
    require (sourceInputs = toolkitSnapshot ()) "toolkit source changed during qualification"
    completed <- true
finally
    write (Path.Combine(output,"qualification.json")) {|schema="fsharp-application-qualification/0.1";complete=completed;checks=results.ToArray();toolkitSources=sourceInputs;toolkitSourcesCurrent=(sourceInputs=toolkitSnapshot());authority="Application review only. Every real FsAssay receipt retains Inconclusive and authoritative=false; Gate C/admission unchanged."|}
printfn "FSharp application lifecycle complete=%b; evidence=%s" completed output
