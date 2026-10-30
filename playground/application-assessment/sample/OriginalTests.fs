module Approval.OriginalTests
open Approval.Domain

let cases = [
    "amount-positive", "AMOUNT", fun () -> decide { Reference = "invoice-1"; AmountCents = 1L } = Decision.Approved("invoice-1", 1L)
    "amount-zero", "AMOUNT", fun () -> decide { Reference = "invoice-1"; AmountCents = 0L } = Decision.Rejected Fault.InvalidAmount
    "limit-low", "LIMIT", fun () -> decide { Reference = "invoice-1"; AmountCents = 9999L } = Decision.Approved("invoice-1", 9999L)
    "limit-boundary", "LIMIT", fun () -> decide { Reference = "invoice-1"; AmountCents = 10000L } = Decision.Approved("invoice-1", 10000L)
    "limit-high", "LIMIT", fun () -> decide { Reference = "invoice-1"; AmountCents = 10001L } = Decision.NeedsReview("invoice-1", 10001L)
]
