#load "Decision.fs"
open FsAssay.Playground.DecisionAssist
let probe = { Id = "quantity-boundary"; RequirementId = "QTY" }
let valid = { Baseline = Passed 6; Candidate = Some (Passed 6); SnapshotMatches = true; RemainingBudget = 3; Probe = Some probe }
let cases = [
    "survivor requests only registered probe", Decision.decide valid = Advice.ProbeRequested probe
    "killed mutation needs no investigation", Decision.decide { valid with Candidate = Some (Failed 1) } = Advice.NoInvestigation
    "failed baseline cannot request probe", match Decision.decide { valid with Baseline = Failed 1 } with Advice.RepairBaseline _ -> true | _ -> false
    "invalid baseline cannot request probe", match Decision.decide { valid with Baseline = Invalid "compile" } with Advice.RepairBaseline _ -> true | _ -> false
    "missing baseline needs context", match Decision.decide { valid with Baseline = Unavailable "missing" } with Advice.ContextRequired _ -> true | _ -> false
    "empty baseline needs context", match Decision.decide { valid with Baseline = Passed 0 } with Advice.ContextRequired _ -> true | _ -> false
    "zero failed count is invalid", match Decision.decide { valid with Candidate = Some (Failed 0) } with Advice.ContextRequired _ -> true | _ -> false
    "negative failed count is invalid", match Decision.decide { valid with Candidate = Some (Failed -1) } with Advice.ContextRequired _ -> true | _ -> false
    "missing candidate needs context", match Decision.decide { valid with Candidate = None } with Advice.ContextRequired _ -> true | _ -> false
    "interrupted candidate needs context", match Decision.decide { valid with Candidate = Some (Unavailable "timeout") } with Advice.ContextRequired _ -> true | _ -> false
    "invalid candidate is not killed", match Decision.decide { valid with Candidate = Some (Invalid "compile") } with Advice.RepairCandidate _ -> true | _ -> false
    "empty candidate needs context", match Decision.decide { valid with Candidate = Some (Passed 0) } with Advice.ContextRequired _ -> true | _ -> false
    "survival is not equivalence", Decision.decide { valid with Probe = None } = Advice.SurvivorReview
    "budget exhaustion stops probe", Decision.decide { valid with RemainingBudget = 0 } = Advice.BudgetExhausted
    "negative budget stops probe", Decision.decide { valid with RemainingBudget = -1 } = Advice.BudgetExhausted
    "stale snapshot stops probe", match Decision.decide { valid with SnapshotMatches = false } with Advice.ContextRequired _ -> true | _ -> false
    "stale killed result still needs context", match Decision.decide { valid with SnapshotMatches = false; Candidate = Some (Failed 1) } with Advice.ContextRequired _ -> true | _ -> false
]
for name, passed in cases do
    if not passed then failwith ("FAILED: " + name)
printfn "Decision Assist: %d passed; 0 failed" cases.Length
