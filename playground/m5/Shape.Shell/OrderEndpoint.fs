namespace FsAssay.Playground.Shape.Shell

open System
open FsAssay.Playground.Shape.Domain
open FsAssay.Playground.Shape.Application

type PlaceOrderInput =
    { CustomerReference: string
      OrderId: Guid
      Quantity: int }

[<RequireQualifiedAccess>]
type PlaceOrderOutput =
    | Created of orderId: Guid * quantity: int
    | Rejected of reason: string

[<RequireQualifiedAccess>]
module OrderEndpoint =
    let private validationError errors =
        errors |> String.concat "; " |> PlaceOrderOutput.Rejected

    let handle ports input = async {
        match OrderId.create input.OrderId, Quantity.create input.Quantity with
        | Error idError, Error quantityError ->
            return validationError [ idError; quantityError ]
        | Error error, _
        | _, Error error -> return PlaceOrderOutput.Rejected error
        | Ok orderId, Ok quantity ->
            let request: PlaceOrderRequest =
                { CustomerReference = input.CustomerReference
                  OrderId = orderId
                  Quantity = quantity }

            let! outcome = PlaceOrder.execute ports request
            return
                match outcome with
                | Ok accepted ->
                    PlaceOrderOutput.Created (
                        accepted |> AcceptedOrder.id |> OrderId.value,
                        accepted |> AcceptedOrder.quantity |> Quantity.value)
                | Error OrderDecisionError.CustomerNotFound -> PlaceOrderOutput.Rejected "customer not found"
                | Error (OrderDecisionError.OpenOrderLimitReached limit) ->
                    PlaceOrderOutput.Rejected $"open order limit reached ({limit})"
    }
