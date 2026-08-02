namespace FsAssay.Specimens.SectionA

open System

// FSA2001 — Missing Computation Expression
module MissingCE =
    let processUser (idOpt: int option) =
        // EXPECT: REMOVED
        match idOpt with
        | Some id ->
            match Some (id * 2) with
            | Some val2 -> Some (val2 + 10)
            | None -> None
        | None -> None

// FSA2002 — Service Interface Obsession
// EXPECT: REMOVED
type IClockService =
    abstract member GetNow : unit -> DateTime

// FSA2003 — Signature Blindness
module SignatureBlindness =
    // EXPECT: REMOVED
    type AccountId = string
    let transfer (source: AccountId) (target: AccountId) (amount: decimal) = ()

// FSA2004 — Flag-Based State Machine
type OrderStateFlags = {
    IsCreated: bool
    IsPaid: bool
    IsShipped: bool
    IsCancelled: bool
}

// FSA2006 — Nested Function Application
module NestedCalls =
    let add1 x = x + 1
    // EXPECT: REMOVED
    let compute x = add1 (add1 (add1 (add1 x)))

// FSA2007 — Missed Active Pattern
module MissedActivePattern =
    let classifyScore score =
        // EXPECT: REMOVED
        if score > 90 then "S"
        elif score > 80 then "A"
        elif score > 70 then "B"
        else "F"
