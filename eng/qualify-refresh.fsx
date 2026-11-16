#r "../FsAssay.Analyzers/bin/Release/net10.0/FsAssay.Analyzers.dll"
#r "../FsAssay.Runner/bin/Release/net10.0/FsAssay.Runner.dll"

open System
open System.IO
open System.Text.Json
open FsAssay.Runner

let root = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__,".."))
let arguments = fsi.CommandLineArgs |> Array.skip 1
if arguments.Length <> 1 then failwith "usage: dotnet fsi eng/qualify-refresh.fsx <fresh-output>"
let output = Path.GetFullPath arguments.[0]
if Directory.Exists output || File.Exists output then failwith "qualification output must be new"
Directory.CreateDirectory output |> ignore
let gates = ResizeArray<obj>()
let commands = ResizeArray<obj>()
let write name value = File.WriteAllBytes(Path.Combine(output,name),JsonSerializer.SerializeToUtf8Bytes(value,JsonSerializerOptions(WriteIndented=true)))
let execute directory name executable args expected =
    let result = Processes.run directory executable args 600000
    File.WriteAllText(Path.Combine(output,name+".stdout.log"),result.Stdout)
    File.WriteAllText(Path.Combine(output,name+".stderr.log"),result.Stderr)
    commands.Add(box {|name=name;workingDirectory=directory;executable=executable;arguments=args;exitCode=result.ExitCode;timedOut=result.TimedOut|})
    write "commands.json" commands
    if result.TimedOut || result.ExitCode <> expected then failwithf "%s: expected %d, actual %d; inspect retained logs" name expected result.ExitCode
    result
let dotnet name args = execute root name "dotnet" args 0 |> ignore
let source = (execute root "source" "git" ["rev-parse";"HEAD"] 0).Stdout.Trim()
let dirty = (execute root "status" "git" ["status";"--porcelain";"--untracked-files=normal"] 0).Stdout
if dirty <> "" then failwith "qualification requires a clean committed candidate"
let save complete = write "qualification.json" {|schema="fsassay-refresh-qualification/0.1";source=source;complete=complete;gates=gates;packagePublished=false|}
save false
let passed name = gates.Add(box {|name=name;state="Passed"|}); save false

dotnet "locked-restore" ["restore";"FsAssay.Stable.slnx";"--locked-mode"]
dotnet "build" ["build";"FsAssay.Stable.slnx";"--no-restore";"-c";"Release"]
execute root "stable-tests" "bash" ["eng/run-stable-tests.sh";"ordinary"] 0 |> ignore
execute root "direct-tests" "bash" ["eng/run-stable-tests.sh";"direct"] 0 |> ignore
execute root "zero-tests" "bash" ["eng/assert-zero-test-fails.sh"] 0 |> ignore
execute root "lock-inventory" "bash" ["eng/assert-required-locks.sh"] 0 |> ignore
execute root "missing-lock" "bash" ["eng/assert-missing-lock-fails.sh"] 0 |> ignore
execute root "locked-drift" "bash" ["eng/assert-locked-restore.sh"] 0 |> ignore
passed "stable-tests-and-negative-controls"
let feed = Path.Combine(output,"feed")
let second = Path.Combine(output,"second-pack")
for name,destination in ["pack",feed;"second-pack",second] do
    dotnet name ["pack";"FsAssay.Runner/FsAssay.Runner.fsproj";"--no-build";"--no-restore";"-c";"Release";"-o";destination]
