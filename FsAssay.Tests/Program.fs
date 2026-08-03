module FsAssay.Tests.Program

open Expecto
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK
open FsAssay.Analyzers
open FsAssay.Analyzers.Domain
open System.IO
open System
open System.Text.RegularExpressions

let checker = FSharpChecker.Create(keepAssemblyContents = true)

let runFsAssayNamed (filePrefix: string) (source: string) =
    let file = Path.Combine(Path.GetTempPath(), filePrefix + "_" + Guid.NewGuid().ToString() + ".fs")
    File.WriteAllText(file, source)
    try
        match
            FsAssay.Runner.Orchestrator.evaluateSingleFileWithProfile
                file
                Domain.Profile.Core
                []
            |> Async.RunSynchronously
        with
        | FsAssay.Runner.Completed (violations, _, _) -> violations
        | FsAssay.Runner.Skipped reason -> failwithf "FsAssay skipped behavioral specimen: %A" reason
        | FsAssay.Runner.Failed failure -> failwithf "FsAssay failed behavioral specimen: %A" failure
    finally
        if File.Exists(file) then File.Delete(file)

let runFsAssay source =
    runFsAssayNamed "Specimen" source

let runFsAssayMulti (sources: (string * string) list) =
    let tmpDir = Path.Combine(Path.GetTempPath(), "FsAssayTest_" + Guid.NewGuid().ToString())
    Directory.CreateDirectory(tmpDir) |> ignore
    let filePaths = sources |> List.map (fun (name, src) ->
        let file = Path.Combine(tmpDir, name)
        File.WriteAllText(file, src)
        file, src
    )
    
    let allTrees = ResizeArray<string * FSharpImplementationFileContents * ISourceText>()
    
    for (file, src) in filePaths do
        let sourceText = SourceText.ofString src
        let optionsUnresolved, _ = checker.GetProjectOptionsFromScript(file, sourceText) |> Async.RunSynchronously
        let fsCore = typeof<option<int>>.Assembly.Location
        let trustedPlatformReferences =
            match AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") with
            | :? string as assemblies ->
                assemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun assembly -> "-r:" + assembly)
            | _ -> [||]
        let validOriginalOptions =
            optionsUnresolved.OtherOptions
            |> Array.filter (fun option ->
                if option.StartsWith("-r:") then File.Exists(option.Substring(3))
                else true)
        let options = {
            optionsUnresolved with
                OtherOptions =
                    Array.concat [ validOriginalOptions; trustedPlatformReferences; [| "-r:" + fsCore |] ]
                    |> Array.distinct
        }
        let parseResults, checkAnswer = checker.ParseAndCheckFileInProject(file, 0, sourceText, options) |> Async.RunSynchronously
        match checkAnswer with
        | FSharpCheckFileAnswer.Succeeded(checkResults) ->
            if checkResults.ImplementationFile.IsSome then
                allTrees.Add((file, checkResults.ImplementationFile.Value, sourceText))
        | _ -> ()

    let violations = Library.projectAnalyzer (allTrees |> Seq.toList) |> Async.RunSynchronously
    Directory.Delete(tmpDir, true)
    violations

let expectViolation code (messages: Violation list) =
    let hasViolation = messages |> List.exists (fun m -> m.Code = code)
    Expect.isTrue hasViolation (sprintf "Expected %s to be triggered. Actual messages: %A" code (messages |> List.map (fun m -> m.Code)))

let expectNoViolation code (messages: Violation list) =
    let hasViolation = messages |> List.exists (fun m -> m.Code = code)
    Expect.isFalse hasViolation (sprintf "Expected %s to NOT be triggered." code)

