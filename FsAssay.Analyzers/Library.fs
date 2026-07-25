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
let coreAnalyzer (ctxParseTree: FSharp.Compiler.Syntax.ParsedInput option) (ctxTypedTree: FSharpImplementationFileContents option) (ctxFileName: string) (ctxSourceText: ISourceText) (ctxDiagnostics: FSharp.Compiler.Diagnostics.FSharpDiagnostic[]) (profile: Profile) =
    async {
        let diagFindings =
            ctxDiagnostics
            |> Seq.filter (fun d -> d.ErrorNumber = 25) // EXPECT: FSA-AI10
            |> Seq.choose (fun d -> mkLocated FSAC05 d.Range)
            |> Seq.toList // EXPECT: FSA-P03

        match ctxTypedTree with
        | Some tree ->
            let topLevelSups = [ "PROFILE:" + profile.ToString().ToLowerInvariant() ]
            
            let compExprRanges = AstContext.getCompExprRanges ctxSourceText ctxFileName
            
            let isTestFile = profile = Profile.Test || topLevelSups |> List.contains "PROFILE:test" || ctxFileName.ToLowerInvariant().Contains("test")
            
            let (astFindings, finalHasProperty) =
                tree.Declarations
                |> List.fold (fun (accF, accP) d -> 
                    let (f, p) = analyzeDecl d topLevelSups ctxSourceText compExprRanges isTestFile accP
                    (Set.union accF f, p)
                ) (Set.empty, false)
            
            let additionalFindings = 
                if isTestFile && not finalHasProperty then
                    mkLocated FSATDD02 FSharp.Compiler.Text.Range.range0 |> Option.toList
                else []
            
            let allFindings = (astFindings |> Set.toList) @ diagFindings @ additionalFindings
            
            let! lintFindings =
                match ctxParseTree with
                | Some pt -> FsAssay.Analyzers.LintDelegation.lintAnalyzer pt ctxFileName ctxSourceText profile
                | None -> async.Return []
                
            return (allFindings @ lintFindings) |> List.choose (toViolation ctxSourceText)
        | None -> return diagFindings |> List.choose (toViolation ctxSourceText)
    }

let projectAnalyzer (files: (string * FSharpImplementationFileContents * ISourceText) list) =
    async {
        let trees = files |> List.map (fun (f, tree, _) -> (f, tree))
        let graph = FsAssay.Analyzers.Graph.buildGraph trees
        
        let cycleFindings = FsAssay.Analyzers.Graph.detectCycles graph
        let depthFindings = FsAssay.Analyzers.Graph.calculateDepth graph
        let layerFindings = FsAssay.Analyzers.Graph.checkLayerViolations graph
        let ssrfFindings = FsAssay.Analyzers.Graph.checkSSRF graph
        let tddFindings = FsAssay.Analyzers.Graph.checkTDD graph
        
        let fsprojFile = files |> List.tryPick (fun (f, _, _) -> // fsharp-assay-ignore FSA2022
            let dir = System.IO.Path.GetDirectoryName(f) // EXPECT: FSA2022
            let fsprojs = System.IO.Directory.GetFiles(dir, "*.fsproj") // EXPECT: FSA2022
            if fsprojs.Length > 0 then Some fsprojs.[0] else None
        )
        let nugetFindings = match fsprojFile with Some proj -> FsAssay.Analyzers.ProjectParser.parseProjectFile proj | None -> []
        
        let allFindings = cycleFindings @ depthFindings @ layerFindings @ ssrfFindings @ tddFindings
        
        // Since architectural violations don't have a specific file snippet easily, we just use a dummy source text or the first file's text
        let dummyText = match files with | (_, _, t) :: _ -> t | [] -> FSharp.Compiler.Text.SourceText.ofString ""
        return (allFindings |> List.choose (toViolation dummyText)) @ nugetFindings
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
            let parseTree = Some ctx.ParseFileResults.ParseTree
            let! violations = coreAnalyzer parseTree ctx.TypedTree ctx.FileName ctx.SourceText ctx.CheckFileResults.Diagnostics Core
            return violations |> List.map toSDKMessage
        }

[<EditorAnalyzer "FSA_All_Editor">]
let antiPatternEditorAnalyzer : Analyzer<EditorContext> =
    fun ctx -> 
        async {
            let diagnostics = match ctx.CheckFileResults with Some res -> res.Diagnostics | None -> [||]
            let parseTree = Some ctx.ParseFileResults.ParseTree 
            let! violations = coreAnalyzer parseTree ctx.TypedTree ctx.FileName ctx.SourceText diagnostics Core
            return violations |> List.map toSDKMessage
        }
