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
    
    let evaluateFileWithProfile (options: FSharpProjectOptions) (file: string) (profile: Profile) = async {
        if not (File.Exists(file)) then return Skipped UnrelatedFile
        else
            let source = File.ReadAllText(file)
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
                        let! violations = Library.coreAnalyzer context.TypedTree context.FileName context.SourceText context.CheckFileResults.Diagnostics profile
                        return Completed (violations, context.TypedTree, context.SourceText)
                    with e ->
                        return Failed (AnalyzerException e.Message)
                else
                    return Skipped CompilerErrors
    }

    let evaluateFile options file = evaluateFileWithProfile options file Core

    [<SuppressMessage("FsAssay", "FSA2017")>]
    [<SuppressMessage("FsAssay", "FSA-C01")>]
    let evaluateSingleFileWithProfile (file: string) (profile: Profile) = async {
        if not (File.Exists(file)) then return Skipped UnrelatedFile
        else
            let source = File.ReadAllText(file)
            let sourceText = SourceText.ofString source
            let! (optionsUnresolved, _) = checker.GetProjectOptionsFromScript(file, sourceText)
            let fsCore = typeof<option<int>>.Assembly.Location
            let sysLib = typeof<System.Object>.Assembly.Location
            let sysRuntime = typeof<System.Action>.Assembly.Location
            let options = { optionsUnresolved with OtherOptions = Array.append optionsUnresolved.OtherOptions [| "-r:" + fsCore; "-r:" + sysLib; "-r:" + sysRuntime |] }
            
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
                        let! violations = Library.coreAnalyzer context.TypedTree context.FileName context.SourceText context.CheckFileResults.Diagnostics profile
                        return Completed (violations, context.TypedTree, context.SourceText)
                    with e ->
                        return Failed (AnalyzerException e.Message)
    }

    let evaluateSingleFile file = evaluateSingleFileWithProfile file Core

    let analyzeProject path = async {
        let optionsList =
            try ProjectSystem.getTargetProjects path
            with _ -> []
        let files = 
            if List.isEmpty optionsList then
                if File.Exists(path) && path.EndsWith(".fs") then [ (path, None) ]
                elif Directory.Exists(path) then
                    Directory.GetFiles(path, "*.fs", SearchOption.AllDirectories)
                    |> Array.filter (fun f -> not (f.Contains("obj") || f.Contains("bin")))
                    |> Array.map (fun f -> (f, None))
                    |> Array.toList
                else []
            else
                optionsList |> List.collect (fun opts -> opts.SourceFiles |> Array.map (fun f -> (f, Some opts)) |> Array.toList)
        
        let mutable results = []
        for (f, o) in files do
            if f.EndsWith(".fs") && not (f.Contains("AssemblyAttributes.fs") || f.Contains("AssemblyInfo.fs")) then
                let! verdict = match o with Some opt -> evaluateFileWithProfile opt f Core | None -> evaluateSingleFileWithProfile f Core
                match verdict with
                | Completed (v, _, _) -> results <- results @ v
                | _ -> ()
        return results
    }
