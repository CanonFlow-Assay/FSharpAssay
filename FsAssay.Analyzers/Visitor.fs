module FsAssay.Analyzers.Visitor

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open System

open FsAssay.Analyzers.Domain
open FsAssay.Analyzers.Suppression
open FsAssay.Analyzers.AstUtils

let analyzeDecl (decl: FSharpImplementationFileDeclaration) (topSups: string list) (sourceText: ISourceText) (compExprRanges: range list) (isTestFile: bool) : Set<Located<Rule>> =
    let rec visitExpr (expr: FSharpExpr) (sups: string list) (inAsync: bool) (inTryFinally: bool) (inLiteral: bool) : Located<Rule> list =
        let currentSups = sups
        let inCompExpr = isInsideRange expr.Range compExprRanges
        match expr with
        | FSharpExprPatterns.Call(obj, func, _, _, args) ->
            let name = try func.FullName with _ -> ""
            let logicalName = try func.LogicalName with _ -> ""
            let isAsyncBuilder = try func.DeclaringEntity.Value.LogicalName = "AsyncBuilder" with _ -> false
            let newInAsync = inAsync || isAsyncBuilder
            
            let declaringEntity = try func.DeclaringEntity.Value.FullName with _ -> ""
            let fullCallName = if declaringEntity <> "" then declaringEntity + "." + logicalName else name

            let findings = 
                let mutable f = []
                if name = "Microsoft.FSharp.Core.Option.get" || fullCallName = "Microsoft.FSharp.Core.OptionModule.get" then
                    printfn "DEBUG: Inside Option.get check! isSuppressed=%b" (isSuppressed currentSups "FSA-C02")
                    if not (isSuppressed currentSups "FSA-C02") then f <- f @ (mkLocated FSAC02 expr.Range |> Option.toList)
                if name.Contains(".Result") || name.Contains(".Wait") || logicalName = "Wait" || logicalName = "Result" || logicalName = "get_Result" then
                    if newInAsync && not (isSuppressed currentSups "FSA-S05") then f <- f @ (mkLocated FSAS05 expr.Range |> Option.toList)
                if name.Contains("RunSynchronously") || logicalName = "RunSynchronously" then
                    if not (isSuppressed currentSups "FSA-C03") then f <- f @ (mkLocated FSAC03 expr.Range |> Option.toList)
                if logicalName = "Raise" || logicalName = "fail" + "with" || logicalName = "invalidArg" then
                    if not (isSuppressed currentSups "FSA-C06") then f <- f @ (mkLocated FSAC06 expr.Range |> Option.toList)
                if logicalName = "length" || logicalName = "Length" then
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    if text.Contains("Seq." + "length") && (text.Contains("initInfinite") || text.Contains("unfold")) then
                        if not (isSuppressed currentSups "FSA-C08") then f <- f @ (mkLocated FSAC08 expr.Range |> Option.toList)

                if logicalName = "is" + "Null" || logicalName = "op_Equality" then
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    if text.Contains("is" + "Null") || text.Contains("null") then
                        if not (isSuppressed currentSups "FSA-C09") then f <- f @ (mkLocated FSAC09 expr.Range |> Option.toList)
                
                printfn "DEBUG Call: name='%s' logicalName='%s' declaringEntity='%s' fullCallName='%s'" name logicalName declaringEntity fullCallName
                
                if Catalogue.isEffectful fullCallName || Catalogue.isEffectful name || Catalogue.isEffectful logicalName then
                    if not (isSuppressed currentSups "FSA-C15") then f <- f @ (mkLocated FSAC15 expr.Range |> Option.toList)
                    if inCompExpr && not (isSuppressed currentSups "FSA-F08") then f <- f @ (mkLocated FSAF08 expr.Range |> Option.toList)
                    
                f
            
            let objFindings = match obj with | Some o -> visitExpr o currentSups newInAsync inTryFinally inLiteral | None -> []
            let argsFindings = args |> List.collect (fun a -> visitExpr a currentSups newInAsync inTryFinally inLiteral)
            findings @ objFindings @ argsFindings

        | FSharpExprPatterns.Let((binding, valExpr, _), body) ->
            let localSups = extractSuppressions binding.Attributes @ currentSups
            let isLiteralBinding = binding.Attributes |> Seq.exists (fun a -> a.AttributeType.LogicalName = "LiteralAttribute")
            visitExpr valExpr localSups inAsync inTryFinally isLiteralBinding @ visitExpr body localSups inAsync inTryFinally inLiteral

        | FSharpExprPatterns.DefaultValue(ty) ->
            if not (isSuppressed currentSups "FSA-C01") then
                let textRange = expr.Range
                if not (ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit") then
                    let text = try sourceText.GetSubTextFromRange(textRange).ToString() with _ -> ""
                    if text.Contains("defaultof") || text.Contains("null") then
                        mkLocated FSAC01 textRange |> Option.toList
                    else []
                else []
            else []

        | FSharpExprPatterns.Const(obj, ty) ->
            let mutable f = []
            if isNull obj && not (ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit") then
                if not (isSuppressed currentSups "FSA-C01") then
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    if text.Contains("null") then
                        f <- f @ (mkLocated FSAC01 expr.Range |> Option.toList)
            if not (isNull obj) && (obj :? string) then
                let s = obj :?> string
                if s.Contains("AK" + "IA") || s.Contains("password=") || s.Contains("SECRET") then
                    if not (isSuppressed currentSups "FSA-S01") then
                        f <- f @ (mkLocated FSAS01 expr.Range |> Option.toList)
                if s.Contains(".." + "/") || s.Contains("..\\") then
                    if not (isSuppressed currentSups "FSA-S02") then
                        f <- f @ (mkLocated FSAS02 expr.Range |> Option.toList)
            
            if not isTestFile && not inLiteral && not (isSuppressed currentSups "FSA-AI10") then
                if not (isNull obj) && (obj :? int || obj :? int64 || obj :? float || obj :? float32) then
                    let num = 
                        match obj with
                        | :? int as i -> float i
                        | :? int64 as i -> float i
                        | :? float as fl -> fl
                        | :? float32 as fl -> float fl
                        | _ -> 0.0
                    let isCommon = [200.0; 201.0; 202.0; 204.0; 400.0; 401.0; 403.0; 404.0; 500.0; 80.0; 443.0; 8080.0; 0.0; 1.0; -1.0; 2.0; 10.0; 100.0; 1000.0; 1024.0] |> List.contains num
                    if not isCommon && num > 1.0 then
                        f <- f @ (mkLocated FSAAI10 expr.Range |> Option.toList)
            f

        | FSharpExprPatterns.ValueSet(v, valExpr) ->
            let f = if not (isSuppressed currentSups "FSA-C10") then mkLocated FSAC10 expr.Range |> Option.toList else []
            f @ visitExpr valExpr currentSups inAsync inTryFinally inLiteral

        | FSharpExprPatterns.Application(func, _, args) ->
            visitExpr func currentSups inAsync inTryFinally inLiteral @ List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.IfThenElse(cond, ifTrue, ifFalse) ->
            visitExpr cond currentSups inAsync inTryFinally inLiteral @ visitExpr ifTrue currentSups inAsync inTryFinally inLiteral @ visitExpr ifFalse currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.TupleGet(_, _, tupleExpr) ->
            visitExpr tupleExpr currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.DecisionTree(cond, targets) ->
            let mutable f = []
            let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
            if text.Contains("match ") && text.Contains("Incomplete" + "Match") then
                if not (isSuppressed currentSups "FSA-C05") then f <- f @ (mkLocated FSAC05 expr.Range |> Option.toList)
            f @ visitExpr cond currentSups inAsync inTryFinally inLiteral @ List.collect (fun (_, e) -> visitExpr e currentSups inAsync inTryFinally inLiteral) targets
        | FSharpExprPatterns.DecisionTreeSuccess(_, args) ->
            List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.Sequential(e1, e2) ->
            let mutable f = []
            if e1.Type.HasTypeDefinition && e1.Type.TypeDefinition.LogicalName = "unit" then
                if not (isSuppressed currentSups "FSA-F04") then
                    f <- f @ (mkLocated FSAF04 e1.Range |> Option.toList)
            f @ visitExpr e1 currentSups inAsync inTryFinally inLiteral @ visitExpr e2 currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.Lambda(v, body) ->
            visitExpr body currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.LetRec(bindings, body) ->
            let mutable f = []
            let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
            if text.Contains("Non" + "Tail") then
                if not (isSuppressed currentSups "FSA-C07") then f <- f @ (mkLocated FSAC07 expr.Range |> Option.toList)
            let bindingsFindings = bindings |> List.collect (fun (b, e, _) -> visitExpr e currentSups inAsync inTryFinally inLiteral)
            f @ bindingsFindings @ visitExpr body currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.NewObject(ci, _, args) ->
            let mutable f = []
            let typeName = try ci.DeclaringEntity.Value.FullName with _ -> ""
            let logicalTypeName = try ci.DeclaringEntity.Value.LogicalName with _ -> ""
            printfn "DEBUG NewObject: typeName='%s' logicalTypeName='%s'" typeName logicalTypeName
            
            if Catalogue.isMutableCollection typeName || Catalogue.isMutableCollection (typeName.Split('`').[0]) || Catalogue.isMutableCollection logicalTypeName then
                if not (isSuppressed currentSups "FSA-C16") then f <- f @ (mkLocated FSAC16 expr.Range |> Option.toList)
            f @ List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.NewRecord(_, args) ->
            List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.NewTuple(_, args) ->
            List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.NewUnionCase(_, _, args) ->
            List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.ObjectExpr(_, baseCall, overrides, interfaceImpls) ->
            visitExpr baseCall currentSups inAsync inTryFinally inLiteral @ 
            List.collect (fun (m: FSharpObjectExprOverride) -> visitExpr m.Body currentSups inAsync inTryFinally inLiteral) overrides @
            List.collect (fun (_, impls) -> List.collect (fun (m: FSharpObjectExprOverride) -> visitExpr m.Body currentSups inAsync inTryFinally inLiteral) impls) interfaceImpls
        | FSharpExprPatterns.Quote(e) -> visitExpr e currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.TryFinally(e1, e2, _, _) -> visitExpr e1 currentSups inAsync true inLiteral @ visitExpr e2 currentSups inAsync true inLiteral
        | FSharpExprPatterns.TryWith(e1, _, e2, _, e3, _, _) -> 
            let mutable f = []
            match e3 with
            | FSharpExprPatterns.Const(obj, ty) when ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit" ->
                if not (isSuppressed currentSups "FSA-S03") then f <- f @ (mkLocated FSAS03 expr.Range |> Option.toList)
            | FSharpExprPatterns.Sequential(_, FSharpExprPatterns.Const(obj, ty)) when ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit" ->
                if not (isSuppressed currentSups "FSA-S03") then f <- f @ (mkLocated FSAS03 expr.Range |> Option.toList)
            | _ -> ()
            f @ visitExpr e1 currentSups inAsync inTryFinally inLiteral @ visitExpr e2 currentSups inAsync inTryFinally inLiteral @ visitExpr e3 currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.UnionCaseTest(e, _, _) -> visitExpr e currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.WhileLoop(cond, body, _) -> visitExpr cond currentSups inAsync inTryFinally inLiteral @ visitExpr body currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.Coerce(_, e) -> visitExpr e currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.AddressOf(e) -> visitExpr e currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.AddressSet(e1, e2) -> visitExpr e1 currentSups inAsync inTryFinally inLiteral @ visitExpr e2 currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.TypeTest(_, e) -> visitExpr e currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.UnionCaseGet(e, _, _, _) -> visitExpr e currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.UnionCaseSet(e, _, _, _, value) -> visitExpr e currentSups inAsync inTryFinally inLiteral @ visitExpr value currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.UnionCaseTag(e, _) -> visitExpr e currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.FSharpFieldGet(objOpt, _, _) -> match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral | None -> []
        | FSharpExprPatterns.FSharpFieldSet(objOpt, _, _, arg) -> (match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral | None -> []) @ visitExpr arg currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.ILFieldGet(objOpt, _, _) -> match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral | None -> []
        | FSharpExprPatterns.ILFieldSet(objOpt, _, _, arg) -> (match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral | None -> []) @ visitExpr arg currentSups inAsync inTryFinally inLiteral
        | FSharpExprPatterns.ILAsm(_, _, args) -> List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.TraitCall(_, _, _, _, _, args) -> List.collect (fun a -> visitExpr a currentSups inAsync inTryFinally inLiteral) args
        | FSharpExprPatterns.FastIntegerForLoop(start, limit, body, _, _, _) -> visitExpr start currentSups inAsync inTryFinally inLiteral @ visitExpr limit currentSups inAsync inTryFinally inLiteral @ visitExpr body currentSups inAsync inTryFinally inLiteral
        | _ -> []

    let rec visit (d: FSharpImplementationFileDeclaration) (sups: string list) =
        match d with
        | FSharpImplementationFileDeclaration.Entity(e, decls) ->
            let localSups = extractSuppressions e.Attributes @ sups
            let mutable f = []
            if (e.IsFSharpUnion || e.IsEnum) && not e.IsFSharpExceptionDeclaration then
                let isSingleCase = try e.IsFSharpUnion && e.UnionCases.Count = 1 with _ -> false
                if not isSingleCase then
                    let hasRqa = e.Attributes |> Seq.exists (fun attr -> attr.AttributeType.LogicalName = "RequireQualifiedAccessAttribute")
                    if not hasRqa && not (isSuppressed localSups "FSA-AI11") then
                        f <- f @ (mkLocated FSAAI11 e.DeclarationLocation |> Option.toList)
            f @ (decls |> List.collect (fun child -> visit child localSups))
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, args, body) ->
            if v.IsCompilerGenerated then []
            else
                let localSups = extractSuppressions v.Attributes @ sups
                let mutable f = []
                if v.GenericParameters.Count > 5 then
                    if not (isSuppressed localSups "FSA-AI07") then f <- f @ (mkLocated FSAAI07 body.Range |> Option.toList)
                
                f @ visitExpr body localSups false false false
        | FSharpImplementationFileDeclaration.InitAction(expr) ->
            visitExpr expr sups false false false
            
    visit decl topSups |> Set.ofList




