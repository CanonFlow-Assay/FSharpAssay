module FsAssay.Analyzers.Visitor

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open System
open System.Text.RegularExpressions

open FsAssay.Analyzers.Domain
open FsAssay.Analyzers.Suppression
open FsAssay.Analyzers.AstUtils

let private isExternalIoType (typeName: string) =
    let isMemoryOnly =
        typeName.StartsWith("System.IO.MemoryStream", StringComparison.Ordinal)

    not isMemoryOnly
    && (typeName.StartsWith("System.IO", StringComparison.Ordinal)
        || typeName.StartsWith("System.Net.Http", StringComparison.Ordinal)
        || typeName.Contains("HttpClient", StringComparison.Ordinal))

let private semanticNameTokens (name: string) =
    Regex.Matches(name, "[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+")
    |> Seq.map (fun value -> value.Value.ToLowerInvariant())
    |> Set.ofSeq

let private isAiOperationName name =
    let tokens = semanticNameTokens name
    Set.contains "ai" tokens
    || Set.contains "llm" tokens
    || Set.contains "generate" tokens

let isExplicitBoxCall logicalName compilerGenerated =
    logicalName = "box" && not compilerGenerated

let isExplicitObjectCoercion (text: string) =
    text.Contains(":> obj", StringComparison.Ordinal)
    || text.Contains(":> System.Object", StringComparison.Ordinal)

