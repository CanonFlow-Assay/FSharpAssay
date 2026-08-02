module FsAssay.Runner.McpServer

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open FsAssay.Runner.Orchestrator
open FsAssay.Runner.Output

let sendResponse (id: JsonNode) (result: JsonNode) =
    let response = JsonObject()
    response.Add("jsonrpc", JsonValue.Create("2.0")) // EXPECT: FSA-F04
    if not (isNull id) then // EXPECT: FSA-F04 // EXPECT: FSA-C09
        let idNode = JsonNode.Parse(id.ToJsonString())
        response.Add("id", idNode)
    response.Add("result", result) // EXPECT: FSA-F04
    let json = response.ToJsonString()
    Console.WriteLine(json) // EXPECT: FSA-F04 // EXPECT: FSA-C15
    Console.Out.Flush() // EXPECT: FSA2022

let sendError (id: JsonNode) (code: int) (msg: string) =
    let response = JsonObject()
    response.Add("jsonrpc", JsonValue.Create("2.0")) // EXPECT: FSA-F04
    if not (isNull id) then // EXPECT: FSA-F04 // EXPECT: FSA-C09
        let idNode = JsonNode.Parse(id.ToJsonString())
        response.Add("id", idNode)
    let errorObj = JsonObject()
    errorObj.Add("code", JsonValue.Create(code)) // EXPECT: FSA-F04
    errorObj.Add("message", JsonValue.Create(msg)) // EXPECT: FSA-F04
    response.Add("error", errorObj) // EXPECT: FSA-F04
    Console.WriteLine(response.ToJsonString()) // EXPECT: FSA-F04 // EXPECT: FSA-C15
    Console.Out.Flush() // EXPECT: FSA2022

let handleInitialize (id: JsonNode) =
    let result = JsonObject()
    result.Add("protocolVersion", JsonValue.Create("2024-11-05")) // EXPECT: FSA-F04
    let capabilities = JsonObject()
    let tools = JsonObject()
    capabilities.Add("tools", tools) // EXPECT: FSA-F04
    result.Add("capabilities", capabilities) // EXPECT: FSA-F04
    let serverInfo = JsonObject()
    serverInfo.Add("name", JsonValue.Create("fsassay-mcp")) // EXPECT: FSA-F04
    serverInfo.Add("version", JsonValue.Create(ProductIdentity.Version)) // EXPECT: FSA-F04
    result.Add("serverInfo", serverInfo) // EXPECT: FSA-F04
    sendResponse id result

let handleToolsList (id: JsonNode) =
    let result = JsonObject()
    let toolsArray = JsonArray()
    let analyzeTool = JsonObject()
    analyzeTool.Add("name", JsonValue.Create("analyze_fsharp")) // EXPECT: FSA-F04
    analyzeTool.Add("description", JsonValue.Create("Run FsAssay on an F# project to detect anti-patterns and compositional code smells.")) // EXPECT: FSA-F04
    
    let inputSchema = JsonObject()
    inputSchema.Add("type", JsonValue.Create("object")) // EXPECT: FSA-F04
    let props = JsonObject()
    let projectPath = JsonObject()
    projectPath.Add("type", JsonValue.Create("string")) // EXPECT: FSA-F04
    projectPath.Add("description", JsonValue.Create("Absolute path to the .fsproj file or directory containing it.")) // EXPECT: FSA-F04
    props.Add("projectPath", projectPath) // EXPECT: FSA-F04
    inputSchema.Add("properties", props) // EXPECT: FSA-F04
    let required = JsonArray()
    required.Add(JsonValue.Create("projectPath")) // EXPECT: FSA-F04
    inputSchema.Add("required", required) // EXPECT: FSA-F04
    
    analyzeTool.Add("inputSchema", inputSchema) // EXPECT: FSA-F04
    toolsArray.Add(analyzeTool) // EXPECT: FSA-F04
    result.Add("tools", toolsArray) // EXPECT: FSA-F04
    sendResponse id result

let handleToolsCall (id: JsonNode) (paramsNode: JsonNode) =
    try
        let nameNode = paramsNode.["name"]
        if isNull nameNode || nameNode.GetValue<string>() <> "analyze_fsharp" then // EXPECT: FSA-C09
            sendError id -32601 "Tool not found"
        else
            let argsNode = paramsNode.["arguments"]
            let projectPath = argsNode.["projectPath"].GetValue<string>()
            let results = FsAssay.Runner.Orchestrator.analyzeProject projectPath |> Async.RunSynchronously // EXPECT: FSA-C03
            
            // Format results as JSON text
            let sb = System.Text.StringBuilder()
            let mutable c = 0
            let mutable s = 0
            let mutable f = 0
            let mutable violations = []
            
            for res in results do // EXPECT: FSA-P02 // EXPECT: FSA-F04
                match res with
                | FsAssay.Runner.Completed (v, _, _) ->
                    c <- c + 1 // EXPECT: FSA-F04 // EXPECT: FSA-C10
                    violations <- violations @ v // EXPECT: FSA-P01 // EXPECT: FSA-C10
                | FsAssay.Runner.Skipped _ -> s <- s + 1 // EXPECT: FSA-C10
                | FsAssay.Runner.Failed _ -> f <- f + 1 // EXPECT: FSA-C10
                
            sb.AppendLine(sprintf "FsAssay Analysis Results (Completed: %d, Skipped: %d, Failed: %d):" c s f) |> ignore // EXPECT: FSA-F04
            for res in violations do // EXPECT: FSA-P02 // EXPECT: FSA-F04
                sb.AppendLine(sprintf "- [%s] %s at %s:%d" (res.Severity.ToString()) res.Message res.Range.FileName res.Range.StartLine) |> ignore
                
            let result = JsonObject()
            let contentArray = JsonArray()
            let contentObj = JsonObject()
            contentObj.Add("type", JsonValue.Create("text")) // EXPECT: FSA-F04
            contentObj.Add("text", JsonValue.Create(sb.ToString())) // EXPECT: FSA-F04
            contentArray.Add(contentObj) // EXPECT: FSA-F04
            result.Add("content", contentArray) // EXPECT: FSA-F04
            sendResponse id result
    with ex ->
        sendError id -32000 (ex.Message)

let run () =
    try // EXPECT: FSA-S03
        let reader = new StreamReader(Console.OpenStandardInput()) // EXPECT: FSA2022
        while not reader.EndOfStream do // EXPECT: FSA2022
            let line = reader.ReadLine() // EXPECT: FSA2022
            if not (String.IsNullOrWhiteSpace(line)) then
                try
                    let json = JsonNode.Parse(line)
                    let id = json.["id"]
                    let methodNode = json.["method"]
                    if not (isNull methodNode) then // EXPECT: FSA-C09
                        let method = methodNode.GetValue<string>()
                        match method with
                        | "initialize" -> handleInitialize id
                        | "tools/list" -> handleToolsList id
                        | "tools/call" -> handleToolsCall id (json.["params"])
                        | "notifications/initialized" -> ()
                        | _ -> sendError id -32601 "Method not found"
                with ex ->
                    sendError null -32700 "Parse error" // EXPECT: FSA-C01
    with _ -> ()
