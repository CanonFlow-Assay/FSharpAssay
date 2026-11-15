namespace FsAssay.Runner

open System
open System.Diagnostics
open System.Threading.Tasks

/// Explicit effects with structured arguments, bounded execution and process-tree cleanup.
module Processes =
    type Result = { ExitCode: int; TimedOut: bool; Stdout: string; Stderr: string }
    let run workingDirectory executable (arguments: string list) (timeoutMilliseconds: int) =
        let executable =
            if executable = "dotnet" && not (String.IsNullOrWhiteSpace Environment.ProcessPath) && System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath) = "dotnet" then Environment.ProcessPath
            else executable
        let start = ProcessStartInfo(executable)
        start.WorkingDirectory <- workingDirectory
        start.UseShellExecute <- false
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        arguments |> List.iter start.ArgumentList.Add
        // Ionide initializes in-process MSBuild variables. Let child CLI commands resolve
        // their own SDK from global.json instead of inheriting that loader state.
        for name in ["MSBUILD_EXE_PATH"; "MSBuildExtensionsPath"; "MSBuildSDKsPath"; "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] do
            start.Environment.Remove name |> ignore
        if System.IO.Path.IsPathFullyQualified executable && System.IO.Path.GetFileNameWithoutExtension executable = "dotnet" then
            start.Environment.["DOTNET_HOST_PATH"] <- executable
            start.Environment.["DOTNET_ROOT"] <- System.IO.Path.GetDirectoryName executable
        start.Environment.["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
        use child = Process.Start start
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        let completed = child.WaitForExit timeoutMilliseconds
        if not completed then
            try child.Kill(true) with _ -> ()
            child.WaitForExit()
        Task.WaitAll [|stdout :> Task; stderr :> Task|]
        {ExitCode = child.ExitCode; TimedOut = not completed; Stdout = stdout.Result; Stderr = stderr.Result}
