namespace FsAssay.Runner

/// Pure bounded observations and routing. No case constructs an engine verdict or executes code.
module Investigation =
    [<RequireQualifiedAccess>]
    type Observation = Complete | NeedsReview of string list | Unknown of string list | Invalid of string
    type Registration = { Id: string; RequirementId: string }
    [<RequireQualifiedAccess>]
    type Advice = NoInvestigation | ContextRequired | BaselineRepair | BudgetExhausted | ProbeRequested of string | SurvivorReview
    let contract (required: string list) (observed: string list) =
        if required.IsEmpty || observed.IsEmpty then Observation.Unknown ["contract observation is missing"]
        elif (List.distinct required).Length <> required.Length || (List.distinct observed).Length <> observed.Length then Observation.Invalid "duplicate contract member"
        else
            let missing = Set.difference (Set.ofList required) (Set.ofList observed) |> Set.toList
            let added = Set.difference (Set.ofList observed) (Set.ofList required) |> Set.toList
            if missing.IsEmpty && added.IsEmpty then Observation.Complete
            else Observation.NeedsReview ((missing |> List.map ((+) "removed:")) @ (added |> List.map ((+) "added:")))
    let resilience (expected: string list) (observed: string list) =
        if expected.IsEmpty || observed.IsEmpty then Observation.Unknown ["registered trace was not observed"]
        elif expected = observed then Observation.Complete
        else Observation.NeedsReview ["observed attempt/cancellation/commit trace differs from the registered contract"]
    /// Ordinal triage only; absent measured coverage remains unknown, never a defect probability.
    let risk changed (coverage: decimal option) survivors =
        if survivors < 0 then None, Observation.Invalid "negative survivor count"
        else
            match coverage with
            | None -> None, Observation.Unknown ["coverage was not measured for this source"]
            | Some percent when percent < 0m || percent > 100m -> None, Observation.Invalid "coverage outside 0..100"
            | Some percent ->
                let score = (if changed then 2 else 0) + (if percent < 80m then 3 else 0) + (if survivors > 0 then 4 else 0)
                Some score, (if score = 0 then Observation.Complete else Observation.NeedsReview ["review change, measured coverage and surviving mutations separately"])
    let select baselineReady snapshot expectedSnapshot budget registry requirement proposed observation =
        if snapshot <> expectedSnapshot || snapshot = "" then Advice.ContextRequired
        elif not baselineReady then Advice.BaselineRepair
        else
            match observation with
            | Observation.Invalid _ | Observation.Unknown _ -> Advice.ContextRequired
            | Observation.Complete -> Advice.NoInvestigation
            | Observation.NeedsReview _ ->
                match registry |> List.tryFind (fun registration -> registration.Id = proposed && registration.RequirementId = requirement) with
                | None -> Advice.SurvivorReview
                | Some _ when budget <= 0 -> Advice.BudgetExhausted
                | Some registration -> Advice.ProbeRequested registration.Id
