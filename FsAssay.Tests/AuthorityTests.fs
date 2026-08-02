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

let private policy = {
    Authority.unapprovedPolicy with
        requiredProjectClasses = [| "core" |]
        requiredTargetFrameworks = [| "net10.0" |]
        requiredTests = [| requiredTest |]
        advisoryRules = [||]
        experimentalRules = [| "LEGACY001"; "PROTO001" |]
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
            { Path = Path.Combine(root, "src", "Core.fsproj"); ProjectClass = "core"; TargetFrameworks = [ "net10.0" ]; Disposition = Authority.ProjectDisposition.Loaded; Reason = "" }
        ]
        Sources = [
            { Path = Path.Combine(root, "src", "Core.fs"); Disposition = Authority.SourceDisposition.Analyzed; Reason = "" }
        ]
        RequiredTests = [ passedTest root ]
        Rules = [
            { RuleId = "LEGACY001"; Status = "completed"; EvidenceAvailable = true; FindingCount = 0 }
            { RuleId = "PROTO001"; Status = "completed"; EvidenceAvailable = true; FindingCount = 0 }
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
    Authority.createReceipt
        root
        candidateIdentity
        policy
        (Path.Combine(root, "fsassay-policy.lock.json"))
        (String.replicate 64 "c")
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
    testList "M2 authority contract" [
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
                let _, completePrMissing, completePrErrors =
                    Authority.candidateIdentityWithEnvironment root tracked mergeSha "pull_request" approvedFeature mergeSha null
                Expect.isEmpty completePrMissing "reviewed head and synthetic merge evidence must be complete"
                Expect.isEmpty completePrErrors "reviewed head must equal the synthetic merge second parent"
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
                Expect.contains (reasonCodes decision) "projects-zero-discovered" "zero discovery must be explicit")

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
                let unsupported = ({ Path = Path.Combine(root, "ui", "Ui.fsproj"); ProjectClass = "other"; TargetFrameworks = [ "net10.0" ]; Disposition = Authority.ProjectDisposition.Unsupported; Reason = "outside policy" }: Authority.ProjectEvidence)
                let decision = Authority.decide policy { completeFacts root with Projects = (completeFacts root).Projects @ [ unsupported ] }
                Expect.equal decision.Outcome Inconclusive "unsupported inputs prevent authority")

        testCase "project load failure is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let failed = ({ Path = Path.Combine(root, "other", "Other.fsproj"); ProjectClass = "other"; TargetFrameworks = [ "net10.0" ]; Disposition = Authority.ProjectDisposition.LoadFailed; Reason = "load failed" }: Authority.ProjectEvidence)
                let decision = Authority.decide policy { completeFacts root with Projects = (completeFacts root).Projects @ [ failed ] }
                Expect.contains (reasonCodes decision) "project-load-failed" "load failure must be visible")

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
                let advisoryPolicy = { policy with advisoryRules = [| "LEGACY001" |]; experimentalRules = [| "PROTO001" |] }
                let advisoryDecision = Authority.decide advisoryPolicy { completeFacts root with Rules = unavailableRules }
                Expect.equal advisoryDecision.Outcome Pass "advisory availability is not authority evidence")

        testCase "hypothetical unapproved required rule is Inconclusive" <| fun _ ->
            withTempRoot (fun root ->
                let badPolicy = { policy with approvedBlockingRules = [| "LEGACY001" |]; experimentalRules = [| "PROTO001" |] }
                let unavailable =
                    (completeFacts root).Rules
                    |> List.map (fun rule -> if rule.RuleId = "LEGACY001" then { rule with Status = "unavailable"; EvidenceAvailable = false } else rule)
                let decision = Authority.decide badPolicy { completeFacts root with Rules = unavailable }
                Expect.contains (reasonCodes decision) "gate-c-approval-missing" "M2 cannot inherit legacy admission"
                Expect.contains (reasonCodes decision) "required-rule-unavailable" "only a hypothetical authority-required rule creates this gap")

        testCase "rule cannot be complete without project evidence" <| fun _ ->
            withTempRoot (fun root ->
                let badRule = ({ RuleId = "LEGACY001"; Status = "completed"; EvidenceAvailable = false; FindingCount = 0 }: Authority.RuleEvidence)
                let decision = Authority.decide policy { completeFacts root with Rules = [ badRule ] }
                Expect.equal decision.Outcome ToolFailure "contradictory emitted evidence is invalid")

        testCase "policy identity is canonical across whitespace and set ordering" <| fun _ ->
            withTempRoot (fun root ->
                let firstPath = Path.Combine(root, "first.json")
                let secondPath = Path.Combine(root, "second.json")
                let firstPolicy = { policy with experimentalRules = [| "Z"; "A" |] }
                let secondPolicy = { policy with experimentalRules = [| "A"; "Z" |] }
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

                let unsupported = ({ Path = Path.Combine(root, "ui", "Ui.fsproj"); ProjectClass = "other"; TargetFrameworks = [ "net10.0" ]; Disposition = Authority.ProjectDisposition.Unsupported; Reason = "outside policy" }: Authority.ProjectEvidence)
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
                Expect.isEmpty completeReceipt.configuredBaselineFindings "M2 has no configured baseline governance"
                Expect.isEmpty completeReceipt.appliedSuppressions "configured debt must never be reported as applied suppression"
                expectRejected "M2 cannot forge applied suppressions" { completeReceipt with appliedSuppressions = [| "*" |] }
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
                let duplicatePolicy = { policy with experimentalRules = [| "LEGACY001"; "LEGACY001" |] }
                File.WriteAllText(duplicatePath, JsonSerializer.Serialize(duplicatePolicy))
                match Authority.loadPolicy duplicatePath with
                | Authority.PolicyInvalid _ -> ()
                | other -> failtestf "duplicate policy values must be invalid, got %A" other
                let wildcardBaselinePath = Path.Combine(root, "wildcard-baseline-policy.json")
                let wildcardBaseline = { policy with baseline = { identity = "configured"; approvedFindings = [| "*" |] } }
                File.WriteAllText(wildcardBaselinePath, JsonSerializer.Serialize(wildcardBaseline))
                match Authority.loadPolicy wildcardBaselinePath with
                | Authority.PolicyInvalid _ -> ()
                | other -> failtestf "wildcard baseline must remain invalid until baseline governance exists, got %A" other
                let countMismatch = ({ RuleId = "LEGACY001"; Status = "completed"; EvidenceAvailable = true; FindingCount = 1 }: Authority.RuleEvidence)
                let countDecision = Authority.decide policy { completeFacts root with Rules = [ countMismatch; (completeFacts root).Rules.[1] ] }
                Expect.equal countDecision.Outcome ToolFailure "declared rule counts must match findings"
                let badSkipped = { passedTest root with Status = Authority.TestStatus.Skipped; Passed = 0; Skipped = 0 }
                let skippedDecision = Authority.decide policy { completeFacts root with RequiredTests = [ badSkipped ] }
                Expect.equal skippedDecision.Outcome ToolFailure "skipped status requires a nonzero skipped count")
    ]
