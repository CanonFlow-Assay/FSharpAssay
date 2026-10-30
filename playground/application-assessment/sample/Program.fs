open System
open System.IO
open System.Text.Json
open Approval.Domain
open FsAssay.Application

let tests = Approval.OriginalTests.cases @ Approval.AdditionalTests.cases

[<EntryPoint>]
let main arguments =
    match arguments with
    | [| "--assay-tests"; output |] ->
        let observed = tests |> List.map (fun (id, requirementId, test) ->
            try { id = id; requirementId = requirementId; passed = test(); detail = "" }
            with error -> { id = id; requirementId = requirementId; passed = false; detail = error.Message }) |> List.toArray
        let report: TestReport = { schema = "fsharp-application-tests/0.1"; tests = observed }
        File.WriteAllText(output, JsonSerializer.Serialize(report, JsonSerializerOptions(WriteIndented = true)))
        for test in observed do printfn "%s %s %b" test.requirementId test.id test.passed
        if observed |> Array.forall _.passed then 0 else 1
    | [||] -> printfn "%A" (decide { Reference = "invoice-1"; AmountCents = 1250L }); 0
    | _ -> 64
