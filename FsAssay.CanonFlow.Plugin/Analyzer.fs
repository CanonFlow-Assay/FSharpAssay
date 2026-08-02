namespace FsAssay.CanonFlow

open System
open System.IO
open System.Security.Cryptography
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

[<CLIMutable>]
type RequiredCaseProfile = {
    name: string
    payloadTypes: string array
}

[<CLIMutable>]
type RequiredTypeProfile = {
    fullName: string
    cases: RequiredCaseProfile array
}

[<CLIMutable>]
type RequiredMappingProfile = {
    fullName: string
    inputType: string
    outputContainsTypes: string array
}

[<CLIMutable>]
type ObligationProfile = {
    schemaVersion: string
    profileId: string
    obligationId: string
    sourcePath: string
    sourceDigest: string
    manifestPath: string
    manifestDigest: string
    generatedPath: string
    generatedDigest: string
    suppressionAuditPath: string
    suppressionAuditDigest: string
    requiredPrivateType: string
    requiredType: RequiredTypeProfile
    requiredMappings: RequiredMappingProfile array
    forbidWildcardPatterns: bool
    semanticOracle: bool
}

[<RequireQualifiedAccess>]
module ObligationAnalyzer =
    [<Literal>]
    let ProfileEnvironmentVariable = "CANONFLOW_FSASSAY_OBLIGATION_PROFILE"

    let private jsonOptions =
        JsonSerializerOptions(
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)

    let private sha256Bytes (bytes: byte array) =
        use algorithm = SHA256.Create()
        algorithm.ComputeHash(bytes)
        |> Convert.ToHexString
        |> fun value -> "sha256:" + value.ToLowerInvariant()

    let private sha256File path =
        File.ReadAllBytes(path) |> sha256Bytes

    let private resolvePath (profilePath: string) (configuredPath: string) =
        if Path.IsPathRooted(configuredPath) then
            Path.GetFullPath(configuredPath)
        else
            Path.Combine(Path.GetDirectoryName(profilePath), configuredPath)
            |> Path.GetFullPath

    let private loadProfile profilePath =
        let fullPath = Path.GetFullPath(profilePath)
        let bytes = File.ReadAllBytes(fullPath)
        use document = JsonDocument.Parse(bytes)
        let requiredProperties =
            Set.ofList [
                "schemaVersion"; "profileId"; "obligationId"
                "sourcePath"; "sourceDigest"; "manifestPath"; "manifestDigest"
                "generatedPath"; "generatedDigest"
                "suppressionAuditPath"; "suppressionAuditDigest"
                "requiredPrivateType"; "requiredType"; "requiredMappings"
                "forbidWildcardPatterns"; "semanticOracle"
            ]
        let actualProperties =
            document.RootElement.EnumerateObject()
            |> Seq.map _.Name
            |> Set.ofSeq
        if actualProperties <> requiredProperties then
            invalidOp "The CanonFlow obligation profile has missing or unknown root properties."
        let profile =
            JsonSerializer.Deserialize<ObligationProfile>(
                bytes,
                jsonOptions)
        if isNull (box profile) then
            invalidOp "The CanonFlow obligation profile was empty."
        if profile.schemaVersion <> "1.0" then
            invalidOp $"Unsupported CanonFlow obligation profile schema '{profile.schemaVersion}'."
        let requiredText =
            [
                profile.profileId; profile.obligationId
                profile.sourcePath; profile.sourceDigest
                profile.manifestPath; profile.manifestDigest
                profile.generatedPath; profile.generatedDigest
                profile.suppressionAuditPath; profile.suppressionAuditDigest
                profile.requiredPrivateType
            ]
        if requiredText |> List.exists String.IsNullOrWhiteSpace then
            invalidOp "The CanonFlow obligation profile contains an empty required value."
        if isNull (box profile.requiredType)
           || isNull profile.requiredType.cases
           || Array.isEmpty profile.requiredType.cases
           || isNull profile.requiredMappings
           || Array.isEmpty profile.requiredMappings then
            invalidOp "The CanonFlow obligation profile requires non-empty type, case and mapping obligations."
        if not profile.forbidWildcardPatterns then
            invalidOp "An admitted CanonFlow obligation profile must report wildcard patterns."
        if profile.semanticOracle then
            invalidOp "A structural FsAssay profile cannot declare itself a semantic oracle."
        profile, fullPath

    let private message code text severity fileName =
        {
            Type = code
            Message = text
            Code = code
            Severity = severity
            Range =
                Range.mkRange
                    fileName
                    (Position.mkPos 1 0)
                    (Position.mkPos 1 1)
            Fixes = []
        }

    let private error code text fileName =
        message code text Severity.Error fileName

    let private warning code text fileName =
        message code text Severity.Warning fileName

    let private hasExpectedDigest expected path =
        File.Exists(path)
        && String.Equals(sha256File path, expected, StringComparison.Ordinal)

    let private manifestContainsObligation obligationId manifestPath =
        use document = JsonDocument.Parse(File.ReadAllBytes(manifestPath))
        document.RootElement.GetProperty("obligations").EnumerateArray()
        |> Seq.exists (fun obligation ->
            obligation.GetProperty("id").GetString() = obligationId)

    let rec private typeContains expected (candidate: FSharpType) =
        let isExpected =
            candidate.HasTypeDefinition
            && candidate.TypeDefinition.FullName = expected
        isExpected
        || (candidate.GenericArguments
            |> Seq.exists (typeContains expected))

    let private symbolDefinitions (uses: FSharpSymbolUse array) =
        uses |> Array.filter _.IsFromDefinition

    let private entityDefinitions uses =
        symbolDefinitions uses
        |> Array.choose (fun symbolUse ->
            match symbolUse.Symbol with
            | :? FSharpEntity as entity -> Some entity
            | _ -> None)
        |> Array.distinctBy _.FullName

    let private valueDefinitions uses =
        symbolDefinitions uses
        |> Array.choose (fun symbolUse ->
            match symbolUse.Symbol with
            | :? FSharpMemberOrFunctionOrValue as value -> Some value
            | _ -> None)
        |> Array.distinctBy _.FullName

    let private checkDigests profile profilePath fileName =
        let sourcePath = resolvePath profilePath profile.sourcePath
        let manifestPath = resolvePath profilePath profile.manifestPath
        let generatedPath = resolvePath profilePath profile.generatedPath
        let auditPath = resolvePath profilePath profile.suppressionAuditPath
        let mismatches =
            [
                "source", sourcePath, profile.sourceDigest
                "manifest", manifestPath, profile.manifestDigest
                "generated", generatedPath, profile.generatedDigest
                "suppression audit", auditPath, profile.suppressionAuditDigest
            ]
            |> List.choose (fun (kind, path, digest) ->
                if hasExpectedDigest digest path then None
                else Some $"{kind} '{path}'")
        if List.isEmpty mismatches then []
        else
            [
                error
                    "CFF-OBL001"
                    ("Obligation evidence digest mismatch: "
                     + String.concat "; " mismatches
                     + ". Generated edits require a reviewed profile/manifest update.")
                    fileName
            ]

    let private checkManifest profile profilePath fileName =
        let manifestPath = resolvePath profilePath profile.manifestPath
        if File.Exists(manifestPath)
           && manifestContainsObligation profile.obligationId manifestPath then
            []
        else
            [
                error
                    "CFF-OBL001"
                    $"Manifest does not bind obligation '{profile.obligationId}'."
                    fileName
            ]

    let private checkRequiredType profile (entities: FSharpEntity array) fileName =
        match entities |> Array.tryFind (fun entity -> entity.FullName = profile.requiredType.fullName) with
        | None ->
            [
                error
                    "CFF-OBL002"
                    $"Required union type '{profile.requiredType.fullName}' is missing from the typed tree."
                    fileName
            ]
        | Some entity when not entity.IsFSharpUnion ->
            [
                error
                    "CFF-OBL002"
                    $"Required type '{profile.requiredType.fullName}' is no longer an F# union."
                    fileName
            ]
        | Some entity ->
            let actualCases = entity.UnionCases |> Seq.toArray
            let requiredDefects =
                profile.requiredType.cases
                |> Array.choose (fun requiredCase ->
                    match actualCases |> Array.tryFind (fun actual -> actual.Name = requiredCase.name) with
                    | None ->
                        Some $"missing case {requiredCase.name}"
                    | Some actual ->
                        let actualPayloads =
                            actual.Fields
                            |> Seq.map (fun field ->
                                if field.FieldType.HasTypeDefinition then
                                    field.FieldType.TypeDefinition.FullName
                                else
                                    field.FieldType.Format(FSharpDisplayContext.Empty))
                            |> Seq.toArray
                        if actualPayloads = requiredCase.payloadTypes then None
                        else
                            let payloadText = String.concat ", " actualPayloads
                            Some $"case {requiredCase.name} payloads [{payloadText}]")
                |> Array.toList
            let requiredNames =
                profile.requiredType.cases
                |> Array.map _.name
                |> Set.ofArray
            let extraDefects =
                actualCases
                |> Array.choose (fun actual ->
                    if requiredNames.Contains(actual.Name) then None
                    else Some $"unadmitted case {actual.Name}")
                |> Array.toList
            requiredDefects @ extraDefects
            |> function
                | [] -> []
                | defects ->
                    [
                        error
                            "CFF-OBL002"
                            ("Required union shape changed: " + String.concat "; " defects + ".")
                            fileName
                    ]

    let private checkPrivateRepresentation profile (entities: FSharpEntity array) fileName =
        match entities |> Array.tryFind (fun entity -> entity.FullName = profile.requiredPrivateType) with
        | Some entity when entity.RepresentationAccessibility.IsPrivate -> []
        | Some _ ->
            [
                error
                    "CFF-OBL002"
                    $"Required wrapper '{profile.requiredPrivateType}' no longer has a private representation."
                    fileName
            ]
        | None ->
            [
                error
                    "CFF-OBL002"
                    $"Required private wrapper '{profile.requiredPrivateType}' is missing."
                    fileName
            ]

    let private mappingDefect (values: FSharpMemberOrFunctionOrValue array) mapping =
        match values |> Array.tryFind (fun value -> value.FullName = mapping.fullName) with
        | None -> Some $"missing mapping {mapping.fullName}"
        | Some value ->
            let parameters =
                value.CurriedParameterGroups
                |> Seq.collect id
                |> Seq.toArray
            let acceptsInput =
                parameters.Length > 0
                && typeContains mapping.inputType parameters.[0].Type
            let returnsRequiredTypes =
                mapping.outputContainsTypes
                |> Array.forall (fun required ->
                    typeContains required value.ReturnParameter.Type)
            if acceptsInput && returnsRequiredTypes then None
            else Some $"mapping signature changed for {mapping.fullName}"

    let private checkMappings profile values fileName =
        profile.requiredMappings
        |> Array.choose (mappingDefect values)
        |> Array.toList
        |> function
            | [] -> []
            | defects ->
                [
                    error
                        "CFF-OBL003"
                        ("Required boundary mapping changed: " + String.concat "; " defects + ".")
                        fileName
                ]

    let private wildcardPattern =
        Regex(
            @"(?m)^[\t ]*\|[\t ]*_[\t ]*(?:when[^\r\n]*)?->",
            RegexOptions.CultureInvariant)

    let private checkWildcards profile (source: string) fileName =
        if profile.forbidWildcardPatterns && wildcardPattern.IsMatch(source) then
            [
                error
                    "CFF-OBL004"
                    "A wildcard match was added to an obligated generated model; explicit cases are required."
                    fileName
            ]
        else
            []

    let private suppressionPattern =
        Regex(
            @"(?i)(SuppressMessage[^\r\n]*CFF-OBL|FsAssay-Ignore[^\r\n]*CFF-OBL|CFF-OBL[^\r\n]*FsAssay-Ignore)",
            RegexOptions.CultureInvariant)

    let private checkSuppressions profile profilePath (source: string) fileName =
        let suppressedRules =
            source.Split([|'\r'; '\n'|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter suppressionPattern.IsMatch
            |> Array.collect (fun line ->
                Regex.Matches(line, @"CFF-OBL\d{3}", RegexOptions.IgnoreCase)
                |> Seq.map (fun found -> found.Value.ToUpperInvariant())
                |> Seq.toArray)
            |> Array.distinct
        if Array.isEmpty suppressedRules then []
        else
            let auditPath = resolvePath profilePath profile.suppressionAuditPath
            use document = JsonDocument.Parse(File.ReadAllBytes(auditPath))
            let artifactDigest = sha256File fileName
            let isCurrentApproval ruleId (entry: JsonElement) =
                let text (name: string) =
                    match entry.TryGetProperty(name) with
                    | true, property -> property.GetString()
                    | false, _ -> null
                let expires =
                    match DateTimeOffset.TryParse(text "expiresUtc") with
                    | true, value -> value
                    | false, _ -> DateTimeOffset.MinValue
                text "ruleId" = ruleId
                && text "artifactDigest" = artifactDigest
                && not (String.IsNullOrWhiteSpace(text "reason"))
                && not (String.IsNullOrWhiteSpace(text "approvedBy"))
                && expires > DateTimeOffset.UtcNow
            let entries =
                document.RootElement.GetProperty("entries").EnumerateArray()
                |> Seq.toArray
            let unaudited =
                suppressedRules
                |> Array.filter (fun ruleId ->
                    entries |> Array.exists (isCurrentApproval ruleId) |> not)
            if Array.isEmpty unaudited then []
            else
                [
                    error
                        "CFF-OBL005"
                        ("Obligation findings were suppressed without current artifact-bound approvals: "
                         + String.concat ", " unaudited
                         + ".")
                        fileName
                ]

    let analyzeWithProfile profilePath (ctx: CliContext) =
        async {
            let profile, fullProfilePath = loadProfile profilePath
            let generatedPath =
                resolvePath fullProfilePath profile.generatedPath
            if not (
                String.Equals(
                    Path.GetFullPath(ctx.FileName),
                    generatedPath,
                    StringComparison.Ordinal)) then
                return []
            elif ctx.TypedTree.IsNone then
                return [
                    warning
                        "CFF-OBL006"
                        "The compiler produced no typed tree; obligation preservation is Inconclusive."
                        ctx.FileName
                ]
            else
                let source = ctx.SourceText.ToString()
                let uses =
                    ctx.GetAllSymbolUsesOfFile()
                    |> Seq.toArray
                let entities = entityDefinitions uses
                let values = valueDefinitions uses
                return
                    [
                        yield! checkDigests profile fullProfilePath ctx.FileName
                        yield! checkManifest profile fullProfilePath ctx.FileName
                        yield! checkRequiredType profile entities ctx.FileName
                        yield! checkPrivateRepresentation profile entities ctx.FileName
                        yield! checkMappings profile values ctx.FileName
                        yield! checkWildcards profile source ctx.FileName
                        yield! checkSuppressions profile fullProfilePath source ctx.FileName
                    ]
                    |> List.distinctBy (fun finding -> finding.Code, finding.Message)
        }

    let analyzer: Analyzer<CliContext> =
        fun ctx ->
            match Environment.GetEnvironmentVariable(ProfileEnvironmentVariable) with
            | null
            | "" -> async.Return []
            | profilePath -> analyzeWithProfile profilePath ctx

[<AbstractClass; Sealed>]
type CanonFlowObligationPlugin private () =
    [<CliAnalyzer "CanonFlow_Obligation_Preservation">]
    static member Analyzer: Analyzer<CliContext> =
        ObligationAnalyzer.analyzer
