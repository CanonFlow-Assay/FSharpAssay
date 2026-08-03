namespace FsAssay.Playground.Shape.Application

open FsAssay.Playground.Shape.Domain

/// Compatibility boundary retained while callers converge on Quote.calculate.
[<RequireQualifiedAccess>]
module LegacyPricing =
    let calculate unitPrice quantity isPreferred =
        let tier = if isPreferred then CustomerTier.Preferred else CustomerTier.Standard
        Quote.calculate { UnitPrice = unitPrice; Quantity = quantity; Tier = tier }
