#load "Assay.Application/Domain.fs"
open FsAssay.Application
let good = { Missing = []; ToolErrors = []; Failures = []; SourceCurrent = true; EngineUsable = true }
let manifest = { schema = "fsharp-application/0.1"; applicationId = "app"; project = "App.fsproj"; assemblyName = "App"; sdk = "10.0.301"; files = [|"App.fsproj"|]; requirements = "requirements.json"; contract = "contract.json"; minimumPassed = 2; frozenTestFiles = [|"OriginalTests.fs";"Program.fs"|]; acceptanceTestFiles = [|"AdditionalTests.fs"|] }
let old = { schema = "fsharp-application-requirements/0.1"; requirements = [| { id = "AMOUNT"; description = "positive"; testIds = [|"positive";"zero"|]; minimumPassed = 2 } |] }
let compatible m r c = Assessment.compatible manifest old "contract-1" m r c
let probe = { id = "boundary-AMOUNT"; requirementId = "AMOUNT" }
let advise status baseline snapshot budget registry requirement id = Assistance.select status baseline snapshot "source" budget registry requirement id
let checks = [
    "complete evidence is review-ready, not FsAssay Pass", Assessment.evaluate good = ApplicationStatus.ReadyForReview
    "missing named evidence cannot ready", match Assessment.evaluate {good with Missing=["tests"]} with ApplicationStatus.ContextRequired _ -> true | _ -> false
    "stale source cannot ready", match Assessment.evaluate {good with SourceCurrent=false} with ApplicationStatus.ContextRequired _ -> true | _ -> false
    "engine missing cannot ready", match Assessment.evaluate {good with EngineUsable=false} with ApplicationStatus.ContextRequired _ -> true | _ -> false
    "test failure cannot ready", match Assessment.evaluate {good with Failures=["test"]} with ApplicationStatus.NeedsRepair _ -> true | _ -> false
    "tool failure takes precedence", match Assessment.evaluate {good with ToolErrors=["invalid"]; Failures=["test"]} with ApplicationStatus.ToolFailure _ -> true | _ -> false
    "unchanged baseline compatible", compatible manifest old "contract-1" = []
    "different application rejected", compatible {manifest with applicationId="other"} old "contract-1" <> []
    "different contract rejected", compatible manifest old "contract-2" <> []
    "minimum cannot fall", compatible {manifest with minimumPassed=1} old "contract-1" <> []
    "requirement cannot disappear", compatible manifest {old with requirements=[||]} "contract-1" <> []
    "requirement cannot be redefined silently", compatible manifest {old with requirements=[|{old.requirements[0] with description="anything"}|]} "contract-1" <> []
    "old named tests cannot disappear", compatible manifest {old with requirements=[|{old.requirements[0] with testIds=[|"positive"|]}|]} "contract-1" <> []
    "requirement minimum cannot fall", compatible manifest {old with requirements=[|{old.requirements[0] with minimumPassed=1}|]} "contract-1" <> []
    "registered failed-requirement probe eligible", advise (ApplicationStatus.NeedsRepair ["test"]) true "source" 3 [probe] "AMOUNT" probe.id = Advice.ProbeRequested probe.id
    "stale advice blocked", advise (ApplicationStatus.NeedsRepair ["test"]) true "stale" 3 [probe] "AMOUNT" probe.id = Advice.ContextRequired
    "failed baseline blocks advice", advise (ApplicationStatus.NeedsRepair ["test"]) false "source" 3 [probe] "AMOUNT" probe.id = Advice.BaselineRepair
    "incomplete evidence blocks advice", advise (ApplicationStatus.ContextRequired ["missing"]) true "source" 3 [probe] "AMOUNT" probe.id = Advice.ContextRequired
    "ready state requests no probe", advise ApplicationStatus.ReadyForReview true "source" 3 [probe] "AMOUNT" probe.id = Advice.NoInvestigation
    "unknown proposal not executable", advise (ApplicationStatus.NeedsRepair ["test"]) true "source" 3 [probe] "AMOUNT" "shell-command" = Advice.SurvivorReview
    "wrong requirement not executable", advise (ApplicationStatus.NeedsRepair ["test"]) true "source" 3 [probe] "OTHER" probe.id = Advice.SurvivorReview
    "exhausted budget blocks probe", advise (ApplicationStatus.NeedsRepair ["test"]) true "source" 0 [probe] "AMOUNT" probe.id = Advice.BudgetExhausted
]
for name, passed in checks do if not passed then failwith name
printfn "Application contract: %d passed; 0 failed" checks.Length
