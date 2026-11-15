namespace Refresh.Domain

open System

[<RequireQualifiedAccess>]
type Decision = Approved of reference: string * amount: decimal | Rejected of reason: string

module Approval =
    let decide limit reference amount =
        if String.IsNullOrWhiteSpace reference then Decision.Rejected "reference-required"
        elif amount <= 0m then Decision.Rejected "amount-must-be-positive"
        elif amount > limit then Decision.Rejected "approval-limit-exceeded"
        else Decision.Approved(reference.Trim(), amount)
