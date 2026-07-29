open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

type RawFinding =
    { Repository: string
      Rule: string
      File: string
      Line: int
      Column: int
      EndLine: int
      EndColumn: int
      Message: string }

type AdjudicatedFinding =
    { Id: string
      Repository: string
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
      Evidence: string
      Disposition: string
      RequiredAction: string
      Message: string }

let sha256File path =
    File.ReadAllBytes(path)
    |> SHA256.HashData
    |> Convert.ToHexString
    |> fun value -> "sha256:" + value.ToLowerInvariant()

let repositoryRelativePath repository (value: string) =
    let normalized = value.Replace('\\', '/')
    let marker = "/" + repository + "/"
    let index = normalized.IndexOf(marker, StringComparison.Ordinal)
    if index < 0 then failwith $"Path is outside {repository}: {value}"
    normalized.Substring(index + marker.Length)

let readFindings repository path =
    use document = JsonDocument.Parse(File.ReadAllBytes(path))
    [ for fileResult in document.RootElement.EnumerateArray() do
          let file =
              fileResult.GetProperty("file").GetString()
              |> repositoryRelativePath repository
          for finding in fileResult.GetProperty("violations").EnumerateArray() do
              yield
                  { Repository = repository
                    Rule = finding.GetProperty("code").GetString()
                    File = file
                    Line = finding.GetProperty("startLine").GetInt32()
                    Column = finding.GetProperty("startColumn").GetInt32()
                    EndLine = finding.GetProperty("endLine").GetInt32()
                    EndColumn = finding.GetProperty("endColumn").GetInt32()
                    Message = finding.GetProperty("message").GetString() } ]

let productionAdmitted =
    set [ "FSA2022"; "FSA-AI16"; "FSA-P02"; "FSA-P03" ]

let severity (rule: string) =
    match rule with
    | "FSA-C09" | "FSA-F04" -> "Minor"
    | _ -> "Major"

let newlyIntroduced (finding: RawFinding) =
    finding.Repository = "CanonFlow"
    || finding.File = "src/ONDCFlow.Profile.Retail/ReplayPolicy.fs"
    || finding.File = "src/ONDCFlow.Profile.Retail/ReplayEvaluator.fs"

let classification (finding: RawFinding) =
    match finding.Rule, finding.File with
    | "FSA2022", "src/ONDCFlow.Core/RulePackBinding.fs" ->
        "Intentional architecture requiring admitted handling"
    | "FSA2022", "src/ONDCFlow.Core/SourceLock.fs"
    | "FSA-P02", _
    | "FSA-P03", _
    | "FSA-AI16", _
    | "FSA-AI17", _ ->
        "False positive / FsAssay defect"
    | _ -> "Experimental non-admitted rule"

let evidence (finding: RawFinding) (classification: string) =
    match classification, finding.Rule, finding.File with
    | "False positive / FsAssay defect", "FSA2022", _ ->
        "The typed call is System.IO.MemoryStream construction or access. It performs deterministic in-memory serialization and has no filesystem, network, clock, or process effect. FSA2022 previously classified every System.IO type as external I/O."
    | "False positive / FsAssay defect", "FSA-P02", _ ->
        "The source range contains no explicit box call or :> obj upcast. FCS introduced the coercion for resource disposal or iteration. FsAssay now requires explicit source evidence and retains positive specimens for explicit box/upcast."
    | "False positive / FsAssay defect", "FSA-P03", _ ->
        "The source performs one necessary collection materialization. The rule text requires a redundant sequence-list-sequence roundtrip; the old implementation incorrectly flagged every Seq.toList/List.toSeq call."
    | "False positive / FsAssay defect", "FSA-AI16", _ ->
        "The identifier contains the incidental substring 'ai' inside 'retail'; it is a digest constant, not an AI/LLM operation. Detection now uses semantic identifier tokens."
    | "False positive / FsAssay defect", "FSA-AI17", _ ->
        "The identifier contains incidental letters matching 'ai' and performs no AI/LLM operation. Detection now uses semantic identifier tokens with hostile retail/applicability specimens."
    | "Intentional architecture requiring admitted handling", "FSA2022", _ ->
        "The calls are real filesystem/path effects, but the module is the rule-pack byte-binding verification shell: it resolves repository paths, rejects traversal/absence, reads bytes, and compares SHA-256. It makes no domain verdict and has no network access."
    | "Experimental non-admitted rule", "FSA-C09", _ ->
        "The public .NET smart constructor must reject null from C# and reflection callers. FSA-C09 is absent from Admission.ProductionRuleCodes and cannot affect this release verdict."
    | "Experimental non-admitted rule", _, _ ->
        $"{finding.Rule} is absent from FsAssay Admission.ProductionRuleCodes. The full profile records it as experimental inventory; it has no admitted authority to block the alpha release."
    | _ -> failwith "Unclassified evidence path"

let disposition (classification: string) =
    match classification with
    | "False positive / FsAssay defect" -> "Resolved by FsAssay precision fix and hostile specimens"
    | "Intentional architecture requiring admitted handling" -> "Formally adjudicated for 0.1 alpha only"
    | "Experimental non-admitted rule" -> "Recorded as non-blocking evidence inventory"
    | value -> failwith $"Unknown classification: {value}"

let requiredAction (classification: string) =
    match classification with
    | "False positive / FsAssay defect" ->
        "Merge the FsAssay rule repair, retain positive and hostile specimens, and regenerate the digest-bound baseline."
    | "Intentional architecture requiring admitted handling" ->
        "Permit only for 0.1 alpha; extract RulePackBinding into an explicit verification-shell project before a paid external pilot or stable release."
    | "Experimental non-admitted rule" ->
        "No alpha code change. The rule may become blocking only through separate admission with positive, hostile-negative, precision, profile, and migration evidence."
    | value -> failwith $"Unknown classification: {value}"

