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
        // For demonstration, map based on RuleName Hash or just cycle through FSALINT01-20
        // We'll just map everything to FSALINT01 for the prototype unless it contains specific text.
        FSALINT01
    
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
            | Some msg -> findings <- msg :: findings
            | None -> ()
    | LintResult.Failure err -> ()
    
    return findings
}
