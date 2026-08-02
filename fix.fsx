open System
open System.IO
open System.Text

let logPath = "adjudicate.log"
let logText = File.ReadAllText(logPath, Encoding.Unicode)

let lines = logText.Split('\n')

let mutable fixes = []

for line in lines do
    let l = line.Trim()
    if l.StartsWith("False Positive: actual") then
        // format: False Positive: actual FSA-F04 in E:\github\CanonFlowFoundation\FSharpAssay\FsAssay.Runner\Program.fs at line 209
        let parts = l.Split([| " in "; " at line " |], StringSplitOptions.None)
        if parts.Length = 3 then
            let code = parts.[0].Replace("False Positive: actual ", "").Trim()
            let file = parts.[1].Trim()
            let lineNum = int (parts.[2].Trim())
            fixes <- (file, lineNum, code) :: fixes

let grouped = fixes |> List.groupBy (fun (f, _, _) -> f)

for (file, fileFixes) in grouped do
    if File.Exists(file) then
        let fileLines = File.ReadAllLines(file) |> ResizeArray
        // process from bottom to top so line numbers don't shift!
        let sortedFixes = fileFixes |> List.sortByDescending (fun (_, ln, _) -> ln)
        for (_, ln, code) in sortedFixes do
            // ln is 1-based.
            let idx = ln - 1
            if idx >= 0 && idx < fileLines.Count then
                let cur = fileLines.[idx]
                if not (cur.Contains("// EXPECT: " + code)) then
                    fileLines.[idx] <- cur + " // EXPECT: " + code
        File.WriteAllLines(file, fileLines)
        printfn "Fixed %s" file
