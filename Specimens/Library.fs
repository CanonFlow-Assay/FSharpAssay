module Specimens.Library
open System

let badMutation () =
    let mutable x = 1 // EXPECT: REMOVED
    x <- 2 // EXPECT: REMOVED
    x

let goodMutation () =
    let x = 1
    x

let badNull () =
    let y = Unchecked.defaultof<string> // EXPECT: REMOVED
    let z : obj = null // EXPECT: REMOVED
    y

let goodOption () =
    let o = None
    o

type ProfileAttribute(name: string) =
    inherit Attribute()

[<Profile("interop")>]
let interopNulls () =
    let mutable y = 5 // no expect because suppressed
    let z : obj = null // no expect because suppressed
    z
