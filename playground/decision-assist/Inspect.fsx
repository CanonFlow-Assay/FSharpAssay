open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

let arguments = fsi.CommandLineArgs |> Array.skip 1
let defaultRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
let reportPath, sourceRoot =
    match arguments with
    | [| report |] -> Path.GetFullPath report, defaultRoot
    | [| report; "--source-root"; root |] -> Path.GetFullPath report, Path.GetFullPath root
    | _ -> failwith "usage: dotnet fsi --exec playground/decision-assist/Inspect.fsx <investigation.json> [--source-root <checkout>]"
let sha bytes = Convert.ToHexString(SHA256.HashData(bytes: byte array)).ToLowerInvariant()
let within (root: string) (relative: string) =
    if Path.IsPathRooted relative then failwith "absolute evidence path"
    let resolved = Path.GetFullPath(Path.Combine(root, relative))
    if not (resolved.StartsWith(Path.GetFullPath(root) + string Path.DirectorySeparatorChar, StringComparison.Ordinal)) then failwith "evidence path escapes root"
    resolved
let checkBindings root (bindings: JsonElement) =
    bindings.EnumerateArray() |> Seq.forall (fun binding ->
        let file = within root (binding.GetProperty("path").GetString())
        File.Exists file && isNull (FileInfo(file).LinkTarget) && (File.ReadAllBytes(file) |> sha) = binding.GetProperty("sha256").GetString())
try
    let output = Path.GetDirectoryName reportPath
    let bytes = File.ReadAllBytes reportPath
    let digestCurrent = (bytes |> sha) = File.ReadAllText(Path.Combine(output, "investigation.sha256")).Trim()
    use document = JsonDocument.Parse bytes
    let report = document.RootElement
    let artifactsCurrent = digestCurrent && checkBindings output (report.GetProperty "artifacts")
    use sourceDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "source-bindings.json")))
    let sourceCurrent = artifactsCurrent && checkBindings sourceRoot sourceDocument.RootElement
    let snapshot = sourceDocument.RootElement.EnumerateArray() |> Seq.map (fun source -> source.GetProperty("path").GetString() + ":" + source.GetProperty("sha256").GetString()) |> String.concat "\n" |> Encoding.UTF8.GetBytes |> sha
    let current = sourceCurrent && snapshot = report.GetProperty("snapshotSha256").GetString() && report.GetProperty("complete").GetBoolean()
    printfn "evidence=%s; sourcesCurrent=%b; artifactsCurrent=%b; FsAssay verdict unchanged" (if current then "Available" else "NotAvailable") sourceCurrent artifactsCurrent
    if not current then exit 2
with error ->
    eprintfn "evidence=NotAvailable; %s" error.Message
    exit 2
