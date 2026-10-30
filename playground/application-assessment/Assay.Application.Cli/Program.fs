open FsAssay.Application

[<EntryPoint>]
let main arguments =
    try
        match Array.toList arguments with
        | "assess" :: manifest :: engine :: output :: options ->
            let rec parse rest found =
                match rest with
                | [] -> found
                | key :: value :: tail when ["--baseline";"--failure"] |> List.contains key ->
                    if Map.containsKey key found then failwith "duplicate lineage option"
                    parse tail (Map.add key value found)
                | _ -> failwith "invalid assessment option"
            let values = parse options Map.empty
            let result = Workflow.assess manifest engine output (Map.tryFind "--baseline" values) (Map.tryFind "--failure" values)
            printfn "%s; FsAssay=%s authoritative=%b; advice=%s" result.status result.engineOutcome result.engineAuthoritative result.advice
            if result.status="ReadyForReview" then 0 elif result.status="NeedsRepair" then 1 else 2
        | ["inspect"; run] ->
            match Workflow.inspect run None with Ok envelope -> printfn "Available:%s" envelope.status; 0 | Error reason -> eprintfn "NotAvailable:%s" reason; 2
        | ["inspect"; run; "--source-root"; root] ->
            match Workflow.inspect run (Some root) with Ok envelope -> printfn "Available:%s" envelope.status; 0 | Error reason -> eprintfn "NotAvailable:%s" reason; 2
        | _ -> eprintfn "assess <manifest> <fsassay.dll> <new-output> [--baseline <run>] [--failure <red-run>] | inspect <run> [--source-root <root>]"; 64
    with error -> eprintfn "ContextRequired:%s" error.Message; 2
