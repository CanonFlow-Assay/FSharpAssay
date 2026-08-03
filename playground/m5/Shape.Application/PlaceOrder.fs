namespace FsAssay.Playground.Shape.Application

open FsAssay.Playground.Shape.Domain

type OrderFacts =
    { CustomerExists: bool
      ExistingOpenOrders: int }

type LoadOrderFacts = string -> Async<OrderFacts>
type SaveAcceptedOrder = AcceptedOrder -> Async<unit>

type PlaceOrderPorts =
    { LoadOrderFacts: LoadOrderFacts
      SaveAcceptedOrder: SaveAcceptedOrder }

type PlaceOrderRequest =
    { CustomerReference: string
      OrderId: OrderId
      Quantity: Quantity }

[<RequireQualifiedAccess>]
module PlaceOrder =
    let execute ports request = async {
        let! facts = ports.LoadOrderFacts request.CustomerReference

        let decisionFacts =
            { CustomerExists = facts.CustomerExists
              ExistingOpenOrders = facts.ExistingOpenOrders
              MaximumOpenOrders = OrderDecision.MaximumOpenOrders }

        match OrderDecision.decide request.OrderId request.Quantity decisionFacts with
        | Error error -> return Error error
        | Ok accepted ->
            do! ports.SaveAcceptedOrder accepted
            return Ok accepted
    }
