module FsAssay.Analyzers.Domain

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open System


[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C01")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C03")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C06")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C08")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C09")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-S05")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-C14")>]
[<System.Diagnostics.CodeAnalysis.SuppressMessage("FsAssay", "FSA-1301")>]

type RuleStatus =
    | Proposed
    | Dummy
    | Prototype
    | Delegated of string
    | Implemented

type Rule = 
    | FSAC01 | FSAC02 | FSAC03 | FSAC04 | FSAC05 | FSAC06 | FSAC07 | FSAC08 | FSAC09 | FSAC10
    | FSAC11 | FSAC12 | FSAC13 | FSAC14 | FSAC15 | FSAC16
    | FSAS01 | FSAS02 | FSAS03 | FSAS04 | FSAS05
    | FSAML01 | FSAML02 | FSAB01 | FSAB02 | FSAB03
    | FSAF01 | FSAF02 | FSAF03 | FSAF04 | FSAF05 | FSAF06 | FSAF07 | FSAF08
    | FSAE01 | FSAE02 | FSAE03 | FSAE04
    | FSAM01 | FSAM03 | FSAM04
    | FSAAI10 | FSAAI07 | FSAAI11
    with
        member this.Code = 
            match this with
            | FSAC01 -> "FSA-C01"
            | FSAC02 -> "FSA-C02"
            | FSAC03 -> "FSA-C03"
            | FSAC04 -> "FSA-C04"
            | FSAC05 -> "FSA-C05"
            | FSAC06 -> "FSA-C06"
            | FSAC07 -> "FSA-C07"
            | FSAC08 -> "FSA-C08"
            | FSAC09 -> "FSA-C09"
            | FSAC10 -> "FSA-C10"
            | FSAC11 -> "FSA-C11"
            | FSAC12 -> "FSA-C12"
            | FSAC13 -> "FSA-C13"
            | FSAC14 -> "FSA-C14"
            | FSAC15 -> "FSA-C15"
            | FSAC16 -> "FSA-C16"
            | FSAS01 -> "FSA-S01"
            | FSAS02 -> "FSA-S02"
            | FSAS03 -> "FSA-S03"
            | FSAS04 -> "FSA-S04"
            | FSAS05 -> "FSA-S05"
            | FSAML01 -> "FSA-ML01"
            | FSAML02 -> "FSA-ML02"
            | FSAB01 -> "FSA-B01"
            | FSAB02 -> "FSA-1301"
            | FSAB03 -> "FSA-1402"
            | FSAF01 -> "FSA-F01"
            | FSAF02 -> "FSA-F02"
            | FSAF03 -> "FSA-F03"
            | FSAF04 -> "FSA-F04"
            | FSAF05 -> "FSA-F05"
            | FSAF06 -> "FSA-F06"
            | FSAF07 -> "FSA-F07"
            | FSAF08 -> "FSA-F08"
            | FSAE01 -> "FSA-E01"
            | FSAE02 -> "FSA-E02"
            | FSAE03 -> "FSA-E03"
            | FSAE04 -> "FSA-E04"
            | FSAM01 -> "FSA-M01"
            | FSAM03 -> "FSA-M03"
            | FSAM04 -> "FSA-M04"
            | FSAAI10 -> "FSA-AI10"
            | FSAAI07 -> "FSA-AI07"
            | FSAAI11 -> "FSA-AI11"
            
        member this.Message =
            match this with
            | FSAC01 -> "Unchecked.defaultof<_> in Non-Interop Code"
            | FSAC02 -> "Option.get / .Value Without Guard"
            | FSAC03 -> "Async.RunSynchronously in Library Code"
            | FSAC04 -> "IDisposable Disposed Before Async Runs"
            | FSAC05 -> "Incomplete Pattern Match on DU"
            | FSAC06 -> "failwith / invalidArg / raise in Public API"
            | FSAC07 -> "Non-Tail Recursion in let rec"
            | FSAC08 -> "Seq.length on Infinite Sequences"
            | FSAC09 -> "Null Checking (isNull / = null) Instead of Option"
            | FSAC10 -> "Mutable State Instead of Functional Constructs"
            | FSAC11 -> "Use _.Property shorthand for lambdas (F# 8+)"
            | FSAC12 -> "Use nested record updates (F# 8+)"
            | FSAC13 -> "Missing [<TailCall>] attribute on recursive function"
            | FSAC14 -> "Evasion: Use of ref cells or Dictionary to bypass mutability rules"
            | FSAC15 -> "Catalogue Violation: Direct use of known effectful sink in core logic"
            | FSAC16 -> "Catalogue Violation: Direct use of known mutable collection"
            | FSAS01 -> "Hard-Coded Credentials / Secrets"
            | FSAS02 -> "Path Traversal in File Operations"
            | FSAS03 -> "Swallowed Exceptions"
            | FSAS04 -> "async { ... } Missing return"
            | FSAS05 -> "Task.Result / .Wait() Blocking Calls"
            | FSAML01 -> "Raw array mutation in core ML logic. Use pure Tensors."
            | FSAML02 -> "OOP Inheritance in ML Model. Use pure DUs/Records."
            | FSAB01 -> "Mutable state / arrays detected outside 'shell' profile."
            | FSAB02 -> "EF Core DbContext leakage outside shell/oracle profile"
            | FSAB03 -> "Argu ParseResults leakage outside cli/shell profile"
            | FSAF01 -> "No Throwing in Core"
            | FSAF02 -> "Total Pattern Matching"
            | FSAF03 -> "Enforce Result Binding over Imperative Checks"
            | FSAF04 -> "No Implicit Unit Sequences in Core"
            | FSAF05 -> "Domain Signature Purity"
            | FSAF06 -> "Total Immutable Enforcement"
            | FSAF07 -> "Ban Classes in Domain"
            | FSAF08 -> "Effectful or impure operation detected inside a computation expression"
            | FSAE01 -> "No Public Classes/Inheritance in API"
            | FSAE02 -> "No Hidden Exceptions in API"
            | FSAE03 -> "No C# Delegates (Action/Func) in API"
            | FSAE04 -> "No Leaked Mutability in API"
            | FSAM01 -> "Struct DU contains reference fields"
            | FSAM03 -> "Unit-of-measure loss via implicit cast"
            | FSAM04 -> "Active pattern partiality without fallback"
            | FSAAI10 -> "Magic numbers: numeric literals > 1 in non-test code"
            | FSAAI07 -> "Overly Generic: more than 5 generic parameters in a function/method"
            | FSAAI11 -> "Missing [<RequireQualifiedAccess>] attribute on Discriminated Union or Enum"

        member this.Status =
            match this with
            | FSAC02 | FSAC05 -> Implemented
            | FSAC07 | FSAC11 | FSAC12 | FSAC13
            | FSAS04
            | FSAML01 | FSAML02
            | FSAB01
            | FSAF01 | FSAF02 | FSAF03 | FSAF05 | FSAF06 | FSAF07
            | FSAE01 | FSAE02 | FSAE03 | FSAE04
            | FSAM01 | FSAM03 | FSAM04 -> Dummy
            | FSAC04 -> Delegated "DisposedBeforeAsyncRunAnalyzer"
            | _ -> Prototype


