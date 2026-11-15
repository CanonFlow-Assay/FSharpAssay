module Refresh.Tests.Program

open System
open System.IO
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Text.Json
open System.Threading
open Expecto
open Refresh.Domain
open Refresh.Shell

let withStore action =
    let directory = Path.Combine(Path.GetTempPath(),"fsassay-refresh-"+Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory directory |> ignore
    try action (Store(Path.Combine(directory,"approval.db")))
    finally Directory.Delete(directory,true)
let approved = Decision.Approved("REF-1",25m)

[<Tests>]
let tests = testList "Refresh application" [
    testCase "limit equality is accepted" <| fun _ -> Expect.equal (Approval.decide 25m "REF-1" 25m) approved "the exact boundary is permitted"
    testCase "amount above limit is rejected" <| fun _ -> Expect.equal (Approval.decide 25m "REF-1" 25.01m) (Decision.Rejected "approval-limit-exceeded") "one cent above the limit is rejected"
    testCase "zero amount is rejected" <| fun _ -> Expect.equal (Approval.decide 25m "REF-1" 0m) (Decision.Rejected "amount-must-be-positive") "zero is invalid"
    testCase "negative amount is rejected" <| fun _ -> Expect.equal (Approval.decide 25m "REF-1" -1m) (Decision.Rejected "amount-must-be-positive") "negative is invalid"
    testCase "blank reference is rejected" <| fun _ -> Expect.equal (Approval.decide 25m " " 1m) (Decision.Rejected "reference-required") "blank input has explicit failure"
    testCase "reference is normalized" <| fun _ -> Expect.equal (Approval.decide 25m " REF-1 " 25m) approved "surrounding whitespace is normalized"
    testCase "domain has no persistence or network assembly dependency" <| fun _ ->
        let references = typeof<Decision>.Assembly.GetReferencedAssemblies() |> Array.map _.Name
        for forbidden in ["Microsoft.Data.Sqlite";"System.Net.Http";"Refresh.Shell"] do Expect.isFalse (Array.contains forbidden references) "domain dependency must remain pure"
    testCase "sqlite persists response" <| fun _ -> withStore (fun store ->
        Expect.equal (store.Record("REF-1",25m,approved,CancellationToken.None)) (Wire.encode approved) "actual SQLite roundtrip"
        Expect.equal store.Count 1 "one durable row"
        Expect.isGreaterThanOrEqual store.EngineVersion (System.Version(3,50,2)) "executed native SQLite must meet the advisory remediation version")
    testCase "identical replay remains one record" <| fun _ -> withStore (fun store ->
        let first = store.Record("REF-1",25m,approved,CancellationToken.None)
        let second = store.Record("REF-1",25m,approved,CancellationToken.None)
        Expect.equal second first "original response is replayed"
        Expect.equal store.Count 1 "replay cannot insert a second row")
    testCase "conflicting replay is refused" <| fun _ -> withStore (fun store ->
        store.Record("REF-1",25m,approved,CancellationToken.None) |> ignore
        Expect.throws (fun () -> store.Record("REF-1",24m,approved,CancellationToken.None) |> ignore) "changed payload is not replay"
        Expect.equal store.Count 1 "conflict cannot insert")
    testCase "cancelled write changes no rows" <| fun _ -> withStore (fun store ->
        use token = new CancellationTokenSource()
        token.Cancel()
        Expect.throws (fun () -> store.Record("REF-1",25m,approved,token.Token) |> ignore) "cancellation is observed before side effect"
        Expect.equal store.Count 0 "cancelled request leaves no row")
    testCase "wire contract keeps exact response names" <| fun _ ->
        use json = JsonDocument.Parse(Wire.encode approved)
        Expect.sequenceEqual (json.RootElement.EnumerateObject() |> Seq.map _.Name |> Seq.sort) ["reason";"reference";"status"] "wire names are part of the contract"
        Expect.equal (json.RootElement.GetProperty("status").GetString()) "approved" "native outcome is projected"
    testCaseAsync "actual loopback HTTP returns declared JSON" <| async {
        use port = new TcpListener(IPAddress.Loopback,0)
        port.Start()
        let number = (port.LocalEndpoint :?> IPEndPoint).Port
        port.Stop()
        use listener = new HttpListener()
        listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/" number)
        listener.Start()
        use timeout = new CancellationTokenSource(TimeSpan.FromSeconds 10.)
        let response = Wire.encode approved
        let serving = Http.serveOnce listener response timeout.Token
        use client = new HttpClient(Timeout=TimeSpan.FromSeconds 5.)
        let! actual = client.GetStringAsync(sprintf "http://127.0.0.1:%d/" number,timeout.Token) |> Async.AwaitTask
        do! serving |> Async.AwaitTask
        Expect.equal actual response "real HTTP preserves contract bytes"
    }
]
