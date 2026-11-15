namespace FsAssay.Runner

open System
open System.IO
open System.Xml
open System.Xml.Linq

module Verification =
    type FailureDetail = { testName: string; message: string }
    type TestRun = {
        evidence: Authority.TestEvidence
        failureDetails: FailureDetail array
        failureDetailsTruncated: bool
        toolErrors: string list
        reportSha256: string
    }
    let private bound maximum (value: string) =
        if value.Length <= maximum then value
        elif Char.IsHighSurrogate value.[maximum - 1] then value.Substring(0, maximum - 1)
        else value.Substring(0, maximum)
    let private attribute name (element: XElement) =
        match element.Attribute(XName.Get name) with
        | null -> invalidOp ("TRX is missing " + name)
        | value -> value.Value
    let private count name element =
        match Int32.TryParse(attribute name element) with
        | true, value when value >= 0 -> value
        | _ -> invalidOp ("TRX has invalid " + name)

    /// Parse producer-owned structured results. Arbitrary console text is never test evidence.
    let parseTrx (requirement: Authority.RequiredTestPolicy) project exitCode (bytes: byte[]) =
        let empty = {Id = requirement.id; Project = project; Status = Authority.TestStatus.NotRun; Passed = 0; Failed = 0; Skipped = 0} : Authority.TestEvidence
        try
            if bytes.Length > 16 * 1024 * 1024 then invalidOp "TRX exceeds 16 MiB"
            let settings = XmlReaderSettings(DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16L * 1024L * 1024L)
            use stream = new MemoryStream(bytes)
            use reader = XmlReader.Create(stream, settings)
            let document = XDocument.Load reader
            let ns = XNamespace.Get "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
            if document.Root.Name <> ns + "TestRun" then invalidOp "unsupported TRX root or namespace"
            let summaries = document.Root.Elements(ns + "ResultSummary") |> Seq.toArray
            if summaries.Length <> 1 then invalidOp "TRX requires one result summary"
            let counters = summaries.[0].Elements(ns + "Counters") |> Seq.toArray
            if counters.Length <> 1 then invalidOp "TRX requires one counter set"
            let counters = counters.[0]
            let total, passed, failed, executed = count "total" counters, count "passed" counters, count "failed" counters, count "executed" counters
            let skipped = total - executed
            if total < 1 || skipped < 0 || executed <> passed + failed then invalidOp "empty or inconsistent TRX counters"
            let results = document.Descendants(ns + "UnitTestResult") |> Seq.toArray
            if results.Length <> total then invalidOp "TRX individual results do not reconcile with counters"
            let identities = results |> Array.map (attribute "testId")
            if Array.distinct identities |> Array.length <> identities.Length then invalidOp "duplicate TRX test identity"
            let actual outcome = results |> Array.filter (fun result -> attribute "outcome" result = outcome) |> Array.length
            if actual "Passed" <> passed || actual "Failed" <> failed || actual "NotExecuted" <> skipped then invalidOp "unsupported outcomes or inconsistent individual results"
            if (failed = 0 && skipped = 0 && exitCode <> 0) || (failed > 0 && exitCode = 0) then invalidOp "test exit and report disagree"
            let failures = results |> Array.filter (fun result -> attribute "outcome" result = "Failed")
            let fullDetails = failures |> Array.map (fun result ->
                let messages = result.Descendants(ns + "Message") |> Seq.toArray
                {testName = attribute "testName" result; message = if messages.Length = 0 then "" else messages.[0].Value})
            let details = fullDetails |> Array.truncate 20 |> Array.map (fun detail -> {testName = bound 1024 detail.testName; message = bound 4096 detail.message})
            { evidence = {empty with Status = (if failed > 0 then Authority.TestStatus.Failed elif skipped > 0 then Authority.TestStatus.Skipped else Authority.TestStatus.Passed); Passed = passed; Failed = failed; Skipped = skipped}
              failureDetails = details
              failureDetailsTruncated = fullDetails.Length > 20 || fullDetails |> Array.exists (fun detail -> detail.testName.Length > 1024 || detail.message.Length > 4096)
              toolErrors = []; reportSha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData bytes) }
        with error -> {evidence = empty; failureDetails = [||]; failureDetailsTruncated = false; toolErrors = ["Invalid required test evidence: " + error.Message]; reportSha256 = "unavailable"}

    let execute repositoryRoot evidenceDirectory (requirement: Authority.RequiredTestPolicy) =
        let project = Path.GetFullPath(Path.Combine(repositoryRoot, requirement.project))
        let empty error = {
            evidence = {Id = requirement.id; Project = project; Status = Authority.TestStatus.NotRun; Passed = 0; Failed = 0; Skipped = 0}
            failureDetails = [||]; failureDetailsTruncated = false; toolErrors = [error]; reportSha256 = "unavailable"
        }
        try
            let relative = Path.GetRelativePath(repositoryRoot, project)
            if Path.IsPathRooted relative || relative = ".." || relative.StartsWith(".." + string Path.DirectorySeparatorChar) || not (File.Exists project) then invalidOp "required test project must exist inside the policy root"
            if Directory.Exists evidenceDirectory || File.Exists evidenceDirectory then invalidOp "required-test evidence directory must be new"
            Directory.CreateDirectory evidenceDirectory |> ignore
            let build = Processes.run repositoryRoot "dotnet" ["build"; project; "--no-restore"; "-c"; "Release"] 300000
            File.WriteAllText(Path.Combine(evidenceDirectory, "build.stdout.log"), build.Stdout)
            File.WriteAllText(Path.Combine(evidenceDirectory, "build.stderr.log"), build.Stderr)
            if build.TimedOut || build.ExitCode <> 0 then invalidOp "required test build failed or timed out"
            let report = Path.Combine(evidenceDirectory, "results.trx")
            let arguments = ["run"; "--project"; project; "--no-build"; "--no-restore"; "-c"; "Release"; "--"; "--report-trx"; "--report-trx-filename"; "results.trx"; "--results-directory"; evidenceDirectory]
            File.WriteAllText(Path.Combine(evidenceDirectory,"command.json"),System.Text.Json.JsonSerializer.Serialize({|workingDirectory=repositoryRoot; executable="dotnet"; arguments=arguments|}))
            let result = Processes.run repositoryRoot "dotnet" arguments 300000
            File.WriteAllText(Path.Combine(evidenceDirectory, "tests.stdout.log"), result.Stdout)
            File.WriteAllText(Path.Combine(evidenceDirectory, "tests.stderr.log"), result.Stderr)
            if result.TimedOut then invalidOp "required test execution timed out"
            if not (File.Exists report) then invalidOp "required test producer did not emit the registered TRX report"
            parseTrx requirement project result.ExitCode (File.ReadAllBytes report)
        with error -> empty error.Message