let package directory = Path.Combine(directory,"FsAssay.Cli.1.0.4.nupkg")
let normalized = Path.Combine(output,"normalized")
Directory.CreateDirectory normalized |> ignore
for name,directory in ["first",feed;"second",second] do dotnet ("normalize-"+name) ["fsi";"eng/normalize-nupkg.fsx";package directory;Path.Combine(normalized,name+".nupkg")]
if Assessment.fileHash(Path.Combine(normalized,"first.nupkg")) <> Assessment.fileHash(Path.Combine(normalized,"second.nupkg")) then failwith "package bytes are not reproducible after registered metadata normalization"
passed "repeat-package-reproducibility"
let config = Path.Combine(output,"NuGet.config")
File.WriteAllText(config,"<configuration><packageSources><clear/><add key=\"candidate\" value=\""+System.Security.SecurityElement.Escape(feed)+"\"/></packageSources></configuration>")
let tools = Path.Combine(output,"tool")
dotnet "fresh-tool-install" ["tool";"install";"FsAssay.Cli";"--version";"1.0.4";"--tool-path";tools;"--configfile";config;"--no-cache"]
let cli = Path.Combine(tools,"fsassay")
let app = Path.Combine(output,"consumer")
let rec copy directory target =
    Directory.CreateDirectory target |> ignore
    for path in Directory.GetFiles directory do File.Copy(path,Path.Combine(target,Path.GetFileName path))
    for child in Directory.GetDirectories directory do
        if not (Set.contains (Path.GetFileName child) (set ["bin";"obj";"artifacts";".git"])) then copy child (Path.Combine(target,Path.GetFileName child))
copy (Path.Combine(root,"playground/refresh")) app
// The parent config intentionally admits only the candidate tool archive. The
// application has ordinary locked NuGet dependencies and must not inherit that
// tool-only feed or depend on packages already present in a developer cache.
File.WriteAllText(Path.Combine(app,"NuGet.Config"),"<configuration><packageSources><clear/><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\"/></packageSources></configuration>")
let git name args = execute app name "git" args 0 |> ignore
git "consumer-init" ["init";"--quiet"]
git "consumer-user" ["config";"user.name";"FsAssay qualification"]
git "consumer-email" ["config";"user.email";"qualification@localhost.invalid"]
File.WriteAllText(Path.Combine(app,".gitignore"),"**/bin/\n**/obj/\nartifacts/\n")
let commit name = git (name+"-add") ["add";"."]; git (name+"-commit") ["commit";"--quiet";"-m";name]
commit "baseline"
execute app "consumer-restore" "dotnet" ["restore";"Refresh.slnx";"--locked-mode"] 0 |> ignore
let run name mode expected =
    let directory = Path.Combine(output,name)
    Directory.CreateDirectory directory |> ignore
    let args = [mode;"--out-json";Path.Combine(directory,"receipt.json");"--out-sarif";Path.Combine(directory,"results.sarif");"--assessment-json";Path.Combine(directory,"assessment.json");"--ratecard-md";Path.Combine(directory,"report.md");"--material-html";Path.Combine(directory,"report.html");"--test-artifacts";Path.Combine(directory,"test-evidence");Path.Combine(app,"Refresh.slnx")]
    let args =
        if mode <> "check" then args
        else ["check";"--out-json";Path.Combine(directory,"receipt.json");Path.Combine(app,"Refresh.slnx")]
    execute app name cli args expected |> ignore
    let bytes = File.ReadAllBytes(Path.Combine(directory,"receipt.json"))
    let receipt = Authority.deserializeAndValidateReceipt bytes |> Result.defaultWith (String.concat "; " >> failwith)
    receipt
let checkedReceipt = run "analysis-not-run" "check" 2
if checkedReceipt.authoritative || checkedReceipt.outcome <> "Inconclusive" then failwith "analysis manufactured authority"
let baseline = run "baseline" "verify" 0
if not baseline.authoritative || baseline.tests.[0].passed <> 13 then failwith "baseline did not execute all required tests"
let repeat = run "repeat" "verify" 0
for file in ["receipt.json";"results.sarif"] do
    if Assessment.fileHash(Path.Combine(output,"baseline",file)) <> Assessment.fileHash(Path.Combine(output,"repeat",file)) then failwith ("canonical evidence depends on destination: "+file)
