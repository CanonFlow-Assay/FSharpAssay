namespace FsAssay.Analyzers

open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

module AstContext =
    let getCompExprRanges (sourceText: ISourceText) (fileName: string) =
        let mutable ranges = []
        let text = sourceText.ToString()
        let keywords = ["async {"; "task {"; "seq {"; "computation {"]
        
        for k in keywords do // EXPECT: FSA-P02 // EXPECT: FSA-F04
            let mutable pos = 0
            while pos < text.Length do
                let idx = text.IndexOf(k, pos)
                if idx >= 0 then
                    let startIdx = idx
                    let mutable braceCount = 1
                    let mutable curr = idx + k.Length
                    while curr < text.Length && braceCount > 0 do // EXPECT: FSA-F04
                        if text.[curr] = '{' then braceCount <- braceCount + 1 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                        elif text.[curr] = '}' then braceCount <- braceCount - 1 // EXPECT: FSA-C10
                        curr <- curr + 1 // EXPECT: FSA-C10
                    
                    let lineStartStr = text.Substring(0, startIdx)
                    let startLine = (lineStartStr |> Seq.filter ((=) '\n') |> Seq.length) + 1
                    let startCol = startIdx - lineStartStr.LastIndexOf('\n') - 1
                    
                    let lineEndStr = text.Substring(0, curr)
                    let endLine = (lineEndStr |> Seq.filter ((=) '\n') |> Seq.length) + 1
                    let endCol = curr - lineEndStr.LastIndexOf('\n') - 1
                    
                    let r = Range.mkRange fileName (Position.mkPos startLine (max 0 startCol)) (Position.mkPos endLine (max 0 endCol))
                    ranges <- r :: ranges // EXPECT: FSA-F04 // EXPECT: FSA-C10
                    pos <- curr // EXPECT: FSA-C10
                else pos <- text.Length // EXPECT: FSA-C10
        ranges