let tests =
    testList "Elite F# Anti-Pattern Tests" [
        testCase "Production admission contains exactly the independently exercised rules" <| fun _ ->
            let expected =
                set [
                    "FSA2022"; "FSA2017"; "FSA-AI01"; "FSA-AI12"; "FSA-AI13"
                    "FSA-AI15"; "FSA-AI16"; "FSA-C02"; "FSA-C05"; "FSA-P01"
                    "FSA-P02"; "FSA-P03"; "FSA-P04"; "FSA-P05"; "FSA-SEC08"
                    "FSA-SEC11"; "FSA-SEC12"; "FSA-SEC13"; "FSA-TDD01"
                    "FSA-TDD02"; "FSA-TDD03"
                ]
            Expect.equal Admission.ProductionRuleCodes expected "Production admission drifted from the behavioral suite"
            Expect.equal Admission.ProductionRuleCodes.Count 21 "Production admission count changed"
            Expect.equal Rule.AllRules.Length 93 "Catalog rule count changed"
            Expect.equal (Rule.AllRules |> List.filter (fun rule -> rule.Status = Implemented) |> List.length) 35 "Implemented rule count changed"
            Expect.equal (Rule.AllRules |> List.filter (fun rule -> rule.Status = Dummy) |> List.length) 22 "Dummy rule count changed"
            Expect.equal (Rule.AllRules |> List.filter (fun rule -> rule.Status = Prototype) |> List.length) 36 "Prototype rule count changed"
            Admission.ProductionRuleCodes
            |> Set.iter (fun code ->
                let rule = Rule.AllRules |> List.find (fun rule -> rule.Code = code)
                Expect.equal rule.Status Implemented (sprintf "%s must be implemented before admission" code))

        testCase "Phase 0: FCS and SDK Compatibility" <| fun _ ->
            let fcsAssembly = typeof<FSharpChecker>.Assembly
            Expect.isNotNull fcsAssembly "FSharpChecker should be loaded from FCS"
            
            let sdkAssembly = typeof<Analyzer<_>>.Assembly
            Expect.isNotNull sdkAssembly "Analyzer SDK should be loaded"
            
            let fcsName = fcsAssembly.GetName().Name
            Expect.equal fcsName "FSharp.Compiler.Service" "FCS assembly name mismatch"
            Expect.equal FsAssay.Runner.ProductIdentity.Version "1.0.4" "CLI product identity drifted"

        testCase "FSA-C01: Unchecked.defaultof Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// Unchecked.defaultof should not trigger here
let doSomething () =
    let x = 0
    x
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-C01" results

        testCase "FSA-C02: Partial Access Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// .Value should not trigger here
let doSomething () =
    let x = Some 5
    let y = 0
    y
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-C02" results

        testCase "FSA-C03: Async RunSynchronously Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// Async.RunSynchronously should not trigger here
let doSomething () =
    let a = async { return 1 }
    ()
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-C03" results

        testCase "FSA-C06: Exception in Public API Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// failwith invalidArg raise should not trigger here
let doSomething () =
    Error "Error"
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-C06" results

        testCase "FSA-C08: Seq.length on Infinite Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// Seq.length on infinite should not trigger here
let doSomething () =
    [1..10] |> Seq.length
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-C08" results

        testCase "FSA-S01: Hard-Coded Credentials Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// AKIA1234567890 should not trigger here
let doSomething () =
    let x = "Normal string"
    x
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-S01" results

        testCase "FSA-S02: Path Traversal Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// ../secret.txt should not trigger here
let doSomething () =
    let x = "normal.txt"
    x
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-S02" results

        testCase "FSA-S03: Swallowed Exception Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// try with _ -> () should not trigger here
let doSomething () =
    try
        ()
    with ex -> printfn "%A" ex
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-S03" results

        testCase "FSA-S05: Task Blocking Negative & Comment Invariance" <| fun _ ->
            let sourceCode = """
module BadCode
// .Wait() should not trigger here
open System.Threading.Tasks
let doSomething () =
    let t = Task.Run(fun () -> ())
    ()
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-S05" results

        testCase "FSA-C02: Option.get triggers C02" <| fun _ ->
            let sourceCode = """
module BadCode
type ProfileAttribute(name: string) = inherit System.Attribute()

[<Profile("core")>]
let doSomething () =
    let x = Some 5
    Option.get x
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-C02" results

        testCase "FSA-C05: Incomplete Match triggers C05" <| fun _ ->
            let sourceCode = """
module BadCode
let doSomething (x: int option) =
    match x with
    | Some v -> v
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-C05" results

        testCase "FSA2022: System.IO usage triggers FSA2022" <| fun _ ->
            let sourceCode = """
module BadCode
let doSomething () =
    System.IO.File.ReadAllText("test.txt")
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA2022" results

        testCase "FSA2022 hostile: in-memory serialization is not external IO" <| fun _ ->
            let sourceCode = """
module MemoryOnly
let canonicalBytes () =
    let stream = new System.IO.MemoryStream()
    stream.ToArray()
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA2022" results

        testCase "FSA-AI16 hostile: retail names are not AI operations" <| fun _ ->
            let sourceCode = """
module RetailPolicy
let retailRulePackDigestText () = "sha256:abc"
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-AI16" results
            expectNoViolation "FSA-AI17" results

        testCase "FSA-AI01: Unvalidated AI output triggers FSA-AI01" <| fun _ ->
            let sourceCode = """
module BadCode
module OpenAI =
    let GenerateText () = "AI Output"
let doSomething () =
    OpenAI.GenerateText()
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-AI01" results
            
        testCase "FSA2017: Circular Dependency triggers FSA2017" <| fun _ ->
            let sources = [
                "A.fs", """
module rec Circular

module ModuleA =
    let doA () = ModuleB.doB ()

module ModuleB =
    let doB () = ModuleA.doA ()
"""
            ]
            let results = runFsAssayMulti sources
            expectViolation "FSA2017" results
            
        testCase "FSA-SEC08: Broken Access Control triggers FSA-SEC08" <| fun _ ->
            let sourceCode = """
module BadCode
type HttpGetAttribute() = inherit System.Attribute()

[<HttpGet>]
let getSensitiveData () = "Sensitive"
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-SEC08" results

        testCase "FSA-SEC11: Unsigned ONDC Message triggers FSA-SEC11" <| fun _ ->
            let sourceCode = """
module BadCode
type ONDCMessage = { Data: string }
let send msg = ()
let doSomething () =
    let msg = { Data = "test" }
    send msg
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-SEC11" results

        testCase "FSA-SEC12: PII in Logs triggers FSA-SEC12" <| fun _ ->
            let sourceCode = """
module BadCode
let Log (msg: string) = ()
let doSomething () =
    Log "User password is test"
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-SEC12" results

        testCase "FSA-SEC13: SSRF triggers FSA-SEC13" <| fun _ ->
            let sources = [
                "Api.fs", """
module Api.Controllers
let doSomething (url: string) =
    let client = System.Net.WebRequest.Create(url)
    client.GetResponse() |> ignore
"""
            ]
            let results = runFsAssayMulti sources
            expectViolation "FSA-SEC13" results

        testCase "FSA-TDD01: Missing test for Domain module triggers FSA-TDD01" <| fun _ ->
            let sources = [
                "Domain.Models.fs", """
module Domain.Models
let doDomainThing () = 1
"""
            ]
            let results = runFsAssayMulti sources
            expectViolation "FSA-TDD01" results

        testCase "FSA-TDD02: Test file without Property triggers FSA-TDD02" <| fun _ ->
            let sourceCode = """
module MyTests
type FactAttribute() = class inherit System.Attribute() end
[<Fact>]
let myTest () = ()
"""
            let results = runFsAssayNamed "MyTests" sourceCode
            expectViolation "FSA-TDD02" results

        testCase "FSA-TDD03: Multiple assertions trigger FSA-TDD03" <| fun _ ->
            let sourceCode = """
module MyTests
type PropertyAttribute() = class inherit System.Attribute() end
module Expect =
    let equal a b c = ()
[<Property>]
let myTest () =
    Expect.equal 1 1 "first"
    Expect.equal 2 2 "second"
"""
            let results = runFsAssayNamed "MyTests" sourceCode
            expectViolation "FSA-TDD03" results
    ]

