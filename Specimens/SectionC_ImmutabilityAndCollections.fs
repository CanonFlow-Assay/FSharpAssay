namespace FsAssay.Specimens.SectionC

open System
open System.Collections.Generic

module ImmutabilityAndCollections =
    // FSA2012 — Mutable Collection Intrusion
    // EXPECT: REMOVED
    let mutable globalItems = ResizeArray<string>()

    // FSA2014 — Imperative Accumulation
    let sumNumbers (numbers: int list) =
        // EXPECT: REMOVED
        let mutable total = 0
        // EXPECT: REMOVED
        // EXPECT: REMOVED
        for n in numbers do
            total <- total + n
        total

    // FSA2016 — Unsafe Runtime Cast
    let castValue (o: obj) =
        // EXPECT: FSA2016
        o :?> string
