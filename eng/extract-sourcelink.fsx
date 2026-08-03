open System
open System.IO
open System.IO.Compression
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.Text

let fail message =
    eprintfn "extract-sourcelink: %s" message
    Environment.Exit(1)

let arguments = fsi.CommandLineArgs |> Array.skip 1
if arguments.Length <> 2 then
    eprintfn "usage: dotnet fsi eng/extract-sourcelink.fsx <package.nupkg> <portable-pdb-entry>"
    Environment.Exit(64)

let packagePath = Path.GetFullPath(arguments.[0])
let pdbEntryPath = arguments.[1]
if not (File.Exists(packagePath)) then fail $"package does not exist: {packagePath}"

let extractSourceLink () =
    use packageStream = File.OpenRead(packagePath)
    use archive = new ZipArchive(packageStream, ZipArchiveMode.Read)
    let entry = archive.GetEntry(pdbEntryPath)
    if isNull entry then fail $"portable PDB entry does not exist: {pdbEntryPath}"
    use pdbStream = entry.Open()
    use buffer = new MemoryStream()
    pdbStream.CopyTo(buffer)
    buffer.Position <- 0L
    use provider = MetadataReaderProvider.FromPortablePdbStream(buffer)
    let reader = provider.GetMetadataReader()
    let sourceLinkKind = Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A")
    let sourceLink =
        reader.CustomDebugInformation
        |> Seq.tryPick (fun handle ->
            let information = reader.GetCustomDebugInformation(handle)
            if reader.GetGuid(information.Kind) = sourceLinkKind then
                reader.GetBlobBytes(information.Value) |> Encoding.UTF8.GetString |> Some
            else None)
    match sourceLink with
    | Some document -> printf "%s" document
    | None ->
        let rowCount = reader.GetTableRowCount(TableIndex.CustomDebugInformation)
        let availableKinds =
            reader.CustomDebugInformation
            |> Seq.map (fun handle -> reader.GetCustomDebugInformation(handle).Kind |> reader.GetGuid |> string)
            |> String.concat ","
        fail $"portable PDB has no SourceLink custom debug information; rows: {rowCount}; available kinds: {availableKinds}"

extractSourceLink ()
