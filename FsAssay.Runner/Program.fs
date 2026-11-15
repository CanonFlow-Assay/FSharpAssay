#line 1 "/_/FsAssay.Runner/Program.fs"

open System
open System.IO
open System.Text.RegularExpressions
open FsAssay.Runner
open FSharp.Analyzers.SDK
open FsAssay.Analyzers.Domain
open Argu

type Arguments = // EXPECT: FSA-AI17 // EXPECT: FSA-AI11
    | [<MainCommand; Last>] Target of path:string
    | Assessment_Json of path:string
    | Format of name:string
    | Test_Artifacts of path:string
    | [<AltCommandLine("-j")>] Out_Json of path:string
    | [<AltCommandLine("-s")>] Out_Sarif of path:string
    | [<AltCommandLine("-t")>] Out_Toolchain of path:string
    | [<AltCommandLine("-r")>] RateCard_Md of path:string
    | [<AltCommandLine("-m")>] Material_Html of path:string
    | [<AltCommandLine("-x")>] SuppressionReport_Json of path:string
    | [<AltCommandLine("-w")>] Watch
    | [<AltCommandLine("-d")>] Diff of gitRef:string
    | [<AltCommandLine("-p")>] Serve of port:int
    | [<AltCommandLine("-a")>] Adjudicate
    | [<AltCommandLine("-c")>] Files of paths:string
    | [<AltCommandLine("-P")>] Profile of profileName:string
    | [<AltCommandLine("-f")>] Fix
    | [<AltCommandLine("-mcp")>] Mcp
    | [<AltCommandLine("-docs")>] Docs of dir:string
    | [<CustomCommandLine("--plugin")>] Plugin of paths:string
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Assessment_Json _ -> "Write the additive native assessment envelope."
                | Format _ -> "Console output: text or msbuild."
                | Test_Artifacts _ -> "New evidence directory for required-test execution."
                | Target _ -> "Target directory or file to scan."
                | Out_Json _ -> "Output file path for canonical JSON."
                | Out_Sarif _ -> "Output file path for SARIF."
                | Out_Toolchain _ -> "Output file path for toolchain record."
                | RateCard_Md _ -> "Output file path for Markdown Code Quality Rate Card."
                | Material_Html _ -> "Output file path for Material Design 5 HTML Dashboard."
                | SuppressionReport_Json _ -> "Output file path for explicit suppression report."
                | Watch -> "Watch directory for file changes and re-run scans continuously."
                | Diff _ -> "Compare quality findings against a Git reference branch."
                | Serve _ -> "Start live Material Design 5 HTML dashboard web server on specified port."
                | Adjudicate -> "Run in adjudication mode (evaluate Precision/Recall against // EXPECT comments)."
                | Files _ -> "Comma-separated list of explicit files to scan (Incremental mode)."
                | Profile _ -> "Specify active domain profile (core, interop, cli, etl, test, script)."
                | Fix -> "Automatically apply recommended fixes to source files."
                | Mcp -> "Start Model Context Protocol (MCP) JSON-RPC server on stdio."
                | Docs _ -> "Generate markdown documentation for all rules to specified directory."
                | Plugin _ -> "Path to a compiled assembly (.dll) containing custom F# analyzers."

