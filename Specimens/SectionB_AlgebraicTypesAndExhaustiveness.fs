namespace FsAssay.Specimens.SectionB

open System

// FSA2008 — Enum Instead of DU
type PaymentStateEnum =
    | Unpaid = 0
    | Authorized = 1
    | Captured = 2

// FSA2009 — Exhaustiveness Evasion
type Shape = Circle of float | Rectangle of float * float | Triangle of float

module Exhaustiveness =
    let getArea shape =
        match shape with
        | Circle r -> Math.PI * r * r
        | _ -> 0.0

// FSA2010 — Object Erasure
module ObjectErasure =
    let processData (data: obj) =
        // EXPECT: FSA2017
        if data.GetType() = typeof<string> then "string" else "other"

// FSA2011 — Conditional Dispatch on Sum Types
module ConditionalDispatch =
    let handleShape (shape: Shape) =
        // EXPECT: REMOVED
        if shape.GetType().Name = "Circle" then 1.0 else 0.0
