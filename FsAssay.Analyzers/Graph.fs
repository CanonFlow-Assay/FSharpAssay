module FsAssay.Analyzers.Graph

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FsAssay.Analyzers.Domain
open FsAssay.Analyzers.Catalogue
open FsAssay.Analyzers.AstUtils
open System.IO
open System.Collections.Generic

type Layer = // EXPECT: FSA-AI17 // EXPECT: FSA-AI11
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
    | API -> 3 // EXPECT: FSA-AI10
    | ONDC -> 4 // EXPECT: FSA-AI10
    | Infrastructure -> 5 // EXPECT: FSA-AI10
    | Unknown -> 10

type ModuleNode = {
    Name: string
    File: string
    Layer: Layer
    Opens: string list
    References: string list
    MakesHttpCall: bool
    Location: Range
}

type ModuleGraph = {
    Nodes: Map<string, ModuleNode>
}

let extractDependencies (decl: FSharpImplementationFileDeclaration) : Set<string> * bool =
    let rec visitExpr (expr: FSharpExpr) : Set<string> * bool =
        let foldExprs exprs =
            exprs |> List.fold (fun (accD, accH) e -> 
                let d, h = visitExpr e
                (Set.union accD d, accH || h)
            ) (Set.empty, false)

        match expr with
        | FSharpExprPatterns.Call(obj, func, _, _, args) ->
            let d1 = try match func.DeclaringEntity with Some e -> Set.singleton e.FullName | None -> Set.empty with _ -> Set.empty
            let declName = try func.DeclaringEntity.Value.FullName with _ -> ""
            let logicalName = try func.LogicalName with _ -> ""
            let h1 =
                declName.StartsWith("System.Net")
                && (logicalName.Contains("Create")
                    || logicalName.Contains("GetResponse")
                    || logicalName.Contains("Send"))
            let oDeps, oHttp = match obj with Some o -> visitExpr o | None -> (Set.empty, false)
            let aDeps, aHttp = foldExprs args
            (Set.unionMany [d1; oDeps; aDeps], h1 || oHttp || aHttp)
            
        | FSharpExprPatterns.Value(v) ->
            let d = try match v.DeclaringEntity with Some e -> Set.singleton e.FullName | None -> Set.empty with _ -> Set.empty
            (d, false)
            
        | FSharpExprPatterns.Let((binding, valExpr, _), body) ->
            let d1, h1 = visitExpr valExpr
            let d2, h2 = visitExpr body
            (Set.union d1 d2, h1 || h2)
            
        | FSharpExprPatterns.Application(func, _, args) ->
            let d1, h1 = visitExpr func
            let aDeps, aHttp = foldExprs args
            (Set.union d1 aDeps, h1 || aHttp)
            
        | FSharpExprPatterns.IfThenElse(cond, ifTrue, ifFalse) ->
            let d1, h1 = visitExpr cond
            let d2, h2 = visitExpr ifTrue
            let d3, h3 = visitExpr ifFalse
            (Set.unionMany [d1; d2; d3], h1 || h2 || h3)
            
        | FSharpExprPatterns.TupleGet(_, _, tupleExpr) ->
            visitExpr tupleExpr
            
        | FSharpExprPatterns.DecisionTree(cond, targets) ->
            let d1, h1 = visitExpr cond
            let tDeps, tHttp = targets |> List.map snd |> foldExprs
            (Set.union d1 tDeps, h1 || tHttp)
            
        | FSharpExprPatterns.DecisionTreeSuccess(_, args) ->
            foldExprs args
            
        | FSharpExprPatterns.Sequential(e1, e2) ->
            let d1, h1 = visitExpr e1
            let d2, h2 = visitExpr e2
            (Set.union d1 d2, h1 || h2)
            
        | FSharpExprPatterns.Lambda(v, body) ->
            visitExpr body
            
        | FSharpExprPatterns.LetRec(bindings, body) ->
            let bDeps, bHttp = bindings |> List.map (fun (_, e, _) -> e) |> foldExprs
            let d, h = visitExpr body
            (Set.union bDeps d, bHttp || h)
            
        | FSharpExprPatterns.NewObject(ci, _, args) ->
            let d1 = try match ci.DeclaringEntity with Some e -> Set.singleton e.FullName | None -> Set.empty with _ -> Set.empty
            let aDeps, aHttp = foldExprs args
            (Set.union d1 aDeps, aHttp)
            
        | FSharpExprPatterns.NewRecord(ty, args) ->
            let d1 = try Set.singleton ty.TypeDefinition.FullName with _ -> Set.empty
            let aDeps, aHttp = foldExprs args
            (Set.union d1 aDeps, aHttp)
            
        | FSharpExprPatterns.NewTuple(_, args) ->
            foldExprs args
            
        | FSharpExprPatterns.NewUnionCase(ty, uc, args) ->
            let d1 = try Set.singleton ty.TypeDefinition.FullName with _ -> Set.empty
            let aDeps, aHttp = foldExprs args
            (Set.union d1 aDeps, aHttp)
            
        | FSharpExprPatterns.ObjectExpr(ty, baseCall, overrides, interfaceImpls) ->
            let d1 = try Set.singleton ty.TypeDefinition.FullName with _ -> Set.empty
            let bd, bh = visitExpr baseCall
            let od, oh = overrides |> List.map (fun m -> m.Body) |> foldExprs
            let id, ih = interfaceImpls |> List.collect (fun (_, impls) -> impls |> List.map (fun m -> m.Body)) |> foldExprs
            (Set.unionMany [d1; bd; od; id], bh || oh || ih)
            
        | FSharpExprPatterns.TryFinally(e1, e2, _, _) ->
            let d1, h1 = visitExpr e1
            let d2, h2 = visitExpr e2
            (Set.union d1 d2, h1 || h2)
            
        | FSharpExprPatterns.TryWith(e1, _, e2, _, e3, _, _) -> 
            let d1, h1 = visitExpr e1
            let d2, h2 = visitExpr e2
            let d3, h3 = visitExpr e3
            (Set.unionMany [d1; d2; d3], h1 || h2 || h3)
            
        | FSharpExprPatterns.WhileLoop(cond, body, _) ->
            let d1, h1 = visitExpr cond
            let d2, h2 = visitExpr body
            (Set.union d1 d2, h1 || h2)

        | FSharpExprPatterns.Coerce(_, expression)
        | FSharpExprPatterns.AddressOf(expression)
        | FSharpExprPatterns.TypeTest(_, expression)
        | FSharpExprPatterns.UnionCaseTest(expression, _, _)
        | FSharpExprPatterns.UnionCaseGet(expression, _, _, _)
        | FSharpExprPatterns.UnionCaseTag(expression, _) ->
            visitExpr expression
            
        | FSharpExprPatterns.FSharpFieldGet(objOpt, ty, _) ->
            let d1 = try Set.singleton ty.TypeDefinition.FullName with _ -> Set.empty
            let od, oh = match objOpt with Some o -> visitExpr o | None -> (Set.empty, false)
            (Set.union d1 od, oh)
            
        | FSharpExprPatterns.FSharpFieldSet(objOpt, ty, _, arg) ->
            let d1 = try Set.singleton ty.TypeDefinition.FullName with _ -> Set.empty
            let od, oh = match objOpt with Some o -> visitExpr o | None -> (Set.empty, false)
            let ad, ah = visitExpr arg
            (Set.unionMany [d1; od; ad], oh || ah)
            
        | _ -> (Set.empty, false)

    let rec visit (d: FSharpImplementationFileDeclaration) : Set<string> * bool =
        match d with
        | FSharpImplementationFileDeclaration.Entity(e, decls) ->
            decls |> List.fold (fun (accD, accH) child -> 
                let d, h = visit child
                (Set.union accD d, accH || h)
            ) (Set.empty, false)
            
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, args, body) ->
            let rec checkHttp (e: FSharpExpr) =
                match e with
                | FSharpExprPatterns.Call(objOpt, func, _, _, exprArgs) ->
                    let decl = try func.DeclaringEntity.Value.FullName with _ -> ""
                    let name = try func.LogicalName with _ -> ""
                    let h1 = decl.StartsWith("System.Net") || name.Contains("HttpClient") || name.Contains("WebRequest")
                    let oh = match objOpt with Some o -> checkHttp o | None -> false
                    let ah = exprArgs |> List.exists checkHttp
                    h1 || oh || ah
                | FSharpExprPatterns.Let((b, vExpr, _), bExpr) ->
                    checkHttp vExpr || checkHttp bExpr
                | FSharpExprPatterns.Sequential(e1, e2) ->
                    checkHttp e1 || checkHttp e2
                | FSharpExprPatterns.Application(f, _, a) ->
                    checkHttp f || a |> List.exists checkHttp
                | FSharpExprPatterns.IfThenElse(c, t, f) ->
                    checkHttp c || checkHttp t || checkHttp f
                | FSharpExprPatterns.NewObject(ci, _, exprArgs) ->
                    let decl = try ci.DeclaringEntity.Value.FullName with _ -> ""
                    let logical = try ci.DeclaringEntity.Value.LogicalName with _ -> ""
                    let h1 = decl.StartsWith("System.Net") || logical.Contains("HttpClient") || logical.Contains("WebRequest")
                    let ah = exprArgs |> List.exists checkHttp
                    h1 || ah
                | FSharpExprPatterns.Coerce(_, expression)
                | FSharpExprPatterns.AddressOf(expression)
                | FSharpExprPatterns.TypeTest(_, expression)
                | FSharpExprPatterns.UnionCaseTest(expression, _, _)
                | FSharpExprPatterns.UnionCaseGet(expression, _, _, _)
                | FSharpExprPatterns.UnionCaseTag(expression, _) ->
                    checkHttp expression
                | _ -> false
                
            let isHttp1 = checkHttp body
            let deps, isHttp2 = visitExpr body
            (deps, isHttp1 || isHttp2)
            
        | FSharpImplementationFileDeclaration.InitAction(expr) ->
            visitExpr expr
            
    visit decl