let runE2EWithArgumentsCapture (projectCode: string) (sourceCode: string) (arguments: string list) =
    let tmpDir = Path.Combine(Path.GetTempPath(), "FsAssayE2E_" + Guid.NewGuid().ToString())
    Directory.CreateDirectory(tmpDir) |> ignore
    File.WriteAllText(Path.Combine(tmpDir, "TestProj.fsproj"), projectCode)
    if not (String.IsNullOrWhiteSpace(sourceCode)) then
        File.WriteAllText(Path.Combine(tmpDir, "Library.fs"), sourceCode)
    
    let runnerAssembly =
        Path.Combine(
            __SOURCE_DIRECTORY__,
            "..",
            "FsAssay.Runner",
            "bin",
            "Release",
            "net10.0",
            "FsAssay.Runner.dll")
    let pi = new System.Diagnostics.ProcessStartInfo("dotnet")
    pi.ArgumentList.Add(runnerAssembly)
    for argument in arguments do pi.ArgumentList.Add(argument)
    pi.ArgumentList.Add(tmpDir)
    pi.RedirectStandardOutput <- true
    pi.RedirectStandardError <- true
    pi.UseShellExecute <- false
    use p = System.Diagnostics.Process.Start(pi)
    let standardOutput = p.StandardOutput.ReadToEndAsync()
    let standardError = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    let captured = p.ExitCode, standardOutput.Result, standardError.Result
    Directory.Delete(tmpDir, true)
    captured