[<CustomEquality; CustomComparison>]
type Located<'F when 'F : comparison> = 
    { Finding: 'F; Range: range }
    override x.Equals(yobj) =
        match yobj with
        | :? Located<'F> as y -> x.Finding = y.Finding && x.Range = y.Range
        | _ -> false
    override x.GetHashCode() = hash (x.Finding, x.Range)
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? Located<'F> as y ->
                let c1 = compare x.Finding y.Finding
                if c1 <> 0 then c1
                else
                    let c2 = compare x.Range.StartLine y.Range.StartLine
                    if c2 <> 0 then c2
                    else
                        let c3 = compare x.Range.StartColumn y.Range.StartColumn
                        if c3 <> 0 then c3
                        else
                            let c4 = compare x.Range.EndLine y.Range.EndLine
                            if c4 <> 0 then c4
                            else compare x.Range.EndColumn y.Range.EndColumn
            | _ -> invalidArg "yobj" "cannot compare values of different types"


let toMessage (loc: Located<Rule>) : Message option =
    match loc.Finding.Status with
    | Dummy | Proposed -> None
    | status ->
        let fixes =
            match loc.Finding.Code with
            | "FSA-C09" ->
                [ { FromRange = loc.Range; FromText = "is" + "Null"; ToText = "Option.isNone" } ]
            | _ -> []
            
        let severity =
            match status with
            | Implemented | Delegated _ -> Severity.Error
            | Prototype -> Severity.Warning
            | _ -> Severity.Warning
            
        Some {
            Type = loc.Finding.Code
            Message = loc.Finding.Message
            Code = loc.Finding.Code
            Severity = severity
            Range = loc.Range
            Fixes = fixes
        }
    

type Profile =
    | Core
    | Shell
    | Oracle
    | Api
    | Test
    | Script
