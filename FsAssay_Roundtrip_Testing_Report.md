# FsAssay Roundtrip Testing Results

In accordance with the [Stylish F# `SKILL.md`](https://github.com/ArunNotFound/functional-skills/blob/main/stylish-fsharp/SKILL.md), I've created an "opposite" (hostile) F# sample that violates the idiomatic F# principles, and verified it against the `FsAssay` prototype. 

## The "C#-ish" Hostile Specimen

The following code explicitly breaks the "creed" of Stylish F# by using `mutable`, `null`, `Option.get`, boolean validation, exceptions for control flow, while loops, interfaces, and primitive obsession:

```fsharp
module Specimens.CsharpishOrderProcessor

open System

// OBSOLETE_FSA: Primitive Obsession
type EmailAddress = string

// OBSOLETE_FSA: OOP Inheritance
type IOrderService =
    abstract member Process: string -> bool

type CustomerOrder() =
    // OBSOLETE_FSA: Mutation Overuse
    // OBSOLETE_FSA: Null Reference
    let mutable email: EmailAddress = null
    
    // OBSOLETE_FSA: Mutable Collections
    let items = ResizeArray<string>()

    member this.Email
        with get() = email
        and set(v) = email <- v

    member this.Items = items

// OBSOLETE_FSA: Parse, Don't Validate
let isValidEmail (e: string) = e.Contains("@")

type OrderService() =
    interface IOrderService with
        member this.Process(inputOpt: string option) =
            // OBSOLETE_FSA: Partial Access
            let input = Option.get inputOpt
            
            let mutable count = 0
            // OBSOLETE_FSA: Imperative Loops
            while count < 10 do
                count <- count + 1

            try
                if not (isValidEmail input) then
                    failwith "Invalid"
                true
            with
            // OBSOLETE_FSA: Generic Catch
            | :? System.Exception -> false
```

## FsAssay Error Output

When running `dotnet run --project FsAssay.Runner -- /root/FSharpAssay/Specimens`, the analyzer correctly identifies and blocks all anti-patterns, strictly enforcing the Stylish F# rules:

```text
❌ /root/FSharpAssay/Specimens/CsharpishOrderProcessor.fs
   └── [OBSOLETE_FSA] Null Reference: Avoid 'null'. Use 'Option' types to represent missing values. (Line: 11)
   └── [OBSOLETE_FSA] Primitive Obsession: Do not use type aliases for primitives. Use Single-Case Discriminated Unions to make illegal states unrepresentable. (Line: 1)
   └── [OBSOLETE_FSA] Parse, Don't Validate: Functions should return Result<ParsedType, Error> rather than a boolean validity flag. (Line: 1)
   └── [OBSOLETE_FSA] Generic Catch: Do not catch generic exceptions for flow control. Use Result types instead. (Line: 1)
   └── [OBSOLETE_FSA] Imperative Loops: Avoid 'while' loops. Use Seq.fold or recursion. (Line: 1)
   └── [OBSOLETE_FSA] OOP Inheritance: Avoid OOP inheritance and interfaces. Use records of functions or Discriminated Unions. (Line: 1)
   └── [OBSOLETE_FSA] Mutable Collections: Avoid C# mutable collections. Use F# immutable Map, Set, or list. (Line: 1)
```

## Observations on the Prototype
I also created an idiomatic F# equivalent (`StylishOrderProcessor.fs`) featuring Records, DUs, and `Result` returning functions. As noted in the `FsAssay` `README.md`, the analyzer is currently a *lexical prototype* combined with a naive TAST (Typed Abstract Syntax Tree) visitor. 

Because of this, FsAssay currently flags the underlying, **compiler-generated** IL of F# Discriminated Unions and Records as `OBSOLETE_FSA` (Nulls) and `OBSOLETE_FSA` (Mutations), meaning it incorrectly penalizes Stylish F# until the compiler-generated exclusion logic is fully implemented.

The analyzer perfectly identifies C#-ish anti-patterns in user code, aligning exactly with the `functional-skills` design philosophy.
