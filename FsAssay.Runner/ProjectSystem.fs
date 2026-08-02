namespace FsAssay.Runner

open System.IO
open Ionide.ProjInfo
open FSharp.Compiler.CodeAnalysis

module ProjectSystem =

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
        | _ when path.EndsWith(".sln") || path.EndsWith(".slnx") -> loadSolution path
        | _ when path.EndsWith(".fsproj") -> loadProjects [path]
        | _ when File.Exists(path) -> [] // EXPECT: FSA2022
        | _ -> 
            let projs = Directory.GetFiles(path, "*.fsproj", SearchOption.AllDirectories) // EXPECT: FSA2022
            if projs.Length = 0 then []
            else projs |> Array.toList |> loadProjects
