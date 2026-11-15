namespace FsAssay.Runner

open System
open System.IO
open System.Text.Json

module Doctor =
    let inspect root =
        let toolchain = Authority.currentToolchain root
        let rec find directory name =
            let path = Path.Combine(directory, name)
            if File.Exists path then Some path
            else match Directory.GetParent directory with null -> None | parent -> find parent.FullName name
        let pin =
            match find root "global.json" with
            | None -> None
            | Some path ->
                use document = JsonDocument.Parse(File.ReadAllBytes path)
                Some(document.RootElement.GetProperty("sdk").GetProperty("version").GetString())
        let errors = [
            if toolchain.SdkVersion = "unavailable" then "local .NET SDK is unavailable"
            if toolchain.FSharpCompilerServiceVersion = "unavailable" then "F# compiler service is unavailable"
            match pin with Some expected when expected <> toolchain.SdkVersion -> "SDK differs from global.json: expected " + expected | _ -> ()
            match Config.tryLoadConfig root with Error error -> "Invalid .fsassayrc: " + error | _ -> ()
        ]
        let policyStatus =
            match find root "fsassay-policy.lock.json" with
            | None -> "unavailable; verification needs an explicit reviewed policy lock"
            | Some path ->
                match Authority.loadPolicy path with
                | Authority.PolicyLoaded _ -> "loaded; zero blocking/advisory rule admissions"
                | Authority.PolicyInvalid (_, error) -> "invalid: " + error
                | Authority.PolicyUnavailable error -> error
        let msbuild = Processes.run root "dotnet" ["msbuild"; "-version"; "-nologo"] 30000
        let errors = if msbuild.TimedOut || msbuild.ExitCode <> 0 then errors @ ["MSBuild is unavailable"] else errors
        let output =
            [
                yield "FsAssay doctor"; yield "ToolVersion: " + ProductIdentity.Version
                yield "RuntimeVersion: " + toolchain.RuntimeVersion; yield "SdkVersion: " + toolchain.SdkVersion
                yield "FSharpCompilerServiceVersion: " + toolchain.FSharpCompilerServiceVersion
                yield "MSBuildVersion: " + msbuild.Stdout.Trim()
                yield "Policy: " + policyStatus
                yield "AnalysisNetworkDefault: offline"; yield "SourceUpload: none"; yield "FsAssayTelemetry: none"
                yield "RequiredTests: verify executes registered test projects; analysis leaves tests notRun"
                yield "Status: " + (if errors.IsEmpty then "healthy" else "ToolFailure")
                yield! errors
            ]
            |> String.concat "\n"
        output, errors
