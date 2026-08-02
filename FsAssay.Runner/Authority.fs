namespace FsAssay.Runner

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Serialization

module Authority =
    [<Literal>]
    let PolicySchemaVersion = "fsassay-policy/1.0.0"

    [<Literal>]
    let EvidenceSchemaVersion = "fsassay-authority-receipt/1.0.0"

    [<Literal>]
    let ContractVersion = "authority-contract/1.0.0"

    type RequiredTestPolicy = {
        id: string
        project: string
        minimumPassed: int
    }

    type BaselinePolicy = {
        identity: string
        approvedFindings: string[]
    }

    type PolicyException = {
        id: string
        owner: string
        reason: string
        expiresOn: string
    }

    type PolicyLock = {
        policySchemaVersion: string
        evidenceSchemaVersion: string
        authorityContractVersion: string
        shapeContractVersion: string
        toolVersion: string
        enabledProfiles: string[]
        approvedBlockingRules: string[]
        advisoryRules: string[]
        experimentalRules: string[]
        requiredProjectClasses: string[]
        requiredTargetFrameworks: string[]
        requiredTests: RequiredTestPolicy[]
        baseline: BaselinePolicy
        exceptions: PolicyException[]
    }

    type TestStatus =
        | Passed
        | Failed
        | NotRun
        | Skipped

    type ProjectDisposition =
        | Loaded
        | LoadFailed
        | ProjectSkipped
        | Unsupported

    type ProjectEvidence = {
        Path: string
        ProjectClass: string
        TargetFrameworks: string list
        Disposition: ProjectDisposition
        Reason: string
    }

    type SourceDisposition =
        | Analyzed
        | CompilerIncomplete
        | GeneratedExcluded
        | PolicyExcluded

    type SourceEvidence = {
        Path: string
        Disposition: SourceDisposition
        Reason: string
    }

    type ToolchainEvidence = {
        SdkVersion: string
        RuntimeVersion: string
        FSharpCompilerServiceVersion: string
    }

    type TestEvidence = {
        Id: string
        Project: string
        Status: TestStatus
        Passed: int
        Failed: int
        Skipped: int
    }

    type FindingEvidence = {
        RuleId: string
        Path: string
        Line: int
        Column: int
        Message: string
        Fingerprint: string
    }

    type RuleEvidence = {
        RuleId: string
        Status: string
        EvidenceAvailable: bool
        FindingCount: int
    }

    type EvidenceFacts = {
        PolicyErrors: string list
        EvidenceErrors: string list
        ToolFailures: string list
        MissingEvidence: string list
        Toolchain: ToolchainEvidence
        Projects: ProjectEvidence list
        Sources: SourceEvidence list
        RequiredTests: TestEvidence list
        Rules: RuleEvidence list
        Findings: FindingEvidence list
    }

    type AuthorityDecision = {
        Outcome: AssayVerdict
        Authoritative: bool
        Reasons: (string * string) list
    }

    type PolicyLoadResult =
        | PolicyLoaded of PolicyLock * string * string
        | PolicyUnavailable of string
        | PolicyInvalid of string * string

    let unapprovedPolicy = {
        policySchemaVersion = PolicySchemaVersion
        evidenceSchemaVersion = EvidenceSchemaVersion
        authorityContractVersion = ContractVersion
        shapeContractVersion = "not-established"
        toolVersion = ProductIdentity.Version
        enabledProfiles = [| "core" |]
        approvedBlockingRules = [||]
        advisoryRules = [||]
        experimentalRules = [||]
        requiredProjectClasses = [||]
        requiredTargetFrameworks = [||]
        requiredTests = [||]
        baseline = { identity = "none"; approvedFindings = [||] }
        exceptions = [||]
    }

    let private jsonOptions () =
        let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        options.UnmappedMemberHandling <- JsonUnmappedMemberHandling.Disallow
        options

    let private sha256Bytes (bytes: byte[]) =
        bytes
        |> SHA256.HashData
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let sha256Text (value: string) =
        value |> Encoding.UTF8.GetBytes |> sha256Bytes

    let private normalizePolicy policy =
        {
            policy with
                enabledProfiles = policy.enabledProfiles |> Array.sort
                approvedBlockingRules = policy.approvedBlockingRules |> Array.sort
                advisoryRules = policy.advisoryRules |> Array.sort
                experimentalRules = policy.experimentalRules |> Array.sort
                requiredProjectClasses = policy.requiredProjectClasses |> Array.sort
                requiredTargetFrameworks = policy.requiredTargetFrameworks |> Array.sort
                requiredTests = policy.requiredTests |> Array.sortBy (fun test -> test.id, test.project)
                baseline = { policy.baseline with approvedFindings = policy.baseline.approvedFindings |> Array.sort }
                exceptions = policy.exceptions |> Array.sortBy (fun item -> item.id)
        }

    let private duplicates values =
        values |> Seq.countBy id |> Seq.filter (fun (_, count) -> count > 1) |> Seq.map fst |> Seq.toList

    let private validatePolicy policy =
        [
            if policy.policySchemaVersion <> PolicySchemaVersion then
                $"unsupported policy schema '{policy.policySchemaVersion}'"
            if policy.evidenceSchemaVersion <> EvidenceSchemaVersion then
                $"unsupported evidence schema '{policy.evidenceSchemaVersion}'"
            if policy.authorityContractVersion <> ContractVersion then
                $"unsupported authority contract '{policy.authorityContractVersion}'"
            if policy.toolVersion <> ProductIdentity.Version then
                $"policy tool version '{policy.toolVersion}' does not match '{ProductIdentity.Version}'"
            if policy.shapeContractVersion <> "not-established" then
                "Shape contract is not established in M2"
            if not (Array.isEmpty policy.approvedBlockingRules) then
                "blocking rules require Human Gate C approval; none are approved in M2"
            if not (Array.isEmpty policy.advisoryRules) then
                "advisory rule admission requires Human Gate C approval; none are approved in M2"
            if Array.isEmpty policy.enabledProfiles then
                "at least one profile must be enabled"
            if Array.isEmpty policy.requiredProjectClasses then
                "at least one required project class must be locked"
            if Array.isEmpty policy.requiredTargetFrameworks then
                "at least one required target framework must be locked"
            if Array.isEmpty policy.requiredTests then
                "at least one required test must be locked"
            if String.IsNullOrWhiteSpace(policy.baseline.identity) then
                "baseline identity must not be blank"
            for name, values in [
                "enabled profiles", policy.enabledProfiles
                "blocking rules", policy.approvedBlockingRules
                "advisory rules", policy.advisoryRules
                "experimental rules", policy.experimentalRules
                "project classes", policy.requiredProjectClasses
                "target frameworks", policy.requiredTargetFrameworks
                "baseline findings", policy.baseline.approvedFindings
            ] do
                let repeated = duplicates values
                if not repeated.IsEmpty then
                    sprintf "%s contain duplicates: %s" name (String.concat "," repeated)
            let ruleSets = [ policy.approvedBlockingRules; policy.advisoryRules; policy.experimentalRules ]
            let ruleConflicts = ruleSets |> Array.concat |> duplicates
            if not ruleConflicts.IsEmpty then
                sprintf "rule authority classes overlap: %s" (String.concat "," ruleConflicts)
            let duplicateTestIds = policy.requiredTests |> Seq.map _.id |> duplicates
            if not duplicateTestIds.IsEmpty then
                sprintf "required test IDs contain duplicates: %s" (String.concat "," duplicateTestIds)
            let duplicateTestProjects = policy.requiredTests |> Seq.map _.project |> duplicates
            if not duplicateTestProjects.IsEmpty then
                sprintf "required test projects contain duplicates: %s" (String.concat "," duplicateTestProjects)
            let duplicateExceptionIds = policy.exceptions |> Seq.map _.id |> duplicates
            if not duplicateExceptionIds.IsEmpty then
                sprintf "exception IDs contain duplicates: %s" (String.concat "," duplicateExceptionIds)
            for test in policy.requiredTests do
                if String.IsNullOrWhiteSpace(test.id) || String.IsNullOrWhiteSpace(test.project) || test.minimumPassed < 1 then
                    $"invalid required test '{test.id}'"
            for item in policy.exceptions do
                if String.IsNullOrWhiteSpace(item.id) || String.IsNullOrWhiteSpace(item.owner) || String.IsNullOrWhiteSpace(item.reason) || String.IsNullOrWhiteSpace(item.expiresOn) then
                    $"invalid exception '{item.id}'"
        ]

    let loadPolicy path =
        if not (File.Exists(path)) then
            PolicyUnavailable $"required policy lock not found: {Path.GetFileName(path)}"
        else
            let raw = File.ReadAllText(path)
            try
                let parsed = JsonSerializer.Deserialize<PolicyLock>(raw, jsonOptions ())
                if isNull (box parsed) then
                    PolicyInvalid(path, "policy JSON was null")
                else
                    match validatePolicy parsed with
                    | [] ->
                        let normalized = normalizePolicy parsed
                        let canonical = JsonSerializer.Serialize(normalized, jsonOptions ())
                        PolicyLoaded(normalized, sha256Text canonical, path)
                    | errors -> PolicyInvalid(path, String.concat "; " errors)
            with ex ->
                PolicyInvalid(path, ex.Message)

    let emptyFacts = {
        PolicyErrors = []
        EvidenceErrors = []
        ToolFailures = []
        MissingEvidence = []
        Toolchain = { SdkVersion = "unavailable"; RuntimeVersion = Environment.Version.ToString(); FSharpCompilerServiceVersion = "unavailable" }
        Projects = []
        Sources = []
        RequiredTests = []
        Rules = []
        Findings = []
    }

    let private validateFacts policy facts =
        [
            let duplicateProjects = facts.Projects |> Seq.map _.Path |> duplicates
            if not duplicateProjects.IsEmpty then
                "project evidence contains duplicate paths"
            let duplicateSources = facts.Sources |> Seq.map _.Path |> duplicates
            if not duplicateSources.IsEmpty then
                "source evidence contains duplicate paths"
            for project in facts.Projects do
                if String.IsNullOrWhiteSpace(project.Path) || String.IsNullOrWhiteSpace(project.ProjectClass) then
                    "project evidence has a blank path or class"
                if project.Disposition = ProjectDisposition.Loaded && project.TargetFrameworks.IsEmpty then
                    $"loaded project {project.Path} has no target framework evidence"
                if project.Disposition = ProjectDisposition.Loaded && project.TargetFrameworks |> List.exists (fun framework -> not (Array.contains framework policy.requiredTargetFrameworks)) then
                    $"loaded project {project.Path} contains a target framework outside policy"
                if project.Disposition <> ProjectDisposition.Loaded && String.IsNullOrWhiteSpace(project.Reason) then
                    $"non-loaded project {project.Path} has no disposition reason"
            for source in facts.Sources do
                if String.IsNullOrWhiteSpace(source.Path) then
                    "source evidence has a blank path"
                if source.Disposition <> SourceDisposition.Analyzed && String.IsNullOrWhiteSpace(source.Reason) then
                    $"non-analyzed source {source.Path} has no disposition reason"
            let duplicateRules = facts.Rules |> List.countBy _.RuleId |> List.filter (fun (_, count) -> count > 1)
            if not duplicateRules.IsEmpty then
                "rule outcomes contain duplicate rule IDs"
            let duplicateTests = facts.RequiredTests |> Seq.map _.Id |> duplicates
            if not duplicateTests.IsEmpty then
                "test evidence contains duplicate IDs"
            let requestedRules = Array.concat [| policy.approvedBlockingRules; policy.advisoryRules; policy.experimentalRules |] |> Set.ofArray
            for rule in facts.Rules do
                if facts.PolicyErrors.IsEmpty && not (Set.contains rule.RuleId requestedRules) then
                    $"unknown or unrequested rule outcome '{rule.RuleId}'"
                if not (Set.contains rule.Status (set [ "completed"; "incomplete"; "unavailable" ])) then
                    $"rule {rule.RuleId} has invalid status '{rule.Status}'"
                if (rule.Status = "completed") <> rule.EvidenceAvailable then
                    $"rule {rule.RuleId} status and evidence availability contradict"
                if rule.FindingCount < 0 then
                    $"rule {rule.RuleId} has a negative finding count"
                let actual = facts.Findings |> List.filter (fun finding -> finding.RuleId = rule.RuleId) |> List.length
                if rule.FindingCount <> actual then
                    $"rule {rule.RuleId} declares {rule.FindingCount} findings but evidence contains {actual}"
            for finding in facts.Findings do
                if facts.PolicyErrors.IsEmpty && not (Set.contains finding.RuleId requestedRules) then
                    $"finding uses unknown or unrequested rule '{finding.RuleId}'"
                if finding.Line < 1 || finding.Column < 0 || String.IsNullOrWhiteSpace(finding.Path) || String.IsNullOrWhiteSpace(finding.Message) then
                    $"finding for {finding.RuleId} has invalid source evidence"
            for test in facts.RequiredTests do
                if test.Passed < 0 || test.Failed < 0 || test.Skipped < 0 then
                    $"test {test.Id} has a negative count"
                match test.Status with
                | TestStatus.Passed when test.Passed < 1 || test.Failed <> 0 || test.Skipped <> 0 ->
                    $"test {test.Id} passed status contradicts counts"
                | TestStatus.Failed when test.Failed < 1 ->
                    $"test {test.Id} failed status requires a nonzero failure count"
                | TestStatus.NotRun when test.Passed <> 0 || test.Failed <> 0 || test.Skipped <> 0 ->
                    $"test {test.Id} notRun status requires zero counts"
                | TestStatus.Skipped when test.Passed <> 0 || test.Failed <> 0 || test.Skipped < 1 ->
                    $"test {test.Id} skipped status contradicts counts"
                | _ -> ()
        ]

    let decide policy facts =
        let invalidEvidence = facts.EvidenceErrors @ validateFacts policy facts
        let requiredTestFailures = facts.RequiredTests |> List.filter (fun test -> test.Status = TestStatus.Failed)
        let requiredTestsNotRun = facts.RequiredTests |> List.filter (fun test -> test.Status = TestStatus.NotRun)
        let requiredTestsSkipped = facts.RequiredTests |> List.filter (fun test -> test.Status = TestStatus.Skipped)
        let approvedBlocking = Set.ofArray policy.approvedBlockingRules
        let blockingFindings = facts.Findings |> List.filter (fun finding -> approvedBlocking.Contains finding.RuleId)
        let loadedProjects = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.Loaded)
        let failedProjects = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.LoadFailed)
        let skippedProjects = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.ProjectSkipped)
        let unsupportedProjects = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.Unsupported)
        let analyzedSources = facts.Sources |> List.filter (fun source -> source.Disposition = SourceDisposition.Analyzed)
        let compilerIncompleteSources = facts.Sources |> List.filter (fun source -> source.Disposition = SourceDisposition.CompilerIncomplete)
        let eligibleSources = facts.Sources |> List.filter (fun source -> source.Disposition = SourceDisposition.Analyzed || source.Disposition = SourceDisposition.CompilerIncomplete)
        let reportedRules = facts.Rules |> List.map _.RuleId |> Set.ofList

        let incompleteness = [
            if not (Array.isEmpty policy.approvedBlockingRules) then
                yield "gate-c-approval-missing", "blocking rules require Human Gate C approval; none are approved in M2"
            yield! facts.PolicyErrors |> List.map (fun value -> "policy-invalid", value)
            yield! facts.MissingEvidence |> List.map (fun value -> "evidence-missing", value)
            if String.IsNullOrWhiteSpace(facts.Toolchain.SdkVersion) || facts.Toolchain.SdkVersion = "unavailable" then
                yield "sdk-identity-missing", "exact .NET SDK identity is unavailable"
            if String.IsNullOrWhiteSpace(facts.Toolchain.RuntimeVersion) || String.IsNullOrWhiteSpace(facts.Toolchain.FSharpCompilerServiceVersion) || facts.Toolchain.FSharpCompilerServiceVersion = "unavailable" then
                yield "compiler-identity-missing", "runtime or F# compiler service identity is unavailable"
            if facts.Projects.IsEmpty then yield "projects-zero-discovered", "no eligible F# projects were discovered"
            if loadedProjects.IsEmpty then yield "projects-zero-loaded", "no eligible F# projects were loaded"
            if not failedProjects.IsEmpty then yield "project-load-failed", $"{failedProjects.Length} project(s) failed to load"
            if not skippedProjects.IsEmpty then yield "project-skipped", $"{skippedProjects.Length} project(s) were skipped"
            if not unsupportedProjects.IsEmpty then yield "project-unsupported", $"{unsupportedProjects.Length} project(s) or framework(s) were unsupported"
            for requiredClass in policy.requiredProjectClasses do
                let matching = facts.Projects |> List.filter (fun project -> project.ProjectClass = requiredClass)
                if matching.IsEmpty then
                    yield "required-project-class-missing", $"required project class '{requiredClass}' was not discovered"
                elif matching |> List.forall (fun project -> project.Disposition <> ProjectDisposition.Loaded) then
                    yield "required-project-class-unavailable", $"required project class '{requiredClass}' was not loaded"
            for requiredFramework in policy.requiredTargetFrameworks do
                if loadedProjects |> List.forall (fun project -> not (List.contains requiredFramework project.TargetFrameworks)) then
                    yield "required-framework-missing", $"required target framework '{requiredFramework}' was not loaded"
            if eligibleSources.IsEmpty then yield "files-zero-eligible", "no eligible F# source files were discovered"
            if analyzedSources.IsEmpty then yield "files-zero-analyzed", "no eligible F# source files were analyzed"
            if not compilerIncompleteSources.IsEmpty then yield "compiler-workspace-incomplete", $"{compilerIncompleteSources.Length} file(s) lacked complete compiler/workspace evidence"
            for test in requiredTestsNotRun do
                yield "required-test-not-run", $"required test '{test.Id}' was not run"
            for test in requiredTestsSkipped do
                yield "required-test-skipped", $"required test '{test.Id}' was skipped ({test.Skipped})"
            for requirement in policy.requiredTests do
                match facts.RequiredTests |> List.tryFind (fun test -> test.Id = requirement.id && test.Project.Replace('\\', '/').EndsWith(requirement.project.Replace('\\', '/'), StringComparison.Ordinal)) with
                | None -> yield "required-test-evidence-missing", $"required test '{requirement.id}' has no evidence"
                | Some test when test.Status = TestStatus.Passed && test.Passed < requirement.minimumPassed ->
                    yield "required-test-count-insufficient", $"required test '{requirement.id}' passed {test.Passed}, below minimum {requirement.minimumPassed}"
                | _ -> ()
            for missingRule in Set.difference approvedBlocking reportedRules do
                yield "required-rule-outcome-missing", $"requested rule '{missingRule}' has no outcome"
            for rule in facts.Rules do
                if approvedBlocking.Contains(rule.RuleId) && rule.Status = "incomplete" then
                    yield "required-rule-incomplete", $"requested rule '{rule.RuleId}' had incomplete project evidence"
                elif approvedBlocking.Contains(rule.RuleId) && rule.Status = "unavailable" then
                    yield "required-rule-unavailable", $"requested rule '{rule.RuleId}' is unavailable in the current implementation"
        ]

        let failures = [
            for test in requiredTestFailures do
                yield "required-test-failed", $"required test '{test.Id}' failed ({test.Failed})"
            for finding in blockingFindings do
                yield "approved-blocking-finding", $"{finding.RuleId} at {finding.Path}:{finding.Line}"
        ]

        let toolReasons = [
            yield! facts.ToolFailures |> List.map (fun value -> "tool-failure", value)
            yield! invalidEvidence |> List.map (fun value -> "invalid-evidence", value)
        ]

        let outcome =
            if not toolReasons.IsEmpty then ToolFailure
            elif not failures.IsEmpty then Fail
            elif not incompleteness.IsEmpty then Inconclusive
            else Pass

        {
            Outcome = outcome
            Authoritative = outcome <> ToolFailure && incompleteness.IsEmpty && invalidEvidence.IsEmpty
            Reasons = (toolReasons @ failures @ incompleteness) |> List.distinct |> List.sort
        }

    let private tryProcess workingDirectory executable arguments =
        try
            let info = ProcessStartInfo(executable)
            info.WorkingDirectory <- workingDirectory
            info.RedirectStandardOutput <- true
            info.RedirectStandardError <- true
            info.UseShellExecute <- false
            for argument in arguments do info.ArgumentList.Add(argument)
            use gitProcess = Process.Start(info)
            let output = gitProcess.StandardOutput.ReadToEnd().Trim()
            gitProcess.WaitForExit()
            if gitProcess.ExitCode = 0 then Some output else None
        with _ -> None

    let private tryGit workingDirectory arguments =
        tryProcess workingDirectory "git" arguments

    let private sdkVersion workingDirectory =
        let host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
        let executable = if String.IsNullOrWhiteSpace(host) then "dotnet" else host
        tryProcess workingDirectory executable [ "--version" ]
        |> Option.defaultValue "unavailable"

    let currentToolchain workingDirectory = {
        SdkVersion = sdkVersion workingDirectory
        RuntimeVersion = Environment.Version.ToString()
        FSharpCompilerServiceVersion = typeof<FSharp.Compiler.CodeAnalysis.FSharpChecker>.Assembly.GetName().Version.ToString()
    }

    type CandidateIdentity = {
        kind: string
        commitSha: string
        approvedHeadSha: string
        treeSha: string
        dirty: bool
        syntheticMergeSha: string
        packageSha256: string
        repositoryRelativeTarget: string
    }

    type PolicyIdentity = {
        status: string
        path: string
        sha256: string
        schemaVersion: string
        authorityContractVersion: string
        shapeContractVersion: string
        error: string
    }

    type ToolchainIdentity = {
        sdkVersion: string
        runtimeVersion: string
        fsharpCompilerServiceVersion: string
    }

    type ProjectReceipt = {
        path: string
        projectClass: string
        targetFrameworks: string[]
        status: string
        reason: string
    }

    type SourceReceipt = {
        path: string
        disposition: string
        reason: string
    }

    type TestReceipt = {
        id: string
        project: string
        status: string
        passed: int
        failed: int
        skipped: int
    }

    type RuleReceipt = {
        ruleId: string
        authorityClass: string
        status: string
        evidenceAvailable: bool
        findingCount: int
    }

    type FindingReceipt = {
        ruleId: string
        path: string
        line: int
        column: int
        message: string
        fingerprint: string
        authorityClass: string
    }

    type ReasonReceipt = {
        code: string
        detail: string
    }

    type CountReceipt = {
        projectsDiscovered: int
        projectsLoaded: int
        projectsFailed: int
        projectsSkipped: int
        projectsUnsupported: int
        eligibleFiles: int
        analyzedFiles: int
        compilerIncompleteFiles: int
    }

    type AuthorityReceipt = {
        schemaVersion: string
        tool: string
        toolVersion: string
        candidate: CandidateIdentity
        policy: PolicyIdentity
        toolchain: ToolchainIdentity
        outcome: string
        authoritative: bool
        reasons: ReasonReceipt[]
        counts: CountReceipt
        projects: ProjectReceipt[]
        sources: SourceReceipt[]
        tests: TestReceipt[]
        rules: RuleReceipt[]
        findings: FindingReceipt[]
        baselineIdentity: string
        suppressions: string[]
        exceptions: PolicyException[]
        missingEvidence: string[]
        toolFailures: string[]
    }

    let private verdictName = function
        | Pass -> "Pass"
        | Fail -> "Fail"
        | Inconclusive -> "Inconclusive"
        | ToolFailure -> "ToolFailure"

    let private testStatusName = function
        | TestStatus.Passed -> "passed"
        | TestStatus.Failed -> "failed"
        | TestStatus.NotRun -> "notRun"
        | TestStatus.Skipped -> "skipped"

    let private projectStatusName = function
        | ProjectDisposition.Loaded -> "loaded"
        | ProjectDisposition.LoadFailed -> "failed"
        | ProjectDisposition.ProjectSkipped -> "skipped"
        | ProjectDisposition.Unsupported -> "unsupported"

    let private sourceDispositionName = function
        | SourceDisposition.Analyzed -> "analyzed"
        | SourceDisposition.CompilerIncomplete -> "compiler-incomplete"
        | SourceDisposition.GeneratedExcluded -> "generated-excluded"
        | SourceDisposition.PolicyExcluded -> "policy-excluded"

    let private authorityClass policy ruleId =
        if Array.contains ruleId policy.approvedBlockingRules then "blocking"
        elif Array.contains ruleId policy.advisoryRules then "advisory"
        else "experimental"

    let repositoryRelativePath repositoryRoot path =
        if path = "Architecture" then "Architecture"
        else
            let root = Path.GetFullPath(repositoryRoot)
            let fullPath = if Path.IsPathRooted(path) then Path.GetFullPath(path) else Path.GetFullPath(Path.Combine(root, path))
            let relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/')
            if relative.StartsWith("../", StringComparison.Ordinal) then "external/" + Path.GetFileName(path)
            else relative

    let findingFingerprint ruleId path line column message =
        sha256Text $"{ruleId}\n{path}\n{line}\n{column}\n{message}"

    let private isHex length (value: string) =
        not (isNull value) && value.Length = length && value |> Seq.forall Uri.IsHexDigit

    let private candidateAuthorityEvidence (candidate: CandidateIdentity) =
        let missing = [
            if candidate.dirty then
                "candidate worktree contains tracked or untracked changes"
            if candidate.commitSha = "unavailable" then
                "candidate commit identity is unavailable"
            if candidate.treeSha = "unavailable" then
                "candidate tree identity is unavailable"
            if candidate.approvedHeadSha = "unavailable" then
                "approved candidate head identity is unavailable"
        ]
        let errors = [
            if not (Set.contains candidate.kind (set [ "commit"; "dirty-worktree"; "synthetic-merge"; "package"; "unversioned" ])) then
                "candidate kind is invalid"
            if candidate.commitSha <> "unavailable" && not (isHex 40 candidate.commitSha) then
                "actual HEAD is not a 40-character Git object ID"
            if candidate.treeSha <> "unavailable" && not (isHex 40 candidate.treeSha) then
                "actual tree is not a 40-character Git object ID"
            if candidate.approvedHeadSha <> "unavailable" && not (isHex 40 candidate.approvedHeadSha) then
                "approved head identity is malformed"
            if candidate.syntheticMergeSha <> "not-applicable" && not (isHex 40 candidate.syntheticMergeSha) then
                "synthetic merge identity is malformed"
            if candidate.packageSha256 <> "not-applicable" && not (isHex 64 candidate.packageSha256) then
                "package identity is not a SHA-256 digest"
            if candidate.kind = "dirty-worktree" && not candidate.dirty then
                "dirty-worktree candidate kind contradicts the clean worktree flag"
            if candidate.kind <> "dirty-worktree" && candidate.kind <> "unversioned" && candidate.dirty then
                "clean candidate kind contradicts the dirty worktree flag"
            if candidate.kind = "unversioned" && (candidate.commitSha <> "unavailable" || candidate.treeSha <> "unavailable") then
                "unversioned candidate kind contradicts available Git identities"
            if (candidate.kind = "commit" || candidate.kind = "dirty-worktree")
               && candidate.commitSha <> "unavailable"
               && candidate.approvedHeadSha <> "unavailable"
               && candidate.commitSha <> candidate.approvedHeadSha then
                "approved head identity does not match the analyzed commit"
            if candidate.kind = "synthetic-merge" && candidate.syntheticMergeSha <> candidate.commitSha then
                "synthetic merge identity does not match the analyzed HEAD"
            if candidate.kind = "commit" && candidate.syntheticMergeSha <> "not-applicable" then
                "commit candidate unexpectedly carries a synthetic merge identity"
            if candidate.kind = "package" && candidate.packageSha256 = "not-applicable" then
                "package candidate lacks package identity"
        ]
        missing, errors

    let createReceipt repositoryRoot candidate policy policyPath policyHash facts =
        let candidateMissing, candidateErrors = candidateAuthorityEvidence candidate
        let decisionFacts = {
            facts with
                MissingEvidence = facts.MissingEvidence @ candidateMissing
                EvidenceErrors = facts.EvidenceErrors @ candidateErrors
        }
        let decision = decide policy decisionFacts
        let relative value = repositoryRelativePath repositoryRoot value
        let normalizedFindings =
            facts.Findings
            |> List.map (fun finding ->
                let path = relative finding.Path
                {
                    ruleId = finding.RuleId
                    path = path
                    line = finding.Line
                    column = finding.Column
                    message = finding.Message
                    fingerprint = findingFingerprint finding.RuleId path finding.Line finding.Column finding.Message
                    authorityClass = authorityClass policy finding.RuleId
                })
            |> List.sortBy (fun finding -> finding.ruleId, finding.path, finding.line, finding.column, finding.message)
            |> List.toArray

        let ruleCounts = normalizedFindings |> Array.countBy _.ruleId |> Map.ofArray
        let normalizedRules =
            facts.Rules
            |> List.map (fun rule ->
                {
                    ruleId = rule.RuleId
                    authorityClass = authorityClass policy rule.RuleId
                    status = rule.Status
                    evidenceAvailable = rule.EvidenceAvailable
                    findingCount = Map.tryFind rule.RuleId ruleCounts |> Option.defaultValue 0
                })
            |> List.sortBy _.ruleId
            |> List.toArray

        let projectReceipts =
            facts.Projects
            |> List.map (fun project ->
                {
                    path = relative project.Path
                    projectClass = project.ProjectClass
                    targetFrameworks = project.TargetFrameworks |> List.sort |> List.toArray
                    status = projectStatusName project.Disposition
                    reason = project.Reason
                })
            |> List.sortBy (fun project -> project.path, project.status)
            |> List.toArray

        let sourceReceipts =
            facts.Sources
            |> List.map (fun source ->
                { path = relative source.Path; disposition = sourceDispositionName source.Disposition; reason = source.Reason })
            |> List.sortBy (fun source -> source.path, source.disposition)
            |> List.toArray

        let testReceipts =
            facts.RequiredTests
            |> List.map (fun test ->
                {
                    id = test.Id
                    project = relative test.Project
                    status = testStatusName test.Status
                    passed = test.Passed
                    failed = test.Failed
                    skipped = test.Skipped
                })
            |> List.sortBy (fun test -> test.id, test.project)
            |> List.toArray

        {
            schemaVersion = EvidenceSchemaVersion
            tool = "FsAssay"
            toolVersion = ProductIdentity.Version
            candidate = candidate
            policy = {
                status = if isHex 64 policyHash then "loaded" else policyHash
                path = relative policyPath
                sha256 = policyHash
                schemaVersion = if isHex 64 policyHash then policy.policySchemaVersion else "unavailable"
                authorityContractVersion = if isHex 64 policyHash then policy.authorityContractVersion else "unavailable"
                shapeContractVersion = if isHex 64 policyHash then policy.shapeContractVersion else "unavailable"
                error = if isHex 64 policyHash then "" else String.concat "; " facts.PolicyErrors
            }
            toolchain = {
                sdkVersion = facts.Toolchain.SdkVersion
                runtimeVersion = facts.Toolchain.RuntimeVersion
                fsharpCompilerServiceVersion = facts.Toolchain.FSharpCompilerServiceVersion
            }
            outcome = verdictName decision.Outcome
            authoritative = decision.Authoritative
            reasons = decision.Reasons |> List.map (fun (code, detail) -> { code = code; detail = detail }) |> List.toArray
            counts = {
                projectsDiscovered = facts.Projects.Length
                projectsLoaded = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.Loaded) |> List.length
                projectsFailed = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.LoadFailed) |> List.length
                projectsSkipped = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.ProjectSkipped) |> List.length
                projectsUnsupported = facts.Projects |> List.filter (fun project -> project.Disposition = ProjectDisposition.Unsupported) |> List.length
                eligibleFiles = facts.Sources |> List.filter (fun source -> source.Disposition = SourceDisposition.Analyzed || source.Disposition = SourceDisposition.CompilerIncomplete) |> List.length
                analyzedFiles = facts.Sources |> List.filter (fun source -> source.Disposition = SourceDisposition.Analyzed) |> List.length
                compilerIncompleteFiles = facts.Sources |> List.filter (fun source -> source.Disposition = SourceDisposition.CompilerIncomplete) |> List.length
            }
            projects = projectReceipts
            sources = sourceReceipts
            tests = testReceipts
            rules = normalizedRules
            findings = normalizedFindings
            baselineIdentity = policy.baseline.identity
            suppressions = policy.baseline.approvedFindings |> Array.distinct |> Array.sort
            exceptions = policy.exceptions |> Array.sortBy _.id
            missingEvidence = decisionFacts.MissingEvidence |> List.distinct |> List.sort |> List.toArray
            toolFailures = decisionFacts.ToolFailures |> List.distinct |> List.sort |> List.toArray
        }

    let validateReceipt (receipt: AuthorityReceipt) =
        try
            let relativePath value =
                not (String.IsNullOrWhiteSpace(value))
                && not (Path.IsPathRooted(value))
                && not (value.Replace('\\', '/').StartsWith("../", StringComparison.Ordinal))
            let projectCount status = receipt.projects |> Array.filter (fun project -> project.status = status) |> Array.length
            let sourceCount status = receipt.sources |> Array.filter (fun source -> source.disposition = status) |> Array.length
            [
                if receipt.schemaVersion <> EvidenceSchemaVersion then "receipt schema version is unsupported"
                if receipt.tool <> "FsAssay" || receipt.toolVersion <> ProductIdentity.Version then "receipt tool identity is invalid"
                if not (Set.contains receipt.outcome (set [ "Pass"; "Fail"; "Inconclusive"; "ToolFailure" ])) then "receipt outcome is invalid"
                if receipt.authoritative && (receipt.outcome = "Inconclusive" || receipt.outcome = "ToolFailure") then "incomplete or failed tool evidence cannot be authoritative"
                if receipt.outcome <> "Pass" && Array.isEmpty receipt.reasons then "non-Pass receipt requires at least one reason"
                if not (isHex 40 receipt.candidate.commitSha || receipt.candidate.commitSha = "unavailable") then "candidate commit identity is invalid"
                if not (isHex 40 receipt.candidate.approvedHeadSha || receipt.candidate.approvedHeadSha = "unavailable") then "approved head identity is invalid"
                if not (isHex 40 receipt.candidate.treeSha || receipt.candidate.treeSha = "unavailable") then "candidate tree identity is invalid"
                if not (isHex 40 receipt.candidate.syntheticMergeSha || receipt.candidate.syntheticMergeSha = "not-applicable") then "synthetic merge identity is invalid"
                if not (isHex 64 receipt.candidate.packageSha256 || receipt.candidate.packageSha256 = "not-applicable") then "package identity is invalid"
                if receipt.authoritative && receipt.candidate.dirty then "dirty candidate cannot be authoritative"
                if receipt.outcome = "Pass" && receipt.candidate.dirty then "dirty candidate cannot pass"
                if receipt.authoritative && (receipt.candidate.commitSha = "unavailable" || receipt.candidate.treeSha = "unavailable" || receipt.candidate.approvedHeadSha = "unavailable") then "unversioned or incomplete candidate cannot be authoritative"
                if receipt.outcome = "Pass" && (receipt.candidate.commitSha = "unavailable" || receipt.candidate.treeSha = "unavailable" || receipt.candidate.approvedHeadSha = "unavailable") then "unversioned or incomplete candidate cannot pass"
                if receipt.candidate.kind = "synthetic-merge" && receipt.candidate.syntheticMergeSha <> receipt.candidate.commitSha then "synthetic merge must equal analyzed HEAD"
                if receipt.candidate.kind = "package" && receipt.candidate.packageSha256 = "not-applicable" then "package candidate lacks package identity"
                if (receipt.candidate.kind = "commit" || receipt.candidate.kind = "dirty-worktree") && receipt.candidate.commitSha <> "unavailable" && receipt.candidate.approvedHeadSha <> "unavailable" && receipt.candidate.commitSha <> receipt.candidate.approvedHeadSha then "approved head does not match analyzed commit"
                if receipt.candidate.kind = "dirty-worktree" && not receipt.candidate.dirty then "dirty-worktree kind contradicts dirty flag"
                if receipt.candidate.kind <> "dirty-worktree" && receipt.candidate.kind <> "unversioned" && receipt.candidate.dirty then "clean candidate kind contradicts dirty flag"
                if not (relativePath receipt.candidate.repositoryRelativeTarget) then "candidate target is not repository-relative"
                if not (relativePath receipt.policy.path) then "policy path is not repository-relative"
                if receipt.policy.status = "loaded" && not (isHex 64 receipt.policy.sha256) then "loaded policy lacks a SHA-256 identity"
                if receipt.policy.status <> "loaded" && String.IsNullOrWhiteSpace(receipt.policy.error) then "unavailable or invalid policy lacks an error"
                if receipt.policy.status = "loaded" && (receipt.policy.schemaVersion <> PolicySchemaVersion || receipt.policy.authorityContractVersion <> ContractVersion || receipt.policy.shapeContractVersion <> "not-established") then "loaded policy versions are incompatible"
                if String.IsNullOrWhiteSpace(receipt.toolchain.sdkVersion) || receipt.toolchain.sdkVersion = "unavailable" then "SDK identity is unavailable"
                if String.IsNullOrWhiteSpace(receipt.toolchain.runtimeVersion) || String.IsNullOrWhiteSpace(receipt.toolchain.fsharpCompilerServiceVersion) then "compiler toolchain identity is unavailable"
                if receipt.counts.projectsDiscovered <> receipt.projects.Length then "project discovery count does not reconcile"
                if receipt.counts.projectsLoaded <> projectCount "loaded" then "loaded project count does not reconcile"
                if receipt.counts.projectsFailed <> projectCount "failed" then "failed project count does not reconcile"
                if receipt.counts.projectsSkipped <> projectCount "skipped" then "skipped project count does not reconcile"
                if receipt.counts.projectsUnsupported <> projectCount "unsupported" then "unsupported project count does not reconcile"
                if receipt.counts.eligibleFiles <> sourceCount "analyzed" + sourceCount "compiler-incomplete" then "eligible source count does not reconcile"
                if receipt.counts.analyzedFiles <> sourceCount "analyzed" then "analyzed source count does not reconcile"
                if receipt.counts.compilerIncompleteFiles <> sourceCount "compiler-incomplete" then "compiler-incomplete source count does not reconcile"
                for project in receipt.projects do
                    if not (relativePath project.path) then $"project path is not repository-relative: {project.path}"
                    if not (Set.contains project.status (set [ "loaded"; "failed"; "skipped"; "unsupported" ])) then $"project status is invalid: {project.status}"
                    if String.IsNullOrWhiteSpace(project.projectClass) || Array.isEmpty project.targetFrameworks then $"project identity is incomplete: {project.path}"
                for source in receipt.sources do
                    if not (relativePath source.path) then $"source path is not repository-relative: {source.path}"
                    if not (Set.contains source.disposition (set [ "analyzed"; "compiler-incomplete"; "generated-excluded"; "policy-excluded" ])) then $"source disposition is invalid: {source.disposition}"
                for test in receipt.tests do
                    if String.IsNullOrWhiteSpace(test.id) || not (relativePath test.project) then "test identity is invalid"
                    match test.status with
                    | "passed" when test.passed < 1 || test.failed <> 0 || test.skipped <> 0 -> "passed test counts contradict status"
                    | "failed" when test.failed < 1 -> "failed test requires nonzero failures"
                    | "notRun" when test.passed <> 0 || test.failed <> 0 || test.skipped <> 0 -> "notRun test requires zero counts"
                    | "skipped" when test.passed <> 0 || test.failed <> 0 || test.skipped < 1 -> "skipped test counts contradict status"
                    | status when not (Set.contains status (set [ "passed"; "failed"; "notRun"; "skipped" ])) -> $"test status is invalid: {status}"
                    | _ -> ()
                let duplicateRuleIds = receipt.rules |> Array.countBy _.ruleId |> Array.filter (fun (_, count) -> count > 1)
                if not (Array.isEmpty duplicateRuleIds) then "rule receipts contain duplicates"
                for rule in receipt.rules do
                    if not (Set.contains rule.status (set [ "completed"; "incomplete"; "unavailable" ])) then $"rule status is invalid: {rule.ruleId}"
                    if (rule.status = "completed") <> rule.evidenceAvailable then $"rule evidence status contradicts availability: {rule.ruleId}"
                    let actual = receipt.findings |> Array.filter (fun finding -> finding.ruleId = rule.ruleId) |> Array.length
                    if rule.findingCount <> actual then $"rule finding count does not reconcile: {rule.ruleId}"
                for finding in receipt.findings do
                    if not (relativePath finding.path) then $"finding path is not repository-relative: {finding.path}"
                    if finding.line < 1 || finding.column < 0 then $"finding location is invalid: {finding.ruleId}"
                    if not (isHex 64 finding.fingerprint) then $"finding fingerprint is invalid: {finding.ruleId}"
                    if finding.fingerprint <> findingFingerprint finding.ruleId finding.path finding.line finding.column finding.message then $"finding fingerprint does not reconcile: {finding.ruleId}"
                if receipt.reasons <> (receipt.reasons |> Array.sortBy (fun reason -> reason.code, reason.detail)) then "reason ordering is unstable"
                if receipt.projects <> (receipt.projects |> Array.sortBy (fun project -> project.path, project.status)) then "project ordering is unstable"
                if receipt.sources <> (receipt.sources |> Array.sortBy (fun source -> source.path, source.disposition)) then "source ordering is unstable"
                if receipt.rules <> (receipt.rules |> Array.sortBy _.ruleId) then "rule ordering is unstable"
                if receipt.findings <> (receipt.findings |> Array.sortBy (fun finding -> finding.ruleId, finding.path, finding.line, finding.column, finding.message)) then "finding ordering is unstable"
                for item in receipt.exceptions do
                    if String.IsNullOrWhiteSpace(item.id) || String.IsNullOrWhiteSpace(item.owner) || String.IsNullOrWhiteSpace(item.reason) || String.IsNullOrWhiteSpace(item.expiresOn) then "exception evidence is incomplete"
            ]
        with ex -> [ "receipt graph is incomplete: " + ex.Message ]

    let deserializeAndValidateReceipt (bytes: byte[]) =
        try
            let receipt = JsonSerializer.Deserialize<AuthorityReceipt>(bytes, jsonOptions ())
            if isNull (box receipt) then Error [ "receipt JSON was null" ]
            else
                match validateReceipt receipt with
                | [] -> Ok receipt
                | errors -> Error errors
        with ex -> Error [ ex.Message ]

    let candidateIdentityWithEnvironment repositoryRoot targetPath environmentSha eventName approvedEnvironmentSha explicitSyntheticSha packageSha =
        let commit = tryGit repositoryRoot [ "rev-parse"; "HEAD" ] |> Option.defaultValue "unavailable"
        let tree = tryGit repositoryRoot [ "rev-parse"; "HEAD^{tree}" ] |> Option.defaultValue "unavailable"
        let dirty =
            tryGit repositoryRoot [ "status"; "--porcelain"; "--untracked-files=all" ]
            |> Option.map (String.IsNullOrWhiteSpace >> not)
            |> Option.defaultValue true
        let isPullRequest = not (String.IsNullOrWhiteSpace(eventName)) && eventName.StartsWith("pull_request", StringComparison.Ordinal)
        let synthetic =
            if commit <> "unavailable" && not (String.IsNullOrWhiteSpace(explicitSyntheticSha)) then explicitSyntheticSha
            elif commit <> "unavailable" && isPullRequest && not (String.IsNullOrWhiteSpace(environmentSha)) then environmentSha
            elif String.IsNullOrWhiteSpace(environmentSha) || environmentSha = commit then "not-applicable"
            else environmentSha
        let approved =
            if commit <> "unavailable" && not (String.IsNullOrWhiteSpace(approvedEnvironmentSha)) then approvedEnvironmentSha
            elif commit <> "unavailable" && isPullRequest then "unavailable"
            else commit
        let packageSha = if String.IsNullOrWhiteSpace(packageSha) then "not-applicable" else packageSha
        let kind =
            if packageSha <> "not-applicable" then "package"
            elif commit = "unavailable" then "unversioned"
            elif synthetic <> "not-applicable" then "synthetic-merge"
            elif dirty then "dirty-worktree"
            else "commit"
        let relative =
            try Path.GetRelativePath(repositoryRoot, Path.GetFullPath(targetPath)).Replace('\\', '/')
            with _ -> "unavailable"
        let identity = {
            kind = kind
            commitSha = commit
            approvedHeadSha = approved
            treeSha = tree
            dirty = dirty
            syntheticMergeSha = synthetic
            packageSha256 = packageSha
            repositoryRelativeTarget = if String.IsNullOrWhiteSpace(relative) then "." else relative
        }
        let identityMissing, identityErrors = candidateAuthorityEvidence identity
        let pullRequestMissing, pullRequestErrors =
            if isPullRequest
               && kind = "synthetic-merge"
               && isHex 40 commit
               && isHex 40 approved
               && isHex 40 synthetic then
                match tryGit repositoryRoot [ "rev-parse"; $"{commit}^2" ] with
                | None -> [ "synthetic merge second-parent identity is unavailable" ], []
                | Some secondParent when secondParent <> approved ->
                    [], [ "approved head identity does not match the synthetic merge second parent" ]
                | Some _ -> [], []
            else [], []
        identity,
        (identityMissing @ pullRequestMissing),
        (identityErrors @ pullRequestErrors)

    let candidateIdentity repositoryRoot targetPath =
        candidateIdentityWithEnvironment
            repositoryRoot
            targetPath
            (Environment.GetEnvironmentVariable("GITHUB_SHA"))
            (Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME"))
            (Environment.GetEnvironmentVariable("FSASSAY_APPROVED_HEAD_SHA"))
            (Environment.GetEnvironmentVariable("FSASSAY_SYNTHETIC_MERGE_SHA"))
            (Environment.GetEnvironmentVariable("FSASSAY_PACKAGE_SHA256"))
