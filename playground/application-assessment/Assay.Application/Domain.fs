namespace FsAssay.Application

open System

[<CLIMutable>]
type Manifest = { schema: string; applicationId: string; project: string; assemblyName: string; sdk: string; files: string array; requirements: string; contract: string; minimumPassed: int; frozenTestFiles: string array; acceptanceTestFiles: string array }
[<CLIMutable>]
type Requirement = { id: string; description: string; testIds: string array; minimumPassed: int }
[<CLIMutable>]
type Requirements = { schema: string; requirements: Requirement array }
[<CLIMutable>]
type NamedTest = { id: string; requirementId: string; passed: bool; detail: string }
[<CLIMutable>]
type TestReport = { schema: string; tests: NamedTest array }
[<CLIMutable>]
type Binding = { path: string; sha256: string }
[<CLIMutable>]
type RegisteredProbe = { id: string; requirementId: string }

[<RequireQualifiedAccess>]
type ApplicationStatus = ReadyForReview | NeedsRepair of string list | ContextRequired of string list | ToolFailure of string list
[<RequireQualifiedAccess>]
type Advice = NoInvestigation | ContextRequired | BaselineRepair | ProbeRequested of string | BudgetExhausted | SurvivorReview

type Facts = { Missing: string list; ToolErrors: string list; Failures: string list; SourceCurrent: bool; EngineUsable: bool }
[<RequireQualifiedAccess>]
module Assessment =
    /// This result has no FsAssay verdict or authority constructor.
    let evaluate facts =
        if not facts.ToolErrors.IsEmpty then ApplicationStatus.ToolFailure facts.ToolErrors
        elif not facts.SourceCurrent then ApplicationStatus.ContextRequired [ "source-or-runtime-changed" ]
        elif not facts.Missing.IsEmpty then ApplicationStatus.ContextRequired (facts.Missing @ facts.Failures)
        elif not facts.Failures.IsEmpty then ApplicationStatus.NeedsRepair facts.Failures
        elif not facts.EngineUsable then ApplicationStatus.ContextRequired [ "engine-evidence-incomplete" ]
        else ApplicationStatus.ReadyForReview
    let describe = function
        | ApplicationStatus.ReadyForReview -> "ReadyForReview", [||]
        | ApplicationStatus.NeedsRepair reasons -> "NeedsRepair", List.toArray reasons
        | ApplicationStatus.ContextRequired reasons -> "ContextRequired", List.toArray reasons
        | ApplicationStatus.ToolFailure reasons -> "ToolFailure", List.toArray reasons
    let compatible (oldManifest: Manifest) (oldRequirements: Requirements) oldContract (manifest: Manifest) (requirements: Requirements) contract =
        [
            if oldManifest.project <> manifest.project || oldManifest.assemblyName <> manifest.assemblyName || oldManifest.sdk <> manifest.sdk || oldManifest.contract <> manifest.contract then "baseline-build-contract-changed"
            if Set.ofArray oldManifest.frozenTestFiles <> Set.ofArray manifest.frozenTestFiles || Set.ofArray oldManifest.acceptanceTestFiles <> Set.ofArray manifest.acceptanceTestFiles then "baseline-test-file-registry-changed"
            if oldManifest.applicationId <> manifest.applicationId then "baseline-application-changed"
            if oldContract <> contract then "baseline-contract-changed"
            if manifest.minimumPassed < oldManifest.minimumPassed then "baseline-minimum-reduced"
            for previous in oldRequirements.requirements do
                match requirements.requirements |> Array.tryFind (fun requirement -> requirement.id = previous.id) with
                | None -> "baseline-requirement-removed:" + previous.id
                | Some current when current.description <> previous.description -> "baseline-requirement-redefined:" + previous.id
                | Some current ->
                    if current.minimumPassed < previous.minimumPassed then "baseline-requirement-minimum-reduced:" + previous.id
                    if not (Set.isSubset (Set.ofArray previous.testIds) (Set.ofArray current.testIds)) then "baseline-test-selection-reduced:" + previous.id
        ]

[<RequireQualifiedAccess>]
module Assistance =
    let select status baselineReady snapshot expectedSnapshot budget (registry: RegisteredProbe list) failedRequirement proposedId =
        if snapshot <> expectedSnapshot then Advice.ContextRequired
        elif not baselineReady then Advice.BaselineRepair
        else
            match status with
            | ApplicationStatus.ContextRequired _ | ApplicationStatus.ToolFailure _ -> Advice.ContextRequired
            | ApplicationStatus.ReadyForReview -> Advice.NoInvestigation
            | ApplicationStatus.NeedsRepair _ ->
                match registry |> List.tryFind (fun probe -> probe.id = proposedId && probe.requirementId = failedRequirement) with
                | None -> Advice.SurvivorReview
                | Some _ when budget <= 0 -> Advice.BudgetExhausted
                | Some probe -> Advice.ProbeRequested probe.id
    let describe = function
        | Advice.NoInvestigation -> "NoInvestigation"
        | Advice.ContextRequired -> "ContextRequired"
        | Advice.BaselineRepair -> "BaselineRepair"
        | Advice.ProbeRequested id -> "ProbeRequested:" + id
        | Advice.BudgetExhausted -> "BudgetExhausted"
        | Advice.SurvivorReview -> "SurvivorReview"
