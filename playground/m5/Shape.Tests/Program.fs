module FsAssay.Playground.Shape.Tests

open System
open System.Reflection
open Expecto
open Microsoft.FSharp.Reflection
open FsAssay.Playground.Shape.Domain
open FsAssay.Playground.Shape.Application
open FsAssay.Playground.Shape.Shell

let private guid = Guid.Parse "11111111-1111-1111-1111-111111111111"

let private ports facts saved =
    { LoadOrderFacts = fun _ -> async { return facts }
      SaveAcceptedOrder = fun order -> async { saved order } }

let private validRequest (): PlaceOrderRequest =
    { CustomerReference = "customer-1"
      OrderId = OrderId.create guid |> Result.defaultWith failwith
      Quantity = Quantity.create 2 |> Result.defaultWith failwith }

let domainTests = testList "Shape New domain" [
    test "empty order id is rejected" {
        Expect.isError (OrderId.create Guid.Empty) "empty identifiers are invalid"
    }
    test "non-positive quantity is rejected" {
        Expect.isError (Quantity.create 0) "zero is invalid"
        Expect.isError (Quantity.create -1) "negative is invalid"
    }
    test "validated values round trip" {
        Expect.equal (OrderId.create guid |> Result.map OrderId.value) (Ok guid) "id"
        Expect.equal (Quantity.create 3 |> Result.map Quantity.value) (Ok 3) "quantity"
    }
    test "customer absence is a closed error case" {
        let facts = { CustomerExists = false; ExistingOpenOrders = 0; MaximumOpenOrders = 5 }
        let result = OrderDecision.decide (validRequest().OrderId) (validRequest().Quantity) facts
        Expect.equal result (Error OrderDecisionError.CustomerNotFound) "absence is explicit"
    }
    test "order limit is a closed error case" {
        let facts = { CustomerExists = true; ExistingOpenOrders = 5; MaximumOpenOrders = 5 }
        let result = OrderDecision.decide (validRequest().OrderId) (validRequest().Quantity) facts
        Expect.equal result (Error (OrderDecisionError.OpenOrderLimitReached 5)) "limit is explicit"
    }
    test "accepted order can only be observed through validated values" {
        let facts = { CustomerExists = true; ExistingOpenOrders = 4; MaximumOpenOrders = 5 }
        let accepted = OrderDecision.decide (validRequest().OrderId) (validRequest().Quantity) facts
        Expect.equal (accepted |> Result.map (AcceptedOrder.id >> OrderId.value)) (Ok guid) "accepted id"
    }
]

let applicationTests = testList "application orchestration" [
    testAsync "accepted orders are saved once" {
        let saved = ResizeArray<AcceptedOrder>()
        let facts = { CustomerExists = true; ExistingOpenOrders = 2 }
        let! result = PlaceOrder.execute (ports facts saved.Add) (validRequest())
        Expect.isOk result "decision succeeds"
        Expect.hasLength saved 1 "one effect follows the decision"
    }
    testAsync "rejected orders are not saved" {
        let saved = ResizeArray<AcceptedOrder>()
        let facts = { CustomerExists = true; ExistingOpenOrders = 5 }
        let! result = PlaceOrder.execute (ports facts saved.Add) (validRequest())
        Expect.equal result (Error (OrderDecisionError.OpenOrderLimitReached 5)) "decision is preserved"
        Expect.equal saved.Count 0 "no effect follows failure"
    }
]

let shellTests = testList "imperative shell" [
    testAsync "valid input returns created" {
        let facts = { CustomerExists = true; ExistingOpenOrders = 0 }
        let input = { CustomerReference = "customer-1"; OrderId = guid; Quantity = 2 }
        let! output = OrderEndpoint.handle (ports facts ignore) input
        Expect.equal output (PlaceOrderOutput.Created (guid, 2)) "shell maps success"
    }
    testAsync "invalid input is rejected before effects" {
        let loads = ResizeArray<string>()
        let shellPorts =
            { LoadOrderFacts = fun reference -> async { loads.Add reference; return { CustomerExists = true; ExistingOpenOrders = 0 } }
              SaveAcceptedOrder = fun _ -> async.Zero() }
        let input = { CustomerReference = "customer-1"; OrderId = Guid.Empty; Quantity = 0 }
        let! output = OrderEndpoint.handle shellPorts input
        Expect.equal output (PlaceOrderOutput.Rejected "order id must not be empty; quantity must be positive") "errors are stable"
        Expect.equal loads.Count 0 "invalid representation does not cross the port"
    }
]

let representationTests = testList "representation" [
    test "result handling covers both cases" {
        let render = function Ok value -> $"ok:{value}" | Error error -> $"error:{error}"
        Expect.equal (render (Ok 3)) "ok:3" "success"
        Expect.equal (render (Error "bad")) "error:bad" "failure"
    }
    test "domain unions expose their complete case set" {
        let names type' = FSharpType.GetUnionCases(type') |> Array.map _.Name |> Set.ofArray
        Expect.equal (names typeof<CustomerTier>) (set [ "Standard"; "Preferred" ]) "tiers"
        Expect.equal (names typeof<OrderDecisionError>) (set [ "CustomerNotFound"; "OpenOrderLimitReached" ]) "errors"
    }
]

let convergenceTests = testList "Shape Converge legacy compatibility" [
    testCase "legacy and core pricing agree" <| fun _ ->
        let cases = [ 10m, 2, false; 50m, 2, true; 40m, 3, true ]
        for unitPrice, quantity, preferred in cases do
            let legacy = LegacyPricing.calculate unitPrice quantity preferred
            let tier = if preferred then CustomerTier.Preferred else CustomerTier.Standard
            let refined = Quote.calculate { UnitPrice = unitPrice; Quantity = quantity; Tier = tier }
            Expect.equal legacy refined "compatibility behavior is frozen"
]

let architectureTests = testList "architecture" [
    test "project dependency direction is Domain to Application to Shell" {
        let references (assembly: Assembly) = assembly.GetReferencedAssemblies() |> Array.map _.Name |> Set.ofArray
        let domain = typeof<OrderId>.Assembly |> references
        let application = typeof<PlaceOrderPorts>.Assembly |> references
        let shell = typeof<PlaceOrderOutput>.Assembly |> references
        Expect.isFalse (domain.Contains("Shape.Application")) "domain does not depend on application"
        Expect.isFalse (domain.Contains("Shape.Shell")) "domain does not depend on shell"
        Expect.isTrue (application.Contains("Shape.Domain")) "application depends on domain"
        Expect.isFalse (application.Contains("Shape.Shell")) "application does not depend on shell"
        Expect.isTrue (shell.Contains("Shape.Application")) "shell depends inward"
    }
    test "core projects have no third-party union or framework packages" {
        let packageReferences (assembly: Assembly) =
            assembly.GetReferencedAssemblies()
            |> Array.map _.Name
            |> Array.filter (fun (name: string) -> not (name.StartsWith("System") || name = "FSharp.Core" || name = "netstandard" || name.StartsWith("Shape.")))
        Expect.isEmpty (packageReferences typeof<OrderId>.Assembly) "domain has no third-party runtime dependency"
        Expect.isEmpty (packageReferences typeof<PlaceOrderPorts>.Assembly) "application has no third-party runtime dependency"
    }
]

[<Tests>]
let allTests =
    testList "M5 Shape Playground" [
        domainTests
        applicationTests
        shellTests
        representationTests
        convergenceTests
        architectureTests
    ]