let tryGetFullName (e: FSharp.Compiler.Symbols.FSharpEntity) =
    try Some e.FullName with | _ -> None

let buildGraph (files: (string * FSharpImplementationFileContents) list) : ModuleGraph =
    let allInternalModules = 
        files |> List.fold (fun acc (_, tree) ->
            let rec registerEntities decls s =
                decls |> List.fold (fun (accS: Set<string>) d ->
                    match d with
                    | FSharpImplementationFileDeclaration.Entity(e, childDecls) ->
                        let s1 = match tryGetFullName e with Some name -> accS.Add(name) | None -> accS
                        registerEntities childDecls s1
                    | _ -> accS
                ) s
            registerEntities tree.Declarations acc
        ) Set.empty

    let nodes = 
        files |> List.fold (fun accMap (file, tree) ->
            let rec traverseEntities decls map =
                decls |> List.fold (fun accM d ->
                    match d with
                    | FSharpImplementationFileDeclaration.Entity(e, childDecls) ->
                        match tryGetFullName e with
                        | Some name ->
                            let deps, makesHttp = extractDependencies d
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
                                Location = e.DeclarationLocation
                            }
                            traverseEntities childDecls (accM |> Map.add name node)
                        | None -> traverseEntities childDecls accM
                    | _ -> accM
                ) map
            traverseEntities tree.Declarations accMap
        ) Map.empty
        
    { Nodes = nodes }

