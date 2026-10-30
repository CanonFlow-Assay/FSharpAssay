#load "Order.fs"
#load "Pricing.fs"

open System
open System.IO
open System.Text.Json
open FsAssay.Playground.Shape.Domain

let arguments = fsi.CommandLineArgs |> Array.skip 1
if arguments.Length <> 2 then failwith "usage: Probe.fsx before|expanded|failed-baseline <result-json>"
let selection, output = arguments[0], arguments[1]
if not (List.contains selection ["before"; "expanded"; "failed-baseline"]) then failwith "unknown selection"
let id = OrderId.create (Guid.Parse "11111111-1111-1111-1111-111111111111") |> Result.defaultWith failwith
let quantity = Quantity.create 1 |> Result.defaultWith failwith
let decide exists orders = OrderDecision.decide id quantity { CustomerExists = exists; ExistingOpenOrders = orders; MaximumOpenOrders = 5 }
let quote tier price quantity = Quote.calculate { UnitPrice = price; Quantity = quantity; Tier = tier }
let original = [
    "QTY-positive", fun () -> Quantity.create 1 |> Result.map Quantity.value = Ok 1
    "QTY-negative", fun () -> Quantity.create -1 |> Result.isError
    "ORDER-missing-customer", fun () -> decide false 0 = Error OrderDecisionError.CustomerNotFound
    "ORDER-below-limit", fun () -> decide true 4 |> Result.isOk
    "PRICE-standard", fun () -> quote CustomerTier.Standard 50m 2 = 100m
    "PRICE-above-threshold", fun () -> quote CustomerTier.Preferred 40m 3 = 108m
]
let boundaries = [
    "QTY-zero", fun () -> Quantity.create 0 |> Result.isError
    "ORDER-exact-limit", fun () -> decide true 5 = Error (OrderDecisionError.OpenOrderLimitReached 5)
    "ORDER-above-limit", fun () -> decide true 6 = Error (OrderDecisionError.OpenOrderLimitReached 5)
    "PRICE-exact-threshold", fun () -> quote CustomerTier.Preferred 50m 2 = 90m
    "PRICE-below-threshold", fun () -> quote CustomerTier.Preferred 49m 2 = 98m
    "PRICE-quantity", fun () -> quote CustomerTier.Standard 25m 3 = 75m
]
let tests =
    match selection with
    | "before" -> original
    | "expanded" -> original @ boundaries
    | _ -> original @ [ "CONTROL-deliberately-failed-baseline", fun () -> false ]
type TestResult = { id: string; passed: bool; error: string }
type Report = { schema: string; selection: string; total: int; passed: int; failed: int; tests: TestResult array }
let results = tests |> List.map (fun (name, test) ->
    try { id = name; passed = test(); error = "" }
    with error -> { id = name; passed = false; error = error.GetType().Name + ": " + error.Message }) |> List.toArray
let passed = results |> Array.filter _.passed |> Array.length
let report = { schema = "fsassay-playground-tests/0.1"; selection = selection; total = results.Length; passed = passed; failed = results.Length - passed; tests = results }
File.WriteAllText(output, JsonSerializer.Serialize(report, JsonSerializerOptions(WriteIndented = true)))
if report.failed <> 0 then exit 1
