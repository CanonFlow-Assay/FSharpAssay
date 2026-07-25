namespace FsAssay.Runner

open System.Diagnostics.CodeAnalysis
open FSharp.Analyzers.SDK

[<SuppressMessage("FsAssay", "FSA1001")>]
type SkipReason =
    | NoTast
    | CompilerErrors
    | UnrelatedFile

[<SuppressMessage("FsAssay", "FSA1001")>]
type RuleFailure =
    | AnalyzerException of string

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

[<SuppressMessage("FsAssay", "FSA1001")>]
type RuleEvaluation =
    | Completed of FsAssay.Analyzers.Domain.Violation list * FSharpImplementationFileContents option * ISourceText
    | Skipped of SkipReason
    | Failed of RuleFailure

[<SuppressMessage("FsAssay", "FSA1001")>]
type AssayVerdict =
    | Pass
    | Fail
    | Inconclusive
    | ToolFailure

module ExitCodes =
    let Success = 0
    let BlockingFinding = 1
    let RequiredEvidenceMissing = 2
    let ToolFailure = 3
    let InvalidInvocation = 64
