module FsAssay.Analyzers.Suppression

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Symbols
open System

open FsAssay.Analyzers.Domain

let extractSuppressions (attrs: seq<FSharpAttribute>) =
    attrs
    |> Seq.choose (fun a ->
        let name = try a.AttributeType.LogicalName with _ -> ""
        let fullName = try a.AttributeType.FullName with _ -> ""
        if name.Contains("SuppressMessage") || fullName.Contains("SuppressMessage") then
            let args = a.ConstructorArguments
            printfn "DEBUG: Found SuppressMessage. args.Count=%d" args.Count
            if args.Count >= 2 then
                let category = string (snd args.[0]) |> fun s -> s.Trim('"')
                let checkId = string (snd args.[1]) |> fun s -> s.Trim('"')
                printfn "DEBUG: category=%s, checkId=%s" category checkId
                if category = "FsAssay" then Some checkId else None
            else None
        elif name.Contains("Profile") || fullName.Contains("Profile") then
            let args = a.ConstructorArguments
            if args.Count >= 1 then
                let profile = string (snd args.[0]) |> fun s -> s.Trim('"')
                Some ("PROFILE:" + profile)
            else None
        else None)
    |> Seq.toList

let isSuppressed sups code =
    sups |> List.contains code ||
    (sups |> List.contains "PROFILE:interop" && (code = "FSA-C01" || code = "FSA-C16")) ||
    (sups |> List.contains "PROFILE:shell" && (code = "FSA-ML01" || code = "FSA-B01" || code = "FSA-C14" || code = "FSA-1301" || code = "FSA-1402" || code = "FSA-C15" || code = "FSA-C16")) ||
    (sups |> List.contains "PROFILE:oracle" && (code = "FSA-1301" || code = "FSA-C15")) ||
    (sups |> List.contains "PROFILE:cli" && (code = "FSA-1402" || code = "FSA-C15")) ||
    (sups |> List.contains "PROFILE:test" && (code = "FSA-C06" || code = "FSA-F04" || code = "FSA-C01")) ||
    (sups |> List.contains "PROFILE:etl" && (code = "FSA-C10" || code = "FSA-B01" || code = "FSA-C16")) ||
    (sups |> List.contains "PROFILE:script" && (code = "FSA-E01" || code = "FSA-C15")) ||
    (not (sups |> List.contains "PROFILE:core") && code = "FSA-C02")


