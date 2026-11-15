module RefreshTests

open System
open System.IO
open System.Text
open System.Text.Json
open Expecto
open FsAssay.Runner
open FsAssay.Analyzers

let temporary action =
    let root = Path.Combine(Path.GetTempPath(),"fsassay-refresh-test-"+Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory root |> ignore
    try action root finally Directory.Delete(root,true)

let requirement: Authority.RequiredTestPolicy = {id="required";project="Tests.fsproj";minimumPassed=2}
let trx passed failed skipped =
    let result index outcome = sprintf "<UnitTestResult testId='%d' testName='test-%d' outcome='%s'><Output><ErrorInfo><Message>&lt;assertion&gt;</Message></ErrorInfo></Output></UnitTestResult>" index index outcome
    let results = [for i in 1..passed -> result i "Passed"] @ [for i in 1..failed -> result (i+passed) "Failed"] @ [for i in 1..skipped -> result (i+passed+failed) "NotExecuted"] |> String.concat ""
    sprintf "<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><Results>%s</Results><ResultSummary><Counters total='%d' executed='%d' passed='%d' failed='%d'/></ResultSummary></TestRun>" results (passed+failed+skipped) (passed+failed) passed failed |> Encoding.UTF8.GetBytes
let parse exit bytes = Verification.parseTrx requirement "/scope/Tests.fsproj" exit bytes

let tests = testList "FSharp refresh" [
    testCase "malformed null duplicate unknown configuration fails closed" <| fun _ ->
        for json in ["{";"null";"[]";"{\"profile\":\"core\",\"profile\":\"shell\"}";"{\"profile\":\"typo\"}";"{\"exclude\":null}";"{\"exlcude\":[]}"] do
            Expect.isError (Config.parse "/scope" json) "invalid configuration cannot become permissive defaults"
    testCase "minimal configuration has explicit defaults" <| fun _ ->
        let config = Config.parse "/scope" "{}" |> Result.defaultWith failtest
        Expect.equal config.profile "core" "default profile is core"
        Expect.equal config.exclude Config.defaultConfig.exclude "default exclusions are bounded"
    testCase "exclusion matching respects path components and glob boundaries" <| fun _ ->
        Expect.isFalse (Config.isExcluded "/scope" [|"bin"|] "/scope/Combine/Approval.fs") "bin substring is not a directory"
        Expect.isTrue (Config.isExcluded "/scope" [|"**/obj/**"|] "/scope/obj/AssemblyInfo.fs") "generated folder is excluded"
        Expect.isFalse (Config.isExcluded "/scope" [|"**/obj/**"|] "/scope/objects/Approval.fs") "similar directory remains analyzed"
    testCase "normalized conflicting boundaries are rejected" <| fun _ ->
        let selectors = {BoundaryPolicy.empty with CoreProjects=["src/./App.fsproj"];ShellProjects=["src/App.fsproj"]}
        Expect.isNonEmpty (BoundaryPolicy.validate selectors) "normalized contradiction is invalid"
        Expect.isNonEmpty (BoundaryPolicy.validate {BoundaryPolicy.empty with ShellProjects=["**/App.fsproj"]}) "wildcards cannot widen declared projects"
    testCase "boundary selectors require an evaluated graph identity" <| fun _ ->
        let selectors = {BoundaryPolicy.empty with ShellProjects=["../Adapter/Adapter.fsproj"]}
        Expect.isNonEmpty (BoundaryPolicy.validateGraph selectors ["App.fsproj"]) "selector cannot load external code"
        Expect.isEmpty (BoundaryPolicy.validateGraph selectors ["../Adapter/Adapter.fsproj"]) "a declared evaluated parent-relative project can match"
    testCase "nested shell namespace wins and unrelated names remain default" <| fun _ ->
        let selectors = {BoundaryPolicy.empty with CoreNamespaces=["Order"];ShellNamespaces=["Order.Persistence"]}
        Expect.equal (BoundaryPolicy.classify selectors "App.fsproj" "Order.Persistence.Store") BoundaryPolicy.Scope.Shell "nested exception wins"
        Expect.equal (BoundaryPolicy.classify selectors "App.fsproj" "Order.Domain") BoundaryPolicy.Scope.Core "domain is core"
        Expect.equal (BoundaryPolicy.classify selectors "App.fsproj" "Orderly") BoundaryPolicy.Scope.Default "namespace match is component-aware"
    testCase "shell project wins over namespace and matching stays case sensitive" <| fun _ ->
        let selectors = {BoundaryPolicy.empty with ShellProjects=["Shell.fsproj"];CoreNamespaces=["Order"]}
        Expect.equal (BoundaryPolicy.classify selectors "Shell.fsproj" "Order") BoundaryPolicy.Scope.Shell "project boundary wins"
        Expect.equal (BoundaryPolicy.classify selectors "Other.fsproj" "order") BoundaryPolicy.Scope.Default "namespace identity is ordinal"
    testCase "successful structured nonzero test evidence completes" <| fun _ ->
        let result = parse 0 (trx 2 0 0)
        Expect.isEmpty result.toolErrors "valid TRX"
        Expect.equal result.evidence.Status Authority.TestStatus.Passed "complete nonzero evidence passes"
        Expect.equal result.evidence.Passed 2 "individual counts retained"
    testCase "required test failure retains bounded assertion evidence" <| fun _ ->
        let result = parse 2 (trx 1 1 0)
        Expect.equal result.evidence.Status Authority.TestStatus.Failed "actual test failure"
        Expect.equal result.failureDetails.[0].message "<assertion>" "structured assertion is supplied by test producer"
        Expect.equal result.failureDetails.[0].testName "test-2" "stable test identity retained"
    testCase "mixed skipped evidence remains skipped and preserves passed count" <| fun _ ->
        let result = parse 0 (trx 1 0 1)
        Expect.equal result.evidence.Status Authority.TestStatus.Skipped "missing execution cannot pass"
        Expect.equal result.evidence.Passed 1 "observed successful tests are not erased"
        Expect.equal result.evidence.Skipped 1 "skip is retained"
    testCase "zero malformed missing and contradictory tests cannot pass" <| fun _ ->
        for bytes, exit in [trx 0 0 0,0;trx 2 0 0,2;trx 1 1 0,0;Encoding.UTF8.GetBytes "<x/>",0;Encoding.UTF8.GetBytes "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><x>&e;</x>",0] do
            Expect.isNonEmpty (parse exit bytes).toolErrors "invalid evidence becomes tool failure"
    testCase "duplicate individual test IDs cannot manufacture count" <| fun _ ->
        let forged = Encoding.UTF8.GetString(trx 2 0 0) |> fun text -> text.Replace("testId='2'","testId='1'") |> Encoding.UTF8.GetBytes
        Expect.isNonEmpty (parse 0 forged).toolErrors "duplicate identity rejected"
    testCase "counter and individual outcome mismatch is rejected" <| fun _ ->
        let forged = Encoding.UTF8.GetString(trx 2 0 0) |> fun text -> text.Replace("outcome='Passed'","outcome='NotExecuted'") |> Encoding.UTF8.GetBytes
        Expect.isNonEmpty (parse 0 forged).toolErrors "counts cannot override observations"
    testCase "failure details have explicit bounded truncation" <| fun _ ->
        let result = parse 2 (trx 0 25 0)
        Expect.equal result.evidence.Failed 25 "aggregate failure count remains complete"
        Expect.equal result.failureDetails.Length 20 "detail is bounded"
        Expect.isTrue result.failureDetailsTruncated "truncation is explicit"
    testCase "failed staging preserves existing report and source inputs" <| fun _ -> temporary (fun root ->
        let previous = Path.Combine(root,"report.json")
        File.WriteAllText(previous,"previous")
        let source = Path.Combine(root,"Approval.fs")
        File.WriteAllText(source,"module Approval")
        Expect.isError (Output.publish [source] [previous,[|1uy|];source,[|2uy|]]) "source output is refused"
        Expect.equal (File.ReadAllText previous) "previous" "previous report is retained when validation fails"
        Expect.equal (File.ReadAllText source) "module Approval" "source bytes preserved")
    testCase "report aliases are refused before writes" <| fun _ -> temporary (fun root ->
        let target = Path.Combine(root,"report.json")
        Expect.isError (Output.publish [] [target,[|1uy|];Path.Combine(root,".","report.json"),[|2uy|]]) "same target cannot represent two formats"
        Expect.isFalse (File.Exists target) "no partial output")
    testCase "output failure cannot replace another report" <| fun _ -> temporary (fun root ->
        let previous = Path.Combine(root,"report.json")
        File.WriteAllText(previous,"previous")
        let blockingFile = Path.Combine(root,"blocked")
        File.WriteAllText(blockingFile,"not a directory")
        Expect.isError (Output.publish [] [previous,[|1uy|];Path.Combine(blockingFile,"new.json"),[|2uy|]]) "all staging must complete"
        Expect.equal (File.ReadAllText previous) "previous" "old report not erased during failed staging")
    testCase "symlinked output directories cannot redirect writes" <| fun _ -> temporary (fun root ->
        let actual = Directory.CreateDirectory(Path.Combine(root,"actual"))
        let link = Path.Combine(root,"link")
        Directory.CreateSymbolicLink(link,actual.FullName) |> ignore
        Expect.isError (Output.publish [] [Path.Combine(link,"result.json"),[|1uy|]]) "symlinked path is refused")
    testCase "input snapshot includes scan configuration and detects byte changes" <| fun _ -> temporary (fun root ->
        let path = Path.Combine(root,".fsassayrc")
        File.WriteAllText(path,"{}")
        let first = Assessment.capture root
        File.WriteAllText(path,"{\"profile\":\"shell\"}")
        Expect.notEqual (Assessment.capture root) first "ambient configuration belongs to freshness scope")
    testCase "source input snapshot rejects symlinks" <| fun _ -> temporary (fun root ->
        let original = Path.Combine(root,"A.fs")
        File.WriteAllText(original,"module A")
        File.CreateSymbolicLink(Path.Combine(root,"B.fs"),original) |> ignore
        Expect.throws (fun () -> Assessment.capture root |> ignore) "symlinked source cannot establish scope")
    testCase "rule guidance distinguishes compiler checks heuristics and security" <| fun _ ->
        let compiler,_ = RuleGuidance.explain "FSA-C05"
        let security,_ = RuleGuidance.explain "FSA-SEC09"
        let style,_ = RuleGuidance.explain "FSA-TDD03"
        Expect.stringContains compiler "FS0025" "compiler owns completeness"
        Expect.stringContains security "SecurityAssay" "vulnerability evidence is independent"
        Expect.stringContains style "test style" "assertion count cannot certify test quality"
    testCase "security adapter rejects unpinned or ambiguous requests" <| fun _ ->
        let request: SecurityEvidence.Request = {schema="bad";dotnet="dotnet";engine="x";engineSha256="";sourceRoot=".";manifest="m";policy="p";toolchain="t";catalog="c";bundle="b";asOf="yesterday"}
        Expect.isNonEmpty (SecurityEvidence.validate request) "invalid producer and scope are not a security Pass"
    testCase "contract observations retain removed and added wire members" <| fun _ ->
        match Investigation.contract ["reference";"status"] ["id";"status"] with
        | Investigation.Observation.NeedsReview reasons ->
            Expect.contains reasons "removed:reference" "removed contract retained"
            Expect.contains reasons "added:id" "new wire identity retained"
        | other -> failtestf "unexpected %A" other
    testCase "risk is unknown without measured coverage" <| fun _ ->
        let score, observation = Investigation.risk true None 1
        Expect.isNone score "unknown coverage cannot become zero risk"
        match observation with Investigation.Observation.Unknown _ -> () | _ -> failtest "missing coverage remains unknown"
    testCase "registered investigation requires baseline freshness budget and requirement" <| fun _ ->
        let probe: Investigation.Registration = {Id="wire";RequirementId="CONTRACT"}
        let observation = Investigation.Observation.NeedsReview ["changed"]
        let select ready snapshot budget requirement id = Investigation.select ready snapshot "current" budget [probe] requirement id observation
        Expect.equal (select true "current" 1 "CONTRACT" "wire") (Investigation.Advice.ProbeRequested "wire") "eligible registered investigation"
        Expect.equal (select false "current" 1 "CONTRACT" "wire") Investigation.Advice.BaselineRepair "failed baseline blocks execution"
        Expect.equal (select true "stale" 1 "CONTRACT" "wire") Investigation.Advice.ContextRequired "stale evidence blocks execution"
        Expect.equal (select true "current" 0 "CONTRACT" "wire") Investigation.Advice.BudgetExhausted "budget bound"
        Expect.equal (select true "current" 1 "OTHER" "wire") Investigation.Advice.SurvivorReview "wrong requirement cannot execute"
    testCase "resilience missing trace and reordered trace stay visible" <| fun _ ->
        Expect.equal (Investigation.resilience ["cancel";"no-write"] ["cancel";"no-write"]) Investigation.Observation.Complete "exact observed trace"
        match Investigation.resilience ["cancel";"no-write"] [] with Investigation.Observation.Unknown _ -> () | _ -> failtest "missing trace is unknown"
        match Investigation.resilience ["cancel";"no-write"] ["write";"cancel"] with Investigation.Observation.NeedsReview _ -> () | _ -> failtest "effect before cancellation needs review"

    testCase "public test executor refuses an old evidence directory" <| fun _ -> temporary (fun root ->
        File.WriteAllText(Path.Combine(root,"Tests.fsproj"),"<Project />")
        let directory = Directory.CreateDirectory(Path.Combine(root,"old"))
        File.WriteAllBytes(Path.Combine(directory.FullName,"results.trx"),trx 2 0 0)
        let run = Verification.execute root directory.FullName requirement
        Expect.isNonEmpty run.toolErrors "existing reports cannot be reused"
        Expect.equal run.evidence.Status Authority.TestStatus.NotRun "no stale test result becomes observed execution"
        Expect.isFalse (File.Exists(Path.Combine(directory.FullName,"build.stdout.log"))) "refused before executing project code")

]
