namespace FsAssay.Playground.Shape.Domain

[<RequireQualifiedAccess>]
type CustomerTier =
    | Standard
    | Preferred

type QuoteInput =
    { UnitPrice: decimal
      Quantity: int
      Tier: CustomerTier }

[<RequireQualifiedAccess>]
module Quote =
    let private discount tier subtotal =
        match tier with
        | CustomerTier.Preferred when subtotal >= 100m -> subtotal * 0.10m
        | _ -> 0m

    let calculate input =
        let subtotal = input.UnitPrice * decimal input.Quantity
        subtotal - discount input.Tier subtotal