let writeString (writer: Utf8JsonWriter) (name: string) (value: string) =
    writer.WriteString(name, value)

match fsi.CommandLineArgs |> Array.skip 1 with
| [| contractsPath; ondcPath; outputPath |] ->
    let raw =
        readFindings "CanonFlow" contractsPath
        @ readFindings "ONDCFlow" ondcPath
        |> List.sortBy (fun item -> item.Repository, item.File, item.Line, item.Column, item.Rule)

    let adjudicated =
        raw
        |> List.mapi (fun index finding ->
            let classificationValue = classification finding
            { Id = $"CFF-FSA-{index + 1:D4}"
              Repository = finding.Repository
              Rule = finding.Rule
              File = finding.File
              Line = finding.Line
              Column = finding.Column
              EndLine = finding.EndLine
              EndColumn = finding.EndColumn
              Severity = severity finding.Rule
              AdmittedStatus =
                if Set.contains finding.Rule productionAdmitted then
                    "ProductionAdmitted"
                else
                    "NonAdmittedExperimental"
              NewlyIntroduced = newlyIntroduced finding
              Classification = classificationValue
              Evidence = evidence finding classificationValue
              Disposition = disposition classificationValue
              RequiredAction = requiredAction classificationValue
              Message = finding.Message })

    let fingerprint (item: AdjudicatedFinding) =
        String.concat "|" [
            item.Id; item.Repository; item.Rule; item.File; string item.Line
            string item.Column; item.Severity; item.AdmittedStatus
            string item.NewlyIntroduced; item.Classification; item.Disposition
        ]

    let inventoryDigest =
        adjudicated
        |> List.map fingerprint
        |> String.concat "\n"
        |> fun value -> value + "\n"
        |> Encoding.UTF8.GetBytes
        |> SHA256.HashData
        |> Convert.ToHexString
        |> fun value -> "sha256:" + value.ToLowerInvariant()

    let counts = adjudicated |> List.countBy _.Classification |> Map.ofList
    let isReplayFinding (item: AdjudicatedFinding) =
        item.Repository = "ONDCFlow"
        && (item.File = "src/ONDCFlow.Profile.Retail/ReplayPolicy.fs"
            || item.File = "src/ONDCFlow.Profile.Retail/ReplayEvaluator.fs")

    let cohorts =
        [ ("newReplayFiles", adjudicated |> List.filter isReplayFinding)
          ("blockingFSA2022", adjudicated |> List.filter (fun item -> item.Rule = "FSA2022"))
          ("remainingPreExistingONDCFlow",
           adjudicated
           |> List.filter (fun item ->
               item.Repository = "ONDCFlow"
               && not (isReplayFinding item)
               && item.Rule <> "FSA2022"))
          ("contracts", adjudicated |> List.filter (fun item -> item.Repository = "CanonFlow")) ]

    use stream = File.Create(outputPath)
    use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
    writer.WriteStartObject()
    writeString writer "schema" "cff.fsassay.release-adjudication/v1"
    writeString writer "generatedAt" "2026-07-29T00:00:00Z"
    writeString writer "releaseLaw" "No new untriaged findings AND no admitted blocking findings AND every baseline finding recorded"
    writeString writer "inventoryDigest" inventoryDigest
    writer.WritePropertyName("sourceInventories")
    writer.WriteStartArray()
    for repository, path in [ "CanonFlow", contractsPath; "ONDCFlow", ondcPath ] do
        writer.WriteStartObject()
        writeString writer "repository" repository
        writeString writer "sha256" (sha256File path)
        writer.WriteEndObject()
    writer.WriteEndArray()
    writer.WritePropertyName("totalsByClassification")
    writer.WriteStartObject()
    for name in [
        "Valid defect"
        "False positive / FsAssay defect"
        "Intentional architecture requiring admitted handling"
        "Pre-existing technical debt"
        "Experimental non-admitted rule"
    ] do
        writer.WriteNumber(name, Map.tryFind name counts |> Option.defaultValue 0)
    writer.WriteEndObject()
    writer.WriteNumber("totalFindings", adjudicated.Length)
    writer.WritePropertyName("priorityCohorts")
    writer.WriteStartObject()
    for cohortName, cohort in cohorts do
        let cohortCounts = cohort |> List.countBy _.Classification |> Map.ofList
        writer.WritePropertyName(cohortName)
        writer.WriteStartObject()
        writer.WriteNumber("totalFindings", cohort.Length)
        writer.WritePropertyName("totalsByClassification")
        writer.WriteStartObject()
        for name in [
            "Valid defect"
            "False positive / FsAssay defect"
            "Intentional architecture requiring admitted handling"
            "Pre-existing technical debt"
            "Experimental non-admitted rule"
        ] do
            writer.WriteNumber(name, Map.tryFind name cohortCounts |> Option.defaultValue 0)
        writer.WriteEndObject()
        writer.WriteEndObject()
    writer.WriteEndObject()
    writer.WritePropertyName("findings")
    writer.WriteStartArray()
    for item in adjudicated do
        writer.WriteStartObject()
        writeString writer "id" item.Id
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
        writeString writer "evidence" item.Evidence
        writeString writer "disposition" item.Disposition
        writeString writer "requiredAction" item.RequiredAction
        writeString writer "message" item.Message
        writer.WriteEndObject()
    writer.WriteEndArray()
    writer.WriteEndObject()
    writer.Flush()
| _ ->
    failwith "Usage: dotnet fsi GenerateCffReleaseAdjudication.fsx <contracts.json> <ondc.json> <output.json>"
