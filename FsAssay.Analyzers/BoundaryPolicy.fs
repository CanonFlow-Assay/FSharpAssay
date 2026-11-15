namespace FsAssay.Analyzers

open System
open System.IO

/// Classification over declared identities; never discovers or loads projects.
module BoundaryPolicy =
    type Selectors = {
        CoreProjects: string list
        ShellProjects: string list
        CoreNamespaces: string list
        ShellNamespaces: string list
    }
    [<RequireQualifiedAccess>]
    type Scope = Core | Shell | Default
    let empty = { CoreProjects = []; ShellProjects = []; CoreNamespaces = []; ShellNamespaces = [] }
    let private pathComparison = if OperatingSystem.IsWindows() then StringComparison.OrdinalIgnoreCase else StringComparison.Ordinal
    let normalizeProject (value: string) =
        let rec normalize acc = function
            | [] -> acc |> List.rev |> String.concat "/"
            | "." :: rest -> normalize acc rest
            | ".." :: rest ->
                match acc with
                | head :: tail when head <> ".." -> normalize tail rest
                | _ -> normalize (".." :: acc) rest
            | segment :: rest -> normalize (segment :: acc) rest
        value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries) |> Array.toList |> normalize []
    let private projectMatches selector project = String.Equals(normalizeProject selector, normalizeProject project, pathComparison)
    let private namespaceMatches (selector: string) (identity: string) = identity = selector || identity.StartsWith(selector + ".", StringComparison.Ordinal)
    let validate selectors =
        [
            for values in [selectors.CoreProjects; selectors.ShellProjects] do
                for value in values do
                    if String.IsNullOrWhiteSpace value || Path.IsPathRooted value || value.Contains(':') || value.IndexOfAny([|'*'; '?'|]) >= 0 || not (value.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)) then
                        "project selectors must be literal relative .fsproj paths"
                if values |> List.map normalizeProject |> List.distinct |> List.length <> values.Length then "duplicate project selector"
            for values in [selectors.CoreNamespaces; selectors.ShellNamespaces] do
                if values |> List.exists (fun value -> String.IsNullOrWhiteSpace value || value.Split('.') |> Array.exists String.IsNullOrWhiteSpace || value.IndexOfAny([|'*'; '?'; '/'; '\\'|]) >= 0) then "namespace selectors must be literal nonempty identities"
                if List.distinct values <> values then "duplicate namespace selector"
            for core in selectors.CoreProjects do
                if selectors.ShellProjects |> List.exists (projectMatches core) then "contradictory core/shell project selector: " + core
            for core in selectors.CoreNamespaces do
                if List.contains core selectors.ShellNamespaces then "contradictory core/shell namespace selector: " + core
        ] |> List.distinct
    let validateGraph selectors projects =
        selectors.CoreProjects @ selectors.ShellProjects
        |> List.choose (fun selector -> if projects |> List.exists (projectMatches selector) then None else Some ("boundary selector is outside the evaluated graph: " + selector))
    let classify selectors project namespaceIdentity =
        if selectors.ShellProjects |> List.exists (fun selector -> projectMatches selector project) then Scope.Shell
        elif selectors.ShellNamespaces |> List.exists (fun selector -> namespaceMatches selector namespaceIdentity) then Scope.Shell
        elif selectors.CoreNamespaces |> List.exists (fun selector -> namespaceMatches selector namespaceIdentity) then Scope.Core
        elif selectors.CoreProjects |> List.exists (fun selector -> projectMatches selector project) then Scope.Core
        elif selectors = empty then Scope.Core
        else Scope.Default
