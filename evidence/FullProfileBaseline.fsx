open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

type Finding =
    { Repository: string
      Rule: string
      File: string
      Line: int
      Column: int
      EndLine: int
      EndColumn: int
      Severity: string
      AdmittedStatus: string
      NewlyIntroduced: bool
      Classification: string
      Disposition: string
      RequiredAction: string }

let sha256Bytes (bytes: byte array) =
    bytes
    |> SHA256.HashData
    |> Convert.ToHexString
    |> fun value -> "sha256:" + value.ToLowerInvariant()

let sha256File path = File.ReadAllBytes(path) |> sha256Bytes

let repositoryRelativePath repository (value: string) =
    let normalized = value.Replace('\\', '/')
    let marker = "/" + repository + "/"
    let index = normalized.IndexOf(marker, StringComparison.Ordinal)
    if index < 0 then failwith $"Path is outside {repository}: {value}"
    normalized.Substring(index + marker.Length)

let severity (rule: string) =
    match rule with
    | "FSA-C09" | "FSA-F04" -> "Minor"
    | _ -> "Major"

let productionAdmitted =
    set [ "FSA2022"; "FSA-AI16"; "FSA-P02"; "FSA-P03" ]

let findingKey repository rule file line column endLine endColumn =
    String.concat "|" [
        repository
        rule
        file
        string line
        string column
        string endLine
        string endColumn
    ]

let key (finding: Finding) =
    findingKey
        finding.Repository
        finding.Rule
        finding.File
        finding.Line
        finding.Column
        finding.EndLine
        finding.EndColumn

let readCurrent repository path =
    use document = JsonDocument.Parse(File.ReadAllBytes(path))
    [ for fileResult in document.RootElement.EnumerateArray() do
          let file =
              fileResult.GetProperty("file").GetString()
              |> repositoryRelativePath repository
          for violation in fileResult.GetProperty("violations").EnumerateArray() do
              let rule = violation.GetProperty("code").GetString()
              let line = violation.GetProperty("startLine").GetInt32()
              let column = violation.GetProperty("startColumn").GetInt32()
              let endLine = violation.GetProperty("endLine").GetInt32()
              let endColumn = violation.GetProperty("endColumn").GetInt32()
              yield
                  { Repository = repository
                    Rule = rule
                    File = file
                    Line = line
                    Column = column
                    EndLine = endLine
                    EndColumn = endColumn
                    Severity = severity rule
                    AdmittedStatus =
                        if Set.contains rule productionAdmitted then
                            "ProductionAdmitted"
                        else
                            "NonAdmittedExperimental"
                    NewlyIntroduced = false
                    Classification = ""
                    Disposition = ""
                    RequiredAction = "" } ]
    |> List.sortBy key

let readAdjudication path =
    use document = JsonDocument.Parse(File.ReadAllBytes(path))
    [ for item in document.RootElement.GetProperty("findings").EnumerateArray() do
          yield
              { Repository = item.GetProperty("repository").GetString()
                Rule = item.GetProperty("rule").GetString()
                File = item.GetProperty("file").GetString()
                Line = item.GetProperty("line").GetInt32()
                Column = item.GetProperty("column").GetInt32()
                EndLine = item.GetProperty("endLine").GetInt32()
                EndColumn = item.GetProperty("endColumn").GetInt32()
                Severity = item.GetProperty("severity").GetString()
                AdmittedStatus = item.GetProperty("admittedStatus").GetString()
                NewlyIntroduced = item.GetProperty("newlyIntroduced").GetBoolean()
                Classification = item.GetProperty("classification").GetString()
                Disposition = item.GetProperty("disposition").GetString()
                RequiredAction = item.GetProperty("requiredAction").GetString() } ]
    |> List.map (fun item -> key item, item)
    |> Map.ofList

let readBaseline path =
    use document = JsonDocument.Parse(File.ReadAllBytes(path))
    let recordedDigest = document.RootElement.GetProperty("inventoryDigest").GetString()
    let findings =
        [ for item in document.RootElement.GetProperty("findings").EnumerateArray() do
              yield
                  { Repository = item.GetProperty("repository").GetString()
                    Rule = item.GetProperty("rule").GetString()
                    File = item.GetProperty("file").GetString()
                    Line = item.GetProperty("line").GetInt32()
                    Column = item.GetProperty("column").GetInt32()
                    EndLine = item.GetProperty("endLine").GetInt32()
                    EndColumn = item.GetProperty("endColumn").GetInt32()
                    Severity = item.GetProperty("severity").GetString()
                    AdmittedStatus = item.GetProperty("admittedStatus").GetString()
                    NewlyIntroduced = item.GetProperty("newlyIntroduced").GetBoolean()
                    Classification = item.GetProperty("classification").GetString()
                    Disposition = item.GetProperty("disposition").GetString()
                    RequiredAction = item.GetProperty("requiredAction").GetString() } ]
        |> List.sortBy key
    recordedDigest, findings

let inventoryDigest findings =
    findings
    |> List.map key
    |> String.concat "\n"
    |> fun value -> value + "\n"
    |> Encoding.UTF8.GetBytes
    |> sha256Bytes

let writeString (writer: Utf8JsonWriter) (name: string) (value: string) =
    writer.WriteString(name, value)

