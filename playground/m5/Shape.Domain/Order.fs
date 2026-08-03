namespace FsAssay.Playground.Shape.Domain

open System

type OrderId = private OrderId of Guid

[<RequireQualifiedAccess>]
module OrderId =
    let create value =
        if value = Guid.Empty then Error "order id must not be empty"
        else Ok (OrderId value)

    let value (OrderId value) = value

type Quantity = private Quantity of int

[<RequireQualifiedAccess>]
module Quantity =
    let create value =
        if value <= 0 then Error "quantity must be positive"
        else Ok (Quantity value)

    let value (Quantity value) = value

type AcceptedOrder =
    private
        { Id: OrderId
          Quantity: Quantity }

[<RequireQualifiedAccess>]
module AcceptedOrder =
    let create id quantity = { Id = id; Quantity = quantity }
    let id order = order.Id
    let quantity order = order.Quantity

[<RequireQualifiedAccess>]
type OrderDecisionError =
    | CustomerNotFound
    | OpenOrderLimitReached of limit: int

type OrderDecision =
    { CustomerExists: bool
      ExistingOpenOrders: int
      MaximumOpenOrders: int }

[<RequireQualifiedAccess>]
module OrderDecision =
    [<Literal>]
    let MaximumOpenOrders = 5

    let decide id quantity facts =
        if not facts.CustomerExists then
            Error OrderDecisionError.CustomerNotFound
        elif facts.ExistingOpenOrders >= facts.MaximumOpenOrders then
            Error (OrderDecisionError.OpenOrderLimitReached facts.MaximumOpenOrders)
        else
            Ok (AcceptedOrder.create id quantity)
