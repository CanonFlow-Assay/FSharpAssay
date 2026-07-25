module FsAssay.Runner.DocsGen

open System.IO
open FsAssay.Analyzers.Domain

let generateDocs outputDir =
    if not (Directory.Exists(outputDir)) then // EXPECT: FSA2022 // EXPECT: FSA-F04
        Directory.CreateDirectory(outputDir) |> ignore // EXPECT: FSA2022
    
    printfn "Generating documentation for %d rules..." (Rule.AllRules.Length) // EXPECT: FSA-F04
    
    for rule in Rule.AllRules do // EXPECT: FSA-P02 // EXPECT: FSA-F04
        let code = rule.Code
        let filePath = Path.Combine(outputDir, sprintf "%s.md" code) // EXPECT: FSA2022
        let message = rule.Message
        let severity = rule.Severity.ToString()
        let explanation = rule.Explanation
        let related = if rule.RelatedRules.IsEmpty then "None" else String.concat ", " rule.RelatedRules
        
        let content = sprintf "# %s\n\n## Metadata\n- **Severity:** %s\n- **Message:** %s\n- **Related Rules:** %s\n\n## Explanation\n%s\n" code severity message related explanation
        
        File.WriteAllText(filePath, content) // EXPECT: FSA2022 // EXPECT: FSA-C15
        
    printfn "Successfully generated documentation in %s" outputDir