[<EntryPoint>]
let main originalArguments =
    let mode, argv =
        if originalArguments.Length > 0 && Set.contains originalArguments.[0] (set ["check"; "verify"; "assess"]) then originalArguments.[0], originalArguments.[1..]
        else "check", originalArguments
    let invocationRoot = Directory.GetCurrentDirectory()
    let invocation = System.Text.Json.JsonSerializer.Serialize({| workingDirectory = invocationRoot; executable = Environment.ProcessPath; arguments = originalArguments |})
    if originalArguments.Length > 0 && originalArguments.[0] = "security" then
        if originalArguments.Length <> 3 then
            eprintfn "ERROR: usage: fsassay security <request.json> <fresh-output>"
            Environment.Exit ExitCodes.InvalidInvocation
        Environment.Exit(SecurityEvidence.run originalArguments.[1] originalArguments.[2])
    let parser = ArgumentParser.Create<Arguments>(programName = "fsassay") // EXPECT: FSA-AI17

    let printHelp () =
        printf "%s" (parser.PrintUsage())
        printfn ""
        printfn "COMMANDS:"
        printfn ""
        printfn "    check|verify|assess   Analyze; verify/assess also execute registered required tests."
        printfn "    help                  Display this help. Aliases: --help, -h."
        printfn "    doctor                Report the local toolchain and offline-default posture."
        printfn "    explain <RULE>        Explain one existing catalogue rule and its M3 class."
        printfn ""
        printfn "ANALYSIS:"
        printfn ""
        printfn "    Put the target path last. Default analysis is the check/strict authority path;"
        printfn "    use --out-json and --out-sarif for the four-state evidence receipt."

    if argv.Length = 1 && (argv.[0] = "help" || argv.[0] = "--help" || argv.[0] = "-h") then
        printHelp ()
        Environment.Exit(ExitCodes.Success)

    if argv.Length > 0 && argv.[0] = "help" then
        eprintfn "ERROR: 'help' does not accept arguments."
        Environment.Exit(ExitCodes.InvalidInvocation)

    if argv.Length = 1 && argv.[0] = "doctor" then
        try
            let output, errors = Doctor.inspect invocationRoot
            printfn "%s" output
            Environment.Exit(if errors.IsEmpty then ExitCodes.Success else ExitCodes.ToolFailure)
        with error ->
            eprintfn "FsAssay doctor: ToolFailure; %s" error.Message
            Environment.Exit(ExitCodes.ToolFailure)

    if argv.Length > 0 && argv.[0] = "doctor" then
        eprintfn "ERROR: 'doctor' does not accept arguments."
        Environment.Exit(ExitCodes.InvalidInvocation)

    if argv.Length = 2 && argv.[0] = "explain" then
        let requestedCode = argv.[1].ToUpperInvariant()
        match FsAssay.Analyzers.Domain.Rule.AllRules |> List.tryFind (fun rule -> rule.Code = requestedCode) with
        | Some rule ->
            let implementationStatus, admissionClass =
                match rule.Status with
                | FsAssay.Analyzers.Domain.Implemented -> "implemented", "experimental"
                | FsAssay.Analyzers.Domain.Delegated _ -> "delegated", "experimental"
                | FsAssay.Analyzers.Domain.Prototype -> "prototype", "prototype"
                | FsAssay.Analyzers.Domain.Dummy -> "dummy", "dummy"
                | FsAssay.Analyzers.Domain.Proposed -> "proposed", "unavailable"
            printfn "Rule: %s" rule.Code
            printfn "Message: %s" rule.Message
            printfn "Severity: %A" rule.Severity
            printfn "ImplementationStatus: %s" implementationStatus
            printfn "M3AdmissionClass: %s" admissionClass
            printfn "Explanation: %s" rule.Explanation
            let guidance, example = RuleGuidance.explain rule.Code
            printfn "Repair guidance: %s" guidance
            printfn "Example: %s" example
            printfn "Authority: non-authoritative; M3 admits zero blocking and zero advisory rules."
            Environment.Exit(ExitCodes.Success)
        | None ->
            eprintfn "ERROR: unknown rule '%s'." argv.[1]
            Environment.Exit(ExitCodes.InvalidInvocation)

    if argv.Length > 0 && argv.[0] = "explain" then
        eprintfn "ERROR: usage: fsassay explain <RULE>."
        Environment.Exit(ExitCodes.InvalidInvocation)

    let knownOptions =
        set [
            "--assessment-json"; "--format"; "--test-artifacts"; "--out-json"; "-j"; "--out-sarif"; "-s"; "--out-toolchain"; "-t"
            "--ratecard-md"; "-r"; "--material-html"; "-m"; "--suppressionreport-json"; "-x"
            "--watch"; "-w"; "--diff"; "-d"; "--serve"; "-p"; "--adjudicate"; "-a"
            "--files"; "-c"; "--profile"; "-P"; "--fix"; "-f"; "--mcp"; "-mcp"
            "--docs"; "-docs"; "--plugin"; "--help"; "-h"
        ]

    match argv |> Array.tryFind (fun argument -> argument.StartsWith("-") && not (knownOptions.Contains argument)) with
    | Some unknownOption ->
        eprintfn "ERROR: unknown option '%s'." unknownOption
        Environment.Exit(ExitCodes.InvalidInvocation)
    | None -> ()

    let results =
        try
            parser.ParseCommandLine argv
        with e ->
            eprintfn "%s" e.Message // EXPECT: FSA-F04
            Environment.Exit(ExitCodes.InvalidInvocation) // EXPECT: FSA-F04
            failwith "" // EXPECT: FSA-C06

    if results.Contains(Mcp) then // EXPECT: FSA-F04
        FsAssay.Runner.McpServer.run () // EXPECT: FSA-F04
        Environment.Exit(ExitCodes.Success)

    match results.TryGetResult(Docs) with // EXPECT: FSA-F04
    | Some dir ->
        FsAssay.Runner.DocsGen.generateDocs dir // EXPECT: FSA-F04
        Environment.Exit(ExitCodes.Success)
    | None -> ()

    let path = results.GetResult(Target, defaultValue = Directory.GetCurrentDirectory()) // EXPECT: FSA2022
    let path = Path.GetFullPath path
    let rawConfig, configErrors =
        match Config.tryLoadConfig path with
        | Ok config -> config, []
        | Error error -> {Config.defaultConfig with directory = (if Directory.Exists path then path else Path.GetDirectoryName path)}, ["Invalid .fsassayrc: " + error]
    let activeProfile = results.GetResult(Profile, defaultValue = rawConfig.profile)
    let config = { rawConfig with profile = activeProfile }
    let configErrors = if Config.profiles.Contains activeProfile then configErrors else configErrors @ ["unknown profile: " + activeProfile]
    let consoleFormat = results.GetResult(Format, defaultValue = "text")
    if consoleFormat <> "text" && consoleFormat <> "msbuild" then
        eprintfn "ERROR: --format must be text or msbuild."
        Environment.Exit ExitCodes.InvalidInvocation
    if mode = "check" && results.Contains(Test_Artifacts) then
        eprintfn "ERROR: --test-artifacts requires verify or assess."
        Environment.Exit ExitCodes.InvalidInvocation

    let rec findAuthorityPolicy (directory: string) =
        let candidate = Path.Combine(directory, "fsassay-policy.lock.json")
        if File.Exists(candidate) then Some candidate
        else
            match Directory.GetParent(directory) with
            | null -> None
            | parent -> findAuthorityPolicy parent.FullName

    let policySearchDirectory =
        if Directory.Exists(path) then Path.GetFullPath(path)
        elif String.IsNullOrWhiteSpace(Path.GetDirectoryName(Path.GetFullPath(path))) then Directory.GetCurrentDirectory()
        else Path.GetDirectoryName(Path.GetFullPath(path))

    let policyPath = findAuthorityPolicy policySearchDirectory |> Option.defaultValue (Path.Combine(policySearchDirectory, "fsassay-policy.lock.json"))
    let policy, policyHash, policyErrors =
        match Authority.loadPolicy policyPath with
        | Authority.PolicyLoaded (loaded, hash, _) -> loaded, hash, []
        | Authority.PolicyUnavailable message -> Authority.unapprovedPolicy, "unavailable", [ message ]
        | Authority.PolicyInvalid (_, message) -> Authority.unapprovedPolicy, "invalid", [ message ]
    let repositoryRoot =
        if File.Exists(policyPath) then Path.GetDirectoryName(policyPath)
        else policySearchDirectory
    let discoveredProjects = ProjectSystem.discoverProjectPaths path
    let boundaryErrors = FsAssay.Analyzers.BoundaryPolicy.validateGraph config.boundaries (discoveredProjects |> List.map (fun project -> Path.GetRelativePath(config.directory, project)))
    let configErrors = configErrors @ boundaryErrors
    let initialInputs, snapshotErrors =
        try Assessment.capture repositoryRoot, []
        with error -> [||], [error.Message]
    let mutable loadedProjectPaths = Set.empty<string>
    let mutable projectLoadFailures: string list = []

    printfn "🧪 FsAssay Engine v%s — Scanning target: %s [Profile: %s]" ProductIdentity.Version path config.profile // EXPECT: FSA-F04
    
    let pluginPaths =
        match results.TryGetResult(Plugin) with
        | Some p -> p.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
        | None -> []
    
    let (cliPlugins, editorPlugins, pluginLoadFailures) = 
        try 
            PluginLoader.loadPlugins pluginPaths
        with _ -> 
            ([], [], ["Failed to load plugins"])

    if not (List.isEmpty pluginPaths) then // EXPECT: FSA-F04
        printfn "🔌 Loaded %d CLI plugins and %d Editor plugins." cliPlugins.Length editorPlugins.Length
    
    let typedProfile =
        match config.profile.ToLowerInvariant() with
        | "shell" -> FsAssay.Analyzers.Domain.Profile.Shell
        | "oracle" -> FsAssay.Analyzers.Domain.Profile.Oracle
        | "api" -> FsAssay.Analyzers.Domain.Profile.Api
        | "test" -> FsAssay.Analyzers.Domain.Profile.Test
        | "script" -> FsAssay.Analyzers.Domain.Profile.Script
        | "interop" -> FsAssay.Analyzers.Domain.Profile.Interop
        | "cli" -> FsAssay.Analyzers.Domain.Profile.CLI
        | "etl" -> FsAssay.Analyzers.Domain.Profile.ETL
        | _ -> FsAssay.Analyzers.Domain.Profile.Core

    let explicitFiles = results.TryGetResult(Files)

    let executeScan () =
        let optionsList =
            match explicitFiles with
            | Some _ -> []
            | None ->
                try
                    let loaded = ProjectSystem.getTargetProjects path
                    loadedProjectPaths <- loaded |> List.map (fun options -> Path.GetFullPath(options.ProjectFileName)) |> Set.ofList
                    loaded
                with e ->
                    printfn "💥 Project System Failure: %s" e.Message // EXPECT: FSA-F04
                    projectLoadFailures <- [ e.Message ]
                    []
                
        let hasProjFiles = 
            path.EndsWith(".sln") || path.EndsWith(".slnx") || path.EndsWith(".fsproj") ||
            (Directory.Exists(path) && Directory.GetFiles(path, "*.fsproj", SearchOption.AllDirectories).Length > 0) // EXPECT: FSA2022

        let allDiscoveredFiles =
            if explicitFiles.IsSome then []
            elif List.isEmpty optionsList then
                if hasProjFiles then // EXPECT: FSA-F04
                    printfn "💥 Project System Failure: F# project files were found but failed to load or contained no source files." // EXPECT: FSA-F04

                if File.Exists(path) && path.EndsWith(".fs") then [ (path, None) ] // EXPECT: FSA2022
                elif Directory.Exists(path) then // EXPECT: FSA2022
                    Directory.GetFiles(path, "*.fs", SearchOption.AllDirectories) // EXPECT: FSA2022
                    |> Array.filter (fun f -> not (f.Contains("obj") || f.Contains("bin")))
                    |> Array.map (fun f -> (f, None))
                    |> Array.toList
                else []
            else
                let files = optionsList |> List.collect (fun opts -> opts.SourceFiles |> Array.map (fun f -> (f, Some opts)) |> Array.toList)
                if List.isEmpty files && hasProjFiles then // EXPECT: FSA-F04
                    printfn "💥 Project System Failure: F# project files were found but contained no source files." // EXPECT: FSA-F04
                files

        let filesToScan =
            match explicitFiles with
            | Some explicitPathsStr ->
                explicitPathsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun explicitPath -> explicitPath.Trim() |> Path.GetFullPath)
                |> Array.distinct
                |> Array.map (fun explicitPath ->
                    allDiscoveredFiles
                    |> List.tryFind (fun (filePath, _) ->
                        String.Equals(Path.GetFullPath(filePath), explicitPath, StringComparison.Ordinal))
                    |> Option.defaultValue (explicitPath, None))
                |> Array.toList
            | None -> allDiscoveredFiles

        if List.isEmpty filesToScan then
            printfn "No files found to scan." // EXPECT: FSA-F04
            (0, 0, 0, 0, [], [])
        else
            let mutable totalViolations = 0
            let mutable totalFiles = 0
            let mutable failedFiles = 0
            let mutable skippedFiles = 0
            let allResults = ResizeArray<string * Violation list>() // EXPECT: FSA-C16
            let allTrees = ResizeArray<string * FSharp.Compiler.Symbols.FSharpImplementationFileContents * FSharp.Compiler.Text.ISourceText>() // EXPECT: FSA-C16

            for (file, optsOpt) in filesToScan do // EXPECT: FSA-P02 // EXPECT: FSA-F04
                let isExcluded = Config.isExcluded config.directory config.exclude file
                if not isExcluded && file.EndsWith(".fs") && not (file.Contains("AssemblyAttributes.fs")) && not (file.Contains("AssemblyInfo.fs")) then
                    totalFiles <- totalFiles + 1 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                    
                    let effectiveProfile =
                        if file.EndsWith(".fsx") then FsAssay.Analyzers.Domain.Profile.Script
                        elif file.Contains("Test") || file.Contains("test") || file.Contains("fsi") then FsAssay.Analyzers.Domain.Profile.Test
                        else typedProfile

                    let verdict =
                        match optsOpt with
                        | Some opts -> Orchestrator.evaluateFileWithBoundary config.boundaries (Path.GetRelativePath(config.directory, opts.ProjectFileName)) opts file effectiveProfile cliPlugins |> Async.RunSynchronously // EXPECT: FSA-C03
                        | None -> Orchestrator.evaluateSingleFileWithBoundary config.boundaries "" file effectiveProfile cliPlugins |> Async.RunSynchronously // EXPECT: FSA-C03

                    match verdict with
                    | Completed (violations, treeOpt, sourceText) ->
                        match treeOpt with // EXPECT: FSA-F04
                        | Some t -> allTrees.Add((file, t, sourceText))
                        | None -> ()
                        
                        totalViolations <- totalViolations + violations.Length // EXPECT: FSA-F04 // EXPECT: FSA-C10
                        allResults.Add(file, violations) // EXPECT: FSA-F04
                        if results.Contains(Fix) then printfn "Automatic source fixes remain disabled; preview and review repairs."
                    | Skipped reason ->
                        skippedFiles <- skippedFiles + 1 // EXPECT: FSA-C10
                    | Failed fail ->
                        failedFiles <- failedFiles + 1 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                        printfn "\n❌ %s (Failed to analyze: %A)" file fail

            // Project level analysis
            if allTrees.Count > 0 then // EXPECT: FSA-F04
                let projViolations = FsAssay.Analyzers.Library.projectAnalyzer (allTrees |> Seq.toList) |> Async.RunSynchronously // EXPECT: FSA-P03 // EXPECT: FSA-C03
                if not (List.isEmpty projViolations) then
                    totalViolations <- totalViolations + projViolations.Length // EXPECT: FSA-F04 // EXPECT: FSA-C10
                    allResults.Add("Architecture", projViolations) // EXPECT: FSA-F04

            (totalFiles, skippedFiles, failedFiles + pluginLoadFailures.Length, totalViolations, List.ofSeq allResults, filesToScan |> List.map fst)

    let (totalFiles, skippedFiles, failedFiles, totalViolations, allResults, scannedFiles) = executeScan ()

    if results.Contains(Adjudicate) then // EXPECT: FSA-F04
        printfn "\n--- Adjudication Mode ---" // EXPECT: FSA-F04
        let mutable truePositives = 0
        let mutable falsePositives = 0
        let mutable falseNegatives = 0

        // expected: list of (file, ruleCode, lineNumber)
        let expectedCodes = System.Collections.Generic.List<string * string * int>() // EXPECT: FSA-C16
        // actual: list of (file, ruleCode, startLine)
        let actualCodes = System.Collections.Generic.List<string * string * int>() // EXPECT: FSA-C16

        for file in scannedFiles do // EXPECT: FSA-P02 // EXPECT: FSA-F04
            if file.EndsWith(".fs") then
                let lines = File.ReadAllLines(file) // EXPECT: FSA2022
                for i = 0 to lines.Length - 1 do
                    let line = lines.[i]
                    let m = System.Text.RegularExpressions.Regex.Match(line, @"//\s*EXPECT:\s*(FSA[A-Z0-9-]+)")
                    if m.Success then
                        let code = m.Groups.[1].Value
                        expectedCodes.Add((file, code, i + 1)) // 1-indexed

        for (file, violations) in allResults do // EXPECT: FSA-P02 // EXPECT: FSA-F04
            for v in violations do // EXPECT: FSA-P02
                actualCodes.Add((file, v.Code, v.Range.StartLine))

        if expectedCodes.Count = 0 then // EXPECT: FSA-F04
            printfn "💥 Adjudicate Failed: Zero evidence (no EXPECT comments found)." // EXPECT: FSA-F04
            Environment.Exit(ExitCodes.ToolFailure)

        // Matching logic: an expected code is TP if there is an actual code with same file and ruleCode within 3 lines
        let expectedList = expectedCodes |> List.ofSeq
        let mutable actualRemaining = actualCodes |> List.ofSeq

        for (eFile, eCode, eLine) in expectedList do // EXPECT: FSA-P02 // EXPECT: FSA-F04
            let matchIdx = actualRemaining |> List.tryFindIndex (fun (aFile, aCode, aLine) -> aFile = eFile && aCode = eCode && abs (aLine - eLine) <= 3) // EXPECT: FSA-AI10
            match matchIdx with
            | Some idx ->
                truePositives <- truePositives + 1 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                actualRemaining <- actualRemaining |> List.removeAt idx // EXPECT: FSA-C10
            | None ->
                printfn "   False Negative: expected %s in %s near line %d" eCode eFile eLine // EXPECT: FSA-F04
                falseNegatives <- falseNegatives + 1 // EXPECT: FSA-C10

        for (aFile, aCode, aLine) in actualRemaining do // EXPECT: FSA-P02 // EXPECT: FSA-F04
            printfn "   False Positive: actual %s in %s at line %d" aCode aFile aLine // EXPECT: FSA-F04
            falsePositives <- falsePositives + 1 // EXPECT: FSA-C10

        let precision = if truePositives + falsePositives = 0 then None else Some(float truePositives / float (truePositives + falsePositives))
        let recall = if truePositives + falseNegatives = 0 then None else Some(float truePositives / float (truePositives + falseNegatives))

        match precision with | Some p -> printfn "Precision: %.2f%%" (p * 100.0) | None -> printfn "Precision: undefined/Inconclusive" // EXPECT: FSA-F04
        match recall with | Some r -> printfn "Recall:    %.2f%%" (r * 100.0) | None -> printfn "Recall:    undefined/Inconclusive" // EXPECT: FSA-F04
        printfn "TP: %d | FP: %d | FN: %d" truePositives falsePositives falseNegatives // EXPECT: FSA-F04
        
        let pVal = defaultArg precision 1.0
        let rVal = defaultArg recall 1.0
        if pVal < 1.0 || rVal < 1.0 then // EXPECT: FSA-F04
            Environment.Exit(ExitCodes.BlockingFinding)
        if precision.IsNone || recall.IsNone then
            Environment.Exit(ExitCodes.RequiredEvidenceMissing)
    else
        printfn "\n--- Scan complete! ---" // EXPECT: FSA-F04
        printfn "Files scanned: %d" totalFiles // EXPECT: FSA-F04
        printfn "Skipped: %d" skippedFiles // EXPECT: FSA-F04
        printfn "Failed: %d" failedFiles // EXPECT: FSA-F04
        printfn "Total Violations: %d" totalViolations

    let projectEvidence =
        discoveredProjects
        |> List.map (fun project ->
            let fullPath = Path.GetFullPath(project)
            let projectClass = ProjectSystem.projectClass fullPath
            let frameworks = ProjectSystem.projectTargetFrameworks fullPath
            let policyAvailable = policyErrors.IsEmpty
            let supportedClass = Array.contains projectClass policy.requiredProjectClasses
            let supportedFramework = not (Array.isEmpty frameworks) && frameworks |> Array.forall (fun framework -> Array.contains framework policy.requiredTargetFrameworks)
            let supported = policyAvailable && supportedClass && supportedFramework
            let loaded = loadedProjectPaths.Contains fullPath
            ({
                Path = fullPath
                ProjectClass = projectClass
                TargetFrameworks = frameworks |> Array.toList
                Supported = supported
                Loaded = loaded
                Disposition =
                    if not supported then Authority.ProjectDisposition.Unsupported
                    elif not loaded then Authority.ProjectDisposition.LoadFailed
                    else Authority.ProjectDisposition.Loaded
                Reason =
                    if not supported then
                        let frameworkText = String.concat ", " (frameworks |> Array.toList)
                        let causes = [
                            if not policyAvailable then $"policy unavailable; support classification withheld for project class '{projectClass}' and target frameworks [{frameworkText}]"
                            else
                                if not supportedClass then $"project class '{projectClass}' is outside the locked authority policy"
                                if not supportedFramework then $"target frameworks [{frameworkText}] are outside the locked authority policy"
                        ]
                        String.concat "; " causes
                    elif not loaded then "workspace did not load the discovered supported project"
                    else ""
            }: Authority.ProjectEvidence)
        )

    let supportedLoadedProjects = projectEvidence |> List.filter (fun project -> project.Disposition = Authority.ProjectDisposition.Loaded) |> List.length
    let unsupportedProjects = projectEvidence |> List.filter (fun project -> project.Disposition = Authority.ProjectDisposition.Unsupported) |> List.length
    let completedPaths = allResults |> List.map fst |> Set.ofList
    let sourceEvidence =
        scannedFiles
        |> List.distinct
        |> List.map (fun file ->
            let policyExcluded = Config.isExcluded config.directory config.exclude file
            ({
                Path = file
                Disposition =
                    if policyExcluded then Authority.SourceDisposition.PolicyExcluded
                    elif file.Contains("AssemblyAttributes.fs") || file.Contains("AssemblyInfo.fs") || file.Contains("MicrosoftTestingPlatformEntryPoint.fs") || file.Contains("SelfRegisteredExtensions.fs") then Authority.SourceDisposition.GeneratedExcluded
                    elif completedPaths.Contains file then Authority.SourceDisposition.Analyzed
                    else Authority.SourceDisposition.CompilerIncomplete
                Reason =
                    if policyExcluded then "source matched the locked scan configuration"
                    elif file.Contains("AssemblyAttributes.fs") || file.Contains("AssemblyInfo.fs") || file.Contains("MicrosoftTestingPlatformEntryPoint.fs") || file.Contains("SelfRegisteredExtensions.fs") then "compiler-generated source is not analyzed"
                    elif completedPaths.Contains file then ""
                    else "compiler/workspace evidence was unavailable"
            }: Authority.SourceEvidence)
        )

    let verificationDirectory =
        if mode = "check" then None
        else
            let directory = results.GetResult(Test_Artifacts, defaultValue = Path.Combine(repositoryRoot, "artifacts", "verification", Guid.NewGuid().ToString("N"))) |> fun path -> Path.GetFullPath(path, invocationRoot)
            if Directory.Exists directory || File.Exists directory then
                eprintfn "ERROR: required-test evidence directory must be new."
                Environment.Exit ExitCodes.InvalidInvocation
            Directory.CreateDirectory directory |> ignore
            Some directory
    let testRuns =
        match verificationDirectory with
        | Some directory when configErrors.IsEmpty && policyErrors.IsEmpty ->
            policy.requiredTests |> Array.mapi (fun index requirement -> Verification.execute repositoryRoot (Path.Combine(directory, string index)) requirement) |> Array.toList
        | _ -> []
    let requiredTests =
        policy.requiredTests
        |> Array.map (fun test ->
            ({
                Id = test.id
                Project = Path.Combine(repositoryRoot, test.project)
                Status = Authority.TestStatus.NotRun
                Passed = 0
                Failed = 0
                Skipped = 0
            }: Authority.TestEvidence))
        |> Array.toList

    let requiredTests = if mode = "check" || testRuns.IsEmpty then requiredTests else testRuns |> List.map _.evidence
    let freshnessErrors =
        try if Assessment.capture repositoryRoot = initialInputs then snapshotErrors else snapshotErrors @ ["declared source/configuration changed during evaluation"]
        with error -> snapshotErrors @ [error.Message]
    let findings =
        allResults
        |> List.collect (fun (file, violations) ->
            violations
            |> List.map (fun violation ->
                ({
                    RuleId = violation.Code
                    Path = file
                    Symbol = "file-scope"
                    Line = violation.Range.StartLine
                    Column = violation.Range.StartColumn
                    Message = violation.Message
                    Fingerprint = ""
                }: Authority.FindingEvidence)))

    let evidenceComplete = projectLoadFailures.IsEmpty && failedFiles = 0 && skippedFiles = 0 && totalFiles > 0 && supportedLoadedProjects > 0
    let policyRules =
        let locked =
            Array.concat [|
                policy.approvedBlockingRules
                policy.advisoryRules
                policy.experimentalRules
                policy.prototypeRules
                policy.dummyRules
                policy.deprecatedRules
                policy.removedRules
            |]
        if policyErrors.IsEmpty then locked |> Array.sort
        else findings |> List.map _.RuleId |> List.distinct |> List.sort |> List.toArray
    let rules =
        policyRules
        |> Array.map (fun ruleId ->
            let catalogueRule = FsAssay.Analyzers.Domain.Rule.AllRules |> List.tryFind (fun rule -> rule.Code = ruleId)
            let status, available =
                match catalogueRule with
                | Some rule ->
                    match rule.Status with
                    | FsAssay.Analyzers.Domain.Implemented | FsAssay.Analyzers.Domain.Delegated _ when evidenceComplete -> "completed", true
                    | FsAssay.Analyzers.Domain.Implemented | FsAssay.Analyzers.Domain.Delegated _ -> "incomplete", false
                    | FsAssay.Analyzers.Domain.Dummy | FsAssay.Analyzers.Domain.Prototype | FsAssay.Analyzers.Domain.Proposed -> "unavailable", false
                | None -> "unavailable", false
            ({
                RuleId = ruleId
                Status = status
                EvidenceAvailable = available
                FindingCount = findings |> List.filter (fun finding -> finding.RuleId = ruleId) |> List.length
            }: Authority.RuleEvidence))
        |> Array.toList

    let candidate, candidateMissingEvidence, candidateEvidenceErrors = Authority.candidateIdentity repositoryRoot path
    let authorityFacts = {
        Authority.emptyFacts with
            PolicyErrors = policyErrors
            EvidenceErrors = candidateEvidenceErrors @ configErrors @ freshnessErrors
            ToolFailures = (testRuns |> List.collect _.toolErrors) @ (if pluginLoadFailures.IsEmpty && failedFiles = 0 then [] else pluginLoadFailures @ [ if failedFiles > pluginLoadFailures.Length then $"{failedFiles - pluginLoadFailures.Length} analyzer evaluation(s) failed" ])
            MissingEvidence = projectLoadFailures @ candidateMissingEvidence
            Toolchain = Authority.currentToolchain repositoryRoot
            Projects = projectEvidence
            Sources = sourceEvidence
            RequiredTests = requiredTests
            Rules = rules
            Findings = findings
    }

    let authorityReceipt = Authority.createReceipt repositoryRoot candidate policy policyPath policyHash authorityFacts
    let reportFiles =
        [
            results.TryGetResult(Out_Json) |> Option.map (fun path -> Path.GetFullPath(path, invocationRoot), Output.canonicalJsonBytes authorityReceipt)
            results.TryGetResult(Out_Sarif) |> Option.map (fun path -> Path.GetFullPath(path, invocationRoot), Output.canonicalSarifBytes authorityReceipt)
            results.TryGetResult(Assessment_Json) |> Option.map (fun path -> Path.GetFullPath(path, invocationRoot), Assessment.create mode authorityReceipt initialInputs testRuns |> Assessment.bytes)
            results.TryGetResult(RateCard_Md) |> Option.map (fun path -> Path.GetFullPath(path, invocationRoot), System.Text.Encoding.UTF8.GetBytes(ReadableReport.markdownReport authorityReceipt testRuns invocation))
            results.TryGetResult(Material_Html) |> Option.map (fun path -> Path.GetFullPath(path, invocationRoot), System.Text.Encoding.UTF8.GetBytes(ReadableReport.html authorityReceipt testRuns invocation))
            results.TryGetResult(Out_Toolchain) |> Option.map (fun path -> Path.GetFullPath(path, invocationRoot), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(authorityReceipt.toolchain))
            results.TryGetResult(SuppressionReport_Json) |> Option.map (fun path -> Path.GetFullPath(path, invocationRoot), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes({|schema = "fsassay-suppression-observations/0.1"; appliedAuthoritySuppressions = authorityReceipt.appliedSuppressions; note = "Source attributes affect experimental observations only; no suppression grants engine authority."|}))
        ]
        |> List.choose id
    let protectedFiles = (initialInputs |> Array.map (fun input -> Path.Combine(repositoryRoot, input.path)) |> Array.toList) @ scannedFiles @ discoveredProjects @ [policyPath; Path.Combine(config.directory, ".fsassayrc")]
    let evidenceWriteFailure =
        match Output.publish protectedFiles reportFiles with
        | Ok () -> None
        | Error error -> eprintfn "Evidence output ToolFailure: %s" error; Some error
    if evidenceWriteFailure.IsNone then
        printfn "%s" (if consoleFormat = "msbuild" then ReadableReport.msbuild authorityReceipt else ReadableReport.text authorityReceipt testRuns)
        printfn "Authority outcome: %s" authorityReceipt.outcome
        printfn "Authoritative: %b" authorityReceipt.authoritative
    else printfn "FsAssay ToolFailure: requested evidence could not be published."

    match results.TryGetResult(Serve) with // EXPECT: FSA-F04
    | Some port ->
        Server.startLiveServer allResults totalFiles port
    | None -> ()

    if results.Contains(Watch) then // EXPECT: FSA-F04
        printfn "\n👀 Watch Mode active on %s. Monitoring file changes..." path // EXPECT: FSA-F04
        use watcher = new FileSystemWatcher(path, "*.fs") // EXPECT: FSA-P02 // EXPECT: FSA2022
        watcher.IncludeSubdirectories <- true // EXPECT: FSA2022 // EXPECT: FSA-F04
        watcher.EnableRaisingEvents <- true // EXPECT: FSA2022 // EXPECT: FSA-F04
        watcher.Changed.Add(fun _ -> // EXPECT: FSA2022 // EXPECT: FSA-F04
            printfn "\n🔄 File change detected! Re-analyzing..." // EXPECT: FSA-F04
            executeScan () |> ignore
        )
        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite)

    if evidenceWriteFailure.IsSome then ExitCodes.ToolFailure
    else
        match authorityReceipt.outcome with
        | "ToolFailure" -> ExitCodes.ToolFailure
        | "Fail" -> ExitCodes.BlockingFinding
        | "Inconclusive" -> ExitCodes.RequiredEvidenceMissing
        | "Pass" -> ExitCodes.Success
        | _ -> ExitCodes.ToolFailure
