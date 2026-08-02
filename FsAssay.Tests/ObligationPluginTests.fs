module ObligationPluginTests

open System
open System.IO
open System.Security.Cryptography
open System.Text
open Expecto
open FSharp.Analyzers.SDK
open FsAssay.CanonFlow
open FsAssay.Analyzers.Domain

let private baseline =
    """module RequiredContact.Generated

type ContactText = private ContactText of string

module ContactText =
    let create value = if isNull value then Error () else Ok (ContactText value)
    let value (ContactText value) = value

type Contact =
    | EmailOnly of email: ContactText
    | PhoneOnly of phone: ContactText
    | Both of email: ContactText * phone: ContactText

type ContactDto = { Email: string option; Phone: string option }

let encode = function
    | EmailOnly email -> { Email = Some (ContactText.value email); Phone = None }
    | PhoneOnly phone -> { Email = None; Phone = Some (ContactText.value phone) }
    | Both (email, phone) -> { Email = Some (ContactText.value email); Phone = Some (ContactText.value phone) }

let decode dto =
    match dto.Email, dto.Phone with
    | None, None -> Error "both-fields-missing"
    | Some email, None -> ContactText.create email |> Result.map EmailOnly |> Result.mapError (fun () -> "null-field")
    | None, Some phone -> ContactText.create phone |> Result.map PhoneOnly |> Result.mapError (fun () -> "null-field")
    | Some email, Some phone ->
        match ContactText.create email, ContactText.create phone with
        | Ok validEmail, Ok validPhone -> Ok (Both (validEmail, validPhone))
        | Error (), Ok _ -> Error "null-field"
        | Ok _, Error () -> Error "null-field"
        | Error (), Error () -> Error "null-field"
"""

let private digest (bytes: byte array) =
    use sha = SHA256.Create()
    sha.ComputeHash(bytes)
    |> Convert.ToHexString
    |> fun value -> "sha256:" + value.ToLowerInvariant()

let private textDigest (value: string) =
    Encoding.UTF8.GetBytes(value) |> digest

let private writeUtf8 (path: string) (value: string) =
    File.WriteAllText(path, value, UTF8Encoding(false))

