open System
open System.IO
open System.Text.RegularExpressions

let path = @"e:\github\CanonFlowFoundation\FSharpAssay\FsAssay.Analyzers\Visitor.fs"
let content = File.ReadAllText(path)

// Update signature
let step1 = content.Replace("let rec visitExpr (expr: FSharpExpr) (sups: string list) (inAsync: bool) (inTryFinally: bool) (inLiteral: bool) (assertionsCount: int ref) : Located<Rule> list =", "let rec visitExpr (expr: FSharpExpr) (sups: string list) (inAsync: bool) (inTryFinally: bool) (inLiteral: bool) (inLoop: bool) (assertionsCount: int ref) : Located<Rule> list =")

// Update calls
let step2 = Regex.Replace(step1, @"visitExpr ([a-zA-Z0-9_]+) currentSups inAsync inTryFinally inLiteral assertionsCount", "visitExpr $1 currentSups inAsync inTryFinally inLiteral inLoop assertionsCount")
let step3 = Regex.Replace(step2, @"visitExpr ([a-zA-Z0-9_]+) currentSups inAsync true inLiteral assertionsCount", "visitExpr $1 currentSups inAsync true inLiteral inLoop assertionsCount")
let step4 = Regex.Replace(step3, @"visitExpr ([a-zA-Z0-9_]+) localSups inAsync inTryFinally isLiteralBinding assertionsCount", "visitExpr $1 localSups inAsync inTryFinally isLiteralBinding inLoop assertionsCount")
let step5 = Regex.Replace(step4, @"visitExpr ([a-zA-Z0-9_]+) localSups inAsync inTryFinally inLiteral assertionsCount", "visitExpr $1 localSups inAsync inTryFinally inLiteral inLoop assertionsCount")
let step6 = Regex.Replace(step5, @"visitExpr body localSups false false false assertionsCount", "visitExpr body localSups false false false false assertionsCount")
let step7 = Regex.Replace(step6, @"visitExpr expr sups false false false dummyRef", "visitExpr expr sups false false false false dummyRef")

// Also handle newInAsync
let step8 = Regex.Replace(step7, @"visitExpr ([a-zA-Z0-9_]+) currentSups newInAsync inTryFinally inLiteral assertionsCount", "visitExpr $1 currentSups newInAsync inTryFinally inLiteral inLoop assertionsCount")

File.WriteAllText(path, step8)
