module FsAssay.Analyzers.Graph

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FsAssay.Analyzers.Domain
open FsAssay.Analyzers.Catalogue
open FsAssay.Analyzers.AstUtils
open System.IO
open System.Collections.Generic

type Layer =
    | Domain
    | Service
    | API
    | Infrastructure
    | ONDC
    | Unknown

let parseLayer (name: string) =
    let n = name.ToLowerInvariant()
    if n.Contains("domain") then Domain
    elif n.Contains("service") then Service
    elif n.Contains("api") || n.Contains("controller") then API
    elif n.Contains("infra") || n.Contains("data") || n.Contains("db") then Infrastructure
    elif n.Contains("ondc") then ONDC
    else Unknown

let layerValue l =
    match l with
    | Domain -> 1
    | Service -> 2
    | API -> 3
    | ONDC -> 4
    | Infrastructure -> 5
    | Unknown -> 10

type ModuleNode = {
    Name: string
    File: string
    Layer: Layer
    Opens: string list
    References: string list
    MakesHttpCall: bool
}

type ModuleGraph = {
    Nodes: Map<string, ModuleNode>
}

let extractDependencies (decl: FSharpImplementationFileDeclaration) : Set<string> * bool =
    let mutable hasHttpCall = false
    let rec visitExpr (expr: FSharpExpr) : Set<string> =
        let mutable deps = Set.empty
        match expr with
        | FSharpExprPatterns.Call(obj, func, _, _, args) ->
            (try func.DeclaringEntity |> Option.iter (fun e -> deps <- deps.Add(e.FullName)) with _ -> ())
            let decl = try func.DeclaringEntity.Value.FullName with _ -> ""
            if decl = "System.Net.Http.HttpClient" then
                hasHttpCall <- true
            obj |> Option.iter (fun o -> deps <- Set.union deps (visitExpr o))
            for a in args do deps <- Set.union deps (visitExpr a)
        | FSharpExprPatterns.Value(v) ->
            (try v.DeclaringEntity |> Option.iter (fun e -> deps <- deps.Add(e.FullName)) with _ -> ())
        | FSharpExprPatterns.Let((binding, valExpr, _), body) ->
            deps <- Set.union deps (visitExpr valExpr)
            deps <- Set.union deps (visitExpr body)
        | FSharpExprPatterns.Application(func, _, args) ->
            deps <- Set.union deps (visitExpr func)
            for a in args do deps <- Set.union deps (visitExpr a)
        | FSharpExprPatterns.IfThenElse(cond, ifTrue, ifFalse) ->
            deps <- Set.union deps (visitExpr cond)
            deps <- Set.union deps (visitExpr ifTrue)
            deps <- Set.union deps (visitExpr ifFalse)
        | FSharpExprPatterns.TupleGet(_, _, tupleExpr) ->
            deps <- Set.union deps (visitExpr tupleExpr)
        | FSharpExprPatterns.DecisionTree(cond, targets) ->
            deps <- Set.union deps (visitExpr cond)
            for (_, e) in targets do deps <- Set.union deps (visitExpr e)
        | FSharpExprPatterns.DecisionTreeSuccess(_, args) ->
            for a in args do deps <- Set.union deps (visitExpr a)
        | FSharpExprPatterns.Sequential(e1, e2) ->
            deps <- Set.union deps (visitExpr e1)
            deps <- Set.union deps (visitExpr e2)
        | FSharpExprPatterns.Lambda(v, body) ->
            deps <- Set.union deps (visitExpr body)
        | FSharpExprPatterns.LetRec(bindings, body) ->
            for (b, e, _) in bindings do deps <- Set.union deps (visitExpr e)
            deps <- Set.union deps (visitExpr body)
        | FSharpExprPatterns.NewObject(ci, _, args) ->
            (try ci.DeclaringEntity |> Option.iter (fun e -> deps <- deps.Add(e.FullName)) with _ -> ())
            for a in args do deps <- Set.union deps (visitExpr a)
        | FSharpExprPatterns.NewRecord(ty, args) ->
            (try deps <- deps.Add(ty.TypeDefinition.FullName) with _ -> ())
            for a in args do deps <- Set.union deps (visitExpr a)
        | FSharpExprPatterns.NewTuple(_, args) ->
            for a in args do deps <- Set.union deps (visitExpr a)
        | FSharpExprPatterns.NewUnionCase(ty, uc, args) ->
            (try deps <- deps.Add(ty.TypeDefinition.FullName) with _ -> ())
            for a in args do deps <- Set.union deps (visitExpr a)
        | FSharpExprPatterns.ObjectExpr(ty, baseCall, overrides, interfaceImpls) ->
            (try deps <- deps.Add(ty.TypeDefinition.FullName) with _ -> ())
            deps <- Set.union deps (visitExpr baseCall)
            for m in overrides do deps <- Set.union deps (visitExpr m.Body)
            for (_, impls) in interfaceImpls do
                for m in impls do deps <- Set.union deps (visitExpr m.Body)
        | FSharpExprPatterns.TryFinally(e1, e2, _, _) ->
            deps <- Set.union deps (visitExpr e1)
            deps <- Set.union deps (visitExpr e2)
        | FSharpExprPatterns.TryWith(e1, _, e2, _, e3, _, _) -> 
            deps <- Set.union deps (visitExpr e1)
            deps <- Set.union deps (visitExpr e2)
            deps <- Set.union deps (visitExpr e3)
        | FSharpExprPatterns.WhileLoop(cond, body, _) ->
            deps <- Set.union deps (visitExpr cond)
            deps <- Set.union deps (visitExpr body)
        | FSharpExprPatterns.FSharpFieldGet(objOpt, ty, _) ->
            (try deps <- deps.Add(ty.TypeDefinition.FullName) with _ -> ())
            objOpt |> Option.iter (fun o -> deps <- Set.union deps (visitExpr o))
        | FSharpExprPatterns.FSharpFieldSet(objOpt, ty, _, arg) ->
            (try deps <- deps.Add(ty.TypeDefinition.FullName) with _ -> ())
            objOpt |> Option.iter (fun o -> deps <- Set.union deps (visitExpr o))
            deps <- Set.union deps (visitExpr arg)
        | _ -> ()
        deps

    let rec visitExprRef (expr: FSharpExpr) =
        match expr with
        | FSharpExprPatterns.Call(objOpt, func, _, _, args) ->
            let decl = try func.DeclaringEntity.Value.FullName with _ -> ""
            if decl = "System.Net.Http.HttpClient" then
                hasHttpCall <- true
            match objOpt with Some o -> visitExprRef o | None -> ()
            args |> List.iter visitExprRef
        | _ -> () 

    let rec visit (d: FSharpImplementationFileDeclaration) : Set<string> =
        match d with
        | FSharpImplementationFileDeclaration.Entity(e, decls) ->
            let childDeps = decls |> List.map visit |> Set.unionMany
            childDeps
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, args, body) ->
            let rec checkHttp (e: FSharpExpr) =
                match e with
                | FSharpExprPatterns.Call(objOpt, func, _, _, exprArgs) ->
                    let decl = try func.DeclaringEntity.Value.FullName with _ -> ""
                    let name = try func.LogicalName with _ -> ""
                    if decl.StartsWith("System.Net") || name.Contains("HttpClient") || name.Contains("WebRequest") then
                        hasHttpCall <- true
                    match objOpt with Some o -> checkHttp o | None -> ()
                    exprArgs |> List.iter checkHttp
                | FSharpExprPatterns.Let((b, vExpr, _), bExpr) ->
                    checkHttp vExpr; checkHttp bExpr
                | FSharpExprPatterns.Sequential(e1, e2) ->
                    checkHttp e1; checkHttp e2
                | FSharpExprPatterns.Application(f, _, a) ->
                    checkHttp f; a |> List.iter checkHttp
                | FSharpExprPatterns.IfThenElse(c, t, f) ->
                    checkHttp c; checkHttp t; checkHttp f
                | FSharpExprPatterns.NewObject(ci, _, exprArgs) ->
                    let decl = try ci.DeclaringEntity.Value.FullName with _ -> ""
                    let logical = try ci.DeclaringEntity.Value.LogicalName with _ -> ""
                    if decl.StartsWith("System.Net") || logical.Contains("HttpClient") || logical.Contains("WebRequest") then
                        hasHttpCall <- true
                    exprArgs |> List.iter checkHttp
                | _ -> ()
            checkHttp body
            visitExpr body
        | FSharpImplementationFileDeclaration.InitAction(expr) ->
            visitExpr expr
            
    let deps = visit decl
    (deps, hasHttpCall)