let runE2EWithArguments (projectCode: string) (sourceCode: string) (arguments: string list) =
    let exitCode, _, _ = runE2EWithArgumentsCapture projectCode sourceCode arguments
    exitCode

let runE2E (projectCode: string) (sourceCode: string) =
    runE2EWithArguments projectCode sourceCode []

let runCli (arguments: string list) =
    let runnerAssembly =
        Path.Combine(
            __SOURCE_DIRECTORY__,
            "..",
            "FsAssay.Runner",
            "bin",
            "Release",
            "net10.0",
            "FsAssay.Runner.dll")
    let pi = new System.Diagnostics.ProcessStartInfo("dotnet")
    pi.ArgumentList.Add(runnerAssembly)
    arguments |> List.iter pi.ArgumentList.Add
    pi.RedirectStandardOutput <- true
    pi.RedirectStandardError <- true
    pi.UseShellExecute <- false
    use p = System.Diagnostics.Process.Start(pi)
    let standardOutput = p.StandardOutput.ReadToEndAsync()
    let standardError = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    p.ExitCode, standardOutput.Result, standardError.Result

let runE2EWithPluginPath (pluginPath: string) =
    let tmpDir = Path.Combine(Path.GetTempPath(), "FsAssayPluginE2E_" + Guid.NewGuid().ToString())
    Directory.CreateDirectory(tmpDir) |> ignore
    let sourcePath = Path.Combine(tmpDir, "Library.fs")
    File.WriteAllText(sourcePath, "module PluginLoad\nlet value = 1")
    let runnerAssembly =
        Path.Combine(
            __SOURCE_DIRECTORY__,
            "..",
            "FsAssay.Runner",
            "bin",
            "Release",
            "net10.0",
            "FsAssay.Runner.dll")
    let pi = new System.Diagnostics.ProcessStartInfo("dotnet")
    pi.ArgumentList.Add(runnerAssembly)
    pi.ArgumentList.Add("--plugin")
    pi.ArgumentList.Add(pluginPath)
    pi.ArgumentList.Add(sourcePath)
    pi.RedirectStandardOutput <- true
    pi.RedirectStandardError <- true
    pi.UseShellExecute <- false
    use p = System.Diagnostics.Process.Start(pi)
    p.WaitForExit()
    Directory.Delete(tmpDir, true)
    p.ExitCode