let private createFixture actualGenerated expectedGenerated =
    let directory =
        Path.Combine(Path.GetTempPath(), "FsAssayCanonFlow_" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(directory) |> ignore
    let generatedPath = Path.Combine(directory, "RequiredContact.Generated.fs")
    let sourcePath = Path.Combine(directory, "source.sql")
    let manifestPath = Path.Combine(directory, "manifest.json")
    let auditPath = Path.Combine(directory, "suppressions.json")
    let profilePath = Path.Combine(directory, "profile.json")
    let source = "CHECK (email IS NOT NULL OR phone IS NOT NULL)"
    let manifest =
        """{"manifestType":"CanonFlowObligationManifest","obligations":[{"id":"cff:lab:required-contact"}]}"""
    let audit = """{"entries":[]}"""
    writeUtf8 generatedPath actualGenerated
    writeUtf8 sourcePath source
    writeUtf8 manifestPath manifest
    writeUtf8 auditPath audit
    let profile =
        $$"""{
  "schemaVersion": "1.0",
  "profileId": "cff:fsassay:required-contact:v1",
  "obligationId": "cff:lab:required-contact",
  "sourcePath": "source.sql",
  "sourceDigest": "{{textDigest source}}",
  "manifestPath": "manifest.json",
  "manifestDigest": "{{textDigest manifest}}",
  "generatedPath": "RequiredContact.Generated.fs",
  "generatedDigest": "{{textDigest expectedGenerated}}",
  "suppressionAuditPath": "suppressions.json",
  "suppressionAuditDigest": "{{textDigest audit}}",
  "requiredPrivateType": "RequiredContact.Generated.ContactText",
  "requiredType": {
    "fullName": "RequiredContact.Generated.Contact",
    "cases": [
      { "name": "EmailOnly", "payloadTypes": ["RequiredContact.Generated.ContactText"] },
      { "name": "PhoneOnly", "payloadTypes": ["RequiredContact.Generated.ContactText"] },
      { "name": "Both", "payloadTypes": ["RequiredContact.Generated.ContactText", "RequiredContact.Generated.ContactText"] }
    ]
  },
  "requiredMappings": [
    {
      "fullName": "RequiredContact.Generated.encode",
      "inputType": "RequiredContact.Generated.Contact",
      "outputContainsTypes": ["RequiredContact.Generated.ContactDto"]
    },
    {
      "fullName": "RequiredContact.Generated.decode",
      "inputType": "RequiredContact.Generated.ContactDto",
      "outputContainsTypes": ["RequiredContact.Generated.Contact"]
    }
  ],
  "forbidWildcardPatterns": true,
  "semanticOracle": false
}"""
    writeUtf8 profilePath profile
    directory, generatedPath, profilePath

let private evaluate actualGenerated expectedGenerated =
    let directory, generatedPath, profilePath =
        createFixture actualGenerated expectedGenerated
    try
        let plugin: Analyzer<CliContext> =
            ObligationAnalyzer.analyzeWithProfile profilePath
        FsAssay.Runner.Orchestrator.evaluateSingleFileWithProfile
            generatedPath
            Profile.Core
            [plugin]
        |> Async.RunSynchronously
    finally
        Directory.Delete(directory, true)

let private findingCodes verdict =
    match verdict with
    | FsAssay.Runner.Completed (findings, _, _) ->
        findings
        |> List.map _.Code
        |> List.filter _.StartsWith("CFF-OBL", StringComparison.Ordinal)
    | FsAssay.Runner.Skipped reason ->
        failtestf "Obligation specimen was Inconclusive unexpectedly: %A" reason
    | FsAssay.Runner.Failed failure ->
        failtestf "Obligation specimen produced ToolFailure unexpectedly: %A" failure

let private expectFinding expected actual expectedDigestSource =
    let codes = evaluate actual expectedDigestSource |> findingCodes
    Expect.contains codes expected $"Expected admitted finding {expected}; got {codes}."

let tests =
    testList "CanonFlow obligation preservation plugin" [
        testCase "positive generated model has no obligation finding" <| fun _ ->
            Expect.isEmpty
                (evaluate baseline baseline |> findingCodes)
                "The admitted specimen must be structurally preserved."

        testCase "removing a required DU case triggers CFF-OBL002" <| fun _ ->
            baseline
                .Replace("    | PhoneOnly of phone: ContactText\n", "")
                .Replace(
                    "    | PhoneOnly phone -> { Email = None; Phone = Some (ContactText.value phone) }\n",
                    "")
                .Replace(
                    "    | None, Some phone -> ContactText.create phone |> Result.map PhoneOnly |> Result.mapError (fun () -> \"null-field\")",
                    "    | None, Some phone -> ContactText.create phone |> Result.map EmailOnly |> Result.mapError (fun () -> \"null-field\")")
            |> fun mutation -> expectFinding "CFF-OBL002" mutation baseline

        testCase "wildcard case triggers CFF-OBL004" <| fun _ ->
            baseline
                .Replace(
                    "    | Both of email: ContactText * phone: ContactText",
                    "    | Both of email: ContactText * phone: ContactText\n    | FaxOnly of fax: ContactText")
                .Replace(
                    "    | Both (email, phone) -> { Email = Some (ContactText.value email); Phone = Some (ContactText.value phone) }",
                    "    | _ -> { Email = None; Phone = None }")
            |> fun mutation -> expectFinding "CFF-OBL004" mutation baseline

        testCase "public wrapper representation triggers CFF-OBL002" <| fun _ ->
            baseline.Replace(
                "type ContactText = private ContactText of string",
                "type ContactText = ContactText of string")
            |> fun mutation -> expectFinding "CFF-OBL002" mutation baseline

        testCase "removed encode mapping triggers CFF-OBL003" <| fun _ ->
            baseline.Replace("let encode = function", "let encodeRemoved = function")
            |> fun mutation -> expectFinding "CFF-OBL003" mutation baseline

        testCase "changed mapping triggers evidence digest finding CFF-OBL001" <| fun _ ->
            baseline.Replace(
                "{ Email = None; Phone = Some (ContactText.value phone) }",
                "{ Email = Some (ContactText.value phone); Phone = None }")
            |> fun mutation -> expectFinding "CFF-OBL001" mutation baseline

        testCase "generated edit without profile and manifest update triggers CFF-OBL001" <| fun _ ->
            (baseline + "\nlet downstreamEdit = 1\n")
            |> fun mutation -> expectFinding "CFF-OBL001" mutation baseline

        testCase "unaudited suppression triggers CFF-OBL005" <| fun _ ->
            ("// FsAssay-Ignore CFF-OBL002\n" + baseline)
            |> fun mutation -> expectFinding "CFF-OBL005" mutation baseline

        testCase "comments and local aliases do not change structural detection" <| fun _ ->
            let harmless =
                baseline
                    .Replace(
                        "let encode = function",
                        "// consumer documentation\nlet encode = function")
                    .Replace("EmailOnly email ->", "EmailOnly emailAlias ->")
                    .Replace(
                        "ContactText.value email); Phone = None",
                        "ContactText.value emailAlias); Phone = None")
            Expect.isEmpty
                (evaluate harmless harmless |> findingCodes)
                "Compiler symbols, rather than comments or local aliases, must carry the shape checks."

        testCase "compiler failure is Inconclusive before plugin execution" <| fun _ ->
            let directory, generatedPath, profilePath =
                createFixture "module RequiredContact.Generated\nlet broken =" baseline
            try
                let plugin: Analyzer<CliContext> =
                    ObligationAnalyzer.analyzeWithProfile profilePath
                match
                    FsAssay.Runner.Orchestrator.evaluateSingleFileWithProfile
                        generatedPath
                        Profile.Core
                        [plugin]
                    |> Async.RunSynchronously
                with
                | FsAssay.Runner.Skipped FsAssay.Runner.CompilerErrors -> ()
                | verdict -> failtestf "Expected compiler evidence to be Inconclusive, got %A." verdict
            finally
                Directory.Delete(directory, true)
    ]