let tryGetFullName (e: FSharp.Compiler.Symbols.FSharpEntity) =
    try Some e.FullName with | _ -> None

let buildGraph (files: (string * FSharpImplementationFileContents) list) : ModuleGraph =
    let mutable nodes = Map.empty
    
    let allInternalModules = HashSet<string>()
    for (_, tree) in files do
        let rec registerEntities (decls: FSharpImplementationFileDeclaration list) =
            for d in decls do
                match d with
                | FSharpImplementationFileDeclaration.Entity(e, childDecls) ->
                    match tryGetFullName e with
                    | Some name -> allInternalModules.Add(name) |> ignore
                    | None -> ()
                    registerEntities childDecls
                | _ -> ()
        registerEntities tree.Declarations

    for (file, tree) in files do
        let rec traverseEntities (decls: FSharpImplementationFileDeclaration list) =
            for d in decls do
                match d with
                | FSharpImplementationFileDeclaration.Entity(e, childDecls) ->
                    match tryGetFullName e with
                    | Some name ->
                        let (deps, makesHttp) = extractDependencies d
                        let internalDeps = 
                            deps 
                            |> Set.filter (fun dep -> allInternalModules.Contains(dep) && not (dep.StartsWith(name)))
                        
                        let node = {
                            Name = name
                            File = file
                            Layer = parseLayer name
                            Opens = []
                            References = Set.toList internalDeps
                            MakesHttpCall = makesHttp
                        }
                        nodes <- nodes.Add(name, node)
                    | None -> ()
                    
                    traverseEntities childDecls
                | _ -> ()
        traverseEntities tree.Declarations

        
    { Nodes = nodes }

