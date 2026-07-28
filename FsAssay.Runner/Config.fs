namespace FsAssay.Runner

open System.IO
open System.Text.Json

module Config =
    type PolicyConfig = {
        exclude: string[]
        profile: string
    }

    let defaultConfig = {
        exclude = [| "**/obj/**"; "**/bin/**"; "**/AssemblyAttributes.fs" |]
        profile = "core"
    }

    let rec findConfig (dirPath: string) =
        let configPath = Path.Combine(dirPath, ".fsassayrc") // EXPECT: FSA2022
        if File.Exists(configPath) then Some configPath // EXPECT: FSA2022
        else
            let parent = Directory.GetParent(dirPath) // EXPECT: FSA2022
            if parent <> null then findConfig parent.FullName // EXPECT: FSA2022 // EXPECT: FSA-C01
            else None

    let loadConfig (targetPath: string) =
        let dirPath = if Directory.Exists(targetPath) then targetPath else Path.GetDirectoryName(targetPath) // EXPECT: FSA2022
        match findConfig dirPath with
        | Some configPath ->
            try
                let json = File.ReadAllText(configPath) // EXPECT: FSA2022 // EXPECT: FSA-C15
                let opts = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase) // EXPECT: FSA-F04
                let loaded = JsonSerializer.Deserialize<PolicyConfig>(json, opts)
                Option.ofObj loaded |> Option.defaultValue defaultConfig
            with _ -> defaultConfig
        | None -> defaultConfig

