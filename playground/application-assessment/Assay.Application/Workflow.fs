namespace FsAssay.Application

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions

[<CLIMutable>]
type Envelope = {
    schema: string; applicationId: string; status: string; reasons: string array; advice: string
    engineOutcome: string; engineAuthoritative: bool; engineReceiptSha256: string
    sourceSnapshotSha256: string; contractSha256: string; manifest: Manifest; requirements: Requirements
    sources: Binding array; runtime: Binding array; producers: Binding array; artifacts: Binding array
    passed: int; failed: int; testIds: string array; baselineSnapshot: string; sourceCurrent: bool
    enginePath: string; sdkDirectory: string; applicationRoot: string; evidenceRoot: string; afterSources: Binding array; afterRuntime: Binding array; afterProducers: Binding array
    hasBaseline: bool; hasFailure: bool; failureSnapshot: string
    authorityNote: string
}

[<RequireQualifiedAccess>]
module Workflow =
    let private options = JsonSerializerOptions(WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)
    let private hash (bytes: byte array) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()
    let private fileHash file = File.ReadAllBytes file |> hash
    let private uniqueJson (element: JsonElement) =
        let rec visit (value: JsonElement) =
            if value.ValueKind = JsonValueKind.Object then
                let names = Collections.Generic.HashSet<string>(StringComparer.Ordinal)
                for property in value.EnumerateObject() do
                    if not (names.Add property.Name) then failwith "duplicate JSON field"
                    visit property.Value
            elif value.ValueKind = JsonValueKind.Array then
                for item in value.EnumerateArray() do visit item
        visit element
    let private read<'a> file =
        let bytes = File.ReadAllBytes file
        use document = JsonDocument.Parse bytes
        uniqueJson document.RootElement
        JsonSerializer.Deserialize<'a>(bytes, options)
    let private write file value = File.WriteAllText(file, JsonSerializer.Serialize(value, options))
    let private noLinks (root: string) =
        let mutable current = Path.GetFullPath root
        while not (String.IsNullOrEmpty current) do
            if not (isNull (DirectoryInfo(current).LinkTarget)) || not (isNull (FileInfo(current).LinkTarget)) then failwith "symlinked context is unsupported"
            current <- Path.GetDirectoryName current
    let private safe (root: string) (path: string) =
        noLinks root
        if String.IsNullOrWhiteSpace path || Path.IsPathRooted path || path.Contains('\\') then failwith "invalid relative input path"
        let full = Path.GetFullPath(Path.Combine(root, path))
        if not (full.StartsWith(Path.GetFullPath(root) + string Path.DirectorySeparatorChar, StringComparison.Ordinal)) then failwith "input path escapes root"
        let mutable current = full
        while current <> Path.GetFullPath root do
            if not (isNull (FileInfo(current).LinkTarget)) || not (isNull (DirectoryInfo(current).LinkTarget)) then failwith "symlinked input is unsupported"
            current <- Path.GetDirectoryName current
        full
    let private bindings root paths = paths |> Array.sort |> Array.map (fun path -> { path = path; sha256 = safe root path |> fileHash })
    let private current root values =
        try values |> Array.forall (fun value -> fileHash (safe root value.path) = value.sha256)
        with _ -> false
    let private currentAbsolute values =
        try values |> Array.forall (fun value -> FileInfo(value.path).LinkTarget = null && fileHash value.path = value.sha256)
        with _ -> false
    let private snapshot values = values |> Array.map (fun value -> value.path + ":" + value.sha256) |> String.concat "\n" |> Encoding.UTF8.GetBytes |> hash
    let private validId value = not (String.IsNullOrWhiteSpace value) && Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9._-]*$")
    let private validateManifest root (manifestPath: string) (manifest: Manifest) (requirements: Requirements) =
        if manifestPath <> "assay.app.json" || manifest.schema <> "fsharp-application/0.2" || not (validId manifest.applicationId) || manifest.sdk <> "10.0.301" || manifest.minimumPassed < 1 || isNull manifest.files || manifest.files.Length = 0 then failwith "invalid manifest"
        if manifest.assemblyName <> Path.GetFileNameWithoutExtension manifest.project || not (manifest.project.EndsWith ".fsproj") then failwith "project/assembly identity mismatch"
        if isNull manifest.frozenTestFiles || manifest.frozenTestFiles.Length = 0 || isNull manifest.acceptanceTestFiles || manifest.acceptanceTestFiles.Length = 0 then failwith "explicit frozen/additional test file registries are required"
        let files = Set.ofArray manifest.files
        if files.Count <> manifest.files.Length || not (files.Contains manifestPath && files.Contains manifest.project && files.Contains manifest.requirements && files.Contains manifest.contract) then failwith "incomplete/duplicate manifest input registry"
        if Array.append manifest.frozenTestFiles manifest.acceptanceTestFiles |> Array.exists (files.Contains >> not) then failwith "test implementation files must be declared"
        if not (Set.intersect (Set.ofArray manifest.frozenTestFiles) (Set.ofArray manifest.acceptanceTestFiles)).IsEmpty then failwith "frozen and additive test files overlap"
        for required in [ "global.json"; "packages.lock.json"; "Directory.Build.props"; "Directory.Build.targets"; "Directory.Packages.props"; "NuGet.config"; "fsassay-policy.lock.json" ] do
            if not (files.Contains required) then failwith ("missing build/policy input: " + required)
        let rec inventory directory = [|
            for entry in Directory.EnumerateFileSystemEntries directory do
                let name = Path.GetFileName entry
                if not ([ "bin"; "obj"; ".git" ] |> List.contains name) then
                    if not (isNull (FileInfo(entry).LinkTarget)) || not (isNull (DirectoryInfo(entry).LinkTarget)) then failwith "symlink in application scope"
                    elif Directory.Exists entry then yield! inventory entry
                    elif ([ ".fs"; ".fsx"; ".fsproj"; ".json"; ".props"; ".targets"; ".config" ] |> List.contains (Path.GetExtension entry)) || [".fsassayrc";".editorconfig"] |> List.contains name then yield Path.GetRelativePath(root, entry)
        |]
        if Set.ofArray (inventory root) <> files then failwith "declared files do not match complete authored application scope"
        for input in manifest.files |> Array.filter (fun file -> [".fsproj";".props";".targets"] |> List.contains (Path.GetExtension file)) do
            let project = System.Xml.Linq.XDocument.Load(safe root input)
            for element in project.Descendants() do
                if ["ProjectReference";"Import";"UsingTask";"Target";"Exec"] |> List.contains element.Name.LocalName then failwith "external project/import/custom build target is unsupported"
                if element.Name.LocalName = "Compile" then
                    let includePath = element.Attribute(System.Xml.Linq.XName.Get "Include")
                    if not (isNull includePath) && not (files.Contains includePath.Value) then failwith "undeclared compiler input"
        if requirements.schema <> "fsharp-application-requirements/0.1" || isNull requirements.requirements || requirements.requirements.Length = 0 then failwith "invalid requirements"
        let ids = requirements.requirements |> Array.map _.id
        let tests = requirements.requirements |> Array.collect _.testIds
        if Set.ofArray ids |> Set.count <> ids.Length || Set.ofArray tests |> Set.count <> tests.Length then failwith "duplicate requirement/test identity"
        for requirement in requirements.requirements do
            if not (validId requirement.id) || String.IsNullOrWhiteSpace requirement.description || requirement.minimumPassed < 1 || isNull requirement.testIds || requirement.testIds.Length < requirement.minimumPassed || (requirement.testIds |> Array.exists (validId >> not)) then failwith "invalid requirement obligation"
        if manifest.minimumPassed < (requirements.requirements |> Array.sumBy _.minimumPassed) then failwith "manifest minimum is below required minima"

    type private ProcessResult = { exitCode: int; timedOut: bool; stdout: string; stderr: string; command: string array; workingDirectory: string }
    let private invoke root args =
        let start = ProcessStartInfo("dotnet", WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        for arg in args do start.ArgumentList.Add arg
        start.Environment["DOTNET_NOLOGO"] <- "1"
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        use child = new Process(StartInfo = start)
        child.Start() |> ignore
        let stdout, stderr = child.StandardOutput.ReadToEndAsync(), child.StandardError.ReadToEndAsync()
        let complete = child.WaitForExit 120000
        if not complete then child.Kill true
        child.WaitForExit()
        { exitCode = child.ExitCode; timedOut = not complete; stdout = stdout.GetAwaiter().GetResult(); stderr = stderr.GetAwaiter().GetResult(); command = Array.ofList ("dotnet" :: args); workingDirectory = root }
    let private record output name execution =
        File.WriteAllText(Path.Combine(output, name + ".stdout.log"), execution.stdout)
        File.WriteAllText(Path.Combine(output, name + ".stderr.log"), execution.stderr)
        write (Path.Combine(output, name + ".execution.json")) {| exitCode = execution.exitCode; timedOut = execution.timedOut; command = execution.command; workingDirectory = execution.workingDirectory |}
    let private runtimeBindings root directory =
        if not (Directory.Exists directory) then [||]
        else Directory.GetFiles(directory) |> Array.filter (fun file -> [ ".dll"; ".json" ] |> List.contains (Path.GetExtension file)) |> Array.map (fun file -> Path.GetRelativePath(root, file)) |> bindings root
    let private producerBindings (engine: string) (sdkPath: string) =
        [| yield! Directory.GetFiles(Path.GetDirectoryName engine) |> Array.filter (fun file -> [ ".dll"; ".json" ] |> List.contains (Path.GetExtension file))
           yield! Directory.GetFiles(AppContext.BaseDirectory) |> Array.filter (fun file -> [ ".dll"; ".json" ] |> List.contains (Path.GetExtension file))
           yield Path.Combine(sdkPath, "FSharp", "fsc.dll")
           yield Path.Combine(sdkPath, "MSBuild.dll")
           yield Environment.ProcessPath |]
        |> Array.distinct |> Array.sort |> Array.map (fun file -> { path = file; sha256 = fileHash file })


    type private Execution = { Exit: int; TimedOut: bool; Command: string array; Directory: string }
    let private observedExecution output name =
        let file = Path.Combine(output, name + ".execution.json")
        use document = JsonDocument.Parse(File.ReadAllBytes file)
        uniqueJson document.RootElement
        let value = document.RootElement
        if value.EnumerateObject() |> Seq.map _.Name |> Set.ofSeq <> set ["exitCode";"timedOut";"command";"workingDirectory"] then failwith "incomplete execution observation"
        { Exit = value.GetProperty("exitCode").GetInt32(); TimedOut = value.GetProperty("timedOut").GetBoolean(); Command = value.GetProperty("command").EnumerateArray() |> Seq.map _.GetString() |> Seq.toArray; Directory = value.GetProperty("workingDirectory").GetString() }
    let private canonicalElement (element: JsonElement) =
        let rec normalize (value: JsonElement) : string =
            match value.ValueKind with
            | JsonValueKind.Object -> value.EnumerateObject() |> Seq.sortBy _.Name |> Seq.map (fun item -> JsonSerializer.Serialize(item.Name) + ":" + normalize item.Value) |> String.concat "," |> fun body -> "{" + body + "}"
            | JsonValueKind.Array -> value.EnumerateArray() |> Seq.map normalize |> Seq.sort |> String.concat "," |> fun body -> "[" + body + "]"
            | _ -> value.GetRawText()
        normalize element
    let private engineUsable output (envelope: Envelope) =
        try
            let execution = observedExecution output "fsassay"
            use document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "fsassay.json")))
            use policyDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "snapshot/fsassay-policy.lock.json")))
            uniqueJson document.RootElement
            uniqueJson policyDocument.RootElement
            let receipt = document.RootElement
            let policy = receipt.GetProperty "policy"
            let policySnapshot = policy.GetProperty "snapshot"
            let policyBytes = JsonSerializer.SerializeToUtf8Bytes(policySnapshot, JsonSerializerOptions())
            let expectedSources = envelope.manifest.files |> Array.filter (fun file -> file.EndsWith(".fs", StringComparison.Ordinal)) |> Set.ofArray
            let sources = receipt.GetProperty("sources").EnumerateArray() |> Seq.toArray
            let analyzed = sources |> Array.filter (fun source -> source.GetProperty("disposition").GetString() = "analyzed")
            let projects = receipt.GetProperty("projects").EnumerateArray() |> Seq.toArray
            let reasons = receipt.GetProperty("reasons").EnumerateArray() |> Seq.map (fun reason -> reason.GetProperty("code").GetString()) |> Seq.toArray
            let counts = receipt.GetProperty "counts"
            let expectedTests = policySnapshot.GetProperty("requiredTests").EnumerateArray() |> Seq.toArray
            let actualTests = receipt.GetProperty("tests").EnumerateArray() |> Seq.toArray
            let candidate = receipt.GetProperty "candidate"
            let hex40 (field: string) = Regex.IsMatch(candidate.GetProperty(field).GetString(), "^[0-9a-f]{40}$")
            let empty (field: string) = receipt.GetProperty(field).GetArrayLength() = 0
            execution.Exit = 2 && not execution.TimedOut &&
            receipt.GetProperty("schemaVersion").GetString() = "fsassay-authority-receipt/1.1.0" && receipt.GetProperty("tool").GetString() = "FsAssay" && receipt.GetProperty("toolVersion").GetString() = "1.0.4" &&
            receipt.GetProperty("outcome").GetString() = "Inconclusive" && not (receipt.GetProperty("authoritative").GetBoolean()) &&
            receipt.GetProperty("toolchain").GetProperty("sdkVersion").GetString() = envelope.manifest.sdk &&
            not (candidate.GetProperty("dirty").GetBoolean()) && candidate.GetProperty("kind").GetString() = "commit" &&
            candidate.GetProperty("repositoryRelativeTarget").GetString() = envelope.manifest.project &&
            ["commitSha";"approvedHeadSha";"treeSha"] |> List.forall hex40 && candidate.GetProperty("commitSha").GetString() = candidate.GetProperty("approvedHeadSha").GetString() &&
            policy.GetProperty("status").GetString() = "loaded" && policy.GetProperty("path").GetString() = "fsassay-policy.lock.json" && policy.GetProperty("error").GetString() = "" &&
            canonicalElement policySnapshot = canonicalElement policyDocument.RootElement && policy.GetProperty("sha256").GetString() = hash policyBytes &&
            policySnapshot.GetProperty("approvedBlockingRules").GetArrayLength() = 0 && policySnapshot.GetProperty("advisoryRules").GetArrayLength() = 0 &&
            ["policyErrors";"evidenceErrors";"missingEvidence";"toolFailures"] |> List.forall empty &&
            reasons.Length > 0 && (reasons |> Array.forall ((=) "required-test-not-run")) &&
            projects.Length = 1 && projects[0].GetProperty("path").GetString() = envelope.manifest.project && projects[0].GetProperty("loaded").GetBoolean() && projects[0].GetProperty("supported").GetBoolean() && projects[0].GetProperty("status").GetString() = "loaded" &&
            (projects[0].GetProperty("targetFrameworks").EnumerateArray() |> Seq.map _.GetString() |> Set.ofSeq) = set ["net10.0"] &&
            (analyzed |> Array.map (fun source -> source.GetProperty("path").GetString()) |> Set.ofArray) = expectedSources && analyzed.Length = expectedSources.Count &&
            (sources |> Array.forall (fun source -> source.GetProperty("disposition").GetString() = "analyzed" || (source.GetProperty("disposition").GetString() = "policy-excluded" && source.GetProperty("path").GetString().StartsWith("obj/", StringComparison.Ordinal)))) &&
            ["projectsDiscovered";"projectsLoaded";"projectsWorkspaceLoaded";"projectsSupported"] |> List.forall (fun field -> counts.GetProperty(field).GetInt32() = 1) &&
            ["projectsFailed";"projectsSkipped";"projectsUnsupported";"compilerIncompleteFiles"] |> List.forall (fun field -> counts.GetProperty(field).GetInt32() = 0) &&
            counts.GetProperty("eligibleFiles").GetInt32() = expectedSources.Count && counts.GetProperty("analyzedFiles").GetInt32() = expectedSources.Count &&
            actualTests.Length = expectedTests.Length && actualTests.Length > 0 &&
            (actualTests |> Array.map (fun test -> test.GetProperty("id").GetString()) |> Set.ofArray) = (expectedTests |> Array.map (fun test -> test.GetProperty("id").GetString()) |> Set.ofArray) &&
            actualTests.Length = (actualTests |> Array.map (fun test -> test.GetProperty("id").GetString()) |> Set.ofArray).Count &&
            (receipt.GetProperty("tests").EnumerateArray() |> Seq.forall (fun test -> test.GetProperty("status").GetString() = "notRun" && test.GetProperty("project").GetString() = envelope.manifest.project && ["passed";"failed";"skipped"] |> List.forall (fun field -> test.GetProperty(field).GetInt32() = 0)))
        with _ -> false

    let private frozenDifferences (previous: Envelope) (current: Envelope) = [
        yield! Assessment.compatible previous.manifest previous.requirements previous.contractSha256 current.manifest current.requirements current.contractSha256
        for file in Array.append previous.manifest.frozenTestFiles [|"Directory.Build.props";"Directory.Build.targets";"Directory.Packages.props";"global.json";"NuGet.config";"packages.lock.json"|] do
            if Array.tryFind (fun value -> value.path = file) previous.sources <> Array.tryFind (fun value -> value.path = file) current.sources then yield "baseline-test-implementation-changed:" + file
    ]
    let private reconstruct output (envelope: Envelope) baseline failure =
        let missing, failures, errors = ResizeArray<string>(), ResizeArray<string>(), ResizeArray<string>()
        match baseline with
        | Some previous ->
            if previous.status <> "ReadyForReview" then missing.Add "baseline-not-ready"
            frozenDifferences previous envelope |> List.iter missing.Add
        | None -> ()
        match failure, baseline with
        | Some red, Some previous ->
            if red.status <> "NeedsRepair" || red.failed < 1 || red.baselineSnapshot <> previous.sourceSnapshotSha256 || red.applicationId <> envelope.applicationId then missing.Add "invalid-retained-failure"
            let oldIds = previous.requirements.requirements |> Array.map _.id |> Set.ofArray
            let newRequirements = envelope.requirements.requirements |> Array.filter (fun requirement -> not (oldIds.Contains requirement.id))
            let redTests = read<TestReport>(Path.Combine(output,"failure/tests.json"))
            let newFailed = newRequirements |> Array.exists (fun requirement -> redTests.tests |> Array.exists (fun test -> test.requirementId = requirement.id && Array.contains test.id requirement.testIds && not test.passed))
            let oldPassed = redTests.tests |> Array.filter (fun test -> oldIds.Contains test.requirementId) |> Array.forall _.passed
            if not newFailed || not oldPassed then missing.Add "retained-failure-is-not-new-acceptance"
            if JsonSerializer.Serialize(red.requirements, options) <> JsonSerializer.Serialize(envelope.requirements, options) then missing.Add "retained-failure-requirements-changed"
            for file in envelope.manifest.acceptanceTestFiles do
                if Array.tryFind (fun input -> input.path = file) red.sources <> Array.tryFind (fun input -> input.path = file) envelope.sources then missing.Add("acceptance-test-changed-after-failure:" + file)
        | Some _, None -> missing.Add "retained-failure-needs-baseline"
        | _ -> ()
        let compiler = observedExecution output "compiler"
        if compiler.Command <> [|"dotnet";"build";envelope.manifest.project;"--no-restore";"-c";"Release"|] || compiler.Directory <> envelope.applicationRoot then missing.Add "compiler-command-context-mismatch"
        if compiler.TimedOut then missing.Add "compiler-timeout"
        elif compiler.Exit <> 0 then failures.Add "compiler-failed"
        let mutable tests: NamedTest array = [||]
        let mutable failedRequirement = ""
        if compiler.Exit = 0 && not compiler.TimedOut then
            let execution = observedExecution output "tests"
            if execution.Command <> [|"dotnet";Path.Combine(envelope.applicationRoot,"bin/Release/net10.0",envelope.manifest.assemblyName+".dll");"--assay-tests";Path.Combine(envelope.evidenceRoot,"tests.json")|] || execution.Directory <> envelope.applicationRoot then missing.Add "test-command-context-mismatch"
            if execution.TimedOut then missing.Add "tests-timeout"
            elif not (File.Exists(Path.Combine(output,"tests.json"))) then missing.Add "test-report-missing"
            else
                try
                    let report = read<TestReport>(Path.Combine(output,"tests.json"))
                    if report.schema <> "fsharp-application-tests/0.1" || isNull report.tests then failwith "invalid test schema"
                    tests <- report.tests
                    let ids = tests |> Array.map _.id |> Set.ofArray
                    let expected = envelope.requirements.requirements |> Array.collect _.testIds |> Set.ofArray
                    if ids.Count <> tests.Length || ids <> expected then missing.Add "named-test-selection-mismatch"
                    for requirement in envelope.requirements.requirements do
                        let selected = tests |> Array.filter (fun test -> test.requirementId = requirement.id)
                        if (selected |> Array.map _.id |> Set.ofArray) <> Set.ofArray requirement.testIds then missing.Add("test-requirement-binding-mismatch:" + requirement.id)
                        if (selected |> Array.filter _.passed |> Array.length) < requirement.minimumPassed then failures.Add("required-test-minimum:" + requirement.id)
                    let failed = tests |> Array.filter (fun test -> not test.passed)
                    if failed.Length > 0 then failedRequirement <- failed[0].requirementId; failures.Add "required-tests-failed"
                    if failed.Length = 0 && tests.Length < envelope.manifest.minimumPassed then failures.Add "required-test-minimum"
                    if (execution.Exit = 0) <> (failed.Length = 0) || not (List.contains execution.Exit [0;1]) then missing.Add "test-exit-report-disagreement"
                with error -> missing.Add("test-report-invalid:" + error.Message)
        let engineExecution = observedExecution output "fsassay"
        if engineExecution.Command <> [|"dotnet";envelope.enginePath;"--out-json";Path.Combine(envelope.evidenceRoot,"fsassay.json");Path.Combine(envelope.applicationRoot,envelope.manifest.project)|] || engineExecution.Directory <> envelope.applicationRoot then missing.Add "engine-command-context-mismatch"
        let mutable engineOutcome, engineAuthority = "NotAvailable", false
        if engineExecution.TimedOut then missing.Add "engine-timeout"
        elif not (File.Exists(Path.Combine(output,"fsassay.json"))) then errors.Add "engine-receipt-missing"
        else
            try
                use document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output,"fsassay.json")))
                uniqueJson document.RootElement
                engineOutcome <- document.RootElement.GetProperty("outcome").GetString()
                engineAuthority <- document.RootElement.GetProperty("authoritative").GetBoolean()
                if engineOutcome = "Fail" then failures.Add "fsassay-failed"
                elif engineOutcome = "ToolFailure" || engineExecution.Exit = 3 then errors.Add "fsassay-tool-failure"
                elif not (engineUsable output envelope) then missing.Add "fsassay-unexpected-evidence-state"
            with error -> errors.Add("engine-receipt-invalid:" + error.Message)
        let fresh = envelope.sources = envelope.afterSources && envelope.runtime = envelope.afterRuntime && envelope.producers = envelope.afterProducers
        let additive = baseline |> Option.exists (fun previous -> envelope.requirements.requirements |> Array.exists (fun requirement -> previous.requirements.requirements |> Array.forall (fun old -> old.id <> requirement.id)))
        if additive && failure.IsNone && missing.Count = 0 && failures.Count = 0 && errors.Count = 0 && fresh then missing.Add "retained-failing-acceptance-required"
        let status = Assessment.evaluate {Missing=List.ofSeq missing;Failures=List.ofSeq failures;ToolErrors=List.ofSeq errors;SourceCurrent=fresh;EngineUsable=engineUsable output envelope}
        let statusName, reasons = Assessment.describe status
        let registry = envelope.requirements.requirements |> Array.map (fun requirement -> {id="boundary-"+requirement.id;requirementId=requirement.id}) |> Array.toList
        let advice = Assistance.select status (baseline |> Option.forall (fun previous -> previous.status="ReadyForReview")) envelope.sourceSnapshotSha256 envelope.sourceSnapshotSha256 3 registry failedRequirement ("boundary-"+failedRequirement) |> Assistance.describe
        statusName,reasons,advice,(tests |> Array.filter _.passed |> Array.length),(tests |> Array.filter (fun test -> not test.passed) |> Array.length),(tests |> Array.map _.id),fresh,engineOutcome,engineAuthority

    let private requiredArtifacts output (envelope: Envelope) =
        let compiler = observedExecution output "compiler"
        [| yield! envelope.manifest.files |> Array.map (fun file -> "snapshot/" + file)
           for stage in ["sdk";"compiler";"fsassay"] do
               for suffix in [".stdout.log";".stderr.log";".execution.json"] do yield stage + suffix
           if File.Exists(Path.Combine(output,"fsassay.json")) then yield "fsassay.json"
           if compiler.Exit = 0 && not compiler.TimedOut then
               for suffix in [".stdout.log";".stderr.log";".execution.json"] do yield "tests" + suffix
               if File.Exists(Path.Combine(output,"tests.json")) then yield "tests.json"
           yield! envelope.runtime |> Array.map (fun input -> "runtime-snapshot/" + Path.GetFileName input.path)
        |] |> Set.ofArray
    let private validateInventories output (envelope: Envelope) =
        if isNull envelope.producers || isNull envelope.runtime || isNull envelope.artifacts || isNull envelope.afterSources || isNull envelope.afterRuntime || isNull envelope.afterProducers then failwith "required observation maps are missing"
        if not (Path.IsPathRooted envelope.enginePath) || Path.GetFileName(envelope.enginePath) <> "FsAssay.Runner.dll" || Path.GetFileName(envelope.sdkDirectory) <> envelope.manifest.sdk then failwith "producer role identity is invalid"
        let expectedProducers = producerBindings envelope.enginePath envelope.sdkDirectory
        if envelope.producers <> expectedProducers then failwith "producer/compiler binding inventory differs from current expected roles"
        let expectedRuntime = [envelope.manifest.assemblyName+".dll";envelope.manifest.assemblyName+".deps.json";envelope.manifest.assemblyName+".runtimeconfig.json";"Assay.Application.dll";"FSharp.Core.dll"] |> Set.ofList
        let compiler = observedExecution output "compiler"
        if compiler.Exit = 0 && not compiler.TimedOut && (envelope.runtime |> Array.map (fun input -> Path.GetFileName input.path) |> Set.ofArray) <> expectedRuntime then failwith "runtime binding inventory is incomplete"
        if envelope.runtime.Length <> (envelope.runtime |> Array.map _.path |> Set.ofArray).Count then failwith "duplicate runtime binding"
        for input in envelope.runtime do
            if input.path <> "bin/Release/net10.0/" + Path.GetFileName input.path || fileHash(safe output ("runtime-snapshot/" + Path.GetFileName input.path)) <> input.sha256 then failwith "runtime snapshot differs from bound binary"
        let actual = Directory.GetFiles(output,"*",SearchOption.AllDirectories) |> Array.map (fun file -> Path.GetRelativePath(output,file)) |> Array.filter (fun file -> file <> "assessment.json" && file <> "assessment.sha256") |> Set.ofArray
        let registered = envelope.artifacts |> Array.map _.path |> Set.ofArray
        if registered.Count <> envelope.artifacts.Length || actual <> registered || not (Set.isSubset (requiredArtifacts output envelope) registered) then failwith "required or complete artifact/log inventory is missing"
        if envelope.hasBaseline <> Directory.Exists(Path.Combine(output,"baseline")) || envelope.hasFailure <> Directory.Exists(Path.Combine(output,"failure")) then failwith "lineage archive cannot be erased from its projection"

    let rec inspect (run: string) (sourceRoot: string option) =
        try
            let run = Path.GetFullPath run
            let output = Path.GetDirectoryName run
            if fileHash run <> File.ReadAllText(Path.Combine(output,"assessment.sha256")).Trim() then failwith "assessment checksum mismatch"
            let envelope = read<Envelope> run
            if envelope.schema <> "fsharp-application-assessment/0.2" then failwith "unsupported assessment schema"
            if envelope.applicationId <> envelope.manifest.applicationId || not (Path.IsPathRooted envelope.applicationRoot && Path.IsPathRooted envelope.evidenceRoot) then failwith "application/context identity projection mismatch"
            validateManifest (Path.Combine(output,"snapshot")) "assay.app.json" envelope.manifest envelope.requirements
            validateInventories output envelope
            if not (current output envelope.artifacts) then failwith "artifact missing or modified"
            if snapshot envelope.sources <> envelope.sourceSnapshotSha256 || bindings (Path.Combine(output,"snapshot")) envelope.manifest.files <> envelope.sources then failwith "source snapshot/content mismatch"
            if JsonSerializer.Serialize(read<Manifest>(Path.Combine(output,"snapshot/assay.app.json")),options) <> JsonSerializer.Serialize(envelope.manifest,options) || JsonSerializer.Serialize(read<Requirements>(Path.Combine(output,"snapshot",envelope.manifest.requirements)),options) <> JsonSerializer.Serialize(envelope.requirements,options) then failwith "manifest/requirement projection mismatch"
            if fileHash(Path.Combine(output,"snapshot",envelope.manifest.contract)) <> envelope.contractSha256 then failwith "contract projection mismatch"
            if sourceRoot |> Option.exists (fun root -> not (current root envelope.sources && current root envelope.runtime)) then failwith "source/runtime is stale"
            let lineage name present snapshotIdentity =
                if not present then
                    if snapshotIdentity <> "" then failwith "unexpected lineage identity"
                    None
                else
                    match inspect (Path.Combine(output,name,"assessment.json")) None with
                    | Error reason -> failwith (name + " archive invalid:" + reason)
                    | Ok previous when previous.sourceSnapshotSha256 <> snapshotIdentity -> failwith "lineage snapshot projection mismatch"
                    | Ok previous -> Some previous
            let baseline = lineage "baseline" envelope.hasBaseline envelope.baselineSnapshot
            let failure = lineage "failure" envelope.hasFailure envelope.failureSnapshot
            let status,reasons,advice,passed,failed,ids,fresh,engineOutcome,engineAuthority = reconstruct output envelope baseline failure
            if (envelope.status,envelope.reasons,envelope.advice,envelope.passed,envelope.failed,envelope.testIds,envelope.sourceCurrent,envelope.engineOutcome,envelope.engineAuthoritative) <> (status,reasons,advice,passed,failed,ids,fresh,engineOutcome,engineAuthority) then failwith "assessment status/diagnostics/authority projection differs from raw observations"
            if envelope.engineReceiptSha256 <> (if File.Exists(Path.Combine(output,"fsassay.json")) then fileHash(Path.Combine(output,"fsassay.json")) else "") then failwith "engine receipt projection mismatch"
            Ok envelope
        with error -> Error error.Message

    let assess (manifestFile: string) (engine: string) (output: string) (baselineFile: string option) (failureFile: string option) =
        let manifestFile,engine,output = Path.GetFullPath manifestFile,Path.GetFullPath engine,Path.GetFullPath output
        let root = Path.GetDirectoryName manifestFile
        noLinks root
        noLinks output
        if Directory.Exists output || File.Exists output then failwith "output already exists"
        let mutable ancestor = output
        while not (String.IsNullOrEmpty ancestor) do
            if not (isNull (DirectoryInfo(ancestor).LinkTarget)) || not (isNull (FileInfo(ancestor).LinkTarget)) then failwith "symlinked output context is unsupported"
            ancestor <- Path.GetDirectoryName ancestor
        if output.StartsWith(root+string Path.DirectorySeparatorChar,StringComparison.Ordinal) || root.StartsWith(output+string Path.DirectorySeparatorChar,StringComparison.Ordinal) then failwith "evidence output overlaps application scope"
        let manifest = read<Manifest> manifestFile
        let requirements = read<Requirements>(safe root manifest.requirements)
        validateManifest root (Path.GetFileName manifestFile) manifest requirements
        let sources = bindings root manifest.files
        let loadLineage = Option.map (fun file -> match inspect file None with Ok value -> value | Error reason -> failwith("lineage unavailable:"+reason))
        let baseline,failure = loadLineage baselineFile,loadLineage failureFile
        let version = invoke root ["--version"]
        if version.exitCode <> 0 || version.stdout.Trim() <> manifest.sdk then failwith "exact pinned SDK unavailable"
        let sdks = invoke root ["--list-sdks"]
        let sdk = Regex.Match(sdks.stdout,"(?m)^"+Regex.Escape(manifest.sdk)+" \[(.+)\]$")
        if not sdk.Success then failwith "compiler SDK root unavailable"
        let sdkDirectory = Path.Combine(sdk.Groups[1].Value,manifest.sdk)
        let producers = producerBindings engine sdkDirectory
        Directory.CreateDirectory output |> ignore
        let archive name file previous =
            match file,previous with
            | Some file,Some (previous: Envelope) ->
                let previousOutput = Path.GetDirectoryName(Path.GetFullPath file)
                for path in Array.append (previous.artifacts |> Array.map _.path) [|"assessment.json";"assessment.sha256"|] do
                    let target = safe output (name+"/"+path)
                    Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
                    File.Copy(safe previousOutput path,target)
            | _ -> ()
        archive "baseline" baselineFile baseline
        archive "failure" failureFile failure
        for source in sources do
            let target = safe output ("snapshot/"+source.path)
            Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
            File.Copy(safe root source.path,target)
        record output "sdk" version
        let build = invoke root ["build";manifest.project;"--no-restore";"-c";"Release"]
        record output "compiler" build
        let runtimeDirectory = Path.Combine(root,"bin/Release/net10.0")
        let runtime = if build.exitCode = 0 && not build.timedOut then runtimeBindings root runtimeDirectory else [||]
        Directory.CreateDirectory(Path.Combine(output,"runtime-snapshot")) |> ignore
        for input in runtime do File.Copy(safe root input.path,Path.Combine(output,"runtime-snapshot",Path.GetFileName input.path))
        if build.exitCode = 0 && not build.timedOut then
            invoke root [Path.Combine(runtimeDirectory,manifest.assemblyName+".dll");"--assay-tests";Path.Combine(output,"tests.json")] |> record output "tests"
        invoke root [engine;"--out-json";Path.Combine(output,"fsassay.json");safe root manifest.project] |> record output "fsassay"
        let afterSources = try bindings root manifest.files with _ -> [||]
        let afterRuntime = try runtimeBindings root runtimeDirectory with _ -> [||]
        let afterProducers = try producerBindings engine sdkDirectory with _ -> [||]
        let initial = { schema="fsharp-application-assessment/0.2";applicationId=manifest.applicationId;status="ContextRequired";reasons=[||];advice="ContextRequired"
                        engineOutcome="NotAvailable";engineAuthoritative=false;engineReceiptSha256=if File.Exists(Path.Combine(output,"fsassay.json")) then fileHash(Path.Combine(output,"fsassay.json")) else ""
                        sourceSnapshotSha256=snapshot sources;contractSha256=fileHash(safe root manifest.contract);manifest=manifest;requirements=requirements
                        sources=sources;runtime=runtime;producers=producers;artifacts=[||];passed=0;failed=0;testIds=[||]
                        baselineSnapshot=baseline |> Option.map _.sourceSnapshotSha256 |> Option.defaultValue "";failureSnapshot=failure |> Option.map _.sourceSnapshotSha256 |> Option.defaultValue ""
                        hasBaseline=baseline.IsSome;hasFailure=failure.IsSome;enginePath=engine;sdkDirectory=sdkDirectory;applicationRoot=root;evidenceRoot=output;afterSources=afterSources;afterRuntime=afterRuntime;afterProducers=afterProducers
                        sourceCurrent=false;authorityNote="Separate application review evidence. FsAssay receipt and Gate-C admission are unchanged; no verification grant." }
        let status,reasons,advice,passed,failed,ids,fresh,engineOutcome,engineAuthority = reconstruct output initial baseline failure
        let artifacts = Directory.GetFiles(output,"*",SearchOption.AllDirectories) |> Array.map (fun file -> Path.GetRelativePath(output,file)) |> bindings output
        let envelope = {initial with status=status;reasons=reasons;advice=advice;passed=passed;failed=failed;testIds=ids;sourceCurrent=fresh;engineOutcome=engineOutcome;engineAuthoritative=engineAuthority;artifacts=artifacts}
        write (Path.Combine(output,"assessment.json")) envelope
        File.WriteAllText(Path.Combine(output,"assessment.sha256"),fileHash(Path.Combine(output,"assessment.json")))
        envelope