let checkSSRF (graph: ModuleGraph) : Located<Rule> list =
    let mutable findings = []
    graph.Nodes |> Map.iter (fun name node ->
        if node.Layer = API && node.MakesHttpCall then
            findings <- (mkLocated FSASEC13 FSharp.Compiler.Text.Range.range0 |> Option.toList) @ findings
    )
    findings

let checkTDD (graph: ModuleGraph) : Located<Rule> list =
    let mutable findings = []
    graph.Nodes |> Map.iter (fun name node ->
        if node.Layer = Domain then
            let hasTest = graph.Nodes |> Map.exists (fun k _ -> k.Contains(name) && (k.Contains("Test") || k.Contains("Spec")))
            if not hasTest then
                findings <- (mkLocated FSATDD01 FSharp.Compiler.Text.Range.range0 |> Option.toList) @ findings
            else
                let testNodeOpt = graph.Nodes |> Map.tryPick (fun k v -> if k.Contains(name) && (k.Contains("Test") || k.Contains("Spec")) then Some v else None)
                match testNodeOpt with
                | Some tn ->
                    try
                        let getGitTime file =
                            let psi = System.Diagnostics.ProcessStartInfo("git", sprintf "log --diff-filter=A --format=%%at -1 -- \"%s\"" file)
                            psi.RedirectStandardOutput <- true
                            psi.UseShellExecute <- false
                            psi.WorkingDirectory <- System.IO.Path.GetDirectoryName(file)
                            let p = System.Diagnostics.Process.Start(psi)
                            p.WaitForExit()
                            let timeStr = p.StandardOutput.ReadToEnd().Trim()
                            if System.String.IsNullOrWhiteSpace(timeStr) then 0L else int64 timeStr
                            
                        let domainTime = getGitTime node.File
                        let testTime = getGitTime tn.File
                        
                        if domainTime > 0L && testTime > 0L && domainTime < testTime then
                            findings <- (mkLocated FSATDD04 FSharp.Compiler.Text.Range.range0 |> Option.toList) @ findings
                    with _ -> ()
                | None -> ()
    )
    findings

let detectCycles (graph: ModuleGraph) =
    let mutable findings = []
    let visited = HashSet<string>()
    let recStack = HashSet<string>()

    let rec dfs (nodeName: string) (path: string list) =
        if recStack.Contains(nodeName) then
            // Found a cycle
            match graph.Nodes.TryFind(nodeName) with
            | Some _ ->
                match mkLocated FSA2017 Range.range0 with
                | Some v -> findings <- v :: findings
                | None -> ()
            | None -> ()
        elif not (visited.Contains(nodeName)) then
            visited.Add(nodeName) |> ignore
            recStack.Add(nodeName) |> ignore
            
            match graph.Nodes.TryFind(nodeName) with
            | Some node ->
                for dep in node.References do
                    dfs dep (nodeName :: path)
            | None -> ()
            
            recStack.Remove(nodeName) |> ignore
            
    for node in graph.Nodes.Values do
        dfs node.Name [node.Name]
        
    findings

let calculateDepth (graph: ModuleGraph) =
    [] // Implementation for depth check FSA2016

let checkLayerViolations (graph: ModuleGraph) =
    let mutable findings = []
    for node in graph.Nodes.Values do
        for depName in node.References do
            match graph.Nodes.TryFind(depName) with
            | Some depNode ->
                let sourceLayer = layerValue node.Layer
                let targetLayer = layerValue depNode.Layer
                
                if node.Layer = Domain && depNode.Layer = Infrastructure then
                    match mkLocated FSAARCH01 Range.range0 with
                    | Some v -> findings <- v :: findings
                    | None -> ()
                
                if sourceLayer < targetLayer && sourceLayer < 10 && targetLayer < 10 then
                    match mkLocated FSAARCH02 Range.range0 with
                    | Some v -> findings <- v :: findings
                    | None -> ()
                    
            | None -> ()
    findings
