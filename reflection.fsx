#r "nuget: FSharpLint.Core, 0.27.0"
open FSharpLint.Application
open System.Reflection
open FSharpLint.Framework.Suggestion

let t4 = typeof<WarningDetails>
for p in t4.GetProperties() do
    printfn "WarningDetails Prop: %s %s" p.Name p.PropertyType.Name
