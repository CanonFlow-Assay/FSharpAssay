module FsAssay.Analyzers.LintDelegation

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax
open FsAssay.Analyzers.Domain
open FsAssay.Analyzers.AstUtils
open FSharpLint.Application

let lintAnalyzer (parseTree: ParsedInput) (fileName: string) (sourceText: ISourceText) (profile: Profile) = async {
    let mutable findings = []
    
    let mapLintWarningToRule (warning: FSharpLint.Framework.Suggestion.LintWarning) =
        let hash = abs(warning.Details.Message.GetHashCode()) % 20 + 1 // EXPECT: FSA-AI10
        match hash with // EXPECT: FSA-AI10
        | 1 -> FSALINT01 | 2 -> FSALINT02 | 3 -> FSALINT03 | 4 -> FSALINT04 | 5 -> FSALINT05
        | 6 -> FSALINT06 | 7 -> FSALINT07 | 8 -> FSALINT08 | 9 -> FSALINT09 | 10 -> FSALINT10
        | 11 -> FSALINT11 | 12 -> FSALINT12 | 13 -> FSALINT13 | 14 -> FSALINT14 | 15 -> FSALINT15
        | 16 -> FSALINT16 | 17 -> FSALINT17 | 18 -> FSALINT18 | 19 -> FSALINT19 | _ -> FSALINT20
    
    let parsedInfo = {
        Lint.ParsedFileInformation.Ast = parseTree
        Lint.ParsedFileInformation.Source = sourceText.ToString()
        Lint.ParsedFileInformation.TypeCheckResults = None
        Lint.ParsedFileInformation.ProjectCheckResults = None
    }
    
    let lintResult = Lint.lintParsedSource { Lint.OptionalLintParameters.Default with CancellationToken = None } parsedInfo
    
    match lintResult with
    | LintResult.Success warnings ->
        for w in warnings do
            let rule = mapLintWarningToRule w
            // FSharpLint warnings have Details.Range
            let m = mkLocated rule w.Details.Range
            match m with
            | Some msg -> findings <- msg :: findings // EXPECT: FSA-F04 // EXPECT: FSA-C10
            | None -> () // EXPECT: FSA-F04
    | LintResult.Failure err -> failwith (sprintf "FSharpLint failed: %A" err) // EXPECT: FSA-F04 // EXPECT: FSA-C06
    
    return findings
}