let analyzeDecl (decl: FSharpImplementationFileDeclaration) (topSups: string list) (sourceText: ISourceText) (compExprRanges: range list) (isTestFile: bool) (hasProperty: bool) : Set<Located<Rule>> * bool =
    let rec visitExpr (expr: FSharpExpr) (sups: string list) (inAsync: bool) (inTryFinally: bool) (inLiteral: bool) (inLoop: bool) (assertionsCount: int) : Located<Rule> list * int = // EXPECT: FSA-C07
        let currentSups = sups
        let inCompExpr = isInsideRange expr.Range compExprRanges
        
        let foldExprs exprs state =
            exprs |> List.fold (fun (accF, accS) e -> 
                let f, s = visitExpr e currentSups inAsync inTryFinally inLiteral inLoop accS
                (accF @ f, s)
            ) ([], state)

        match expr with
        | FSharpExprPatterns.Call(obj, func, _, _, args) ->
            let name = try func.FullName with _ -> ""
            let logicalName = try func.LogicalName with _ -> ""
            let isAsyncBuilder = try func.DeclaringEntity.Value.LogicalName = "AsyncBuilder" with _ -> false
            let newInAsync = inAsync || isAsyncBuilder
            
            let declaringEntity = try func.DeclaringEntity.Value.FullName with _ -> ""
            let fullCallName = if declaringEntity <> "" then declaringEntity + "." + logicalName else name

            let f1 = if (name = "Microsoft.FSharp.Core.Option.get" || fullCallName = "Microsoft.FSharp.Core.OptionModule.get") && not (isSuppressed currentSups "FSA-C02") then mkLocated FSAC02 expr.Range |> Option.toList else []
            let f2 = if (name.Contains(".Result") || name.Contains(".Wait") || logicalName = "Wait" || logicalName = "Result" || logicalName = "get_Result") && newInAsync && not (isSuppressed currentSups "FSA-S05") then mkLocated FSAS05 expr.Range |> Option.toList else []
            let f3 = if (name.Contains("RunSynchronously") || logicalName = "RunSynchronously") && not (isSuppressed currentSups "FSA-C03") then mkLocated FSAC03 expr.Range |> Option.toList else []
            let f4 = if (logicalName = "Raise" || logicalName = "failwith" || logicalName = "invalidArg") && not (isSuppressed currentSups "FSA-C06") then mkLocated FSAC06 expr.Range |> Option.toList else []
            
            let f5 = 
                if logicalName = "length" || logicalName = "Length" then
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    if text.Contains("Seq.length") && (text.Contains("initInfinite") || text.Contains("unfold")) && not (isSuppressed currentSups "FSA-C08") then mkLocated FSAC08 expr.Range |> Option.toList else []
                else []

            let f6 = 
                if logicalName = "isNull" || logicalName = "op_Equality" then // EXPECT: FSA-C09
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    if (text.Contains("isNull") || text.Contains("null")) && not (isSuppressed currentSups "FSA-C09") then mkLocated FSAC09 expr.Range |> Option.toList else []
                else []
                
            let f7 =
                if isExternalIoType declaringEntity
                   && not (isSuppressed currentSups "FSA2022") then
                    mkLocated FSA2022 expr.Range |> Option.toList
                else
                    []
            
            let f8 = 
                if fullCallName.Contains("OpenAI") || fullCallName.Contains("Anthropic") || fullCallName.Contains("Gemini") || fullCallName.Contains("LLM") then
                    let a1 = if not (isSuppressed currentSups "FSA-AI01") then mkLocated FSAAI01 expr.Range |> Option.toList else []
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    let a2 = if not (text.Contains("max_tokens") || text.Contains("MaxTokens")) && not (isSuppressed currentSups "FSA-AI13") then mkLocated FSAAI13 expr.Range |> Option.toList else []
                    let a3 = if not (text.Contains("retry") || text.Contains("Polly") || text.Contains("try")) && not (isSuppressed currentSups "FSA-AI14") then mkLocated FSAAI14 expr.Range |> Option.toList else []
                    a1 @ a2 @ a3
                else []
                
            let f9 = 
                if logicalName.Contains("Log") || logicalName = "printfn" || logicalName = "printf" || logicalName = "Write" || logicalName = "WriteLine" then
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString().ToLowerInvariant() with _ -> ""
                    if (text.Contains("password") || text.Contains("ssn") || text.Contains("email") || text.Contains("phone") || text.Contains("pii")) && not (isSuppressed currentSups "FSA-SEC12") then mkLocated FSASEC12 expr.Range |> Option.toList else []
                else []
                
            let f10 = 
                let nLow = logicalName.ToLowerInvariant()
                if nLow.Contains("send") || nLow.Contains("post") || nLow.Contains("publish") then
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    let hasOndcType = args |> List.exists (fun a -> try a.Type.TypeDefinition.LogicalName.Contains("ONDC") with _ -> false)
                    if (text.Contains("ONDCMessage") || hasOndcType) && not (text.Contains("Sign")) && not (isSuppressed currentSups "FSA-SEC11") then mkLocated FSASEC11 expr.Range |> Option.toList else []
                else []
                
            let f11 = 
                if Catalogue.isEffectful fullCallName || Catalogue.isEffectful name || Catalogue.isEffectful logicalName then
                    let b1 = if not (isSuppressed currentSups "FSA-C15") then mkLocated FSAC15 expr.Range |> Option.toList else []
                    let b2 = if inCompExpr && not (isSuppressed currentSups "FSA-F08") then mkLocated FSAF08 expr.Range |> Option.toList else []
                    b1 @ b2
                else []
                
            let isAssert = fullCallName.Contains("Expect") || fullCallName.Contains("Assert") || logicalName = "should"
            let newState = if isAssert then assertionsCount + 1 else assertionsCount
            
            let f12 = 
                if inLoop then
                    let c1 = if (logicalName = "op_Append" || (logicalName = "append" && declaringEntity = "Microsoft.FSharp.Collections.ArrayModule")) && not (isSuppressed currentSups "FSA-P01") then mkLocated FSAP01 expr.Range |> Option.toList else []
                    let c2 = if logicalName = "op_Addition" && args.Length = 2 && args.[0].Type.HasTypeDefinition && args.[0].Type.TypeDefinition.LogicalName = "string" && not (isSuppressed currentSups "FSA-P04") then mkLocated FSAP04 expr.Range |> Option.toList else []
                    c1 @ c2
                else []
                
            let f13 = 
                if logicalName = "op_Addition" && args.Length = 2 && args.[0].Type.HasTypeDefinition && args.[0].Type.TypeDefinition.LogicalName = "string" then
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString().ToLowerInvariant() with _ -> ""
                    let d1 = if (text.Contains("prompt") || text.Contains("system") || text.Contains("user")) && not (isSuppressed currentSups "FSA-AI15") then mkLocated FSAAI15 expr.Range |> Option.toList else []
                    let d2 = if text.Contains("input") && not (isSuppressed currentSups "FSA-AI19") then mkLocated FSAAI19 expr.Range |> Option.toList else []
                    d1 @ d2
                else []
                
            let f14 =
                let compilerGenerated =
                    try func.IsCompilerGenerated with _ -> false

                if isExplicitBoxCall logicalName compilerGenerated
                   && not (isSuppressed currentSups "FSA-P02") then
                    mkLocated FSAP02 expr.Range |> Option.toList
                else
                    []

            let f15 =
                let text =
                    try sourceText.GetSubTextFromRange(expr.Range).ToString()
                    with _ -> ""

                let hasDirectNestedCall expectedName expectedEntity =
                    args
                    |> List.exists (function
                        | FSharpExprPatterns.Call(_, nested, _, _, _) ->
                            let nestedName =
                                try nested.LogicalName with _ -> ""
                            let nestedEntity =
                                try nested.DeclaringEntity.Value.FullName with _ -> ""
                            nestedName = expectedName && nestedEntity = expectedEntity
                        | _ -> false)

                let redundantRoundTrip =
                    (logicalName = "toSeq"
                     && declaringEntity = "Microsoft.FSharp.Collections.ListModule"
                     && (text.Contains("Seq.toList", StringComparison.Ordinal)
                         || hasDirectNestedCall "toList" "Microsoft.FSharp.Collections.SeqModule"))
                    || (logicalName = "toList"
                        && declaringEntity = "Microsoft.FSharp.Collections.SeqModule"
                        && (text.Contains("List.toSeq", StringComparison.Ordinal)
                            || hasDirectNestedCall "toSeq" "Microsoft.FSharp.Collections.ListModule"))

                if redundantRoundTrip
                   && not (isSuppressed currentSups "FSA-P03") then
                    mkLocated FSAP03 expr.Range |> Option.toList
                else
                    []
            
            let findings = f1 @ f2 @ f3 @ f4 @ f5 @ f6 @ f7 @ f8 @ f9 @ f10 @ f11 @ f12 @ f13 @ f14 @ f15
            
            let (objF, state1) = match obj with Some o -> visitExpr o currentSups newInAsync inTryFinally inLiteral inLoop newState | None -> ([], newState)
            let (argsF, state2) = foldExprs args state1
            (findings @ objF @ argsF, state2)

        | FSharpExprPatterns.Let((binding, valExpr, _), body) ->
            let localSups = extractSuppressions binding.Attributes @ currentSups
            let isLiteralBinding = binding.Attributes |> Seq.exists (fun a -> a.AttributeType.LogicalName = "LiteralAttribute")
            let f1, s1 = visitExpr valExpr localSups inAsync inTryFinally isLiteralBinding inLoop assertionsCount
            let f2, s2 = visitExpr body localSups inAsync inTryFinally inLiteral inLoop s1
            (f1 @ f2, s2)

        | FSharpExprPatterns.DefaultValue(ty) ->
            let f =
                if not (isSuppressed currentSups "FSA-C01") then
                    let textRange = expr.Range
                    if not (ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit") then
                        let text = try sourceText.GetSubTextFromRange(textRange).ToString() with _ -> ""
                        if text.Contains("defaultof") || text.Contains("null") then
                            mkLocated FSAC01 textRange |> Option.toList
                        else []
                    else []
                else []
            (f, assertionsCount)

        | FSharpExprPatterns.Const(obj, ty) ->
            let f1 = 
                if isNull obj && not (ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit") && not (isSuppressed currentSups "FSA-C01") then // EXPECT: FSA-C09
                    let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
                    if text.Contains("null") then mkLocated FSAC01 expr.Range |> Option.toList else []
                else []
                
            let f2 = 
                if not (isNull obj) && (obj :? string) then // EXPECT: FSA-C09
                    let s = obj :?> string
                    let a = if (s.Contains("AKIA") || s.Contains("password=") || s.Contains("SECRET")) && not (isSuppressed currentSups "FSA-S01") then mkLocated FSAS01 expr.Range |> Option.toList else [] // EXPECT: FSA-S01
                    let b = if (s.StartsWith("sk-") || s.StartsWith("sk_live")) && not (isSuppressed currentSups "FSA-AI12") then mkLocated FSAAI12 expr.Range |> Option.toList else [] // EXPECT: FSA-AI12
                    let c = if (s.Contains("../") || s.Contains("..\\")) && not (isSuppressed currentSups "FSA-S02") then mkLocated FSAS02 expr.Range |> Option.toList else [] // EXPECT: FSA-S02
                    a @ b @ c
                else []
            
            let f3 = 
                if not isTestFile && not inLiteral && not (isSuppressed currentSups "FSA-AI10") then
                    if not (isNull obj) && (obj :? int || obj :? int64 || obj :? float || obj :? float32) then // EXPECT: FSA-C09
                        let num = 
                            match obj with
                            | :? int as i -> float i
                            | :? int64 as i -> float i
                            | :? float as fl -> fl
                            | :? float32 as fl -> float fl
                            | _ -> 0.0
                        let isCommon = [200.0; 201.0; 202.0; 204.0; 400.0; 401.0; 403.0; 404.0; 500.0; 80.0; 443.0; 8080.0; 0.0; 1.0; -1.0; 2.0; 10.0; 100.0; 1000.0; 1024.0] |> List.contains num
                        let a = if not isCommon && num > 1.0 then mkLocated FSAAI10 expr.Range |> Option.toList else []
                        let b = 
                            if num > 1.0 && not (isSuppressed currentSups "FSA-AI18") then
                                let lineText = try sourceText.GetLineString(expr.Range.StartLine - 1).ToLowerInvariant() with _ -> ""
                                if lineText.Contains("temperature") then mkLocated FSAAI18 expr.Range |> Option.toList else []
                            else []
                        a @ b
                    else []
                else []
            (f1 @ f2 @ f3, assertionsCount)

        | FSharpExprPatterns.ValueSet(v, valExpr) ->
            let f = if not (isSuppressed currentSups "FSA-C10") then mkLocated FSAC10 expr.Range |> Option.toList else []
            let (vf, vs) = visitExpr valExpr currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            (f @ vf, vs)

        | FSharpExprPatterns.Application(func, _, args) ->
            let (ff, fs) = visitExpr func currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (af, as_) = foldExprs args fs
            (ff @ af, as_)
            
        | FSharpExprPatterns.IfThenElse(cond, ifTrue, ifFalse) ->
            let (cf, cs) = visitExpr cond currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (tf, ts) = visitExpr ifTrue currentSups inAsync inTryFinally inLiteral inLoop cs
            let (ff, fs) = visitExpr ifFalse currentSups inAsync inTryFinally inLiteral inLoop ts
            (cf @ tf @ ff, fs)
            
        | FSharpExprPatterns.TupleGet(_, _, tupleExpr) ->
            visitExpr tupleExpr currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            
        | FSharpExprPatterns.DecisionTree(cond, targets) ->
            let (cf, cs) = visitExpr cond currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (tf, ts) = targets |> List.map snd |> fun tgts -> foldExprs tgts cs
            (cf @ tf, ts)
            
        | FSharpExprPatterns.DecisionTreeSuccess(_, args) ->
            foldExprs args assertionsCount
            
        | FSharpExprPatterns.Sequential(e1, e2) ->
            let f = if e1.Type.HasTypeDefinition && e1.Type.TypeDefinition.LogicalName = "unit" && not (isSuppressed currentSups "FSA-F04") then mkLocated FSAF04 e1.Range |> Option.toList else []
            let (e1f, e1s) = visitExpr e1 currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (e2f, e2s) = visitExpr e2 currentSups inAsync inTryFinally inLiteral inLoop e1s
            (f @ e1f @ e2f, e2s)
            
        | FSharpExprPatterns.Lambda(v, body) ->
            visitExpr body currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            
        | FSharpExprPatterns.LetRec(bindings, body) ->
            let text = try sourceText.GetSubTextFromRange(expr.Range).ToString() with _ -> ""
            let f = if text.Contains("NonTail") && not (isSuppressed currentSups "FSA-C07") then mkLocated FSAC07 expr.Range |> Option.toList else []
            let (bf, bs) = bindings |> List.map (fun (_, e, _) -> e) |> fun exprs -> foldExprs exprs assertionsCount
            let (bodyf, bodys) = visitExpr body currentSups inAsync inTryFinally inLiteral inLoop bs
            (f @ bf @ bodyf, bodys)
            
        | FSharpExprPatterns.NewObject(ci, _, args) ->
            let typeName = try ci.DeclaringEntity.Value.FullName with _ -> ""
            let logicalTypeName = try ci.DeclaringEntity.Value.LogicalName with _ -> ""
            let f1 = if (Catalogue.isMutableCollection typeName || Catalogue.isMutableCollection (typeName.Split('`').[0]) || Catalogue.isMutableCollection logicalTypeName) && not (isSuppressed currentSups "FSA-C16") then mkLocated FSAC16 expr.Range |> Option.toList else []
            let f2 =
                if isExternalIoType typeName
                   && not (isSuppressed currentSups "FSA2022") then
                    mkLocated FSA2022 expr.Range |> Option.toList
                else
                    []
            let (af, as_) = foldExprs args assertionsCount
            (f1 @ f2 @ af, as_)
            
        | FSharpExprPatterns.NewRecord(_, args) -> foldExprs args assertionsCount
        | FSharpExprPatterns.NewTuple(_, args) -> foldExprs args assertionsCount
        | FSharpExprPatterns.NewUnionCase(_, _, args) -> foldExprs args assertionsCount
        
        | FSharpExprPatterns.ObjectExpr(_, baseCall, overrides, interfaceImpls) ->
            let (bf, bs) = visitExpr baseCall currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (of_, os) = overrides |> List.map (fun m -> m.Body) |> fun exprs -> foldExprs exprs bs
            let (if_, is_) = interfaceImpls |> List.collect (fun (_, impls) -> impls |> List.map (fun m -> m.Body)) |> fun exprs -> foldExprs exprs os
            (bf @ of_ @ if_, is_)
            
        | FSharpExprPatterns.Quote(e) -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
        
        | FSharpExprPatterns.TryFinally(e1, e2, _, _) -> 
            let (e1f, e1s) = visitExpr e1 currentSups inAsync true inLiteral inLoop assertionsCount
            let (e2f, e2s) = visitExpr e2 currentSups inAsync true inLiteral inLoop e1s
            (e1f @ e2f, e2s)
            
        | FSharpExprPatterns.TryWith(e1, _, e2, _, e3, _, _) -> 
            let f = 
                match e3 with
                | FSharpExprPatterns.Const(obj, ty) when ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit" ->
                    if not (isSuppressed currentSups "FSA-S03") then mkLocated FSAS03 expr.Range |> Option.toList else []
                | FSharpExprPatterns.Sequential(_, FSharpExprPatterns.Const(obj, ty)) when ty.HasTypeDefinition && ty.TypeDefinition.LogicalName = "unit" ->
                    if not (isSuppressed currentSups "FSA-S03") then mkLocated FSAS03 expr.Range |> Option.toList else []
                | _ -> []
            let (e1f, e1s) = visitExpr e1 currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (e2f, e2s) = visitExpr e2 currentSups inAsync inTryFinally inLiteral inLoop e1s
            let (e3f, e3s) = visitExpr e3 currentSups inAsync inTryFinally inLiteral inLoop e2s
            (f @ e1f @ e2f @ e3f, e3s)
            
        | FSharpExprPatterns.UnionCaseTest(e, _, _) -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
        
        | FSharpExprPatterns.WhileLoop(cond, body, _) -> 
            let (cf, cs) = visitExpr cond currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (bf, bs) = visitExpr body currentSups inAsync inTryFinally inLiteral true cs
            (cf @ bf, bs)
            
        | FSharpExprPatterns.Coerce(ty, e) -> 
            let text =
                try sourceText.GetSubTextFromRange(expr.Range).ToString()
                with _ -> ""

            let f =
                if ty.HasTypeDefinition
                   && ty.TypeDefinition.LogicalName = "obj"
                   && isExplicitObjectCoercion text
                   && not (isSuppressed currentSups "FSA-P02") then
                    mkLocated FSAP02 expr.Range |> Option.toList
                else
                    []
            let (ef, es) = visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            (f @ ef, es)
            
        | FSharpExprPatterns.AddressOf(e) -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
        | FSharpExprPatterns.AddressSet(e1, e2) -> 
            let (e1f, e1s) = visitExpr e1 currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (e2f, e2s) = visitExpr e2 currentSups inAsync inTryFinally inLiteral inLoop e1s
            (e1f @ e2f, e2s)
            
        | FSharpExprPatterns.TypeTest(_, e) -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
        | FSharpExprPatterns.UnionCaseGet(e, _, _, _) -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
        | FSharpExprPatterns.UnionCaseSet(e, _, _, _, value) -> 
            let (ef, es) = visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (vf, vs) = visitExpr value currentSups inAsync inTryFinally inLiteral inLoop es
            (ef @ vf, vs)
            
        | FSharpExprPatterns.UnionCaseTag(e, _) -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
        
        | FSharpExprPatterns.FSharpFieldGet(objOpt, _, _) -> match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount | None -> ([], assertionsCount)
        | FSharpExprPatterns.FSharpFieldSet(objOpt, _, _, arg) -> 
            let (of_, os) = match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount | None -> ([], assertionsCount)
            let (af, as_) = visitExpr arg currentSups inAsync inTryFinally inLiteral inLoop os
            (of_ @ af, as_)
            
        | FSharpExprPatterns.ILFieldGet(objOpt, _, _) -> match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount | None -> ([], assertionsCount)
        | FSharpExprPatterns.ILFieldSet(objOpt, _, _, arg) -> 
            let (of_, os) = match objOpt with Some e -> visitExpr e currentSups inAsync inTryFinally inLiteral inLoop assertionsCount | None -> ([], assertionsCount)
            let (af, as_) = visitExpr arg currentSups inAsync inTryFinally inLiteral inLoop os
            (of_ @ af, as_)
            
        | FSharpExprPatterns.ILAsm(_, _, args) -> foldExprs args assertionsCount
        | FSharpExprPatterns.TraitCall(_, _, _, _, _, args) -> foldExprs args assertionsCount
        
        | FSharpExprPatterns.FastIntegerForLoop(start, limit, body, _, _, _) -> 
            let (sf, ss) = visitExpr start currentSups inAsync inTryFinally inLiteral inLoop assertionsCount
            let (lf, ls) = visitExpr limit currentSups inAsync inTryFinally inLiteral inLoop ss
            let (bf, bs) = visitExpr body currentSups inAsync inTryFinally inLiteral true ls
            (sf @ lf @ bf, bs)
            
        | _ -> ([], assertionsCount)

    let rec visit (d: FSharpImplementationFileDeclaration) (sups: string list) (hasProp: bool) : Located<Rule> list * bool =
        match d with
        | FSharpImplementationFileDeclaration.Entity(e, decls) ->
            let localSups = extractSuppressions e.Attributes @ sups
            let f = 
                let f1 = 
                    if (e.IsFSharpUnion || e.IsEnum) && not e.IsFSharpExceptionDeclaration then
                        let isSingleCase = try e.IsFSharpUnion && e.UnionCases.Count = 1 with _ -> false
                        if not isSingleCase then
                            let hasRqa = e.Attributes |> Seq.exists (fun attr -> attr.AttributeType.LogicalName = "RequireQualifiedAccessAttribute")
                            if not hasRqa && not (isSuppressed localSups "FSA-AI11") then mkLocated FSAAI11 e.DeclarationLocation |> Option.toList else []
                        else []
                    else []
                let f2 = 
                    if e.IsValueType && e.FSharpFields.Count > 4 && not (isSuppressed localSups "FSA-P05") then mkLocated FSAP05 e.DeclarationLocation |> Option.toList else [] // EXPECT: FSA-AI10
                f1 @ f2
                
            let (df, dp) = decls |> List.fold (fun (accF, accP) child -> let cf, cp = visit child localSups accP in (accF @ cf, cp)) ([], hasProp)
            (f @ df, dp)
            
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, args, body) ->
            if v.IsCompilerGenerated then ([], hasProp)
            else
                let localSups = extractSuppressions v.Attributes @ sups
                let f1 = if v.GenericParameters.Count > 5 && not (isSuppressed localSups "FSA-AI07") then mkLocated FSAAI07 body.Range |> Option.toList else [] // EXPECT: FSA-AI10
                
                let hasEndpointAttr = v.Attributes |> Seq.exists (fun a -> let n = a.AttributeType.LogicalName in n = "HttpGetAttribute" || n = "HttpPostAttribute" || n = "EndpointAttribute" || n = "RouteAttribute")
                let f2 = 
                    if hasEndpointAttr then
                        let hasAuth = v.Attributes |> Seq.exists (fun a -> let n = a.AttributeType.LogicalName in n = "AuthorizeAttribute" || n = "AllowAnonymousAttribute" || n = "AdminAttribute")
                        if not hasAuth && not (isSuppressed localSups "FSA-SEC08") then mkLocated FSASEC08 body.Range |> Option.toList else []
                    else []
                        
                let hasPropertyAttr = v.Attributes |> Seq.exists (fun a -> try a.AttributeType.LogicalName.Contains("Property") with _ -> false)
                let newHasProp = hasProp || hasPropertyAttr
                
                let isTest = isTestFile && v.Attributes |> Seq.exists (fun a -> try let n = a.AttributeType.LogicalName in n.Contains("Fact") || n.Contains("Test") || n.Contains("Property") || n.Contains("Theory") with _ -> false)
                
                let f3 = 
                    if isAiOperationName v.LogicalName then
                        let isStringReturn = try v.ReturnParameter.Type.HasTypeDefinition && v.ReturnParameter.Type.TypeDefinition.LogicalName = "string" with _ -> false
                        let a = if isStringReturn && not (isSuppressed localSups "FSA-AI16") then mkLocated FSAAI16 v.DeclarationLocation |> Option.toList else []
                        let text = try sourceText.GetSubTextFromRange(body.Range).ToString().ToLowerInvariant() with _ -> ""
                        let b = if not (text.Contains("log") || text.Contains("printf")) && not (isSuppressed localSups "FSA-AI17") then mkLocated FSAAI17 body.Range |> Option.toList else []
                        a @ b
                    else []

                let (exprFindings, assertionsCount) = visitExpr body localSups false false false false 0
                
                let f4 = if isTest && assertionsCount > 1 && not (isSuppressed localSups "FSA-TDD03") then mkLocated FSATDD03 body.Range |> Option.toList else []
                
                (f1 @ f2 @ f3 @ f4 @ exprFindings, newHasProp)
                
        | FSharpImplementationFileDeclaration.InitAction(expr) ->
            let (ef, _) = visitExpr expr sups false false false false 0
            (ef, hasProp)
            
    let (findings, finalHasProp) = visit decl topSups hasProperty
    (findings |> Set.ofList, finalHasProp)
