#r "../FsAssay.Analyzers/bin/Release/net11.0/FsAssay.Analyzers.dll"
#r "../FsAssay.Runner/bin/Release/net11.0/FsAssay.Runner.dll"
open System
open System.IO
open System.Text.Json
open FsAssay.Runner
let args = fsi.CommandLineArgs |> Array.skip 1
if args.Length <> 2 then failwith "usage: dotnet fsi eng/qualify-refresh-security.fsx <request-directory> <fresh-output>"
let requests, output = Path.GetFullPath args.[0], Path.GetFullPath args.[1]
if Directory.Exists output || File.Exists output then failwith "output must be new"
Directory.CreateDirectory output |> ignore
let controls = ResizeArray<obj>()
let save complete = File.WriteAllBytes(Path.Combine(output,"qualification.json"),JsonSerializer.SerializeToUtf8Bytes({|schema="fsharp-security-qualification/0.1";complete=complete;controls=controls;source=(Processes.run __SOURCE_DIRECTORY__ "git" ["rev-parse";"HEAD"] 30000).Stdout.Trim()|},JsonSerializerOptions(WriteIndented=true)))
save false
for name,expected in ["clean",0;"secret",1;"vulnerable",1;"stale",2;"missing",3;"format2",2] do
    let destination = Path.Combine(output,name)
    let actual = SecurityEvidence.run (Path.Combine(requests,name+".request.json")) destination
    controls.Add(box {|name=name;expected=expected;actual=actual;passed=actual=expected|})
    save false
    if actual <> expected then failwithf "%s: expected %d, actual %d" name expected actual
let repeated = Path.Combine(output,"clean-repeat")
if SecurityEvidence.run (Path.Combine(requests,"clean.request.json")) repeated <> 0 then failwith "clean repeat failed"
for file in ["receipt.json";"findings.sarif";"sbom.cdx.json";"vulnerability-input.cdx.json"] do
    if Assessment.fileHash(Path.Combine(output,"clean",file)) <> Assessment.fileHash(Path.Combine(repeated,file)) then failwith ("non-deterministic security evidence: "+file)
controls.Add(box {|name="deterministic-repeat";passed=true|})
let original = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(requests,"clean.request.json")))
let request = JsonSerializer.Deserialize<SecurityEvidence.Request>(original.RootElement.GetRawText())
let tampered = Path.Combine(output,"tampered")
Directory.CreateDirectory tampered |> ignore
for file in ["receipt.json";"receipt.sha256";"sbom.cdx.json";"findings.sarif";"vulnerability-input.cdx.json"] do File.Copy(Path.Combine(output,"clean",file),Path.Combine(tampered,file))
File.AppendAllText(Path.Combine(tampered,"sbom.cdx.json")," ")
let inspection = Processes.run request.sourceRoot request.dotnet [request.engine;"advise";Path.Combine(tampered,"receipt.json");request.sourceRoot;request.manifest;request.policy;request.toolchain;request.catalog;request.asOf;"0"] 120000
File.WriteAllText(Path.Combine(output,"tampered-inspection.json"),inspection.Stdout)
let inspected = JsonDocument.Parse inspection.Stdout
if inspected.RootElement.GetProperty("contextGaps").GetArrayLength() = 0 then failwith "tampered SBOM was accepted"
controls.Add(box {|name="changed-artifact-refused";passed=true|})
let changedRequest = {request with engineSha256=String.replicate 64 "0"}
let changedPath = Path.Combine(output,"changed-engine.request.json")
File.WriteAllBytes(changedPath,JsonSerializer.SerializeToUtf8Bytes changedRequest)
if SecurityEvidence.run changedPath (Path.Combine(output,"changed-engine")) <> 3 then failwith "wrong engine bytes were accepted"
controls.Add(box {|name="changed-producer-refused";passed=true|})
save true
printfn "F# security integration: %d controls passed" controls.Count
