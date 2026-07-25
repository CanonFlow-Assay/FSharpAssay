module FsAssay.Analyzers.Library

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open System

open FsAssay.Analyzers.Domain
open FsAssay.Analyzers.Suppression
open FsAssay.Analyzers.AstUtils
open FsAssay.Analyzers.Visitor

[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-F04")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C01")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C03")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C06")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C08")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-S03")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C09")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C10")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-S05")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C14")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-1301")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-F04")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C01")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C03")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C06")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C08")>]

[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-S03")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C09")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C10")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-S05")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C14")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-1301")>]
let coreAnalyzer (ctxTypedTree: FSharpImplementationFileContents option) (ctxFileName: string) (ctxSourceText: ISourceText) (ctxDiagnostics: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) (profile: Profile) =
    async {
        let diagFindings =
            ctxDiagnostics
            |> Seq.filter (fun d -> d.ErrorNumber = 25)
            |> Seq.choose (fun d -> mkLocated FSAC05 d.Range)
            |> Seq.toList

        match ctxTypedTree with
        | Some tree ->
            let topLevelSups = []
            
            let compExprRanges = AstContext.getCompExprRanges ctxSourceText ctxFileName
            
            let isTestFile = topLevelSups |> List.contains "PROFILE:test" || ctxFileName.ToLowerInvariant().Contains("test")
            let astFindings =
                tree.Declarations
                |> List.map (fun d -> analyzeDecl d topLevelSups ctxSourceText compExprRanges isTestFile)
                |> Set.unionMany
            
            let allFindings = (astFindings |> Set.toList) @ diagFindings
            return allFindings |> List.choose (toViolation ctxSourceText)
        | None -> return diagFindings |> List.choose (toViolation ctxSourceText)
    }

let projectAnalyzer (files: (string * FSharpImplementationFileContents * ISourceText) list) =
    async {
        let trees = files |> List.map (fun (f, tree, _) -> (f, tree))
        let graph = FsAssay.Analyzers.Graph.buildGraph trees
        
        let cycleFindings = FsAssay.Analyzers.Graph.detectCycles graph
        let depthFindings = FsAssay.Analyzers.Graph.calculateDepth graph
        let layerFindings = FsAssay.Analyzers.Graph.checkLayerViolations graph
        
        // Convert to violations. We attach these to the first file conceptually, or we can return them as global violations.
        // For simplicity, we just map them using the first file's sourceText if possible, or dummy.
        let allFindings = cycleFindings @ depthFindings @ layerFindings
        
        // Since architectural violations don't have a specific file snippet easily, we just use a dummy source text or the first file's text
        let dummyText = match files with | (_, _, t) :: _ -> t | [] -> FSharp.Compiler.Text.SourceText.ofString ""
        return allFindings |> List.choose (toViolation dummyText)
    }

let toSDKMessage (v: Violation) : Message =
    {
        Type = v.Code
        Message = v.Message
        Code = v.Code
        Severity = 
            match v.Severity with
            | Critical | Major -> FSharp.Analyzers.SDK.Severity.Error
            | Minor -> FSharp.Analyzers.SDK.Severity.Warning
        Range = v.Range
        Fixes = v.Fixes
    }

[<CliAnalyzer "FSA_All">]
let antiPatternAnalyzer : Analyzer<CliContext> =
    fun ctx -> 
        async {
            let! violations = coreAnalyzer ctx.TypedTree ctx.FileName ctx.SourceText ctx.CheckFileResults.Diagnostics Core
            return violations |> List.map toSDKMessage
        }

[<EditorAnalyzer "FSA_All_Editor">]
let antiPatternEditorAnalyzer : Analyzer<EditorContext> =
    fun ctx -> 
        async {
            let diagnostics = match ctx.CheckFileResults with Some res -> res.Diagnostics | None -> [||]
            let! violations = coreAnalyzer ctx.TypedTree ctx.FileName ctx.SourceText diagnostics Core
            return violations |> List.map toSDKMessage
        }