let writeBaseline adjudicationPath contractsPath ondcPath outputPath =
    let adjudication = readAdjudication adjudicationPath
    let current =
        readCurrent "CanonFlow" contractsPath
        @ readCurrent "ONDCFlow" ondcPath
        |> List.sortBy key
        |> List.map (fun item ->
            match Map.tryFind (key item) adjudication with
            | Some adjudicated ->
                { item with
                    NewlyIntroduced = adjudicated.NewlyIntroduced
                    Classification = adjudicated.Classification
                    Disposition = adjudicated.Disposition
                    RequiredAction = adjudicated.RequiredAction }
            | None -> failwith $"Current finding is absent from adjudication: {key item}")

    let byRepository = current |> List.countBy _.Repository |> Map.ofList
    let byClassification = current |> List.countBy _.Classification |> Map.ofList

    use stream = File.Create(outputPath)
    use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
    writer.WriteStartObject()
    writeString writer "schema" "cff.fsassay.full-profile-baseline/v1"
    writeString writer "generatedAt" "2026-07-29T00:00:00Z"
    writeString writer "policy" "Current full-profile findings are inventory, not authority. Reject any current fingerprint absent from this baseline; allow missing fingerprints as improvements."
    writeString writer "adjudicationSha256" (sha256File adjudicationPath)
    writeString writer "inventoryDigest" (inventoryDigest current)
    writer.WritePropertyName("canonicalScanTargets")
    writer.WriteStartArray()
    writer.WriteStartObject()
    writeString writer "repository" "CanonFlow"
    writeString writer "target" "src/CanonFlow.Assurance.Contracts/CanonFlow.Assurance.Contracts.fsproj"
    writeString writer "scope" "Contracts package production sources"
    writer.WriteEndObject()
    writer.WriteStartObject()
    writeString writer "repository" "ONDCFlow"
    writeString writer "target" "src/ONDCFlow.Profile.Retail/ONDCFlow.Profile.Retail.fsproj"
    writeString writer "scope" "Retail production sources plus ONDCFlow.Core through its real ProjectReference; excludes test projects and package-cache generated sources"
    writer.WriteEndObject()
    writer.WriteEndArray()
    writer.WritePropertyName("sourceInventories")
    writer.WriteStartArray()
    for repository, path in [ "CanonFlow", contractsPath; "ONDCFlow", ondcPath ] do
        writer.WriteStartObject()
        writeString writer "repository" repository
        writeString writer "sha256" (sha256File path)
        writer.WriteEndObject()
    writer.WriteEndArray()
    writer.WriteNumber("totalFindings", current.Length)
    writer.WritePropertyName("totalsByRepository")
    writer.WriteStartObject()
    for repository in [ "CanonFlow"; "ONDCFlow" ] do
        writer.WriteNumber(repository, Map.tryFind repository byRepository |> Option.defaultValue 0)
    writer.WriteEndObject()
    writer.WritePropertyName("totalsByClassification")
    writer.WriteStartObject()
    for classification in [
        "Valid defect"
        "False positive / FsAssay defect"
        "Intentional architecture requiring admitted handling"
        "Pre-existing technical debt"
        "Experimental non-admitted rule"
    ] do
        writer.WriteNumber(
            classification,
            Map.tryFind classification byClassification |> Option.defaultValue 0
        )
    writer.WriteEndObject()
    writer.WritePropertyName("findings")
    writer.WriteStartArray()
    for item in current do
        writer.WriteStartObject()
        writeString writer "repository" item.Repository
        writeString writer "rule" item.Rule
        writeString writer "file" item.File
        writer.WriteNumber("line", item.Line)
        writer.WriteNumber("column", item.Column)
        writer.WriteNumber("endLine", item.EndLine)
        writer.WriteNumber("endColumn", item.EndColumn)
        writeString writer "severity" item.Severity
        writeString writer "admittedStatus" item.AdmittedStatus
        writer.WriteBoolean("newlyIntroduced", item.NewlyIntroduced)
        writeString writer "classification" item.Classification
        writeString writer "disposition" item.Disposition
        writeString writer "requiredAction" item.RequiredAction
        writer.WriteEndObject()
    writer.WriteEndArray()
    writer.WriteEndObject()
    writer.Flush()

let verifyBaseline baselinePath contractsPath ondcPath =
    let recordedDigest, baseline = readBaseline baselinePath
    let computedDigest = inventoryDigest baseline
    if recordedDigest <> computedDigest then
        failwith $"Baseline digest mismatch: recorded {recordedDigest}; computed {computedDigest}"

    let current =
        readCurrent "CanonFlow" contractsPath
        @ readCurrent "ONDCFlow" ondcPath
        |> List.sortBy key

    let baselineKeys = baseline |> List.map key |> Set.ofList
    let currentKeys = current |> List.map key |> Set.ofList
    let unbaselined = Set.difference currentKeys baselineKeys |> Set.toList
    let improvements = Set.difference baselineKeys currentKeys |> Set.toList

    if not (List.isEmpty unbaselined) then
        eprintfn "Full-profile non-regression failed: %d unbaselined finding(s)" unbaselined.Length
        unbaselined |> List.iter (eprintfn "  %s")
        Environment.Exit(1)

    printfn "Full-profile non-regression passed."
    printfn "Baseline findings: %d" baseline.Length
    printfn "Current findings: %d" current.Length
    printfn "Resolved since baseline: %d" improvements.Length
    printfn "Baseline inventory digest: %s" recordedDigest

match fsi.CommandLineArgs |> Array.skip 1 with
| [| "build"; adjudicationPath; contractsPath; ondcPath; outputPath |] ->
    writeBaseline adjudicationPath contractsPath ondcPath outputPath
| [| "verify"; baselinePath; contractsPath; ondcPath |] ->
    verifyBaseline baselinePath contractsPath ondcPath
| _ ->
    failwith "Usage: dotnet fsi FullProfileBaseline.fsx build <adjudication.json> <contracts.json> <ondc.json> <output.json> | verify <baseline.json> <contracts.json> <ondc.json>"
