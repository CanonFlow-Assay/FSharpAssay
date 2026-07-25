namespace FsAssay.Analyzers

module Catalogue =
    open FsAssay.Analyzers.Domain
    // A versioned catalogue to avoid heuristic guesses on unfamiliar libraries.
    // Architectural Rules are defined in Domain.fs
    // Instead of guessing if a method is effectful or mutable, we define known sources and sinks.
    
    let EffectfulMethods = 
        Set.ofList [
            "System.Console.WriteLine"
            "System.Console.Write"
            "System.Console.ReadLine"
            "System.IO.File.WriteAllText"
            "System.IO.File.ReadAllText"
            "System.IO.File.Delete"
            "Microsoft.EntityFrameworkCore.DbContext.SaveChanges"
            "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync"
        ]

    let MutableCollections =
        Set.ofList [
            "System.Collections.Generic.Dictionary"
            "System.Collections.Generic.List"
            "System.Collections.Generic.HashSet"
            "System.Collections.Concurrent.ConcurrentDictionary"
        ]

    let isEffectful (fullName: string) =
        EffectfulMethods |> Set.contains fullName

    let isMutableCollection (fullName: string) =
        MutableCollections |> Set.contains fullName
