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
    Dependencies: Set<string>
}

type ModuleGraph = {
    Nodes: Map<string, ModuleNode>
}

let extractDependencies (decl: FSharpImplementationFileDeclaration) : Set<string> =
    let rec visitExpr (expr: FSharpExpr) : Set<string> =
        let mutable deps = Set.empty
        match expr with
        | FSharpExprPatterns.Call(obj, func, _, _, args) ->
            (try func.DeclaringEntity |> Option.iter (fun e -> deps <- deps.Add(e.FullName)) with _ -> ())
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

    let rec visit (d: FSharpImplementationFileDeclaration) : Set<string> =
        match d with
        | FSharpImplementationFileDeclaration.Entity(e, decls) ->
            let childDeps = decls |> List.map visit |> Set.unionMany
            childDeps
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, args, body) ->
            visitExpr body
        | FSharpImplementationFileDeclaration.InitAction(expr) ->
            visitExpr expr
            
    visit decl

let buildGraph (files: (string * FSharpImplementationFileContents) list) : ModuleGraph =
    let mutable nodes = Map.empty
    
    let allInternalModules = HashSet<string>()
    for (_, tree) in files do
        let rec registerEntities (decls: FSharpImplementationFileDeclaration list) =
            for d in decls do
                match d with
                | FSharpImplementationFileDeclaration.Entity(e, childDecls) ->
                    allInternalModules.Add(e.FullName) |> ignore
                    registerEntities childDecls
                | _ -> ()
        registerEntities tree.Declarations

    for (file, tree) in files do
        let rec traverseEntities (decls: FSharpImplementationFileDeclaration list) =
            for d in decls do
                match d with
                | FSharpImplementationFileDeclaration.Entity(e, childDecls) ->
                    let deps = extractDependencies d
                    let internalDeps = 
                        deps 
                        |> Set.filter (fun dep -> allInternalModules.Contains(dep) && not (dep.StartsWith(e.FullName)))
                    
                    let node = {
                        Name = e.FullName
                        File = file
                        Layer = parseLayer e.FullName
                        Dependencies = internalDeps
                    }
                    nodes <- nodes.Add(e.FullName, node)
                    
                    traverseEntities childDecls
                | _ -> ()
        traverseEntities tree.Declarations
        
    { Nodes = nodes }

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
                for dep in node.Dependencies do
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
        for depName in node.Dependencies do
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
