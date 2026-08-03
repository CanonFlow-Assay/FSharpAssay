namespace FsAssay.Runner

open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq
open Ionide.ProjInfo
open FSharp.Compiler.CodeAnalysis

module ProjectSystem =

    let private legacySolutionProjectPaths (path: string) =
        let baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path))
        let projectPattern = Regex("^\\s*Project\\(\"[^\"]+\"\\)\\s*=\\s*\"[^\"]+\",\\s*\"([^\"]+\\.fsproj)\"", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)
        File.ReadLines(path)
        |> Seq.choose (fun line ->
            let matchResult = projectPattern.Match(line)
            if matchResult.Success then
                let projectPath = matchResult.Groups.[1].Value.Replace('\\', Path.DirectorySeparatorChar)
                let fullPath = Path.GetFullPath(Path.Combine(baseDirectory, projectPath))
                if File.Exists(fullPath) then Some fullPath else None
            else None)
        |> Seq.distinct
        |> Seq.sort
        |> Seq.toList

    let discoverProjectPaths (path: string) =
        if path.EndsWith(".fsproj") then
            [ Path.GetFullPath(path) ]
        elif path.EndsWith(".sln") && File.Exists(path) then
            legacySolutionProjectPaths path
        elif path.EndsWith(".slnx") && File.Exists(path) then
            let baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path))
            XDocument.Load(path).Descendants(XName.Get("Project"))
            |> Seq.choose (fun element ->
                match element.Attribute(XName.Get("Path")) with
                | null -> None
                | attribute when attribute.Value.EndsWith(".fsproj") -> Some(Path.GetFullPath(Path.Combine(baseDirectory, attribute.Value)))
                | _ -> None)
            |> Seq.distinct
            |> Seq.sort
            |> Seq.toList
        elif Directory.Exists(path) then
            Directory.GetFiles(path, "*.fsproj", SearchOption.AllDirectories) // EXPECT: FSA2022
            |> Array.filter (fun project -> not (project.Contains("/obj/") || project.Contains("\\obj\\")))
            |> Array.map Path.GetFullPath
            |> Array.distinct
            |> Array.sort
            |> Array.toList
        else []

    let projectTargetFrameworks (projectPath: string) =
        try
            let document = XDocument.Load(projectPath)
            document.Descendants()
            |> Seq.filter (fun element -> element.Name.LocalName = "TargetFramework" || element.Name.LocalName = "TargetFrameworks")
            |> Seq.collect (fun element -> element.Value.Split(';'))
            |> Seq.map _.Trim()
            |> Seq.filter (System.String.IsNullOrWhiteSpace >> not)
            |> Seq.distinct
            |> Seq.sort
            |> Seq.toArray
        with _ -> [||]

    let projectClass (projectPath: string) =
        let name = Path.GetFileNameWithoutExtension(projectPath).ToLowerInvariant()
        if name.Contains("test") then "test"
        elif name.Contains("analyzer") then "analyzer"
        elif name.Contains("plugin") then "plugin"
        elif name.Contains("runner") then "cli"
        else "other"

    let loadProjects (paths: string list) =
        let toolsPath = None |> Init.init (Directory.GetCurrentDirectory() |> DirectoryInfo) // EXPECT: FSA2022
        let loader = WorkspaceLoader.Create(toolsPath, [])
        let parsed = loader.LoadProjects paths
        
        parsed 
        |> Seq.map (fun p -> FCS.mapToFSharpProjectOptions p parsed)
        |> Seq.toList // EXPECT: FSA-P03

    let loadSolution (path: string) =
        let toolsPath = None |> Init.init (Directory.GetCurrentDirectory() |> DirectoryInfo) // EXPECT: FSA2022
        let loader = WorkspaceLoader.Create(toolsPath, [])
        let parsed = loader.LoadSln path
        
        parsed 
        |> Seq.map (fun p -> FCS.mapToFSharpProjectOptions p parsed)
        |> Seq.toList // EXPECT: FSA-P03

    let getTargetProjects (path: string) =
        match path with
        | _ when path.EndsWith(".sln") -> discoverProjectPaths path |> loadProjects
        | _ when path.EndsWith(".slnx") -> loadSolution path
        | _ when path.EndsWith(".fsproj") -> loadProjects [path]
        | _ when File.Exists(path) -> [] // EXPECT: FSA2022
        | _ -> 
            let projs = Directory.GetFiles(path, "*.fsproj", SearchOption.AllDirectories) // EXPECT: FSA2022
            if projs.Length = 0 then []
            else projs |> Array.toList |> loadProjects
