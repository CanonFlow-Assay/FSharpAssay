module AuthorityTests

open System
open System.IO
open System.Diagnostics
open System.Text.Json
open System.Text
open Expecto
open FsAssay.Runner

let private requiredTest = ({
    id = "stable"
    project = "tests/Tests.fsproj"
    minimumPassed = 2
}: Authority.RequiredTestPolicy)

let private rulesWith status =
    FsAssay.Analyzers.Domain.Rule.AllRules
    |> List.filter (fun rule -> rule.Status = status)
    |> List.map _.Code
    |> List.toArray

let private policy = {
    Authority.unapprovedPolicy with
        requiredProjectClasses = [| "core" |]
        requiredTargetFrameworks = [| "net10.0" |]
        requiredTests = [| requiredTest |]
        advisoryRules = [||]
        experimentalRules = rulesWith FsAssay.Analyzers.Domain.Implemented
        prototypeRules = rulesWith FsAssay.Analyzers.Domain.Prototype
        dummyRules = rulesWith FsAssay.Analyzers.Domain.Dummy
}

let private passedTest root = ({
    Id = "stable"
    Project = Path.Combine(root, "tests", "Tests.fsproj")
    Status = Authority.TestStatus.Passed
    Passed = 2
    Failed = 0
    Skipped = 0
}: Authority.TestEvidence)

let private completeFacts root = {
    Authority.emptyFacts with
        Toolchain = { SdkVersion = "10.0.301"; RuntimeVersion = "10.0.0"; FSharpCompilerServiceVersion = "43.10.100.0" }
        Projects = [
            { Path = Path.Combine(root, "src", "Core.fsproj"); ProjectClass = "core"; TargetFrameworks = [ "net10.0" ]; Supported = true; Loaded = true; Disposition = Authority.ProjectDisposition.Loaded; Reason = "" }
        ]
        Sources = [
            { Path = Path.Combine(root, "src", "Core.fs"); Disposition = Authority.SourceDisposition.Analyzed; Reason = "" }
        ]
        RequiredTests = [ passedTest root ]
        Rules = [
            { RuleId = "FSA-C01"; Status = "completed"; EvidenceAvailable = true; FindingCount = 0 }
            { RuleId = "FSA-C04"; Status = "completed"; EvidenceAvailable = true; FindingCount = 0 }
        ]
}

let private candidate = ({
    kind = "commit"
    commitSha = String.replicate 40 "a"
    approvedHeadSha = String.replicate 40 "a"
    treeSha = String.replicate 40 "b"
    dirty = false
    syntheticMergeSha = "not-applicable"
    packageSha256 = "not-applicable"
    repositoryRelativeTarget = "."
}: Authority.CandidateIdentity)

let private receiptForCandidate root candidateIdentity facts =
    let policyHash =
        match Authority.canonicalPolicyIdentity policy with
        | Ok(_, hash) -> hash
        | Error errors -> failwithf "invalid test policy: %A" errors
    Authority.createReceipt
        root
        candidateIdentity
        policy
        (Path.Combine(root, "fsassay-policy.lock.json"))
        policyHash
        facts

let private receipt root facts = receiptForCandidate root candidate facts

let private runProcess workingDirectory executable arguments =
    let info = ProcessStartInfo(executable)
    info.WorkingDirectory <- workingDirectory
    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true
    info.UseShellExecute <- false
    arguments |> List.iter info.ArgumentList.Add
    use childProcess = Process.Start(info)
    let output = childProcess.StandardOutput.ReadToEnd()
    let error = childProcess.StandardError.ReadToEnd()
    childProcess.WaitForExit()
    if childProcess.ExitCode <> 0 then failtestf "%s %A failed (%d): %s%s" executable arguments childProcess.ExitCode output error

let private readProcess workingDirectory executable arguments =
    let info = ProcessStartInfo(executable)
    info.WorkingDirectory <- workingDirectory
    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true
    info.UseShellExecute <- false
    arguments |> List.iter info.ArgumentList.Add
    use childProcess = Process.Start(info)
    let output = childProcess.StandardOutput.ReadToEnd()
    let error = childProcess.StandardError.ReadToEnd()
    childProcess.WaitForExit()
    if childProcess.ExitCode <> 0 then failtestf "%s %A failed (%d): %s%s" executable arguments childProcess.ExitCode output error
    output.Trim()

let private localCandidateIdentity root target =
    Authority.candidateIdentityWithEnvironment root target null null null null null