let checkSSRF (graph: ModuleGraph) : Located<Rule> list =
    graph.Nodes 
    |> Map.toList 
    |> List.choose (fun (_, node) -> 
        if node.Layer = API && node.MakesHttpCall then mkLocated FSASEC13 node.Location else None
    )

let checkTDD (graph: ModuleGraph) : Located<Rule> list =
    graph.Nodes 
    |> Map.toList 
    |> List.choose (fun (_, node) ->
        if node.Layer = Domain then
            let hasTest = graph.Nodes |> Map.exists (fun k _ -> k.Contains(node.Name) && (k.Contains("Test") || k.Contains("Spec")))
            if not hasTest then
                mkLocated FSATDD01 node.Location
            else
                let testNodeOpt = graph.Nodes |> Map.tryPick (fun k v -> if k.Contains(node.Name) && (k.Contains("Test") || k.Contains("Spec")) then Some v else None)
                match testNodeOpt with
                | Some tn ->
                    try
                        let getGitTime file =
                            let psi = System.Diagnostics.ProcessStartInfo("git", sprintf "log --diff-filter=A --format=%%at -1 -- \"%s\"" file)
                            psi.RedirectStandardOutput <- true // EXPECT: FSA-F04
                            psi.UseShellExecute <- false // EXPECT: FSA-F04
                            psi.WorkingDirectory <- System.IO.Path.GetDirectoryName(file) // EXPECT: FSA2022 // EXPECT: FSA-F04
                            let p = System.Diagnostics.Process.Start(psi)
                            p.WaitForExit() // EXPECT: FSA-F04
                            let timeStr = p.StandardOutput.ReadToEnd().Trim() // EXPECT: FSA2022
                            if System.String.IsNullOrWhiteSpace(timeStr) then 0L else int64 timeStr
                            
                        let domainTime = getGitTime node.File
                        let testTime = getGitTime tn.File
                        
                        if domainTime > 0L && testTime > 0L && domainTime < testTime then
                            mkLocated FSATDD04 node.Location
                        else None
                    with _ -> None
                | None -> None
        else None
    )

