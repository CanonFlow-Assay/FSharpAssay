namespace FsAssay.Specimens.SectionE

open System

module ErrorsAndResourceHandling =
    // FSA2021 / OBSOLETE_FSA — Generic Catch
    let handleOperation () =
        try
            10 / 0
        with
        // EXPECT: REMOVED
        | :? System.Exception as e -> -1

    // FSA2022 / FSA2029 — Exception Throwing in Domain
    let validateAge age =
        if age < 0 then
            // EXPECT: REMOVED
            failwith "Age cannot be negative"
        else age

    // FSA2024 — Statement-Style Branching
    let getStatus (active: bool) =
        // EXPECT: REMOVED
        let mutable status = "Inactive"
        if active then
            status <- "Active"
        status
