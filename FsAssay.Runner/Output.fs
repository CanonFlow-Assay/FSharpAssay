namespace FsAssay.Runner

open System
open System.IO
open System.Text.Json
open FSharp.Analyzers.SDK
open FsAssay.Analyzers.Domain

module Output =
    type JsonViolation = {
        code: string
        message: string
        startLine: int
        startColumn: int
        endLine: int
        endColumn: int
    }
    
    type JsonFileResult = {
        file: string
        violations: JsonViolation[]
    }

    let writeCanonicalJson (results: (string * Violation list) list) (outPath: string) =
        let jsonResults =
            results
            |> List.map (fun (file, violations) ->
                {
                    file = file
                    violations = 
                        violations |> List.map (fun v ->
                            {
                                code = v.Code
                                message = v.Message
                                startLine = v.Range.StartLine
                                startColumn = v.Range.StartColumn
                                endLine = v.Range.EndLine
                                endColumn = v.Range.EndColumn
                            }
                        ) |> List.toArray
                }
            )
            |> List.toArray
        let options = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase) // EXPECT: FSA-F04
        let jsonStr = JsonSerializer.Serialize(jsonResults, options)
        File.WriteAllText(outPath, jsonStr) // EXPECT: FSA2022 // EXPECT: FSA-C15

    // Minimal SARIF generation using anonymous records
    let writeSarif (results: (string * Violation list) list) (outPath: string) =
        let sarifResults =
            results
            |> List.collect (fun (file, violations) ->
                violations |> List.map (fun v ->
                    {|
                        ruleId = v.Code
                        message = {| text = v.Message |}
                        locations = [|
                            {|
                                physicalLocation = {|
                                    artifactLocation = {| uri = "file://" + file.Replace("\\", "/") |}
                                    region = {|
                                        startLine = max 1 v.Range.StartLine
                                        startColumn = max 1 (v.Range.StartColumn + 1)
                                        endLine = max 1 v.Range.EndLine
                                        endColumn = max 1 (v.Range.EndColumn + 1)
                                    |}
                                |}
                            |}
                        |]
                    |}
                )
            )
            |> List.toArray

        let sarifObj = {|
            version = "2.1.0"
            ``$schema`` = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json"
            runs = [|
                {|
                    tool = {|
                        driver = {|
                            name = "FsAssay"
                            informationUri = "https://github.com/CanonFlow-Assay/FSharpAssay"
                            version = ProductIdentity.Version
                        |}
                    |}
                    results = sarifResults
                |}
            |]
        |}

        let options = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase) // EXPECT: FSA-F04
        let jsonStr = JsonSerializer.Serialize(sarifObj, options)
        File.WriteAllText(outPath, jsonStr) // EXPECT: FSA2022 // EXPECT: FSA-C15

    let writeToolchainRecord (outPath: string) =
        let record = {| // EXPECT: FSA-AI17
            os = Environment.OSVersion.ToString()
            dotnet = Environment.Version.ToString()
            fsc = typeof<FSharp.Compiler.CodeAnalysis.FSharpChecker>.Assembly.GetName().Version.ToString()
        |}
        let options = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase) // EXPECT: FSA-F04
        File.WriteAllText(outPath, JsonSerializer.Serialize(record, options)) // EXPECT: FSA2022 // EXPECT: FSA-C15

    let writeRateCard (results: (string * Violation list) list) (outPath: string) =
        let totalViolations = results |> List.sumBy (fun (_, msgs) -> msgs.Length)
        let totalFiles = results.Length
        let score = max 0 (100 - (totalViolations * 5)) // EXPECT: FSA-AI10
        let grade =
            if score >= 95 then "S" // EXPECT: FSA-AI10
            elif score >= 85 then "A" // EXPECT: FSA-AI10
            elif score >= 70 then "B" // EXPECT: FSA-AI10
            elif score >= 50 then "C" // EXPECT: FSA-AI10
            else "F"

        let breakdown =
            results
            |> List.map (fun (f, msgs) ->
                let fileHeader = sprintf "### 📄 `%s`\n" f
                let violationsList =
                    msgs
                    |> List.map (fun m -> sprintf "* **[%s]** (Line %d): %s" m.Code m.Range.StartLine m.Message)
                    |> String.concat "\n"
                fileHeader + violationsList)
            |> String.concat "\n\n"

        let md = 
            "# 🏆 FsAssay Functional Code Quality Rate Card\n\n" +
            "## Executive Summary\n" +
            sprintf "* **Score**: %d / 100\n" score +
            sprintf "* **Grade**: **[%s]**\n" grade +
            sprintf "* **Files Scanned**: %d\n" totalFiles +
            sprintf "* **Total Anti-Patterns Detected**: %d\n\n---\n\n" totalViolations +
            "## Violations Breakdown\n" + breakdown

        File.WriteAllText(outPath, md) // EXPECT: FSA2022 // EXPECT: FSA-C15

    let writeMaterialDashboard (results: (string * Violation list) list) (outPath: string) =
        let totalViolations = results |> List.sumBy (fun (_, msgs) -> msgs.Length)
        let totalFiles = results.Length
        let score = max 0 (100 - (totalViolations * 5)) // EXPECT: FSA-AI10
        let grade =
            if score >= 95 then "S" // EXPECT: FSA-AI10
            elif score >= 85 then "A" // EXPECT: FSA-AI10
            elif score >= 70 then "B" // EXPECT: FSA-AI10
            elif score >= 50 then "C" // EXPECT: FSA-AI10
            else "F"

        let fileSections =
            results
            |> List.map (fun (f, msgs) ->
                let vHtml = msgs |> List.map (fun m -> sprintf "<div class=\"violation\"><span class=\"code\">[%s]</span> (Line %d) %s</div>" m.Code m.Range.StartLine m.Message) |> String.concat ""
                
                // MAGIC: Parse CanonflowSource attributes
                let mutable legacyHtml = ""
                try // EXPECT: FSA-F04
                    let codeLines = File.ReadAllLines(f) // EXPECT: FSA2022
                    let regex = System.Text.RegularExpressions.Regex(@"\[<CanonflowSource\(""(.*?)"",\s*""(.*?)""\)>\]")
                    for line in codeLines do
                        let m = regex.Match(line)
                        if m.Success then
                            let sqlFile = m.Groups.[1].Value
                            let targetTable = m.Groups.[2].Value
                            // Resolve the path relative to the scanned project
                            let projDir = Path.GetDirectoryName(f) // EXPECT: FSA2022
                            let absoluteSqlFile = Path.GetFullPath(Path.Combine(projDir, "..", "..", "..", sqlFile)) // EXPECT: FSA2022
                            if File.Exists(absoluteSqlFile) then // EXPECT: FSA2022
                                let sqlLines = File.ReadAllLines(absoluteSqlFile) // EXPECT: FSA2022
                                // Super simple extraction: find CREATE TABLE targetTable and read until ';'
                                let mutable inTable = false
                                let mutable tableSql = []
                                for sLine in sqlLines do // EXPECT: FSA-F04
                                    if sLine.ToLower().Contains("create table " + targetTable) then inTable <- true // EXPECT: FSA-P04 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                                    if inTable then tableSql <- tableSql @ [sLine] // EXPECT: FSA-P01 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                                    if inTable && sLine.Contains(";") then inTable <- false // EXPECT: FSA-C10
                                
                                let formattedSql = String.concat "\n" tableSql
                                legacyHtml <- legacyHtml + sprintf """ // EXPECT: FSA-P04 // EXPECT: FSA-C10
                                    <div class="diff-container">
                                        <div class="diff-pane old-code">
                                            <h4>Legacy DB Noun (SQL)</h4>
                                            <pre><code>%s</code></pre>
                                        </div>
                                        <div class="diff-pane new-code">
                                            <h4>Uplifted Domain Verb (F#)</h4>
                                            <pre><code>%s</code></pre>
                                        </div>
                                    </div>
                                """ formattedSql line
                with e -> legacyHtml <- "<!-- Error parsing sources: " + e.Message + " -->" // EXPECT: FSA-C10

                sprintf "<h3>%s</h3>%s%s" f legacyHtml (if String.IsNullOrEmpty vHtml then "<p style='color: #03dac6;'>✓ Clean (Zero Violations)</p>" else vHtml))
            |> String.concat ""

        let html = 
            "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"UTF-8\">\n<title>FsAssay Material 5 Dashboard</title>\n<style>\n" +
            "body { font-family: sans-serif; background-color: #121212; color: #e0e0e0; margin: 0; padding: 24px; }\n" +
            ".header { display: flex; justify-content: space-between; align-items: center; background: #1e1e1e; padding: 20px; border-radius: 12px; }\n" +
            ".badge { font-size: 36px; font-weight: bold; padding: 8px 24px; border-radius: 8px; background: #bb86fc; color: #000; }\n" +
            ".card { background: #1e1e1e; margin-top: 20px; padding: 20px; border-radius: 12px; }\n" +
            ".violation { border-left: 4px solid #cf6679; padding-left: 12px; margin: 12px 0; }\n" +
            ".code { font-family: monospace; color: #03dac6; }\n" +
            ".diff-container { display: flex; gap: 20px; margin-top: 20px; }\n" +
            ".diff-pane { flex: 1; background: #2d2d2d; padding: 15px; border-radius: 8px; border: 1px solid #444; }\n" +
            ".diff-pane h4 { margin-top: 0; color: #bb86fc; }\n" +
            ".old-code pre { color: #ff7b72; }\n" +
            ".new-code pre { color: #a5d6ff; }\n" +
            "</style>\n</head>\n<body>\n" +
            "<div class=\"header\">\n<div>\n<h1>FsAssay Quality Dashboard</h1>\n" +
            sprintf "<p>Score: <strong>%d / 100</strong> | Total Anti-Patterns: <strong>%d</strong></p>\n</div>\n" score totalViolations +
            sprintf "<div class=\"badge\">Grade [%s]</div>\n</div>\n" grade +
            "<div class=\"card\">\n" +
            sprintf "<h2>Scanned Files (%d)</h2>\n" totalFiles +
            fileSections + "\n</div>\n</body>\n</html>"

        File.WriteAllText(outPath, html) // EXPECT: FSA2022 // EXPECT: FSA-C15

    let writeSuppressionReport (files: string list) (outPath: string) =
        let suppressions =
            files
            |> List.collect (fun f ->
                let lines = File.ReadAllLines(f) // EXPECT: FSA2022
                lines 
                |> Array.mapi (fun i l -> (i + 1, l))
                |> Array.filter (fun (_, l) -> l.Contains("SuppressMessage") || l.Contains("Profile"))
                |> Array.map (fun (i, l) -> {| file = f; line = i; text = l.Trim() |})
                |> Array.toList
            )
        let options = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase) // EXPECT: FSA-F04
        File.WriteAllText(outPath, JsonSerializer.Serialize(suppressions, options)) // EXPECT: FSA2022 // EXPECT: FSA-C15
