namespace FsAssay.Specimens.SectionH

open System

module StructureAndReadability =
    // FSA2040 — Redundant Lambda
    let square x = x * x
    let applySquare list =
        list |> List.map (fun x -> square x)
