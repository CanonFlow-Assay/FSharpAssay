namespace FsAssay.Runner

open System
open System.IO
open System.Text.Json

/// Adapter to the existing SecurityAssay protocol. It never creates an engine/security Pass.
module SecurityEvidence =
    [<CLIMutable>]
    type Request = {
        schema: string; dotnet: string; engine: string; engineSha256: string
        sourceRoot: string; manifest: string; policy: string; toolchain: string
        catalog: string; bundle: string; asOf: string
    }
    let private options = JsonSerializerOptions(UnmappedMemberHandling = Serialization.JsonUnmappedMemberHandling.Disallow)
    let private rejectLinks (path: string) =
        let rec check path =
            if File.Exists path || Directory.Exists path then
                if File.GetAttributes(path).HasFlag FileAttributes.ReparsePoint then invalidOp "symlinked security path"
            match Directory.GetParent path with null -> () | parent -> check parent.FullName
        check (Path.GetFullPath path)
    let validate request =
        [
            if request.schema <> "fsharp-security-request/0.1" then "unsupported security request schema"
            for path in [request.dotnet;request.engine;request.sourceRoot;request.manifest;request.policy;request.toolchain;request.catalog;request.bundle] do
                if String.IsNullOrWhiteSpace path || not (Path.IsPathFullyQualified path) then "security request paths must be absolute"
            match DateTimeOffset.TryParse request.asOf with
            | true, instant when request.asOf.EndsWith("Z", StringComparison.Ordinal) && instant.Offset = TimeSpan.Zero -> ()
            | _ -> "an explicit UTC evaluation time ending in Z is required"
            if String.IsNullOrEmpty request.engineSha256 || request.engineSha256.Length <> 64 then "pin the exact SecurityAssay DLL hash"
        ]
    let run requestPath output =
        try
            let requestPath, output = Path.GetFullPath requestPath, Path.GetFullPath output
            rejectLinks requestPath
            use json = JsonDocument.Parse(File.ReadAllBytes requestPath)
            let names = json.RootElement.EnumerateObject() |> Seq.map _.Name |> Seq.toArray
            if Array.distinct names |> Array.length <> names.Length then invalidOp "duplicate security request fields"
            let request = JsonSerializer.Deserialize<Request>(json.RootElement.GetRawText(), options)
            match validate request with [] -> () | errors -> invalidOp (String.concat "; " errors)
            for path in [request.dotnet; request.engine; request.sourceRoot; request.manifest; request.policy; request.toolchain; request.catalog; request.bundle; output] do rejectLinks path
            if Directory.Exists output || File.Exists output then invalidOp "security evidence directory must be new"
            if Assessment.fileHash request.engine <> request.engineSha256 then invalidOp "SecurityAssay engine hash mismatch"
            let bindings = [requestPath;request.engine;request.manifest;request.policy;request.toolchain;request.catalog] |> List.map (fun path -> path, Assessment.fileHash path)
            let result = Processes.run request.sourceRoot request.dotnet [request.engine;"scan";request.sourceRoot;request.manifest;request.policy;request.toolchain;request.catalog;request.bundle;request.asOf;Path.GetFullPath output] 600000
            Directory.CreateDirectory output |> ignore
            File.WriteAllText(Path.Combine(output,"adapter.stdout.log"),result.Stdout)
            File.WriteAllText(Path.Combine(output,"adapter.stderr.log"),result.Stderr)
            if result.TimedOut then invalidOp "SecurityAssay execution timed out"
            let receiptPath = Path.Combine(output,"receipt.json")
            use receipt = JsonDocument.Parse(File.ReadAllBytes receiptPath)
            let verdict = receipt.RootElement.GetProperty("verdict").GetString()
            let expectedExit = match verdict with "Pass" -> 0 | "Fail" -> 1 | "Inconclusive" -> 2 | "ToolFailure" -> 3 | _ -> invalidOp "unknown security verdict"
            if result.ExitCode <> expectedExit then invalidOp "security process exit and receipt disagree"
            let review = Processes.run request.sourceRoot request.dotnet [request.engine;"advise";receiptPath;request.sourceRoot;request.manifest;request.policy;request.toolchain;request.catalog;request.asOf;"0"] 120000
            if review.TimedOut || review.ExitCode <> 0 then invalidOp "SecurityAssay receipt inspection failed"
            use inspection = JsonDocument.Parse review.Stdout
            let inspectionRoot = inspection.RootElement
            if inspectionRoot.GetProperty("originalVerdict").GetString() <> verdict || inspectionRoot.GetProperty("contextGaps").GetArrayLength() > 0 then invalidOp "security receipt inspection found missing, changed or stale context"
            if bindings |> List.exists (fun (path, hash) -> Assessment.fileHash path <> hash) then invalidOp "security producer or request inputs changed during execution"
            let envelope = {|schema = "fsharp-security-assessment/0.1";originalVerdict = verdict;receiptSha256 = Assessment.fileHash receiptPath;receipt = receipt.RootElement.Clone();inspection = inspectionRoot.Clone();fsassayAuthority = "unchanged; security evidence is independent"|}
            match Output.publish [] [Path.Combine(output,"fsharp-security.json"), JsonSerializer.SerializeToUtf8Bytes(envelope,JsonSerializerOptions(WriteIndented=true))] with
            | Ok () -> expectedExit
            | Error error -> invalidOp error
        with error ->
            eprintfn "F# SecurityAssay adapter ToolFailure: %s" error.Message
            ExitCodes.ToolFailure
