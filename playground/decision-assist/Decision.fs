module FsAssay.Playground.DecisionAssist

/// Executed test evidence, deliberately separate from FsAssay authority.
type TestExecution =
    | Passed of count: int
    | Failed of count: int
    | Invalid of reason: string
    | Unavailable of reason: string

type RegisteredProbe =
    { Id: string
      RequirementId: string }

type Observation =
    { Baseline: TestExecution
      Candidate: TestExecution option
      SnapshotMatches: bool
      RemainingBudget: int
      Probe: RegisteredProbe option }

/// Advice cannot construct a verification verdict, invoke a process or waive evidence.
[<RequireQualifiedAccess>]
type Advice =
    | RepairBaseline of reason: string
    | RepairCandidate of reason: string
    | ContextRequired of reason: string
    | BudgetExhausted
    | ProbeRequested of RegisteredProbe
    | SurvivorReview
    | NoInvestigation

[<RequireQualifiedAccess>]
module Decision =
    let decide observation =
        if not observation.SnapshotMatches then
            Advice.ContextRequired "source snapshot changed"
        else
            match observation.Baseline with
            | Failed _ -> Advice.RepairBaseline "original tests failed"
            | Invalid reason -> Advice.RepairBaseline reason
            | Unavailable reason -> Advice.ContextRequired reason
            | Passed count when count <= 0 -> Advice.ContextRequired "baseline contains no tests"
            | Passed _ ->
                match observation.Candidate with
                | None -> Advice.ContextRequired "candidate was not executed"
                | Some (Unavailable reason) -> Advice.ContextRequired reason
                | Some (Invalid reason) -> Advice.RepairCandidate reason
                | Some (Failed count) when count <= 0 -> Advice.ContextRequired "invalid failure count"
                | Some (Failed _) -> Advice.NoInvestigation
                | Some (Passed count) when count <= 0 -> Advice.ContextRequired "candidate contains no tests"
                | Some (Passed _) ->
                    match observation.Probe with
                    | None -> Advice.SurvivorReview
                    | Some _ when observation.RemainingBudget <= 0 -> Advice.BudgetExhausted
                    | Some probe -> Advice.ProbeRequested probe

    let describe = function
        | Advice.RepairBaseline reason -> "RepairBaseline", reason
        | Advice.RepairCandidate reason -> "RepairCandidate", reason
        | Advice.ContextRequired reason -> "ContextRequired", reason
        | Advice.BudgetExhausted -> "BudgetExhausted", "registered probe budget exhausted"
        | Advice.ProbeRequested probe -> "ProbeRequested", probe.Id
        | Advice.SurvivorReview -> "SurvivorReview", "survival does not establish equivalence"
        | Advice.NoInvestigation -> "NoInvestigation", "tests distinguished the candidate"