let private withTempRoot action =
    let root = Path.Combine(Path.GetTempPath(), "fsassay-authority-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(root) |> ignore
    try action root
    finally Directory.Delete(root, true)

let private reasonCodes (decision: Authority.AuthorityDecision) = decision.Reasons |> List.map fst |> Set.ofList

let tests =
    testList "authority contract" [
        testCase "credible complete nonzero evidence passes" <| fun _ ->
            withTempRoot (fun root ->
                let decision = Authority.decide policy (completeFacts root)
                Expect.equal decision.Outcome Pass "complete required evidence must pass"
                Expect.isTrue decision.Authoritative "complete evidence is authoritative")

        testCase "candidate identity gaps and contradictions cannot produce authority" <| fun _ ->
            withTempRoot (fun root ->
                runProcess root "git" [ "init"; "--quiet" ]
                runProcess root "git" [ "config"; "user.email"; "fsassay@example.invalid" ]
                runProcess root "git" [ "config"; "user.name"; "FsAssay Tests" ]
                let tracked = Path.Combine(root, "candidate.fs")
                File.WriteAllText(tracked, "module Candidate\n")
                runProcess root "git" [ "add"; "candidate.fs" ]
                runProcess root "git" [ "commit"; "--quiet"; "-m"; "candidate" ]

                let cleanIdentity, cleanMissing, cleanErrors = localCandidateIdentity root tracked
                Expect.isFalse cleanIdentity.dirty "committed candidate must begin clean"
                Expect.isEmpty cleanMissing "clean Git identity must be complete"
                Expect.isEmpty cleanErrors "clean Git identity must be consistent"
                let cleanReceipt = receiptForCandidate root cleanIdentity (completeFacts root)
                let cleanContext = ({
                    expectedPolicySha256 = cleanReceipt.policy.sha256
                    expectedCommitSha = cleanIdentity.commitSha
                    expectedTreeSha = cleanIdentity.treeSha
                    expectedApprovedHeadSha = Some cleanIdentity.approvedHeadSha
                    expectedSyntheticMergeSha = None
                    expectedPackageSha256 = None
                }: Authority.ReceiptValidationContext)
                Expect.isOk (Authority.deserializeAndValidateReceiptForContext cleanContext (Output.canonicalJsonBytes cleanReceipt)) "actual local commit and tree must validate against caller pins"

                File.AppendAllText(tracked, "let trackedChange = 1\n")
                let trackedDirty, _, _ = localCandidateIdentity root tracked
                let trackedReceipt = receiptForCandidate root trackedDirty (completeFacts root)
                Expect.equal trackedReceipt.outcome "Inconclusive" "tracked dirt prevents Pass"
                Expect.isFalse trackedReceipt.authoritative "tracked dirt prevents authority"

                runProcess root "git" [ "add"; "candidate.fs" ]
                runProcess root "git" [ "commit"; "--quiet"; "-m"; "tracked change" ]
                let generatedSdk = Path.Combine(root, ".dotnet", "sdk")
                Directory.CreateDirectory(generatedSdk) |> ignore
                File.WriteAllText(Path.Combine(generatedSdk, "host"), "generated toolchain")
                let generatedOnly, generatedMissing, generatedErrors = localCandidateIdentity root tracked
                Expect.isFalse generatedOnly.dirty "untracked generated non-inputs must not dirty candidate evidence"
                Expect.isEmpty generatedMissing "generated non-inputs must not remove candidate identity"
                Expect.isEmpty generatedErrors "generated non-inputs must not contradict candidate identity"
                File.WriteAllText(Path.Combine(root, "fsassay-policy.local.json"), "{}")
                let untrackedDirty, _, _ = localCandidateIdentity root tracked
                let untrackedReceipt = receiptForCandidate root untrackedDirty (completeFacts root)
                Expect.equal untrackedReceipt.outcome "Inconclusive" "untracked config dirt prevents Pass"
                Expect.isFalse untrackedReceipt.authoritative "untracked config dirt prevents authority"
                File.Delete(Path.Combine(root, "fsassay-policy.local.json"))

                let mainBranch = readProcess root "git" [ "branch"; "--show-current" ]
                runProcess root "git" [ "switch"; "--quiet"; "-c"; "feature" ]
                File.AppendAllText(tracked, "let feature = 2\n")
                runProcess root "git" [ "add"; "candidate.fs" ]
                runProcess root "git" [ "commit"; "--quiet"; "-m"; "feature" ]
                let approvedFeature = readProcess root "git" [ "rev-parse"; "HEAD" ]
                runProcess root "git" [ "switch"; "--quiet"; mainBranch ]
                File.WriteAllText(Path.Combine(root, "base.fs"), "module Base\n")
                runProcess root "git" [ "add"; "base.fs" ]
                runProcess root "git" [ "commit"; "--quiet"; "-m"; "base" ]
                let wrongApproved = readProcess root "git" [ "rev-parse"; "HEAD" ]
                runProcess root "git" [ "merge"; "--quiet"; "--no-ff"; "feature"; "-m"; "synthetic merge" ]
                let mergeSha = readProcess root "git" [ "rev-parse"; "HEAD" ]
                let completePrIdentity, completePrMissing, completePrErrors =
                    Authority.candidateIdentityWithEnvironment root tracked mergeSha "pull_request" approvedFeature mergeSha null
                Expect.isEmpty completePrMissing "reviewed head and synthetic merge evidence must be complete"
                Expect.isEmpty completePrErrors "reviewed head must equal the synthetic merge second parent"
                let completePrReceipt = receiptForCandidate root completePrIdentity (completeFacts root)
                let completePrContext = ({
                    expectedPolicySha256 = completePrReceipt.policy.sha256
                    expectedCommitSha = mergeSha
                    expectedTreeSha = completePrIdentity.treeSha
                    expectedApprovedHeadSha = Some approvedFeature
                    expectedSyntheticMergeSha = Some mergeSha
                    expectedPackageSha256 = None
                }: Authority.ReceiptValidationContext)
                Expect.isOk (Authority.deserializeAndValidateReceiptForContext completePrContext (Output.canonicalJsonBytes completePrReceipt)) "actual synthetic merge, reviewed head and tree must validate together"
                let _, _, mismatchedPrErrors =
                    Authority.candidateIdentityWithEnvironment root tracked mergeSha "pull_request" wrongApproved mergeSha null
                Expect.contains mismatchedPrErrors "approved head identity does not match the synthetic merge second parent" "wrong reviewed head must be rejected"
                let _, _, staleSyntheticErrors =
                    Authority.candidateIdentityWithEnvironment root tracked mergeSha "pull_request" approvedFeature wrongApproved null
                Expect.contains staleSyntheticErrors "synthetic merge identity does not match the analyzed HEAD" "stale event merge identity must not replace actual HEAD"

                let unavailable = {
                    candidate with
                        kind = "unversioned"
                        commitSha = "unavailable"
                        approvedHeadSha = "unavailable"
                        treeSha = "unavailable"
                        dirty = true
                }
                let unavailableReceipt = receiptForCandidate root unavailable (completeFacts root)
                Expect.equal unavailableReceipt.outcome "Inconclusive" "unversioned candidates are incomplete"
                Expect.isFalse unavailableReceipt.authoritative "missing commit and tree prevent authority"

                let contradictory = { candidate with approvedHeadSha = String.replicate 40 "d" }
                let contradictoryReceipt = receiptForCandidate root contradictory (completeFacts root)
                Expect.equal contradictoryReceipt.outcome "ToolFailure" "mismatched approved identity is contradictory"
                Expect.isFalse contradictoryReceipt.authoritative "contradictory identity prevents authority"

                let syntheticMismatch = {
                    candidate with
                        kind = "synthetic-merge"
                        syntheticMergeSha = String.replicate 40 "d"
                }
                let syntheticReceipt = receiptForCandidate root syntheticMismatch (completeFacts root)
                Expect.equal syntheticReceipt.outcome "ToolFailure" "synthetic mismatch is contradictory"
                Expect.isFalse syntheticReceipt.authoritative "synthetic mismatch prevents authority")

        testCase "required test failure is Fail" <| fun _ ->
            withTempRoot (fun root ->
                let failed = { passedTest root with Status = Authority.TestStatus.Failed; Passed = 1; Failed = 1 }
                let decision = Authority.decide policy { completeFacts root with RequiredTests = [ failed ] }
                Expect.equal decision.Outcome Fail "known required failure is conclusive")

        testCase "required test notRun is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let notRun = { passedTest root with Status = Authority.TestStatus.NotRun; Passed = 0 }
                let decision = Authority.decide policy { completeFacts root with RequiredTests = [ notRun ] }
                Expect.equal decision.Outcome Inconclusive "absence is never success")

        testCase "required test skipped is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let skipped = { passedTest root with Status = Authority.TestStatus.Skipped; Passed = 0; Skipped = 2 }
                let decision = Authority.decide policy { completeFacts root with RequiredTests = [ skipped ] }
                Expect.contains (reasonCodes decision) "required-test-skipped" "skipped requirements are explicit")

        testCase "missing required test evidence is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let decision = Authority.decide policy { completeFacts root with RequiredTests = [] }
                Expect.contains (reasonCodes decision) "required-test-evidence-missing" "missing test must be named")

        testCase "insufficient required test total is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let tooFew = { passedTest root with Passed = 1 }
                let decision = Authority.decide policy { completeFacts root with RequiredTests = [ tooFew ] }
                Expect.equal decision.Outcome Inconclusive "minimum is locked")

        testCase "zero discovered projects is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let decision = Authority.decide policy { completeFacts root with Projects = [] }
                Expect.contains (reasonCodes decision) "projects-zero-discovered" "zero discovery must be explicit"
                let src = Path.Combine(root, "src")
                Directory.CreateDirectory(src) |> ignore
                let core = Path.Combine(src, "Core.fsproj")
                let tests = Path.Combine(src, "Tests.fsproj")
                let solution = Path.Combine(root, "Legacy.sln")
                let projectXml = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Library.fs\" /></ItemGroup></Project>"
                File.WriteAllText(core, projectXml)
                File.WriteAllText(tests, projectXml)
                File.WriteAllText(Path.Combine(src, "Library.fs"), "module Library\nlet value = 1\n")
                File.WriteAllText(solution, String.concat Environment.NewLine [
                    "Microsoft Visual Studio Solution File, Format Version 12.00"
                    "Project(\"{FAKE}\") = \"Core\", \"src\\Core.fsproj\", \"{CORE}\""
                    "EndProject"
                    "Project(\"{FAKE}\") = \"Tests\", \"src\\Tests.fsproj\", \"{TESTS}\""
                    "EndProject"
                ])
                let discovered = ProjectSystem.discoverProjectPaths solution |> List.map Path.GetFullPath
                Expect.sequenceEqual discovered [ Path.GetFullPath(core); Path.GetFullPath(tests) ] "legacy solution discovery must match existing F# projects"
                let failed = { (completeFacts root).Projects.Head with Path = core; Loaded = false; Disposition = Authority.ProjectDisposition.LoadFailed; Reason = "workspace failed to load legacy solution project" }
                let decision = Authority.decide policy { completeFacts root with Projects = [ failed ] }
                Expect.equal decision.Outcome Inconclusive "project loading failure cannot fall back to a conclusive result"
                Expect.contains (reasonCodes decision) "project-load-failed" "project loading failure must be explicit"
                let loadedOptions = ProjectSystem.loadProjects [ tests ]
                Expect.isGreaterThan loadedOptions.Length 0 "the smallest net10.0 SDK-style test project must load genuinely"
                Expect.isGreaterThan loadedOptions.Head.SourceFiles.Length 0 "loaded fixture must carry source-file evidence"
                let unsupportedLoaded = { (completeFacts root).Projects.Head with Supported = false; Loaded = true; Disposition = Authority.ProjectDisposition.Unsupported; Reason = "policy unavailable" }
                let receipt = Authority.createReceipt root candidate policy "fsassay-policy.lock.json" "unavailable" { completeFacts root with Projects = [ unsupportedLoaded ] }
                Expect.equal receipt.counts.projectsLoaded 1 "receipt must retain workspace-loaded evidence"
                Expect.equal receipt.counts.projectsSupported 0 "receipt must retain unsupported classification"
                Expect.equal receipt.counts.projectsUnsupported 1 "receipt must retain unsupported count")

        testCase "zero loaded projects is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let unavailable = { (completeFacts root).Projects.Head with Disposition = Authority.ProjectDisposition.LoadFailed; Reason = "load failed" }
                let decision = Authority.decide policy { completeFacts root with Projects = [ unavailable ] }
                Expect.equal decision.Outcome Inconclusive "zero loaded cannot pass")

        testCase "zero eligible files is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let decision = Authority.decide policy { completeFacts root with Sources = [] }
                Expect.contains (reasonCodes decision) "files-zero-eligible" "zero eligible must be explicit")

        testCase "compiler workspace incompleteness is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let incomplete = ({ Path = Path.Combine(root, "src", "Broken.fs"); Disposition = Authority.SourceDisposition.CompilerIncomplete; Reason = "compiler errors" }: Authority.SourceEvidence)
                let decision = Authority.decide policy { completeFacts root with Sources = (completeFacts root).Sources @ [ incomplete ] }
                Expect.contains (reasonCodes decision) "compiler-workspace-incomplete" "compiler evidence must be complete")

        testCase "unsupported project is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let unsupported = ({ Path = Path.Combine(root, "ui", "Ui.fsproj"); ProjectClass = "other"; TargetFrameworks = [ "net10.0" ]; Supported = false; Loaded = true; Disposition = Authority.ProjectDisposition.Unsupported; Reason = "outside policy" }: Authority.ProjectEvidence)
                let decision = Authority.decide policy { completeFacts root with Projects = (completeFacts root).Projects @ [ unsupported ] }
                Expect.equal decision.Outcome Inconclusive "unsupported inputs prevent authority")

        testCase "project load failure is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let failed = ({ Path = Path.Combine(root, "other", "Other.fsproj"); ProjectClass = "other"; TargetFrameworks = [ "net10.0" ]; Supported = true; Loaded = false; Disposition = Authority.ProjectDisposition.LoadFailed; Reason = "load failed" }: Authority.ProjectEvidence)
                let decision = Authority.decide policy { completeFacts root with Projects = (completeFacts root).Projects @ [ failed ] }
                Expect.contains (reasonCodes decision) "project-load-failed" "load failure must be visible"
                let finding = ({ RuleId = "FSA-C01"; Path = Path.Combine(root, "src", "Core.fs"); Symbol = "file-scope"; Line = 1; Column = 0; Message = "policyless observation"; Fingerprint = "" }: Authority.FindingEvidence)
                let facts = {
                    completeFacts root with
                        Projects = []
                        Rules = [ { RuleId = finding.RuleId; Status = "incomplete"; EvidenceAvailable = false; FindingCount = 1 } ]
                        Findings = [ finding ]
                        PolicyErrors = [ "required policy lock not found" ]
                }
                let receipt = Authority.createReceipt root candidate Authority.unapprovedPolicy (Path.Combine(root, "fsassay-policy.lock.json")) "unavailable" facts
                Expect.equal receipt.outcome "Inconclusive" "policyless analysis must remain inconclusive"
                Expect.isFalse receipt.authoritative "policyless analysis cannot be authoritative"
                Expect.equal receipt.findings.[0].authorityClass "unclassified" "policyless findings must not be reported as removed"
                Expect.equal receipt.rules.[0].authorityClass "unclassified" "policyless rule outcomes must be explicit"
                Expect.isEmpty (Authority.validateReceipt receipt) "unclassified policyless receipt must validate"
                let json = Output.canonicalJsonBytes receipt |> Encoding.UTF8.GetString
                let sarif = Output.canonicalSarifBytes receipt |> Encoding.UTF8.GetString
                Expect.stringContains json "unclassified" "JSON must preserve policyless classification"
                Expect.stringContains sarif "unclassified" "SARIF must preserve policyless classification"
                let removedPolicy = {
                    policy with
                        experimentalRules = policy.experimentalRules |> Array.filter ((<>) finding.RuleId)
                        removedRules = [| finding.RuleId |]
                }
                let removedHash =
                    match Authority.canonicalPolicyIdentity removedPolicy with
                    | Ok(_, hash) -> hash
                    | Error errors -> failwithf "removed policy must be valid: %A" errors
                let removedReceipt = Authority.createReceipt root candidate removedPolicy (Path.Combine(root, "fsassay-policy.lock.json")) removedHash { facts with PolicyErrors = [] }
                Expect.equal removedReceipt.findings.[0].authorityClass "removed" "removed requires an explicit valid policy entry")

        testCase "internal tool failure is ToolFailure" <| fun _ ->
            withTempRoot (fun root ->
                let decision = Authority.decide policy { completeFacts root with ToolFailures = [ "plugin crashed" ] }
                Expect.equal decision.Outcome ToolFailure "untrustworthy tool execution has highest precedence")

        testCase "duplicate itemized evidence is ToolFailure" <| fun _ ->
            withTempRoot (fun root ->
                let decision = Authority.decide policy { completeFacts root with Sources = (completeFacts root).Sources @ (completeFacts root).Sources }
                Expect.contains (reasonCodes decision) "invalid-evidence" "invalid receipt facts are not trustworthy")

        testCase "ToolFailure outranks required failure" <| fun _ ->
            withTempRoot (fun root ->
                let failed = { passedTest root with Status = Authority.TestStatus.Failed; Failed = 1 }
                let decision = Authority.decide policy { completeFacts root with RequiredTests = [ failed ]; ToolFailures = [ "crash" ] }
                Expect.equal decision.Outcome ToolFailure "precedence is deterministic")

        testCase "Fail outranks concurrent incompleteness but removes authority" <| fun _ ->
            withTempRoot (fun root ->
                let failed = { passedTest root with Status = Authority.TestStatus.Failed; Failed = 1 }
                let incomplete = ({ Path = Path.Combine(root, "src", "Broken.fs"); Disposition = Authority.SourceDisposition.CompilerIncomplete; Reason = "compiler errors" }: Authority.SourceEvidence)
                let decision = Authority.decide policy { completeFacts root with RequiredTests = [ failed ]; Sources = (completeFacts root).Sources @ [ incomplete ] }
                Expect.equal decision.Outcome Fail "known required failure remains conclusive"
                Expect.isFalse decision.Authoritative "concurrent incompleteness removes authority")

        testCase "unavailable observational rules neither help nor prevent Pass" <| fun _ ->
            withTempRoot (fun root ->
                let unavailableRules =
                    (completeFacts root).Rules
                    |> List.map (fun rule -> { rule with Status = "unavailable"; EvidenceAvailable = false })
                let experimentalDecision = Authority.decide policy { completeFacts root with Rules = unavailableRules }
                Expect.equal experimentalDecision.Outcome Pass "experimental availability is not authority evidence"
                let advisoryPolicy = { policy with advisoryRules = [| "FSA-C01" |]; experimentalRules = [| "FSA-C04" |] }
                let advisoryDecision = Authority.decide advisoryPolicy { completeFacts root with Rules = unavailableRules }
                Expect.equal advisoryDecision.Outcome Pass "advisory availability is not authority evidence")

        testCase "hypothetical unapproved required rule is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let badPolicy = { policy with approvedBlockingRules = [| "FSA-C01" |]; experimentalRules = [| "FSA-C04" |] }
                let unavailable =
                    (completeFacts root).Rules
                    |> List.map (fun rule -> if rule.RuleId = "FSA-C01" then { rule with Status = "unavailable"; EvidenceAvailable = false } else rule)
                let decision = Authority.decide badPolicy { completeFacts root with Rules = unavailable }
                Expect.contains (reasonCodes decision) "gate-c-approval-missing" "M3 cannot infer admission from implementation status"
                Expect.contains (reasonCodes decision) "required-rule-unavailable" "only a hypothetical authority-required rule creates this gap")

        testCase "rule cannot be complete without project evidence" <| fun _ ->
            withTempRoot (fun root ->
                let badRule = ({ RuleId = "FSA-C01"; Status = "completed"; EvidenceAvailable = false; FindingCount = 0 }: Authority.RuleEvidence)
                let decision = Authority.decide policy { completeFacts root with Rules = [ badRule ] }
                Expect.equal decision.Outcome ToolFailure "contradictory emitted evidence is invalid")

        testCase "policy identity is canonical across whitespace and set ordering" <| fun _ ->
            withTempRoot (fun root ->
                let firstPath = Path.Combine(root, "first.json")
                let secondPath = Path.Combine(root, "second.json")
                let firstPolicy = { policy with experimentalRules = policy.experimentalRules |> Array.rev }
                let secondPolicy = { policy with experimentalRules = policy.experimentalRules |> Array.sort }
                File.WriteAllText(firstPath, JsonSerializer.Serialize(firstPolicy))
                File.WriteAllText(secondPath, JsonSerializer.Serialize(secondPolicy, JsonSerializerOptions(WriteIndented = true)))
                match Authority.loadPolicy firstPath, Authority.loadPolicy secondPath with
                | Authority.PolicyLoaded (_, firstHash, _), Authority.PolicyLoaded (_, secondHash, _) ->
                    Expect.equal firstHash secondHash "policy hash must be semantic and deterministic"
                | first, second -> failtestf "expected two valid policies, got %A and %A" first second)

        testCase "JSON is deterministic across checkout roots" <| fun _ ->
            withTempRoot (fun first -> withTempRoot (fun second ->
                let firstBytes = receipt first (completeFacts first) |> Output.canonicalJsonBytes
                let secondBytes = receipt second (completeFacts second) |> Output.canonicalJsonBytes
                Expect.sequenceEqual firstBytes secondBytes "absolute checkout roots must not leak"))

        testCase "SARIF is deterministic across checkout roots" <| fun _ ->
            withTempRoot (fun first -> withTempRoot (fun second ->
                let firstBytes = receipt first (completeFacts first) |> Output.canonicalSarifBytes
                let secondBytes = receipt second (completeFacts second) |> Output.canonicalSarifBytes
                Expect.sequenceEqual firstBytes secondBytes "SARIF paths and ordering must be root-independent"))

        testCase "producer receipt passes strict round-trip validation" <| fun _ ->
            withTempRoot (fun root ->
                let bytes = receipt root (completeFacts root) |> Output.canonicalJsonBytes
                match Authority.deserializeAndValidateReceipt bytes with
                | Ok parsed -> Expect.equal parsed.outcome "Pass" "producer schema must round-trip"
                | Error errors -> failtestf "producer emitted invalid receipt: %A" errors)

        testCase "strict receipt mutations reject unknown fields hashes and count drift" <| fun _ ->
            withTempRoot (fun root ->
                let json = receipt root (completeFacts root) |> Output.canonicalJsonBytes |> Encoding.UTF8.GetString
                Expect.stringContains json "\"supported\": true" "project support state must be explicit in receipts"
                let unknown = json.Replace("\"kind\": \"commit\",", "\"kind\": \"commit\",\n    \"unexpected\": true,") |> Encoding.UTF8.GetBytes
                let badHash = json.Replace(String.replicate 40 "a", "bad") |> Encoding.UTF8.GetBytes
                let badCount = json.Replace("\"analyzedFiles\": 1", "\"analyzedFiles\": 2") |> Encoding.UTF8.GetBytes
                Expect.isError (Authority.deserializeAndValidateReceipt unknown) "unknown nested fields must be rejected"
                Expect.isError (Authority.deserializeAndValidateReceipt badHash) "malformed identities must be rejected"
                Expect.isError (Authority.deserializeAndValidateReceipt badCount) "counts must reconcile with itemized evidence")

        testCase "semantic receipt mutations cannot forge outcome authority or reasons" <| fun _ ->
            withTempRoot (fun root ->
                let serialize value = JsonSerializer.SerializeToUtf8Bytes(value)
                let expectRejected label value =
                    Expect.isError (Authority.deserializeAndValidateReceipt (serialize value)) label

                let unsupported = ({ Path = Path.Combine(root, "ui", "Ui.fsproj"); ProjectClass = "other"; TargetFrameworks = [ "net10.0" ]; Supported = false; Loaded = true; Disposition = Authority.ProjectDisposition.Unsupported; Reason = "outside policy" }: Authority.ProjectEvidence)
                let notRun = { passedTest root with Status = Authority.TestStatus.NotRun; Passed = 0 }
                let incompleteFacts = {
                    completeFacts root with
                        Projects = (completeFacts root).Projects @ [ unsupported ]
                        RequiredTests = [ notRun ]
                }
                let incompleteReceipt = receipt root incompleteFacts
                Expect.equal incompleteReceipt.outcome "Inconclusive" "fixture must contain honest incompleteness"
                Expect.isFalse incompleteReceipt.authoritative "fixture must not be authoritative"
                expectRejected "Pass true cannot hide notRun and unsupported evidence" { incompleteReceipt with outcome = "Pass"; authoritative = true }
                expectRejected "Pass false is never a valid semantic outcome" { incompleteReceipt with outcome = "Pass"; authoritative = false }

                let completeReceipt = receipt root (completeFacts root)
                Expect.equal completeReceipt.outcome "Pass" "complete fixture must pass"
                Expect.isTrue completeReceipt.authoritative "Pass fixture must be authoritative"
                Expect.isEmpty completeReceipt.policy.snapshot.baseline.records "M3 has no accepted baseline debt"
                Expect.isEmpty completeReceipt.appliedSuppressions "configured debt must never be reported as applied suppression"
                expectRejected "M3 cannot forge applied suppressions" { completeReceipt with appliedSuppressions = [| "*" |] }
                expectRejected "Pass false contradicts complete evidence" { completeReceipt with authoritative = false }
                expectRejected "Inconclusive cannot be forged over complete evidence" { completeReceipt with outcome = "Inconclusive"; authoritative = false }
                let forgedFailureReason = ({ code = "required-test-failed"; detail = "forged failure" }: Authority.ReasonReceipt)
                expectRejected "Fail requires an itemized required failure or blocking finding" { completeReceipt with outcome = "Fail"; reasons = [| forgedFailureReason |] }
                let forgedToolReason = ({ code = "tool-failure"; detail = "forged tool failure" }: Authority.ReasonReceipt)
                expectRejected "ToolFailure requires itemized tool or invalid evidence" { completeReceipt with outcome = "ToolFailure"; authoritative = false; reasons = [| forgedToolReason |] }

                let removedReason = { incompleteReceipt with reasons = incompleteReceipt.reasons |> Array.tail }
                expectRejected "removing a required reason must fail reconciliation" removedReason
                let forgedReason = ({ code = "evidence-missing"; detail = "forged extra reason" }: Authority.ReasonReceipt)
                let forgedReasons = Array.append incompleteReceipt.reasons [| forgedReason |] |> Array.sortBy (fun reason -> reason.code, reason.detail)
                expectRejected "adding a forged reason must fail reconciliation" { incompleteReceipt with reasons = forgedReasons }

                let failedTest = { passedTest root with Status = Authority.TestStatus.Failed; Passed = 1; Failed = 1 }
                let conclusiveFail = receipt root { completeFacts root with RequiredTests = [ failedTest ] }
                Expect.equal conclusiveFail.outcome "Fail" "real required failure is Fail"
                Expect.isTrue conclusiveFail.authoritative "complete conclusive failure is authoritative"
                Expect.isOk (Authority.deserializeAndValidateReceipt (serialize conclusiveFail)) "complete Fail must validate"
                let incompleteFail = receipt root { incompleteFacts with RequiredTests = [ failedTest ] }
                Expect.equal incompleteFail.outcome "Fail" "known failure outranks incompleteness"
                Expect.isFalse incompleteFail.authoritative "concurrent incompleteness removes Fail authority"
                Expect.isOk (Authority.deserializeAndValidateReceipt (serialize incompleteFail)) "Fail false is valid only with reconciled incompleteness"
                let honestToolFailure = receipt root { completeFacts root with ToolFailures = [ "plugin crashed" ] }
                Expect.equal honestToolFailure.outcome "ToolFailure" "itemized tool failure is ToolFailure"
                Expect.isOk (Authority.deserializeAndValidateReceipt (serialize honestToolFailure)) "honest ToolFailure must validate")

        testCase "complete policy snapshot is hash-bound and can be caller-pinned" <| fun _ ->
            withTempRoot (fun root ->
                let serialize value = JsonSerializer.SerializeToUtf8Bytes(value)
                let honest = receipt root (completeFacts root)
                let expectedHash = honest.policy.sha256
                let expectRejected label snapshot =
                    let mutated = { honest with policy = { honest.policy with snapshot = snapshot } }
                    Expect.isError (Authority.deserializeAndValidateReceipt (serialize mutated)) label

                expectRejected "removing required tests without changing the hash must fail" { honest.policy.snapshot with requiredTests = [||] }
                expectRejected "removing project classes without changing the hash must fail" { honest.policy.snapshot with requiredProjectClasses = [||] }
                expectRejected "removing target frameworks without changing the hash must fail" { honest.policy.snapshot with requiredTargetFrameworks = [||] }
                expectRejected "changing rule authority without changing the hash must fail" { honest.policy.snapshot with experimentalRules = [| "FSA-C04" |] }
                expectRejected "changing profiles without changing the hash must fail" { honest.policy.snapshot with enabledProfiles = [| "alternate" |] }
                expectRejected "changing baseline configuration without changing the hash must fail" {
                    honest.policy.snapshot with baseline = { identity = "configured"; reviewedBy = ""; reviewedOn = ""; records = [||] }
                }
                let exceptionEvidence = ({
                    id = "reviewed-exception"
                    category = "hosting"
                    relativePath = "src/Host.fs"
                    symbol = "Host.start"
                    owner = "human@example.invalid"
                    reason = "bounded test mutation"
                    createdOn = "2026-08-03"
                    expiresOn = "2099-01-01"
                    shapeClauses = [| "SN-CORE" |]
                }: Authority.PolicyException)
                let alternatePolicy = { honest.policy.snapshot with exceptions = [| exceptionEvidence |] }
                expectRejected "changing exceptions without changing the hash must fail" alternatePolicy

                match Authority.canonicalPolicyIdentity alternatePolicy with
                | Error errors -> failtestf "alternate policy fixture must be valid: %A" errors
                | Ok(alternateSnapshot, alternateHash) ->
                    let internallyConsistentReplacement = {
                        honest with
                            policy = { honest.policy with snapshot = alternateSnapshot; sha256 = alternateHash }
                    }
                    let replacementBytes = serialize internallyConsistentReplacement
                    Expect.isOk (Authority.deserializeAndValidateReceipt replacementBytes) "internal policy consistency is not a signature"
                    Expect.isError (Authority.deserializeAndValidateReceiptForPolicy expectedHash replacementBytes) "caller pin must reject a jointly replaced snapshot and hash"

                let honestBytes = serialize honest
                Expect.isOk (Authority.deserializeAndValidateReceiptForPolicy expectedHash honestBytes) "caller pin must accept the expected policy identity"
                Expect.isError (Authority.deserializeAndValidateReceiptForPolicy (String.replicate 64 "f") honestBytes) "wrong caller pin must be rejected"
                let honestContext = ({
                    expectedPolicySha256 = expectedHash
                    expectedCommitSha = candidate.commitSha
                    expectedTreeSha = candidate.treeSha
                    expectedApprovedHeadSha = Some candidate.approvedHeadSha
                    expectedSyntheticMergeSha = None
                    expectedPackageSha256 = None
                }: Authority.ReceiptValidationContext)
                Expect.isOk (Authority.deserializeAndValidateReceiptForContext honestContext honestBytes) "honest commit receipt must validate against reviewed context"

                let alternateCandidate = {
                    candidate with
                        commitSha = String.replicate 40 "d"
                        approvedHeadSha = String.replicate 40 "d"
                        treeSha = String.replicate 40 "e"
                }
                let coherentCandidateReplacement = { honest with candidate = alternateCandidate }
                let coherentCandidateBytes = serialize coherentCandidateReplacement
                Expect.isOk (Authority.deserializeAndValidateReceipt coherentCandidateBytes) "context-free validation proves consistency, not candidate authenticity"
                Expect.isError (Authority.deserializeAndValidateReceiptForContext honestContext coherentCandidateBytes) "reviewed commit and tree pins must reject coherent candidate replacement"

                let syntheticSha = String.replicate 40 "f"
                let approvedHead = String.replicate 40 "d"
                let syntheticCandidate = {
                    candidate with
                        kind = "synthetic-merge"
                        commitSha = syntheticSha
                        approvedHeadSha = approvedHead
                        treeSha = String.replicate 40 "e"
                        syntheticMergeSha = syntheticSha
                }
                let syntheticReceipt = receiptForCandidate root syntheticCandidate (completeFacts root)
                let syntheticContext = {
                    honestContext with
                        expectedCommitSha = syntheticSha
                        expectedTreeSha = syntheticCandidate.treeSha
                        expectedApprovedHeadSha = Some approvedHead
                        expectedSyntheticMergeSha = Some syntheticSha
                }
                let syntheticBytes = serialize syntheticReceipt
                Expect.isOk (Authority.deserializeAndValidateReceiptForContext syntheticContext syntheticBytes) "reviewed PR head, synthetic merge and tree must validate together"
                Expect.isError (Authority.deserializeAndValidateReceiptForContext { syntheticContext with expectedApprovedHeadSha = None } syntheticBytes) "synthetic receipt requires the reviewed head pin"

                let repositoryRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
                let actualPolicyPath = Path.Combine(repositoryRoot, "fsassay-policy.lock.json")
                match Authority.loadPolicy actualPolicyPath with
                | Authority.PolicyLoaded(actualPolicy, actualHash, _) ->
                    let actualReceipt = Authority.createReceipt repositoryRoot candidate actualPolicy actualPolicyPath actualHash Authority.emptyFacts
                    Expect.equal actualReceipt.policy.snapshot actualPolicy "receipt snapshot must equal the actual canonical lock policy"
                    Expect.equal actualReceipt.policy.sha256 actualHash "receipt hash must equal the actual lock policy identity"
                    Expect.isOk (Authority.deserializeAndValidateReceiptForPolicy actualHash (serialize actualReceipt)) "actual policy receipt must validate against its pinned identity"
                | other -> failtestf "repository policy lock must load: %A" other)

        testCase "failed evidence write removes stale requested artifacts" <| fun _ ->
            withTempRoot (fun root ->
                let stale = Path.Combine(root, "receipt.json")
                File.WriteAllText(stale, "stale")
                let denied = "/proc/fsassay-m2-denied.sarif"
                let result = Output.writeRequestedEvidence (receipt root (completeFacts root)) (Some stale) (Some denied)
                Expect.isError result "denied evidence target must map to failure"
                Expect.isFalse (File.Exists(stale)) "stale sibling evidence must be removed")

        testCase "SARIF carries every non-Pass reason as execution notification" <| fun _ ->
            withTempRoot (fun root ->
                let notRun = { passedTest root with Status = Authority.TestStatus.NotRun; Passed = 0 }
                let bytes = receipt root { completeFacts root with RequiredTests = [ notRun ] } |> Output.canonicalSarifBytes
                use document = JsonDocument.Parse(bytes)
                let run = document.RootElement.GetProperty("runs").[0]
                let notifications = run.GetProperty("invocations").[0].GetProperty("toolExecutionNotifications")
                let reasons = run.GetProperty("properties").GetProperty("reasons")
                Expect.equal (notifications.GetArrayLength()) (reasons.GetArrayLength()) "SARIF notifications must reconcile with receipt reasons")

        testCase "policy rule and skipped-test contradictions fail closed" <| fun _ ->
            withTempRoot (fun root ->
                let duplicatePath = Path.Combine(root, "duplicate-policy.json")
                let duplicatePolicy = { policy with experimentalRules = [| "FSA-C01"; "FSA-C01" |] }
                File.WriteAllText(duplicatePath, JsonSerializer.Serialize(duplicatePolicy))
                match Authority.loadPolicy duplicatePath with
                | Authority.PolicyInvalid _ -> ()
                | other -> failtestf "duplicate policy values must be invalid, got %A" other
                let wildcardBaselinePath = Path.Combine(root, "wildcard-baseline-policy.json")
                let wildcardBaseline = { policy with baseline = { identity = "configured"; reviewedBy = ""; reviewedOn = ""; records = [||] } }
                File.WriteAllText(wildcardBaselinePath, JsonSerializer.Serialize(wildcardBaseline))
                match Authority.loadPolicy wildcardBaselinePath with
                | Authority.PolicyInvalid _ -> ()
                | other -> failtestf "wildcard baseline must remain invalid until baseline governance exists, got %A" other
                let countMismatch = ({ RuleId = "FSA-C01"; Status = "completed"; EvidenceAvailable = true; FindingCount = 1 }: Authority.RuleEvidence)
                let countDecision = Authority.decide policy { completeFacts root with Rules = [ countMismatch; (completeFacts root).Rules.[1] ] }
                Expect.equal countDecision.Outcome ToolFailure "declared rule counts must match findings"
                let badSkipped = { passedTest root with Status = Authority.TestStatus.Skipped; Passed = 0; Skipped = 0 }
                let skippedDecision = Authority.decide policy { completeFacts root with RequiredTests = [ badSkipped ] }
                Expect.equal skippedDecision.Outcome ToolFailure "skipped status requires a nonzero skipped count")

        testCase "M3 policy and classification partition the exact catalogue" <| fun _ ->
            let policyPath = Path.GetFullPath("fsassay-policy.lock.json")
            let classificationPath = Path.GetFullPath("docs/contracts/fsassay-rule-classification-v1.json")
            match Authority.loadPolicy policyPath with
            | Authority.PolicyLoaded(locked, _, _) ->
                let allClasses =
                    Array.concat [|
                        locked.approvedBlockingRules; locked.advisoryRules; locked.experimentalRules
                        locked.prototypeRules; locked.dummyRules; locked.deprecatedRules; locked.removedRules
                    |]
                let catalogue = FsAssay.Analyzers.Domain.Rule.AllRules |> List.map _.Code |> List.sort
                Expect.sequenceEqual (allClasses |> Array.sort) catalogue "every catalogue identity must appear exactly once"
                Expect.equal locked.approvedBlockingRules.Length 0 "candidate proposes no blockers pending Human Gate C"
                Expect.equal locked.advisoryRules.Length 0 "candidate proposes no advisories pending Human Gate C"
                Expect.equal locked.experimentalRules.Length 35 "implemented rules remain experimental"
                Expect.equal locked.prototypeRules.Length 36 "prototype count is locked"
                Expect.equal locked.dummyRules.Length 22 "dummy count is locked"
            | other -> failtestf "M3 policy must load: %A" other
            use document = JsonDocument.Parse(File.ReadAllBytes(classificationPath))
            let root = document.RootElement
            let jsonClass (name: string) = root.GetProperty(name).EnumerateArray() |> Seq.map _.GetString() |> Seq.toArray
            Expect.equal (root.GetProperty("catalogueCount").GetInt32()) 93 "machine classification count is locked"
            let machine = Array.concat [| jsonClass "blocking"; jsonClass "advisory"; jsonClass "experimental"; jsonClass "prototype"; jsonClass "dummy"; jsonClass "deprecated"; jsonClass "removed" |]
            Expect.sequenceEqual (machine |> Array.sort) (FsAssay.Analyzers.Domain.Rule.AllRules |> List.map _.Code |> List.sort) "policy-independent classification must cover the same catalogue"

        testCase "M3 Shape contract exposes stable clauses and bounded exceptions" <| fun _ ->
            use document = JsonDocument.Parse(File.ReadAllBytes(Path.GetFullPath("docs/contracts/fsassay-shape-v1.json")))
            let root = document.RootElement
            let values (name: string) = root.GetProperty(name).EnumerateArray() |> Seq.map _.GetString() |> Seq.toArray
            Expect.equal (root.GetProperty("contractVersion").GetString()) Authority.ShapeContractVersion "Shape identity must match policy validation"
            Expect.equal (values "newClauses").Length 9 "Shape New clause count is locked"
            Expect.equal (values "convergeClauses").Length 10 "Shape Converge sequence is locked"
            Expect.sequenceEqual (values "frameworkExceptionCategories") [| "dependency-injection"; "hosting"; "interoperability"; "persistence"; "serialization"; "ui" |] "exception categories must remain bounded"
            Expect.isFalse (root.GetProperty("authorityLaws").GetProperty("missingEvidenceCanPass").GetBoolean()) "missing evidence cannot become Pass"

        testCase "M3 schema bump is strict and preserves M2 evidence fields" <| fun _ ->
            withTempRoot (fun root ->
                let currentJson = receipt root (completeFacts root) |> Output.canonicalJsonBytes |> Encoding.UTF8.GetString
                let m2Version =
                    currentJson
                        .Replace(Authority.EvidenceSchemaVersion, "fsassay-authority-receipt/1.0.0")
                        .Replace(Authority.PolicySchemaVersion, "fsassay-policy/1.0.0")
                        .Replace(Authority.ShapeContractVersion, "not-established")
                    |> Encoding.UTF8.GetBytes
                Expect.isError (Authority.deserializeAndValidateReceipt m2Version) "M2 receipt identities require explicit migration"
                use schema = JsonDocument.Parse(File.ReadAllBytes(Path.GetFullPath("docs/contracts/fsassay-authority-receipt.schema.json")))
                let required = schema.RootElement.GetProperty("required").EnumerateArray() |> Seq.map _.GetString() |> Set.ofSeq
                for retained in [ "candidate"; "policy"; "toolchain"; "counts"; "projects"; "sources"; "tests"; "rules"; "findings"; "appliedSuppressions"; "policyErrors"; "evidenceErrors"; "missingEvidence"; "toolFailures" ] do
                    Expect.contains required retained (sprintf "M2 evidence field '%s' remains required" retained)
                Expect.contains required "appliedBaselineRecords" "M3 adds explicit applied-baseline evidence")

        testCase "baseline matching is exact and expiry is inclusive on explicit policy date" <| fun _ ->
            let finding = ({
                RuleId = "FSA-C01"
                Path = "src/Core.fs"
                Symbol = "Core.value"
                Line = 12
                Column = 4
                Message = "deterministic specimen"
                Fingerprint = String.replicate 64 "a"
            }: Authority.FindingEvidence)
            let record = ({
                id = "BL-001"
                ruleId = finding.RuleId
                fingerprint = finding.Fingerprint
                relativePath = finding.Path
                symbol = finding.Symbol
                owner = "shape-review@example.invalid"
                rationale = "bounded migration debt"
                disposition = "accepted"
                createdOn = "2026-07-01"
                expiresOn = "2026-08-03"
                policyVersion = Authority.PolicySchemaVersion
            }: Authority.BaselineRecord)
            let baseline = ({ identity = "test-only"; reviewedBy = "shape-review@example.invalid"; reviewedOn = "2026-07-01"; records = [| record |] }: Authority.BaselinePolicy)
            let inclusive = Authority.evaluateBaseline "2026-08-03" baseline [ finding ]
            Expect.sequenceEqual inclusive.AppliedRecordIds [ "BL-001" ] "record remains active on its expiry date"
            Expect.isEmpty inclusive.NewFindings "exact active match is reviewed debt"
            let expired = Authority.evaluateBaseline "2026-08-04" baseline [ finding ]
            Expect.isEmpty expired.AppliedRecordIds "expired record is not reported as applied"
            Expect.equal expired.NewFindings.Length 1 "expired match is new debt"
            let symbolMismatch = Authority.evaluateBaseline "2026-08-03" baseline [ { finding with Symbol = "Core.other" } ]
            Expect.isEmpty symbolMismatch.AppliedRecordIds "symbol mismatch cannot reuse a record"
            Expect.equal symbolMismatch.NewFindings.Length 1 "exact symbol is required"

        testCase "resolved baseline debt reappears and reviewed metadata changes identity" <| fun _ ->
            let finding = ({ RuleId = "FSA-C01"; Path = "src/Core.fs"; Symbol = "Core.value"; Line = 1; Column = 0; Message = "x"; Fingerprint = String.replicate 64 "b" }: Authority.FindingEvidence)
            let record = ({ id = "BL-002"; ruleId = finding.RuleId; fingerprint = finding.Fingerprint; relativePath = finding.Path; symbol = finding.Symbol; owner = "owner-a"; rationale = "resolved after migration"; disposition = "resolved"; createdOn = "2026-07-01"; expiresOn = ""; policyVersion = Authority.PolicySchemaVersion }: Authority.BaselineRecord)
            let baseline = ({ identity = ""; reviewedBy = "reviewer-a"; reviewedOn = "2026-07-02"; records = [| record |] }: Authority.BaselinePolicy)
            let evaluation = Authority.evaluateBaseline "2026-08-03" baseline [ finding ]
            Expect.isEmpty evaluation.AppliedRecordIds "resolved records are never applied"
            Expect.equal evaluation.ReappearingFindings.Length 1 "resolved debt must be identified as reappearing"
            let firstIdentity = Authority.canonicalBaselineIdentity baseline
            let secondIdentity = Authority.canonicalBaselineIdentity { baseline with reviewedBy = "reviewer-b" }
            Expect.notEqual firstIdentity secondIdentity "review metadata is hash-bound"

        testCase "receipt reports only exact active baseline records as applied" <| fun _ ->
            withTempRoot (fun root ->
                let relativePath = "src/Core.fs"
                let message = "deterministic receipt specimen"
                let fingerprint = Authority.findingFingerprint "FSA-C01" relativePath 7 2 message
                let finding = ({ RuleId = "FSA-C01"; Path = Path.Combine(root, "src", "Core.fs"); Symbol = "Core.value"; Line = 7; Column = 2; Message = message; Fingerprint = "producer-recomputes" }: Authority.FindingEvidence)
                let record = ({ id = "BL-RECEIPT"; ruleId = finding.RuleId; fingerprint = fingerprint; relativePath = relativePath; symbol = finding.Symbol; owner = "owner"; rationale = "bounded"; disposition = "accepted"; createdOn = "2026-07-01"; expiresOn = "2026-08-03"; policyVersion = Authority.PolicySchemaVersion }: Authority.BaselineRecord)
                let baseline = ({ identity = "test-only"; reviewedBy = "reviewer"; reviewedOn = "2026-07-01"; records = [| record |] }: Authority.BaselinePolicy)
                let blockingPolicy = {
                    policy with
                        approvedBlockingRules = [| "FSA-C01" |]
                        experimentalRules = policy.experimentalRules |> Array.filter ((<>) "FSA-C01")
                        baseline = baseline
                        evaluationDate = "2026-08-03"
                }
                let findingRule = ({ RuleId = "FSA-C01"; Status = "completed"; EvidenceAvailable = true; FindingCount = 1 }: Authority.RuleEvidence)
                let facts = { completeFacts root with Rules = findingRule :: (completeFacts root).Rules.Tail; Findings = [ finding ] }
                let activeReceipt = Authority.createReceipt root candidate blockingPolicy (Path.Combine(root, "fsassay-policy.lock.json")) (String.replicate 64 "d") facts
                Expect.sequenceEqual activeReceipt.appliedBaselineRecords [| "BL-RECEIPT" |] "only exact active record is itemized"
                Expect.isFalse (activeReceipt.reasons |> Array.exists (fun reason -> reason.code = "new-blocking-finding")) "active reviewed debt is not new"
                let expiredPolicy = { blockingPolicy with evaluationDate = "2026-08-04" }
                let expiredReceipt = Authority.createReceipt root candidate expiredPolicy (Path.Combine(root, "fsassay-policy.lock.json")) (String.replicate 64 "e") facts
                Expect.isEmpty expiredReceipt.appliedBaselineRecords "expired record is not reported as applied"
                Expect.isTrue (expiredReceipt.reasons |> Array.exists (fun reason -> reason.code = "new-blocking-finding")) "expired debt fails explicitly")

        testCase "baseline cannot hide missing authority evidence" <| fun _ ->
            withTempRoot (fun root ->
                let finding = ({ RuleId = "FSA-C01"; Path = "src/Core.fs"; Symbol = "Core.value"; Line = 1; Column = 0; Message = "x"; Fingerprint = String.replicate 64 "c" }: Authority.FindingEvidence)
                let record = ({ id = "BL-003"; ruleId = finding.RuleId; fingerprint = finding.Fingerprint; relativePath = "src/Core.fs"; symbol = finding.Symbol; owner = "owner"; rationale = "bounded"; disposition = "accepted"; createdOn = "2026-07-01"; expiresOn = "2026-09-01"; policyVersion = Authority.PolicySchemaVersion }: Authority.BaselineRecord)
                let blockingPolicy = {
                    policy with
                        approvedBlockingRules = [| "FSA-C01" |]
                        experimentalRules = policy.experimentalRules |> Array.filter ((<>) "FSA-C01")
                        baseline = { identity = "test-only"; reviewedBy = "reviewer"; reviewedOn = "2026-07-01"; records = [| record |] }
                        evaluationDate = "2026-08-03"
                }
                let unavailableRule = ({ RuleId = "FSA-C01"; Status = "unavailable"; EvidenceAvailable = false; FindingCount = 1 }: Authority.RuleEvidence)
                let facts = { completeFacts root with MissingEvidence = [ "generated members unavailable" ]; Rules = unavailableRule :: (completeFacts root).Rules.Tail; Findings = [ finding ] }
                let decision = Authority.decide blockingPolicy facts
                Expect.equal decision.Outcome Inconclusive "active debt does not hide incomplete evidence"
                Expect.isFalse decision.Authoritative "incomplete evidence can never be authoritative"
                Expect.contains (reasonCodes decision) "evidence-missing" "missing evidence remains explicit"
                Expect.contains (reasonCodes decision) "required-rule-unavailable" "rule unavailability remains explicit")
    ]