let e2eTests =
    testList "Phase 5 Hardening E2E Fault Injection" [
        testCase "CLI consumer contract and corrupted project remain honest" <| fun _ ->
            let results = [ "help"; "--help"; "-h" ] |> List.map (fun argument -> runCli [ argument ])
            let expectedExit, expectedOutput, expectedError = results.Head
            Expect.equal expectedExit 0 "help must succeed"
            Expect.equal expectedError "" "help must not write an error"
            Expect.equal (Regex.Matches(expectedOutput, "USAGE: fsassay").Count) 1 "help must render exactly once"
            results.Tail
            |> List.iter (fun (exitCode, standardOutput, standardError) ->
                Expect.equal exitCode expectedExit "all help forms use the same exit code"
                Expect.equal standardOutput expectedOutput "all help forms render the same output"
                Expect.equal standardError expectedError "all help forms use the same streams")

            let doctorExit, doctorOutput, doctorError = runCli [ "doctor" ]
            Expect.equal doctorExit 0 "doctor must succeed for the qualification toolchain"
            Expect.equal doctorError "" "healthy doctor must not write an error"
            for expected in [ "ToolVersion: 1.0.4"; "RuntimeVersion:"; "SdkVersion: 10.0.301"; "FSharpCompilerServiceVersion:"; "AnalysisNetworkDefault: offline"; "SourceUpload: none"; "FsAssayTelemetry: none"; "Status: healthy" ] do
                Expect.stringContains doctorOutput expected $"doctor must report {expected}"

            let explainExit, explainOutput, explainError = runCli [ "explain"; "FSA-C02" ]
            Expect.equal explainExit 0 "known rule explanation must succeed"
            Expect.equal explainError "" "known rule explanation must not write an error"
            Expect.stringContains explainOutput "Rule: FSA-C02" "the canonical code is reported"
            Expect.stringContains explainOutput "ImplementationStatus: implemented" "catalogue implementation status is reported"
            Expect.stringContains explainOutput "M3AdmissionClass: experimental" "M3 admission class is reported"
            Expect.stringContains explainOutput "Authority: non-authoritative" "explanation cannot be mistaken for authority"
            let unknownExit, unknownOutput, unknownError = runCli [ "explain"; "UNKNOWN0000" ]
            Expect.equal unknownExit 64 "unknown rules are invalid invocations"
            Expect.equal unknownOutput "" "unknown rule output stays off stdout"
            Expect.stringContains unknownError "unknown rule 'UNKNOWN0000'" "unknown rule is named"

            for arguments, expected in [ [ "--definitely-unknown" ], "unknown option '--definitely-unknown'"; [ "--out-json" ], "must be followed by <path>"; [ "explain" ], "usage: fsassay explain <RULE>" ] do
                let invalidExit, invalidOutput, invalidError = runCli arguments
                Expect.equal invalidExit 64 "invalid invocations use EX_USAGE"
                Expect.equal invalidOutput "" "invalid invocation output stays off stdout"
                Expect.stringContains invalidError expected "invalid invocation is actionable"

            let proj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"
            let code = "module Corrupt\nlet x = 1"
            let exitCode = runE2E proj code
            Expect.equal exitCode 2 "Expected Inconclusive (2) for a diagnosed project-load failure"

        testCase "Fault Injection 2: Missing source files" <| fun _ ->
            let proj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"NonExistent.fs\" /></ItemGroup></Project>"
            let exitCode = runE2E proj ""
            Expect.isTrue (exitCode <> 0) (sprintf "Expected failure on missing evidence, got %d" exitCode)

        testCase "Fault Injection 3: Unparseable F# file" <| fun _ ->
            let proj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Library.fs\" /></ItemGroup></Project>"
            let code = "module SyntaxErr\nlet x = "
            let exitCode = runE2E proj code
            Expect.equal exitCode 2 "Expected RequiredEvidenceMissing (2) on unparseable F# file"

        testCase "Fault Injection 4: Missing plugin is ToolFailure" <| fun _ ->
            let missing =
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-plugin.dll")
            let exitCode = runE2EWithPluginPath missing
            Expect.equal exitCode 3 "Expected ToolFailure (3) when the admitted plugin cannot load"

        testCase "Adjudication mode cannot override missing authority policy" <| fun _ ->
            let project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Library.fs\" /></ItemGroup></Project>"
            let code = "module AdjudicateBoundary\n// EXPECT: FSA2022\n// EXPECT: FSA-C15\nlet value = System.IO.File.ReadAllText(\"input.txt\")"
            let exitCode, standardOutput, standardError = runE2EWithArgumentsCapture project code [ "--adjudicate" ]
            Expect.equal exitCode 2 $"adjudication success must not bypass an Inconclusive receipt. stdout={standardOutput}; stderr={standardError}"
    ]

