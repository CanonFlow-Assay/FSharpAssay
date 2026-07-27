namespace FsAssay.Runner

open System.IO
open System.Diagnostics.CodeAnalysis
open FSharp.Analyzers.SDK
open FsAssay.Analyzers
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Diagnostics

open FsAssay.Analyzers.Domain

module Orchestrator =
    
    let checker = FSharpChecker.Create(keepAssemblyContents = true)
    
    let evaluateFileWithProfile (options: FSharpProjectOptions) (file: string) (profile: Profile) (plugins: Analyzer<CliContext> list) = async {
        if not (File.Exists(file)) then return Skipped UnrelatedFile // EXPECT: FSA2022
        else
            let source = File.ReadAllText(file) // EXPECT: FSA2022 // EXPECT: FSA-F08 // EXPECT: FSA-C15
            let sourceText = SourceText.ofString source
            
            let! (parseResults, checkAnswer) = checker.ParseAndCheckFileInProject(file, 1, sourceText, options)
            
            match checkAnswer with
            | FSharpCheckFileAnswer.Aborted -> 
                return Failed (AnalyzerException "FSharpCheckFileAnswer.Aborted")
            | FSharpCheckFileAnswer.Succeeded(checkResults) ->
                let hasErrors = 
                    (parseResults.Diagnostics |> Array.exists (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)) ||
                    (checkResults.Diagnostics |> Array.exists (fun d -> d.Severity = FSharpDiagnosticSeverity.Error))

                if not hasErrors && checkResults.HasFullTypeCheckInfo && checkResults.ImplementationFile.IsSome then
                    let context : CliContext = {
                        FileName = file
                        SourceText = sourceText
                        ParseFileResults = parseResults
                        CheckFileResults = checkResults
                        TypedTree = checkResults.ImplementationFile
                        CheckProjectResults = Unchecked.defaultof<_>
                        ProjectOptions = AnalyzerProjectOptions.BackgroundCompilerOptions options
                        AnalyzerIgnoreRanges = Map.empty
                    }
                    
                    try
                        let parseTree = Some context.ParseFileResults.ParseTree
                        let! violations = Library.coreAnalyzer parseTree context.TypedTree context.FileName context.SourceText context.CheckFileResults.Diagnostics profile
                        
                        let mutable pluginViolations = []
                        for plugin in plugins do
                            let! messages = plugin context
                            let mapped = 
                                messages |> List.map (fun m ->
                                    let severity = match m.Severity with | FSharp.Analyzers.SDK.Severity.Error -> Major | _ -> Minor
                                    { Code = m.Code
                                      Message = m.Message
                                      Explanation = "Violation reported by external F# Analyzer SDK plugin."
                                      Range = m.Range
                                      Severity = severity
                                      RelatedRules = []
                                      Fixes = m.Fixes
                                      DocLink = None
                                      CodeSnippet = None }
                                )
                            pluginViolations <- pluginViolations @ mapped // EXPECT: FSA-F04 // EXPECT: FSA-C10
                            
                        return Completed (violations @ pluginViolations, context.TypedTree, context.SourceText)
                    with e ->
                        return Failed (AnalyzerException e.Message)
                else
                    return Skipped CompilerErrors
    }

    let evaluateFile options file = evaluateFileWithProfile options file Core []

    [<SuppressMessage("FsAssay", "FSA2017")>]
    [<SuppressMessage("FsAssay", "FSA-C01")>]
    let evaluateSingleFileWithProfile (file: string) (profile: Profile) (plugins: Analyzer<CliContext> list) = async {
        if not (File.Exists(file)) then return Skipped UnrelatedFile // EXPECT: FSA2022
        else
            let source = File.ReadAllText(file) // EXPECT: FSA2022 // EXPECT: FSA-F08 // EXPECT: FSA-C15
            let sourceText = SourceText.ofString source
            let! (optionsUnresolved, _) = checker.GetProjectOptionsFromScript(file, sourceText)
            let fsCore = typeof<option<int>>.Assembly.Location
            let trustedPlatformReferences =
                match System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") with
                | :? string as assemblies ->
                    assemblies.Split(Path.PathSeparator, System.StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun assembly -> "-r:" + assembly)
                | _ -> [||]
            let validOriginalOptions =
                optionsUnresolved.OtherOptions
                |> Array.filter (fun option ->
                    if option.StartsWith("-r:") then File.Exists(option.Substring(3))
                    else true)
            let references =
                Array.concat [ validOriginalOptions; trustedPlatformReferences; [| "-r:" + fsCore |] ]
                |> Array.distinct
            let options = { optionsUnresolved with OtherOptions = references }
            
            let! (parseResults, checkAnswer) = checker.ParseAndCheckFileInProject(file, 0, sourceText, options)
            match checkAnswer with
            | FSharpCheckFileAnswer.Aborted ->
                return Failed (AnalyzerException "FSharpCheckFileAnswer.Aborted")
            | FSharpCheckFileAnswer.Succeeded(checkResults) ->
                let hasErrors = 
                    (parseResults.Diagnostics |> Array.exists (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)) ||
                    (checkResults.Diagnostics |> Array.exists (fun d -> d.Severity = FSharpDiagnosticSeverity.Error))

                if hasErrors then
                    return Skipped CompilerErrors
                else
                    let context : CliContext = {
                        FileName = file
                        SourceText = sourceText
                        ParseFileResults = parseResults
                        CheckFileResults = checkResults
                        TypedTree = checkResults.ImplementationFile
                        CheckProjectResults = Unchecked.defaultof<_>
                        ProjectOptions = AnalyzerProjectOptions.BackgroundCompilerOptions options
                        AnalyzerIgnoreRanges = Map.empty
                    }
                    try
                        let parseTree = Some context.ParseFileResults.ParseTree
                        let! violations = Library.coreAnalyzer parseTree context.TypedTree context.FileName context.SourceText context.CheckFileResults.Diagnostics profile
                        
                        let mutable pluginViolations = []
                        for plugin in plugins do
                            let! messages = plugin context
                            let mapped = 
                                messages |> List.map (fun m ->
                                    let severity = match m.Severity with | FSharp.Analyzers.SDK.Severity.Error -> Major | _ -> Minor
                                    { Code = m.Code
                                      Message = m.Message
                                      Explanation = "Violation reported by external F# Analyzer SDK plugin."
                                      Range = m.Range
                                      Severity = severity
                                      RelatedRules = []
                                      Fixes = m.Fixes
                                      DocLink = None
                                      CodeSnippet = None }
                                )
                            pluginViolations <- pluginViolations @ mapped // EXPECT: FSA-F04 // EXPECT: FSA-C10
                            
                        return Completed (violations @ pluginViolations, context.TypedTree, context.SourceText)
                    with e ->
                        return Failed (AnalyzerException e.Message)
    }

    let evaluateSingleFile file = evaluateSingleFileWithProfile file Core []

    let analyzeProject path = async {
        let optionsList =
            try ProjectSystem.getTargetProjects path
            with _ -> []
        let files = 
            if List.isEmpty optionsList then
                if File.Exists(path) && path.EndsWith(".fs") then [ (path, None) ] // EXPECT: FSA2022
                elif Directory.Exists(path) then // EXPECT: FSA2022
                    Directory.GetFiles(path, "*.fs", SearchOption.AllDirectories) // EXPECT: FSA2022
                    |> Array.filter (fun f -> not (f.Contains("obj") || f.Contains("bin")))
                    |> Array.map (fun f -> (f, None))
                    |> Array.toList
                else []
            else
                optionsList |> List.collect (fun opts -> opts.SourceFiles |> Array.map (fun f -> (f, Some opts)) |> Array.toList)
        
        let mutable results = []
        for (f, o) in files do
            if f.EndsWith(".fs") && not (f.Contains("AssemblyAttributes.fs") || f.Contains("AssemblyInfo.fs")) then
                let! verdict = match o with Some opt -> evaluateFileWithProfile opt f Core [] | None -> evaluateSingleFileWithProfile f Core []
                results <- verdict :: results // EXPECT: FSA-F04 // EXPECT: FSA-C10
        return results |> List.rev
    }
