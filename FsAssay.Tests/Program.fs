open Expecto
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Analyzers.SDK
open FsAssay.Analyzers
open FsAssay.Analyzers.Domain
open System.IO
open System

let checker = FSharpChecker.Create(keepAssemblyContents = true)

let runFsAssay (source: string) =
    let file = Path.Combine(Path.GetTempPath(), "Test_" + Guid.NewGuid().ToString() + ".fs")
    File.WriteAllText(file, source)
    let sourceText = SourceText.ofString source
    let optionsUnresolved, _ = checker.GetProjectOptionsFromScript(file, sourceText) |> Async.RunSynchronously
    let fsCore = typeof<option<int>>.Assembly.Location
    let sysLib = typeof<System.Object>.Assembly.Location
    let sysRuntime = typeof<System.Action>.Assembly.Location
    let options = { optionsUnresolved with OtherOptions = Array.append optionsUnresolved.OtherOptions [| "-r:" + fsCore; "-r:" + sysLib; "-r:" + sysRuntime |] }
    let parseResults, checkAnswer = checker.ParseAndCheckFileInProject(file, 0, sourceText, options) |> Async.RunSynchronously
    match checkAnswer with
    | FSharpCheckFileAnswer.Succeeded(checkResults) ->
        let context : CliContext = {
            FileName = file
            SourceText = sourceText
            ParseFileResults = parseResults
            CheckFileResults = checkResults
            TypedTree = checkResults.ImplementationFile
            CheckProjectResults = Unchecked.defaultof<_>
            ProjectOptions = Unchecked.defaultof<_>
            AnalyzerIgnoreRanges = Map.empty
        }
        Library.coreAnalyzer context.TypedTree context.FileName context.SourceText context.CheckFileResults.Diagnostics Domain.Profile.Core |> Async.RunSynchronously
    | FSharpCheckFileAnswer.Aborted -> 
        failwith "Failed to parse and check: Aborted"

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
        let sysLib = typeof<System.Object>.Assembly.Location
        let sysRuntime = typeof<System.Action>.Assembly.Location
        let options = { optionsUnresolved with OtherOptions = Array.append optionsUnresolved.OtherOptions [| "-r:" + fsCore; "-r:" + sysLib; "-r:" + sysRuntime |] }
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
        testCase "Phase 0: FCS and SDK Compatibility" <| fun _ ->
            let fcsAssembly = typeof<FSharpChecker>.Assembly
            Expect.isNotNull fcsAssembly "FSharpChecker should be loaded from FCS"
            
            let sdkAssembly = typeof<Analyzer<_>>.Assembly
            Expect.isNotNull sdkAssembly "Analyzer SDK should be loaded"
            
            let fcsName = fcsAssembly.GetName().Name
            Expect.equal fcsName "FSharp.Compiler.Service" "FCS assembly name mismatch"

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
            let results = runFsAssay sourceCode
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
            let results = runFsAssay sourceCode
            expectViolation "FSA-TDD03" results
    ]

let runE2E (projectCode: string) (sourceCode: string) =
    let tmpDir = Path.Combine(Path.GetTempPath(), "FsAssayE2E_" + Guid.NewGuid().ToString())
    Directory.CreateDirectory(tmpDir) |> ignore
    File.WriteAllText(Path.Combine(tmpDir, "TestProj.fsproj"), projectCode)
    if not (String.IsNullOrWhiteSpace(sourceCode)) then
        File.WriteAllText(Path.Combine(tmpDir, "Library.fs"), sourceCode)
    
    let runnerDir = Path.Combine(__SOURCE_DIRECTORY__, "..", "FsAssay.Runner")
    let pi = new System.Diagnostics.ProcessStartInfo("dotnet", sprintf "run --project \"%s\" -- \"%s\"" runnerDir tmpDir)
    pi.RedirectStandardOutput <- true
    pi.RedirectStandardError <- true
    pi.UseShellExecute <- false
    use p = System.Diagnostics.Process.Start(pi)
    p.WaitForExit()
    Directory.Delete(tmpDir, true)
    p.ExitCode

let e2eTests =
    testList "Phase 5 Hardening E2E Fault Injection" [
        testCase "Fault Injection 1: Corrupted .fsproj" <| fun _ ->
            let proj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"
            let code = "module Corrupt\nlet x = 1"
            let exitCode = runE2E proj code
            Expect.equal exitCode 3 "Expected ToolFailure (3) on corrupted project"

        testCase "Fault Injection 2: Missing source files" <| fun _ ->
            let proj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"NonExistent.fs\" /></ItemGroup></Project>"
            let exitCode = runE2E proj ""
            Expect.isTrue (exitCode <> 0) (sprintf "Expected failure on missing evidence, got %d" exitCode)

        testCase "Fault Injection 3: Unparseable F# file" <| fun _ ->
            let proj = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Library.fs\" /></ItemGroup></Project>"
            let code = "module SyntaxErr\nlet x = "
            let exitCode = runE2E proj code
            Expect.equal exitCode 2 "Expected RequiredEvidenceMissing (2) on unparseable F# file"
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

        testCase "FSA-P03: Seq.toList triggers P03" <| fun _ ->
            let sourceCode = """
module P03
let listify xs = Seq.toList xs
"""
            let results = runFsAssay sourceCode
            expectViolation "FSA-P03" results

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

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs [] argv (testList "All Tests" [tests; e2eTests; perfAndCompTests])