let perfAndCompTests =
    testList "Phase 5: Performance and Composition Tests" [
        testCase "FSA-P01: List append inside a loop triggers P01" <| fun _ ->
            let sourceCode = """
module P01
let doLoop () =
    let mutable res = []
    for i in 1..10 do
        res <- res @ [i]
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-P01" results

        testCase "FSA-P02: Boxing triggers P02" <| fun _ ->
            let sourceCode = """
module P02
let x = box 5
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-P02" results

        testCase "FSA-P02 hostile: compiler-generated box is not explicit boxing" <| fun _ ->
            Expect.isFalse
                (FsAssay.Analyzers.Visitor.isExplicitBoxCall "box" true)
                "Compiler-generated resource-management boxing must not trigger P02"
            Expect.isTrue
                (FsAssay.Analyzers.Visitor.isExplicitBoxCall "box" false)
                "An explicit box call must remain detectable"
            Expect.isFalse
                (FsAssay.Analyzers.Visitor.isExplicitObjectCoercion "document")
                "A compiler-generated resource coercion has no explicit source coercion"
            Expect.isTrue
                (FsAssay.Analyzers.Visitor.isExplicitObjectCoercion "value :> obj")
                "An explicit object upcast must remain detectable"

        testCase "FSA-P03: redundant sequence-list-sequence roundtrip triggers P03" <| fun _ ->
            let sourceCode = """
module P03
let bounce xs = List.toSeq (Seq.toList xs)
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-P03" results

        testCase "FSA-P03 hostile: necessary Seq.toList materialization is allowed" <| fun _ ->
            let sourceCode = """
module P03Necessary
let listify xs = Seq.toList xs
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-P03" results

        testCase "FSA-P04: String append in loop triggers P04" <| fun _ ->
            let sourceCode = """
module P04
let doLoop () =
    let mutable s = ""
    for i in 1..10 do
        s <- s + "a"
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-P04" results

        testCase "FSA-P05: Large struct triggers P05" <| fun _ ->
            let sourceCode = """
module P05
[<Struct>]
type LargeStruct = { A: int; B: int; C: int; D: int; E: int }
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-P05" results
    ]

let aiEcosystemTests =
    testList "Phase 6: AI and Ecosystem Tests" [
        testCase "FSA-AI12: Hardcoded API Key triggers AI12" <| fun _ ->
            let sourceCode = """
module AI12
let key = "sk-ant-12345"
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-AI12" results

        testCase "FSA-AI13: Missing max_tokens triggers AI13" <| fun _ ->
            let sourceCode = """
module AI13
module OpenAI =
    let complete prompt = ()
let callAI () = OpenAI.complete "Hello"
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-AI13" results

        testCase "FSA-AI15: String concat for prompt triggers AI15" <| fun _ ->
            let sourceCode = """
module AI15
let buildPrompt user =
    "system prompt" + user
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-AI15" results

        testCase "FSA-AI16: Returning raw string from AI function triggers AI16" <| fun _ ->
            let sourceCode = """
module AI16
let generateText () : string = "result"
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-AI16" results
    ]

let negativeTests =
    testList "Phase 7: Negative and False Positive Tests" [
        testCase "FSA-P01: List.append outside loop does not trigger P01" <| fun _ ->
            let sourceCode = """
module P01Negative
let a = [1; 2]
let b = [3; 4]
let c = a @ b
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-P01" results

        testCase "FSA-P04: String concat outside loop does not trigger P04" <| fun _ ->
            let sourceCode = """
module P04Negative
let a = "hello"
let b = "world"
let c = a + b
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-P04" results

        testCase "FSA-AI15: String concat without prompt keywords does not trigger AI15" <| fun _ ->
            let sourceCode = """
module AI15Negative
let buildName first last =
    first + " " + last
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-AI15" results

        testCase "FSA-C01: Suppressed null does not trigger C01" <| fun _ ->
            let sourceCode = """
module C01Negative
type ProfileAttribute(name: string) = inherit System.Attribute()

[<Profile("interop")>]
let getNull () =
    let a: string = null
    a
"""
            let results = runFsAssay sourceCode
            expectNoViolation "FSA-C01" results
    ]

[<Tests>]
let allTests =
    testList "All Tests" [tests; ObligationPluginTests.tests; AuthorityTests.tests; e2eTests; perfAndCompTests; aiEcosystemTests; negativeTests]
