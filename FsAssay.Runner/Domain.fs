namespace FsAssay.Runner

open System.Diagnostics.CodeAnalysis
open FSharp.Analyzers.SDK


type SkipReason = // EXPECT: FSA-AI11
    | NoTast
    | CompilerErrors
    | UnrelatedFile


type RuleFailure =
    | AnalyzerException of string

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text


type RuleEvaluation = // EXPECT: FSA-AI17 // EXPECT: FSA-AI11
    | Completed of FsAssay.Analyzers.Domain.Violation list * FSharpImplementationFileContents option * ISourceText
    | Skipped of SkipReason
    | Failed of RuleFailure


type AssayVerdict = // EXPECT: FSA-AI17 // EXPECT: FSA-AI11
    | Pass
    | Fail
    | Inconclusive
    | ToolFailure

module ProductIdentity =
    let Version =
        let version = typeof<AssayVerdict>.Assembly.GetName().Version
        $"{version.Major}.{version.Minor}.{version.Build}"

module ExitCodes =
    let Success = 0
    let BlockingFinding = 1
    let RequiredEvidenceMissing = 2
    let ToolFailure = 3 // EXPECT: FSA-AI17 // EXPECT: FSA-AI10
    let InvalidInvocation = 64 // EXPECT: FSA-AI10
