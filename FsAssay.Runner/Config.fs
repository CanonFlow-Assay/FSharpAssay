namespace FsAssay.Runner

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FsAssay.Analyzers

module Config =
    type PolicyConfig = {
        exclude: string[]
        profile: string
        boundaries: BoundaryPolicy.Selectors
        directory: string
    }
    let profiles = set ["core"; "shell"; "oracle"; "api"; "interop"; "cli"; "etl"; "test"; "script"]
    let defaultConfig = {
        exclude = [|"**/obj/**"; "**/bin/**"; "**/AssemblyAttributes.fs"|]
        profile = "core"
        boundaries = BoundaryPolicy.empty
        directory = ""
    }
    let rec findConfig directory =
        let candidate = Path.Combine(directory, ".fsassayrc")
        if File.Exists candidate then Some candidate
        else
            match Directory.GetParent directory with
            | null -> None
            | parent -> findConfig parent.FullName

    let parse directory (json: string) =
        try
            use document = JsonDocument.Parse json
            let root = document.RootElement
            if root.ValueKind <> JsonValueKind.Object then invalidOp "scan configuration must be an object"
            let names = root.EnumerateObject() |> Seq.map _.Name |> Seq.toList
            if names.Length <> (List.distinct names).Length then invalidOp "duplicate scan configuration field"
            let known = set ["exclude"; "profile"; "coreProjects"; "shellProjects"; "coreNamespaces"; "shellNamespaces"]
            if names |> List.exists (known.Contains >> not) then invalidOp "unknown scan configuration field"
            let strings (name: string) fallback =
                match root.TryGetProperty name with
                | false, _ -> fallback
                | true, value when value.ValueKind = JsonValueKind.Array ->
                    value.EnumerateArray() |> Seq.map (fun item ->
                        if item.ValueKind <> JsonValueKind.String || String.IsNullOrWhiteSpace(item.GetString()) then invalidOp (name + " requires nonempty strings")
                        item.GetString()) |> Seq.toArray
                | _ -> invalidOp (name + " must be an array")
            let profile =
                match root.TryGetProperty "profile" with
                | false, _ -> "core"
                | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
                | _ -> invalidOp "profile must be a string"
            if not (profiles.Contains profile) then invalidOp ("unknown profile: " + profile)
            let boundaries: BoundaryPolicy.Selectors = {
                CoreProjects = strings "coreProjects" [||] |> Array.toList
                ShellProjects = strings "shellProjects" [||] |> Array.toList
                CoreNamespaces = strings "coreNamespaces" [||] |> Array.toList
                ShellNamespaces = strings "shellNamespaces" [||] |> Array.toList
            }
            match BoundaryPolicy.validate boundaries with
            | [] -> Ok {exclude = strings "exclude" defaultConfig.exclude; profile = profile; boundaries = boundaries; directory = directory}
            | errors -> Error (String.concat "; " errors)
        with error -> Error error.Message

    let tryLoadConfig target =
        let full = Path.GetFullPath target
        let directory = if Directory.Exists full then full else Path.GetDirectoryName full
        match findConfig directory with
        | None -> Ok {defaultConfig with directory = directory}
        | Some path ->
            try parse (Path.GetDirectoryName path) (File.ReadAllText path)
            with error -> Error error.Message

    let loadConfig target =
        match tryLoadConfig target with
        | Ok config -> config
        | Error error -> invalidOp ("Invalid .fsassayrc: " + error)

    /// Segment-aware matching: a literal component cannot exclude a similarly named directory.
    let isExcluded (root: string) (patterns: string[]) (path: string) =
        let relative = Path.GetRelativePath(root, path).Replace('\\', '/')
        patterns |> Array.exists (fun pattern ->
            let pattern = pattern.Replace('\\', '/').Trim('/')
            if not (pattern.Contains('*') || pattern.Contains('?') || pattern.Contains('/')) then
                relative.Split('/') |> Array.contains pattern
            else
                let expression = Regex.Escape(pattern).Replace(@"\*\*/", "(?:.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]")
                Regex.IsMatch(relative, "^" + expression + "$", RegexOptions.CultureInvariant))