passed "installed-consumer-and-deterministic-authority"
let domain = Path.Combine(app,"Refresh.Domain/Approval.fs")
let original = File.ReadAllText domain
File.WriteAllText(domain,original.Replace("amount > limit","amount >= limit"))
commit "threshold-regression"
let red = run "red-threshold" "verify" 1
if red.tests.[0].failed <> 2 then failwith "threshold equality and normalized-reference failures were not retained"
let thresholdDetails = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output,"red-threshold/assessment.json")))
let names = thresholdDetails.RootElement.GetProperty("tests").[0].GetProperty("failureDetails").EnumerateArray() |> Seq.map (fun detail -> detail.GetProperty("testName").GetString()) |> Seq.toList
if not (names |> List.exists (fun name -> name.EndsWith("limit equality is accepted",StringComparison.Ordinal))) then failwith "threshold failure identity was lost"
File.WriteAllText(domain,original)
commit "threshold-repair"
run "green-threshold" "verify" 0 |> ignore
let testFile = Path.Combine(app,"Refresh.Tests/Program.fs")
let originalTests = File.ReadAllText testFile
File.WriteAllText(testFile,originalTests.Replace("testCase \"limit equality", "ptestCase \"limit equality"))
commit "missing-required-execution"
let missing = run "missing-test" "verify" 2
if missing.tests.[0].skipped <> 1 || missing.tests.[0].passed <> 12 then failwith "mixed skip counts were not preserved"
File.WriteAllText(testFile,originalTests)
commit "restore-required-execution"
run "green-missing-test" "verify" 0 |> ignore
passed "retained-failures-and-fresh-repairs"
let shell = Path.Combine(app,"Refresh.Shell/Adapters.fs")
let shellOriginal = File.ReadAllText shell
File.WriteAllText(shell,shellOriginal.Replace("JsonSerializer.Serialize response","(JsonSerializer.Serialize response).Replace(\"reference\",\"id\")"))
commit "wire-regression"
let contract = run "red-wire-contract" "verify" 1
if contract.tests.[0].failed < 1 then failwith "wire regression did not fail the declared contract"
File.WriteAllText(shell,shellOriginal)
commit "wire-repair"
run "green-wire-contract" "verify" 0 |> ignore
passed "actual-wire-contract-regression"
File.WriteAllText(domain,original.Replace("amount > limit", "amount > limit && amount < 1000000m"))
commit "uncovered-high-amount-mutation"
run "surviving-mutation" "verify" 0 |> ignore
// Passing configured tests does not establish equivalence; retain this unresolved survivor.
File.WriteAllText(domain,original)
commit "restore-surviving-mutation"
File.WriteAllText(domain,original + "\nlet = invalid mutation\n")
commit "invalid-mutation"
run "invalid-mutation" "verify" 3 |> ignore
File.WriteAllText(domain,original)
commit "restore-invalid-mutation"
run "final-consumer" "verify" 0 |> ignore
passed "survivor-and-invalid-mutation-retention"
let observations = [
    {|name="contract-regression";observation=sprintf "%A" (Investigation.contract ["reference";"status";"reason"] ["id";"status";"reason"])|}
    {|name="unmeasured-risk";observation=sprintf "%A" (Investigation.risk true None 1)|}
]
write "investigations.json" observations
execute root "application-preview-contract" "dotnet" ["fsi";"--warnaserror+";"--exec";"playground/application-assessment/Contract.Tests.fsx"] 0 |> ignore
execute root "decision-assist-corpus" "dotnet" ["fsi";"--warnaserror+";"--exec";"playground/decision-assist/Run.fsx";"--output";Path.Combine(output,"decision-assist")] 0 |> ignore
passed "native-application-contract-compatibility"
let after = (execute root "final-status" "git" ["status";"--porcelain";"--untracked-files=normal"] 0).Stdout
if after <> "" then failwith "qualification changed producer source"
write "package.json" {|path=package feed;sha256=Assessment.fileHash(package feed);source=source;published=false|}
save true
printfn "Refresh qualification completed: %d gates; source %s" gates.Count source