let detectCycles (graph: ModuleGraph) =
    let dfs (startNode: string) =
        let rec loop nodeName path (visited: Set<string>) (recStack: Set<string>) findings =
            if recStack.Contains(nodeName) then
                match graph.Nodes.TryFind(nodeName) with
                | Some node -> 
                    match mkLocated FSA2017 node.Location with
                    | Some v -> (visited, recStack, v :: findings)
                    | None -> (visited, recStack, findings)
                | None -> (visited, recStack, findings)
            elif not (visited.Contains(nodeName)) then
                let v1 = visited.Add(nodeName)
                let r1 = recStack.Add(nodeName)
                match graph.Nodes.TryFind(nodeName) with
                | Some node ->
                    let (v2, r2, f2) = 
                        node.References |> List.fold (fun (accV, accR, accF) dep ->
                            let (nv, nr, nf) = loop dep (nodeName :: path) accV accR []
                            (nv, nr, accF @ nf)
                        ) (v1, r1, findings)
                    (v2, r2.Remove(nodeName), f2)
                | None -> (v1, r1.Remove(nodeName), findings)
            else
                (visited, recStack, findings)
        loop startNode [startNode] Set.empty Set.empty []
        
    let (_, _, allFindings) =
        graph.Nodes.Keys |> Seq.fold (fun (accV, accR, accF) nodeName ->
            let (_, _, newFindings) = dfs nodeName
            (accV, accR, accF @ newFindings)
        ) (Set.empty, Set.empty, [])
        
    allFindings

let calculateDepth (graph: ModuleGraph) =
    [] // Implementation for depth check FSA2016

let checkLayerViolations (graph: ModuleGraph) =
    graph.Nodes.Values 
    |> Seq.fold (fun acc node ->
        let findingsForNode = 
            node.References |> List.choose (fun depName ->
                match graph.Nodes.TryFind(depName) with
                | Some depNode ->
                    let sourceLayer = layerValue node.Layer
                    let targetLayer = layerValue depNode.Layer
                    
                    let f1 = if node.Layer = Domain && depNode.Layer = Infrastructure then mkLocated FSAARCH01 node.Location else None
                    let f2 = if sourceLayer < targetLayer && sourceLayer < 10 && targetLayer < 10 then mkLocated FSAARCH02 node.Location else None
                    
                    match f1, f2 with
                    | Some v1, Some v2 -> Some [v1; v2]
                    | Some v1, None -> Some [v1]
                    | None, Some v2 -> Some [v2]
                    | None, None -> None
                | None -> None
            ) |> List.concat
        acc @ findingsForNode
    ) []
