namespace FsAssay.Runner

open System
open System.ComponentModel
open System.Globalization
open System.IO

module GitHistory =
    let private additionTime file =
        let path = Path.GetFullPath(file)
        let directory = Path.GetDirectoryName(path)
        let arguments =
            [ "--no-pager"; "--literal-pathspecs"; "log"; "--no-show-signature"
              "--diff-filter=A"; "--format=%at"; "-1"; "--"; Path.GetFileName(path) ]

        try
            let result = Processes.run directory "git" arguments 5000
            if result.ExitCode <> 0 || result.TimedOut then None
            else
                match Int64.TryParse(result.Stdout.Trim(), NumberStyles.None, CultureInfo.InvariantCulture) with
                | true, timestamp when timestamp > 0L -> Some timestamp
                | _ -> None
        with
        | :? Win32Exception
        | :? IOException -> None

    let additionTimes files =
        files
        |> List.choose (fun file -> additionTime file |> Option.map (fun timestamp -> file, timestamp))
        |> Map.ofList
