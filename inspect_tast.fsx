#r "nuget: FSharp.Compiler.Service, 43.12.201"

open System
open FSharp.Compiler.Symbols

let t = typeof<FSharpExpr>
let prop = t.GetProperty("ImmediateSubExpressions")
if prop <> null then
    printfn "Found ImmediateSubExpressions!"
else
    printfn "Not found!"

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open System.IO

let checker = FSharpChecker.Create()

let file = Path.Combine(Directory.GetCurrentDirectory(), "dummy.fs")
let source = """
module Dummy
let x = Some 42
let y = x.Value
let z = Option.get x
let h = [1;2].Head
let h2 = List.head [1;2]
"""

let sourceText = SourceText.ofString source

let options, _ = checker.GetProjectOptionsFromScript(file, sourceText) |> Async.RunSynchronously
let parse, check = checker.ParseAndCheckFileInProject(file, 0, sourceText, options) |> Async.RunSynchronously

match check with
| FSharpCheckFileAnswer.Succeeded(res) ->
    let rec visitExpr (expr: FSharpExpr) =
        match expr with
        | FSharpExprPatterns.Call(obj, func, typeArgs1, typeArgs2, args) ->
            printfn "Call: %s" func.FullName
            args |> List.iter visitExpr
            obj |> Option.iter visitExpr
        | FSharpExprPatterns.Let((binding, valExpr, _), body) ->
            visitExpr valExpr
            visitExpr body
        | FSharpExprPatterns.Application(func, typeArgs, args) ->
            visitExpr func
            args |> List.iter visitExpr
        | e -> 
            ()

    let rec visitDecl (decl: FSharpImplementationFileDeclaration) =
        match decl with
        | FSharpImplementationFileDeclaration.Entity(e, decls) ->
            decls |> List.iter visitDecl
        | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, args, body) ->
            visitExpr body
        | FSharpImplementationFileDeclaration.InitAction(expr) ->
            visitExpr expr

    res.ImplementationFile.Value.Declarations |> List.iter visitDecl
| _ -> printfn "Check failed"
