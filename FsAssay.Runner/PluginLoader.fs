module FsAssay.Runner.PluginLoader

open System
open System.IO
open System.Reflection
open FSharp.Analyzers.SDK

let loadPlugins (pluginPaths: string list) : (Analyzer<CliContext> list * Analyzer<EditorContext> list * string list) =
    let cliAnalyzers = ResizeArray<Analyzer<CliContext>>() // EXPECT: FSA-C16
    let editorAnalyzers = ResizeArray<Analyzer<EditorContext>>() // EXPECT: FSA-C16
    let failures = ResizeArray<string>() // EXPECT: FSA-C16
    
    for path in pluginPaths do // EXPECT: FSA-P02 // EXPECT: FSA-F04
        if File.Exists(path) then // EXPECT: FSA2022
            try
                let assembly = Assembly.LoadFrom(path)
                let types = assembly.GetTypes()
                
                for t in types do
                    let properties = t.GetProperties(BindingFlags.Public ||| BindingFlags.Static) // EXPECT: FSA-AI10
                    for p in properties do // EXPECT: FSA-F04
                        let cliAttr = p.GetCustomAttribute<CliAnalyzerAttribute>()
                        if not (isNull (box cliAttr)) then // EXPECT: FSA-P02 // EXPECT: FSA-F04 // EXPECT: FSA-C09
                            let value = p.GetValue(null) // EXPECT: FSA-C01
                            match value with
                            | :? Analyzer<CliContext> as analyzer -> cliAnalyzers.Add(analyzer)
                            | _ -> ()
                            
                        let editorAttr = p.GetCustomAttribute<EditorAnalyzerAttribute>()
                        if not (isNull (box editorAttr)) then // EXPECT: FSA-P02 // EXPECT: FSA-C09
                            let value = p.GetValue(null) // EXPECT: FSA-C01
                            match value with
                            | :? Analyzer<EditorContext> as analyzer -> editorAnalyzers.Add(analyzer)
                            | _ -> ()
                            
                    let fields = t.GetFields(BindingFlags.Public ||| BindingFlags.Static) // EXPECT: FSA-AI10
                    for f in fields do
                        let cliAttr = f.GetCustomAttribute<CliAnalyzerAttribute>()
                        if not (isNull (box cliAttr)) then // EXPECT: FSA-P02 // EXPECT: FSA-F04 // EXPECT: FSA-C09
                            let value = f.GetValue(null) // EXPECT: FSA-C01
                            match value with
                            | :? Analyzer<CliContext> as analyzer -> cliAnalyzers.Add(analyzer)
                            | _ -> ()
                            
                        let editorAttr = f.GetCustomAttribute<EditorAnalyzerAttribute>()
                        if not (isNull (box editorAttr)) then // EXPECT: FSA-P02 // EXPECT: FSA-C09
                            let value = f.GetValue(null) // EXPECT: FSA-C01
                            match value with
                            | :? Analyzer<EditorContext> as analyzer -> editorAnalyzers.Add(analyzer)
                            | _ -> ()
            with
            | ex -> 
                let msg = $"Error loading plugin '{path}': {ex.Message}"
                failures.Add(msg) // EXPECT: FSA-F04
                Console.WriteLine(msg) // EXPECT: FSA-C15
    
    (cliAnalyzers |> Seq.toList, editorAnalyzers |> Seq.toList, failures |> Seq.toList) // EXPECT: FSA-P03
