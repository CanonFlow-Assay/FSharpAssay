module FsAssay.Runner.PluginLoader

open System
open System.IO
open System.Reflection
open FSharp.Analyzers.SDK

let loadPlugins (pluginPaths: string list) : (Analyzer<CliContext> list * Analyzer<EditorContext> list) =
    let cliAnalyzers = ResizeArray<Analyzer<CliContext>>()
    let editorAnalyzers = ResizeArray<Analyzer<EditorContext>>()
    
    for path in pluginPaths do
        if File.Exists(path) then
            try
                let assembly = Assembly.LoadFrom(path)
                let types = assembly.GetTypes()
                
                for t in types do
                    let properties = t.GetProperties(BindingFlags.Public ||| BindingFlags.Static)
                    for p in properties do
                        let cliAttr = p.GetCustomAttribute<CliAnalyzerAttribute>()
                        if not (isNull (box cliAttr)) then
                            let value = p.GetValue(null)
                            match value with
                            | :? Analyzer<CliContext> as analyzer -> cliAnalyzers.Add(analyzer)
                            | _ -> ()
                            
                        let editorAttr = p.GetCustomAttribute<EditorAnalyzerAttribute>()
                        if not (isNull (box editorAttr)) then
                            let value = p.GetValue(null)
                            match value with
                            | :? Analyzer<EditorContext> as analyzer -> editorAnalyzers.Add(analyzer)
                            | _ -> ()
                            
                    let fields = t.GetFields(BindingFlags.Public ||| BindingFlags.Static)
                    for f in fields do
                        let cliAttr = f.GetCustomAttribute<CliAnalyzerAttribute>()
                        if not (isNull (box cliAttr)) then
                            let value = f.GetValue(null)
                            match value with
                            | :? Analyzer<CliContext> as analyzer -> cliAnalyzers.Add(analyzer)
                            | _ -> ()
                            
                        let editorAttr = f.GetCustomAttribute<EditorAnalyzerAttribute>()
                        if not (isNull (box editorAttr)) then
                            let value = f.GetValue(null)
                            match value with
                            | :? Analyzer<EditorContext> as analyzer -> editorAnalyzers.Add(analyzer)
                            | _ -> ()
            with
            | ex -> 
                Console.WriteLine($"Error loading plugin '{path}': {ex.Message}")
    
    (cliAnalyzers |> Seq.toList, editorAnalyzers |> Seq.toList)
