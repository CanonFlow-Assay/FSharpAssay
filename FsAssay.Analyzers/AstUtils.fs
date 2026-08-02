module FsAssay.Analyzers.AstUtils

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open System

open FsAssay.Analyzers.Domain

let mkLocated finding (r: range) =
    if r.StartLine = 0 then None
    else Some { Finding = finding; Range = r }


let isInsideRange (r: range) (ranges: range list) =
    ranges |> List.exists (fun astRange -> 
        Range.rangeContainsRange astRange r)


