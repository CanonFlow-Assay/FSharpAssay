// Phase-2 Portable Boundary: Versioned Transport-Fact Boundary in F# M5 Shell
// Parsing, signature verification, and deduplication facts stay outside the pure M5 domain decision.
// Run with: dotnet fsi --warnaserror+ --exec playground/m5/TransportFacts.fsx

#load "Shape.Domain/Order.fs"
#load "Shape.Application/PlaceOrder.fs"
#load "Shape.Shell/OrderEndpoint.fs"

open System
open FsAssay.Playground.Shape.Domain
open FsAssay.Playground.Shape.Application
open FsAssay.Playground.Shape.Shell

[<RequireQualifiedAccess>]
type SignatureFact =
    | Verified
    | Missing
    | Invalid

[<RequireQualifiedAccess>]
type DeliveryFact =
    | FirstDelivery
    | DuplicateDelivery

type TransportEnvelope =
    { Version: string
      EventType: string
      DeliveryId: string
      Payload: PlaceOrderInput }

type TransportFacts =
    { Envelope: TransportEnvelope
      Signature: SignatureFact
      Delivery: DeliveryFact }

[<RequireQualifiedAccess>]
type TransportRouteOutput =
    | Routed of PlaceOrderOutput
    | Rejected of code: string

[<RequireQualifiedAccess>]
module TransportBoundary =
    let route (ports: PlaceOrderPorts) (facts: TransportFacts) : Async<TransportRouteOutput> =
        async {
            match facts.Signature with
            | SignatureFact.Missing ->
                return TransportRouteOutput.Rejected "missing-signature"
            | SignatureFact.Invalid ->
                return TransportRouteOutput.Rejected "invalid-signature"
            | SignatureFact.Verified ->
                match facts.Delivery with
                | DeliveryFact.DuplicateDelivery ->
                    return TransportRouteOutput.Rejected "duplicate-delivery"
                | DeliveryFact.FirstDelivery ->
                    match facts.Envelope.Version, facts.Envelope.EventType with
                    | ("v1" | "v2"), "order.place" ->
                        let! outcome = OrderEndpoint.handle ports facts.Envelope.Payload
                        return TransportRouteOutput.Routed outcome
                    | ("v1" | "v2"), _ ->
                        return TransportRouteOutput.Rejected "unsupported-event"
                    | _ ->
                        return TransportRouteOutput.Rejected "unsupported-version"
        }

    let canonical (output: TransportRouteOutput) : string =
        match output with
        | TransportRouteOutput.Routed (PlaceOrderOutput.Created (orderId, quantity)) ->
            $"accepted|place-order|{orderId}|{quantity}"
        | TransportRouteOutput.Routed (PlaceOrderOutput.Rejected reason) ->
            $"rejected|domain:{reason}"
        | TransportRouteOutput.Rejected code ->
            $"rejected|{code}"

let orderGuid = Guid.Parse "11111111-1111-1111-1111-111111111111"

let makeFacts version eventType signature delivery quantity =
    { Envelope =
        { Version = version
          EventType = eventType
          DeliveryId = "delivery-42"
          Payload =
            { CustomerReference = "customer-1"
              OrderId = orderGuid
              Quantity = quantity } }
      Signature = signature
      Delivery = delivery }

let cases =
    [ ("v1-created", makeFacts "v1" "order.place" SignatureFact.Verified DeliveryFact.FirstDelivery 2, $"accepted|place-order|{orderGuid}|2", 1)
      ("v2-created", makeFacts "v2" "order.place" SignatureFact.Verified DeliveryFact.FirstDelivery 3, $"accepted|place-order|{orderGuid}|3", 1)
      ("missing-signature", makeFacts "v1" "order.place" SignatureFact.Missing DeliveryFact.FirstDelivery 2, "rejected|missing-signature", 0)
      ("invalid-signature", makeFacts "v1" "order.place" SignatureFact.Invalid DeliveryFact.FirstDelivery 2, "rejected|invalid-signature", 0)
      ("duplicate-delivery", makeFacts "v1" "order.place" SignatureFact.Verified DeliveryFact.DuplicateDelivery 2, "rejected|duplicate-delivery", 0)
      ("unsupported-version", makeFacts "v3" "order.place" SignatureFact.Verified DeliveryFact.FirstDelivery 2, "rejected|unsupported-version", 0)
      ("unsupported-event", makeFacts "v1" "order.cancel" SignatureFact.Verified DeliveryFact.FirstDelivery 2, "rejected|unsupported-event", 0)
      ("invalid-domain-payload", makeFacts "v1" "order.place" SignatureFact.Verified DeliveryFact.FirstDelivery 0, "rejected|domain:quantity must be positive", 0) ]

let mutable failures = 0
for (name, facts, expected, expectedSaves) in cases do
    let saved = ResizeArray<AcceptedOrder>()
    let ports: PlaceOrderPorts =
        { LoadOrderFacts = fun _ -> async { return { CustomerExists = true; ExistingOpenOrders = 1 } }
          SaveAcceptedOrder = fun accepted -> async { saved.Add accepted } }
    let actual =
        TransportBoundary.route ports facts
        |> Async.RunSynchronously
        |> TransportBoundary.canonical
    if actual <> expected || saved.Count <> expectedSaves then
        eprintfn "%s: expected (%s, saves=%d), got (%s, saves=%d)" name expected expectedSaves actual saved.Count
        failures <- failures + 1

if failures > 0 then
    exit 1
else
    printfn "F# M5 versioned transport-fact boundary: %d cases passed." cases.Length
    exit 0
