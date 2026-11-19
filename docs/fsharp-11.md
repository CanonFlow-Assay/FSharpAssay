# F# 11 and .NET 11

Maintained projects compile with stable SDK `11.0.100` and target `net11.0`.
The runner and tool archive run on .NET 11. For application compatibility,
qualification installs that archive and verifies an independent .NET 11 sample
and a disposable .NET 10 variant. The latter needs a .NET 10 runtime for its test
executables. The runner itself still uses the .NET 11 runtime in that lane.

The producer pins FSharp.Compiler.Service `43.13.404`, FSharp.Analyzers.SDK
`0.43.0` and Ionide.ProjInfo.FCS `0.76.0`. Those published packages require
FSharp.Core `10.1.404` inside the analyzer and runner graph. The SDK still
compiles that graph with F# 11. Standalone consumers and independent samples
can use FSharp.Core `11.0.101` without the compiler-service dependency.

Microsoft describes improvements in compiler work, text and array handling,
delegate interop, and async composition. This migration uses the stable language
mode. Features that need F# 11.2 preview on SDK `11.0.100` remain outside the
active configuration. [Microsoft's F# 11 release notes](https://learn.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-11)

The historical M1–M7 evidence and its exact source bindings remain historical.
The `Specimens` project is a negative compilation corpus. Its compiler errors
are retained separately from the portable build. Web and Desktop compilation
does not promote those surfaces or admit any analyzer rule.
