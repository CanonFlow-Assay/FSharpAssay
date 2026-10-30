module Approval.Domain

type Request = { Reference: string; AmountCents: int64 }
[<RequireQualifiedAccess>]
type Fault = InvalidAmount | MissingReference
[<RequireQualifiedAccess>]
type Decision = Approved of reference: string * cents: int64 | NeedsReview of reference: string * cents: int64 | Rejected of Fault

let decide request =
    if request.AmountCents <= 0L then Decision.Rejected Fault.InvalidAmount
    elif request.AmountCents <= 10000L then Decision.Approved(request.Reference, request.AmountCents)
    else Decision.NeedsReview(request.Reference, request.AmountCents)
