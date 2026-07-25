open System
open System.IO
open System.Text.RegularExpressions
open FsAssay.Runner
open FSharp.Analyzers.SDK
open FsAssay.Analyzers.Domain
open Argu

type Arguments = // EXPECT: FSA-AI17 // EXPECT: FSA-AI11
    | [<MainCommand; Last>] Target of path:string
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
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "fsassay") // EXPECT: FSA-AI17
    let results =
        try
            parser.ParseCommandLine argv
        with e ->
            printfn "%s" e.Message // EXPECT: FSA-F04
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
    let rawConfig = Config.loadConfig path
    let activeProfile = results.GetResult(Profile, defaultValue = rawConfig.profile)
    let config = { rawConfig with profile = activeProfile }

    printfn "🧪 FsAssay Engine v0.1.0 — Scanning target: %s [Profile: %s]" path config.profile // EXPECT: FSA-F04
    
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
        | _ -> FsAssay.Analyzers.Domain.Profile.Core

    let executeScan () =
        let optionsList =
            try
                ProjectSystem.getTargetProjects path
            with e ->
                printfn "💥 Project System Failure: %s" e.Message // EXPECT: FSA-F04
                Environment.Exit(ExitCodes.ToolFailure) // EXPECT: FSA-F04
                failwith "unreachable" // EXPECT: FSA-C06
                
        let hasProjFiles = 
            path.EndsWith(".sln") || path.EndsWith(".slnx") || path.EndsWith(".fsproj") ||
            (Directory.Exists(path) && Directory.GetFiles(path, "*.fsproj", SearchOption.AllDirectories).Length > 0) // EXPECT: FSA2022

        let allDiscoveredFiles =
            if List.isEmpty optionsList then
                if hasProjFiles then // EXPECT: FSA-F04
                    printfn "💥 Project System Failure: F# project files were found but failed to load or contained no source files." // EXPECT: FSA-F04
                    Environment.Exit(ExitCodes.ToolFailure) // EXPECT: FSA-F04
                    failwith "unreachable" // EXPECT: FSA-C06

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
                    Environment.Exit(ExitCodes.ToolFailure) // EXPECT: FSA-F04
                    failwith "unreachable" // EXPECT: FSA-C06
                files

        let filesToScan =
            match results.TryGetResult(Files) with
            | Some explicitPathsStr ->
                let explicitPaths = explicitPathsStr.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun p -> p.Trim())
                allDiscoveredFiles
                |> List.filter (fun (filePath, _) -> explicitPaths |> Array.exists (fun ep -> filePath.EndsWith(ep) || filePath = ep))
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
                let isExcluded = config.exclude |> Array.exists (fun pat -> file.Contains(pat.Replace("*", "")))
                if not isExcluded && file.EndsWith(".fs") && not (file.Contains("AssemblyAttributes.fs")) && not (file.Contains("AssemblyInfo.fs")) then
                    totalFiles <- totalFiles + 1 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                    let verdict =
                        match optsOpt with
                        | Some opts -> Orchestrator.evaluateFileWithProfile opts file typedProfile cliPlugins |> Async.RunSynchronously // EXPECT: FSA-C03
                        | None -> Orchestrator.evaluateSingleFileWithProfile file typedProfile cliPlugins |> Async.RunSynchronously // EXPECT: FSA-C03

                    match verdict with
                    | Completed (violations, treeOpt, sourceText) ->
                        match treeOpt with // EXPECT: FSA-F04
                        | Some t -> allTrees.Add((file, t, sourceText))
                        | None -> ()
                        
                        totalViolations <- totalViolations + violations.Length // EXPECT: FSA-F04 // EXPECT: FSA-C10
                        allResults.Add(file, violations) // EXPECT: FSA-F04
                        if not (List.isEmpty violations) then
                            if not (results.Contains(Adjudicate)) then // EXPECT: FSA-F04
                                printfn "\n❌ %s:%d:%d" file violations.[0].Range.StartLine violations.[0].Range.StartColumn // EXPECT: FSA-F04
                                for v in violations do // EXPECT: FSA-P02
                                    let severityIcon = 
                                        match v.Severity with
                                        | Critical -> "🔴"
                                        | Major -> "🟠"
                                        | Minor -> "🟡"
                                    printfn "   └── [%s] %s: %s" v.Code severityIcon v.Message // EXPECT: FSA-F04
                                    v.CodeSnippet |> Option.iter (fun s -> // EXPECT: FSA-F04
                                        printfn "       │" // EXPECT: FSA-F04
                                        printfn "       │  %d │ %s" v.Range.StartLine (s.TrimEnd()) // EXPECT: FSA-F04
                                        printfn "       │     │ %s" (String.replicate (max 1 (v.Range.EndColumn - v.Range.StartColumn)) "^")
                                    )
                                    if not (List.isEmpty v.Fixes) then // EXPECT: FSA-F04
                                        printfn "       │" // EXPECT: FSA-F04
                                        printfn "       ├── Fix: %s" v.Fixes.[0].ToText
                                    printfn "       │" // EXPECT: FSA-F04
                                    printfn "       ├── Why: %s" v.Explanation // EXPECT: FSA-F04
                                    if not (List.isEmpty v.RelatedRules) then
                                        printfn "       │" // EXPECT: FSA-F04
                                        printfn "       └── Related: %s" (String.concat ", " v.RelatedRules)
                            
                            
                            if results.Contains(Fix) then
                                printfn "   ✨ Auto-fix is disabled in this sprint."
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
                    if not (results.Contains(Adjudicate)) then
                        printfn "\n❌ Architecture Violations" // EXPECT: FSA-F04
                        for v in projViolations do // EXPECT: FSA-P02
                            let severityIcon = 
                                match v.Severity with
                                | Critical -> "🔴"
                                | Major -> "🟠"
                                | Minor -> "🟡"
                            printfn "   └── [%s] %s: %s" v.Code severityIcon v.Message

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
                    let m = System.Text.RegularExpressions.Regex.Match(line, @"//\s*EXPECT:\s*(FSA[A-Z0-9]+)")
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

    match results.TryGetResult(Out_Json) with // EXPECT: FSA-F04
    | Some outPath ->
        Output.writeCanonicalJson allResults outPath // EXPECT: FSA-F04
        printfn "Wrote JSON output to %s" outPath
    | None -> ()

    match results.TryGetResult(Out_Sarif) with // EXPECT: FSA-F04
    | Some outPath ->
        Output.writeSarif allResults outPath // EXPECT: FSA-F04
        printfn "Wrote SARIF output to %s" outPath
    | None -> ()

    match results.TryGetResult(Out_Toolchain) with // EXPECT: FSA-F04
    | Some outPath ->
        Output.writeToolchainRecord outPath // EXPECT: FSA-F04
        printfn "Wrote toolchain record to %s" outPath
    | None -> ()

    match results.TryGetResult(RateCard_Md) with // EXPECT: FSA-F04
    | Some outPath ->
        Output.writeRateCard allResults outPath // EXPECT: FSA-F04
        printfn "Wrote Markdown Rate Card to %s" outPath
    | None -> ()

    match results.TryGetResult(Material_Html) with // EXPECT: FSA-F04
    | Some outPath ->
        Output.writeMaterialDashboard allResults outPath // EXPECT: FSA-F04
        printfn "Wrote Material Design 5 HTML Dashboard to %s" outPath
    | None -> ()

    match results.TryGetResult(SuppressionReport_Json) with // EXPECT: FSA-F04
    | Some outPath ->
        let files = allResults |> List.map fst
        Output.writeSuppressionReport files outPath // EXPECT: FSA-F04
        printfn "Wrote Suppression Report to %s" outPath
    | None -> ()

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

    let contributes (v: FsAssay.Analyzers.Domain.Violation) =
        let isPlugin = v.Explanation.Contains("external")
        if isPlugin then
            match v.Severity with
            | FsAssay.Analyzers.Domain.Critical | FsAssay.Analyzers.Domain.Major -> FsAssay.Runner.Fail
            | FsAssay.Analyzers.Domain.Minor -> FsAssay.Runner.Inconclusive
        else
            match FsAssay.Analyzers.Domain.Rule.AllRules |> List.tryFind (fun r -> r.Code = v.Code) with
            | Some r ->
                match r.Status, r.Severity with
                | (FsAssay.Analyzers.Domain.Implemented | FsAssay.Analyzers.Domain.Delegated _), (FsAssay.Analyzers.Domain.Critical | FsAssay.Analyzers.Domain.Major) -> FsAssay.Runner.Fail
                | (FsAssay.Analyzers.Domain.Implemented | FsAssay.Analyzers.Domain.Delegated _), FsAssay.Analyzers.Domain.Minor -> FsAssay.Runner.Inconclusive
                | FsAssay.Analyzers.Domain.Prototype, _ -> FsAssay.Runner.Inconclusive
                | _ -> FsAssay.Runner.Pass
            | None -> FsAssay.Runner.Pass

    let maxVerdict a b =
        match a, b with
        | FsAssay.Runner.ToolFailure, _ | _, FsAssay.Runner.ToolFailure -> FsAssay.Runner.ToolFailure
        | FsAssay.Runner.Fail, _ | _, FsAssay.Runner.Fail -> FsAssay.Runner.Fail
        | FsAssay.Runner.Inconclusive, _ | _, FsAssay.Runner.Inconclusive -> FsAssay.Runner.Inconclusive
        | FsAssay.Runner.Pass, FsAssay.Runner.Pass -> FsAssay.Runner.Pass

    let finalVerdict =
        allResults
        |> Seq.collect snd
        |> Seq.map contributes
        |> Seq.fold maxVerdict FsAssay.Runner.Pass
        |> fun v -> if failedFiles > 0 || not pluginLoadFailures.IsEmpty then maxVerdict v FsAssay.Runner.ToolFailure elif skippedFiles > 0 then maxVerdict v FsAssay.Runner.Inconclusive else v

    if results.Contains(Adjudicate) then ExitCodes.Success
    else
        match finalVerdict with
        | FsAssay.Runner.ToolFailure -> ExitCodes.ToolFailure
        | FsAssay.Runner.Fail -> ExitCodes.BlockingFinding
        | FsAssay.Runner.Inconclusive -> ExitCodes.RequiredEvidenceMissing
        | FsAssay.Runner.Pass -> ExitCodes.Success
