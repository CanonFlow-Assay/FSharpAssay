namespace FsAssay.Runner

open System
open System.IO
open System.Security.Cryptography
open System.Text.Json

/// Native assessment projection; the embedded engine receipt retains its own verdict and authority.
module Assessment =
    type Binding = { path: string; sha256: string }
    type TestDetail = { id: string; failureDetails: Verification.FailureDetail array; failureDetailsTruncated: bool; reportSha256: string }
    type Envelope = {
        schema: string
        mode: string
        receipt: Authority.AuthorityReceipt
        inputs: Binding array
        producerSha256: string
        tests: TestDetail array
    }
    let fileHash path = SHA256.HashData(File.ReadAllBytes path) |> Convert.ToHexStringLower
    let private ignored = set [".git"; "bin"; "obj"; "artifacts"; "node_modules"; ".packages"; ".dotnet"; "TestResults"]
    let private relevant (path: string) =
        let name = Path.GetFileName path
        set [".fs"; ".fsi"; ".fsx"; ".fsproj"; ".sln"; ".slnx"; ".props"; ".targets"; ".json"; ".config"; ".ruleset"; ".editorconfig"; ".resx"] |> Set.contains (Path.GetExtension path)
        || name = ".fsassayrc" || name = ".editorconfig"
    let capture root =
        let rec files directory = seq {
            for file in Directory.EnumerateFiles directory do
                if relevant file then
                    if File.GetAttributes(file).HasFlag FileAttributes.ReparsePoint then invalidOp ("symlinked input: " + file)
                    yield {path = Path.GetRelativePath(root,file).Replace('\\','/');sha256 = fileHash file}
            for child in Directory.EnumerateDirectories directory do
                if not (ignored.Contains(Path.GetFileName child)) then
                    if File.GetAttributes(child).HasFlag FileAttributes.ReparsePoint then invalidOp ("symlinked input directory: " + child)
                    yield! files child
        }
        files root |> Seq.sortBy _.path |> Seq.toArray
    let create mode receipt inputs (runs: Verification.TestRun list) =
        {schema = "fsassay-assessment/0.1"; mode = mode; receipt = receipt; inputs = inputs
         producerSha256 = fileHash typeof<Envelope>.Assembly.Location
         tests = runs |> List.map (fun run -> {id = run.evidence.Id;failureDetails = run.failureDetails;failureDetailsTruncated = run.failureDetailsTruncated;reportSha256 = run.reportSha256}) |> List.toArray}
    let bytes envelope = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonSerializerOptions(WriteIndented = true))
    let inspect root envelope =
        [
            if envelope.schema <> "fsassay-assessment/0.1" then "unsupported assessment schema"
            yield! Authority.validateReceipt envelope.receipt
            if envelope.inputs.Length = 0 then "assessment has no declared input bindings"
            if envelope.producerSha256 <> fileHash typeof<Envelope>.Assembly.Location then "assessment producer changed"
            if envelope.inputs <> capture root then "assessment inputs changed"
        ]
