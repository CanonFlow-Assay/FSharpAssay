open System
open System.IO
open System.IO.Compression
open System.Text
open System.Xml.Linq

let fail message =
    eprintfn "normalize-nupkg: %s" message
    Environment.Exit(1)

let arguments = fsi.CommandLineArgs |> Array.skip 1
if arguments.Length <> 2 then
    eprintfn "usage: dotnet fsi eng/normalize-nupkg.fsx <input.nupkg> <output.nupkg>"
    Environment.Exit(64)

let inputPath = Path.GetFullPath(arguments.[0])
let outputPath = Path.GetFullPath(arguments.[1])
if inputPath = outputPath then fail "input and output paths must differ"
if not (File.Exists(inputPath)) then fail $"input package does not exist: {inputPath}"

let readEntry (entry: ZipArchiveEntry) =
    use source = entry.Open()
    use buffer = new MemoryStream()
    source.CopyTo(buffer)
    buffer.ToArray()

let normalizeRelationships (corePropertiesPath: string) (bytes: byte[]) =
    use input = new MemoryStream(bytes)
    let document = XDocument.Load(input)
    let relationshipNamespace = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships")
    let relationships = document.Root.Elements(relationshipNamespace + "Relationship") |> Seq.toArray
    relationships
    |> Array.iter (fun relationship ->
        let relationshipType = relationship.Attribute(XName.Get("Type")).Value
        if relationshipType.EndsWith("/metadata/core-properties", StringComparison.Ordinal) then
            relationship.SetAttributeValue(XName.Get("Target"), "/" + corePropertiesPath)
            relationship.SetAttributeValue(XName.Get("Id"), "RFSASSAYCOREPROPERTIES")
        elif relationshipType.EndsWith("/manifest", StringComparison.Ordinal) then
            relationship.SetAttributeValue(XName.Get("Id"), "RFSASSAYMANIFEST"))
    use output = new MemoryStream()
    let settings = System.Xml.XmlWriterSettings(Encoding = UTF8Encoding(false), Indent = false, OmitXmlDeclaration = false)
    use writer = System.Xml.XmlWriter.Create(output, settings)
    document.Save(writer)
    writer.Flush()
    output.ToArray()

let normalizePackage () =
    let fixedCorePropertiesPath = "package/services/metadata/core-properties/fsassay.psmdcp"
    let fixedTimestamp = DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero)

    use sourceStream = File.OpenRead(inputPath)
    use sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read)

    let corePropertiesEntries =
        sourceArchive.Entries
        |> Seq.filter (fun entry ->
            entry.FullName.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal)
            && entry.FullName.EndsWith(".psmdcp", StringComparison.Ordinal))
        |> Seq.toArray

    if corePropertiesEntries.Length <> 1 then
        fail $"expected exactly one package core-properties entry, found {corePropertiesEntries.Length}"

    let corePropertiesPath = corePropertiesEntries.[0].FullName
    let normalizedEntries =
        sourceArchive.Entries
        |> Seq.map (fun entry ->
            let normalizedPath = if entry.FullName = corePropertiesPath then fixedCorePropertiesPath else entry.FullName
            let bytes = readEntry entry
            let normalizedBytes = if entry.FullName = "_rels/.rels" then normalizeRelationships fixedCorePropertiesPath bytes else bytes
            normalizedPath, normalizedBytes)
        |> Seq.sortBy fst
        |> Seq.toArray

    let outputDirectory = Path.GetDirectoryName(outputPath)
    if not (String.IsNullOrWhiteSpace(outputDirectory)) then Directory.CreateDirectory(outputDirectory) |> ignore

    use outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None)
    use outputArchive = new ZipArchive(outputStream, ZipArchiveMode.Create)
    for path, bytes in normalizedEntries do
        let entry = outputArchive.CreateEntry(path, CompressionLevel.Optimal)
        entry.LastWriteTime <- fixedTimestamp
        entry.ExternalAttributes <- 0
        use destination = entry.Open()
        destination.Write(bytes, 0, bytes.Length)

normalizePackage ()
