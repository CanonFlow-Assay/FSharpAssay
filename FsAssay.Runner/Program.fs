open System
open System.IO
open System.Text.RegularExpressions
open FsAssay.Runner
open FSharp.Analyzers.SDK
open FsAssay.Analyzers.Domain
open Argu

type Arguments =
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

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "fsassay")
    let results =
        try
            parser.ParseCommandLine argv
        with e ->
            printfn "%s" e.Message
            Environment.Exit(ExitCodes.InvalidInvocation)
            failwith ""

    if results.Contains(Mcp) then
        FsAssay.Runner.McpServer.run ()
        Environment.Exit(ExitCodes.Success)

    match results.TryGetResult(Docs) with
    | Some dir ->
        FsAssay.Runner.DocsGen.generateDocs dir
        Environment.Exit(ExitCodes.Success)
    | None -> ()

    let path = results.GetResult(Target, defaultValue = Directory.GetCurrentDirectory())
    let rawConfig = Config.loadConfig path
    let activeProfile = results.GetResult(Profile, defaultValue = rawConfig.profile)
    let config = { rawConfig with profile = activeProfile }

    printfn "🧪 FsAssay Engine v0.1.0 — Scanning target: %s [Profile: %s]" path config.profile
    
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
                printfn "💥 Project System Failure: %s" e.Message
                Environment.Exit(ExitCodes.ToolFailure)
                failwith "unreachable"
                
        let hasProjFiles = 
            path.EndsWith(".sln") || path.EndsWith(".slnx") || path.EndsWith(".fsproj") ||
            (Directory.Exists(path) && Directory.GetFiles(path, "*.fsproj", SearchOption.AllDirectories).Length > 0)

        let allDiscoveredFiles =
            if List.isEmpty optionsList then
                if hasProjFiles then
                    printfn "💥 Project System Failure: F# project files were found but failed to load or contained no source files."
                    Environment.Exit(ExitCodes.ToolFailure)
                    failwith "unreachable"

                if File.Exists(path) && path.EndsWith(".fs") then [ (path, None) ]
                elif Directory.Exists(path) then
                    Directory.GetFiles(path, "*.fs", SearchOption.AllDirectories)
                    |> Array.filter (fun f -> not (f.Contains("obj") || f.Contains("bin")))
                    |> Array.map (fun f -> (f, None))
                    |> Array.toList
                else []
            else
                let files = optionsList |> List.collect (fun opts -> opts.SourceFiles |> Array.map (fun f -> (f, Some opts)) |> Array.toList)
                if List.isEmpty files && hasProjFiles then
                    printfn "💥 Project System Failure: F# project files were found but contained no source files."
                    Environment.Exit(ExitCodes.ToolFailure)
                    failwith "unreachable"
                files

        let filesToScan =
            match results.TryGetResult(Files) with
            | Some explicitPathsStr ->
                let explicitPaths = explicitPathsStr.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun p -> p.Trim())
                allDiscoveredFiles
                |> List.filter (fun (filePath, _) -> explicitPaths |> Array.exists (fun ep -> filePath.EndsWith(ep) || filePath = ep))
            | None -> allDiscoveredFiles

        if List.isEmpty filesToScan then
            printfn "No files found to scan."
            (0, 0, 0, 0, [], [])
        else
            let mutable totalViolations = 0
            let mutable totalFiles = 0
            let mutable failedFiles = 0
            let mutable skippedFiles = 0
            let allResults = ResizeArray<string * Violation list>()
            let allTrees = ResizeArray<string * FSharp.Compiler.Symbols.FSharpImplementationFileContents * FSharp.Compiler.Text.ISourceText>()

            for (file, optsOpt) in filesToScan do
                let isExcluded = config.exclude |> Array.exists (fun pat -> file.Contains(pat.Replace("*", "")))
                if not isExcluded && file.EndsWith(".fs") && not (file.Contains("AssemblyAttributes.fs")) && not (file.Contains("AssemblyInfo.fs")) then
                    totalFiles <- totalFiles + 1
                    let verdict =
                        match optsOpt with
                        | Some opts -> Orchestrator.evaluateFileWithProfile opts file typedProfile |> Async.RunSynchronously
                        | None -> Orchestrator.evaluateSingleFileWithProfile file typedProfile |> Async.RunSynchronously

                    match verdict with
                    | Completed (violations, treeOpt, sourceText) ->
                        match treeOpt with
                        | Some t -> allTrees.Add((file, t, sourceText))
                        | None -> ()
                        
                        totalViolations <- totalViolations + violations.Length
                        allResults.Add(file, violations)
                        if not (List.isEmpty violations) then
                            if not (results.Contains(Adjudicate)) then
                                printfn "\n❌ %s:%d:%d" file violations.[0].Range.StartLine violations.[0].Range.StartColumn
                                for v in violations do
                                    let severityIcon = 
                                        match v.Severity with
                                        | Critical -> "🔴"
                                        | Major -> "🟠"
                                        | Minor -> "🟡"
                                    printfn "   └── [%s] %s: %s" v.Code severityIcon v.Message
                                    v.CodeSnippet |> Option.iter (fun s ->
                                        printfn "       │"
                                        printfn "       │  %d │ %s" v.Range.StartLine (s.TrimEnd())
                                        printfn "       │     │ %s" (String.replicate (max 1 (v.Range.EndColumn - v.Range.StartColumn)) "^")
                                    )
                                    if not (List.isEmpty v.Fixes) then
                                        printfn "       │"
                                        printfn "       ├── Fix: %s" v.Fixes.[0].ToText
                                    printfn "       │"
                                    printfn "       ├── Why: %s" v.Explanation
                                    if not (List.isEmpty v.RelatedRules) then
                                        printfn "       │"
                                        printfn "       └── Related: %s" (String.concat ", " v.RelatedRules)
                            
                            
                            if results.Contains(Fix) then
                                printfn "   ✨ Auto-fix is disabled in this sprint."
                    | Skipped reason ->
                        skippedFiles <- skippedFiles + 1
                    | Failed fail ->
                        failedFiles <- failedFiles + 1
                        printfn "\n❌ %s (Failed to analyze: %A)" file fail

            // Project level analysis
            if allTrees.Count > 0 then
                let projViolations = FsAssay.Analyzers.Library.projectAnalyzer (allTrees |> Seq.toList) |> Async.RunSynchronously
                if not (List.isEmpty projViolations) then
                    totalViolations <- totalViolations + projViolations.Length
                    allResults.Add("Architecture", projViolations)
                    if not (results.Contains(Adjudicate)) then
                        printfn "\n❌ Architecture Violations"
                        for v in projViolations do
                            let severityIcon = 
                                match v.Severity with
                                | Critical -> "🔴"
                                | Major -> "🟠"
                                | Minor -> "🟡"
                            printfn "   └── [%s] %s: %s" v.Code severityIcon v.Message

            (totalFiles, skippedFiles, failedFiles, totalViolations, List.ofSeq allResults, filesToScan |> List.map fst)

    let (totalFiles, skippedFiles, failedFiles, totalViolations, allResults, scannedFiles) = executeScan ()

    if results.Contains(Adjudicate) then
        printfn "\n--- Adjudication Mode ---"
        let mutable truePositives = 0
        let mutable falsePositives = 0
        let mutable falseNegatives = 0

        // expected: list of (file, ruleCode, lineNumber)
        let expectedCodes = System.Collections.Generic.List<string * string * int>()
        // actual: list of (file, ruleCode, startLine)
        let actualCodes = System.Collections.Generic.List<string * string * int>()

        for file in scannedFiles do
            if file.EndsWith(".fs") then
                let lines = File.ReadAllLines(file)
                for i = 0 to lines.Length - 1 do
                    let line = lines.[i]
                    let m = System.Text.RegularExpressions.Regex.Match(line, @"//\s*EXPECT:\s*(FSA[A-Z0-9]+)")
                    if m.Success then
                        let code = m.Groups.[1].Value
                        expectedCodes.Add((file, code, i + 1)) // 1-indexed

        for (file, violations) in allResults do
            for v in violations do
                actualCodes.Add((file, v.Code, v.Range.StartLine))

        if expectedCodes.Count = 0 then
            printfn "💥 Adjudicate Failed: Zero evidence (no EXPECT comments found)."
            Environment.Exit(ExitCodes.ToolFailure)

        // Matching logic: an expected code is TP if there is an actual code with same file and ruleCode within 3 lines
        let expectedList = expectedCodes |> List.ofSeq
        let mutable actualRemaining = actualCodes |> List.ofSeq

        for (eFile, eCode, eLine) in expectedList do
            let matchIdx = actualRemaining |> List.tryFindIndex (fun (aFile, aCode, aLine) -> aFile = eFile && aCode = eCode && abs (aLine - eLine) <= 3)
            match matchIdx with
            | Some idx ->
                truePositives <- truePositives + 1
                actualRemaining <- actualRemaining |> List.removeAt idx
            | None ->
                printfn "   False Negative: expected %s in %s near line %d" eCode eFile eLine
                falseNegatives <- falseNegatives + 1

        for (aFile, aCode, aLine) in actualRemaining do
            printfn "   False Positive: actual %s in %s at line %d" aCode aFile aLine
            falsePositives <- falsePositives + 1

        let precision = if truePositives + falsePositives = 0 then 1.0 else float truePositives / float (truePositives + falsePositives)
        let recall = if truePositives + falseNegatives = 0 then 1.0 else float truePositives / float (truePositives + falseNegatives)

        printfn "Precision: %.2f%%" (precision * 100.0)
        printfn "Recall:    %.2f%%" (recall * 100.0)
        printfn "TP: %d | FP: %d | FN: %d" truePositives falsePositives falseNegatives
        
        if precision < 1.0 || recall < 1.0 then
            Environment.Exit(ExitCodes.BlockingFinding)
    else
        printfn "\n--- Scan complete! ---"
        printfn "Files scanned: %d" totalFiles
        printfn "Skipped: %d" skippedFiles
        printfn "Failed: %d" failedFiles
        printfn "Total Violations: %d" totalViolations

    match results.TryGetResult(Out_Json) with
    | Some outPath ->
        Output.writeCanonicalJson allResults outPath
        printfn "Wrote JSON output to %s" outPath
    | None -> ()

    match results.TryGetResult(Out_Sarif) with
    | Some outPath ->
        Output.writeSarif allResults outPath
        printfn "Wrote SARIF output to %s" outPath
    | None -> ()

    match results.TryGetResult(Out_Toolchain) with
    | Some outPath ->
        Output.writeToolchainRecord outPath
        printfn "Wrote toolchain record to %s" outPath
    | None -> ()

    match results.TryGetResult(RateCard_Md) with
    | Some outPath ->
        Output.writeRateCard allResults outPath
        printfn "Wrote Markdown Rate Card to %s" outPath
    | None -> ()

    match results.TryGetResult(Material_Html) with
    | Some outPath ->
        Output.writeMaterialDashboard allResults outPath
        printfn "Wrote Material Design 5 HTML Dashboard to %s" outPath
    | None -> ()

    match results.TryGetResult(SuppressionReport_Json) with
    | Some outPath ->
        let files = allResults |> List.map fst
        Output.writeSuppressionReport files outPath
        printfn "Wrote Suppression Report to %s" outPath
    | None -> ()

    match results.TryGetResult(Serve) with
    | Some port ->
        Server.startLiveServer allResults totalFiles port
    | None -> ()

    if results.Contains(Watch) then
        printfn "\n👀 Watch Mode active on %s. Monitoring file changes..." path
        use watcher = new FileSystemWatcher(path, "*.fs")
        watcher.IncludeSubdirectories <- true
        watcher.EnableRaisingEvents <- true
        watcher.Changed.Add(fun _ ->
            printfn "\n🔄 File change detected! Re-analyzing..."
            executeScan () |> ignore
        )
        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite)

    if failedFiles > 0 then ExitCodes.ToolFailure
    elif skippedFiles > 0 then ExitCodes.RequiredEvidenceMissing
    elif results.Contains(Adjudicate) then ExitCodes.Success
    elif totalViolations > 0 then ExitCodes.BlockingFinding
    else ExitCodes.Success
