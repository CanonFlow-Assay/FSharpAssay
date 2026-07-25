module FsAssay.Runner.McpServer

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open FsAssay.Runner.Orchestrator
open FsAssay.Runner.Output

let sendResponse (id: JsonNode) (result: JsonNode) =
    let response = JsonObject()
    response.Add("jsonrpc", JsonValue.Create("2.0"))
    if not (isNull id) then
        let idNode = JsonNode.Parse(id.ToJsonString())
        response.Add("id", idNode)
    response.Add("result", result)
    let json = response.ToJsonString()
    Console.WriteLine(json)
    Console.Out.Flush()

let sendError (id: JsonNode) (code: int) (msg: string) =
    let response = JsonObject()
    response.Add("jsonrpc", JsonValue.Create("2.0"))
    if not (isNull id) then
        let idNode = JsonNode.Parse(id.ToJsonString())
        response.Add("id", idNode)
    let errorObj = JsonObject()
    errorObj.Add("code", JsonValue.Create(code))
    errorObj.Add("message", JsonValue.Create(msg))
    response.Add("error", errorObj)
    Console.WriteLine(response.ToJsonString())
    Console.Out.Flush()

let handleInitialize (id: JsonNode) =
    let result = JsonObject()
    result.Add("protocolVersion", JsonValue.Create("2024-11-05"))
    let capabilities = JsonObject()
    let tools = JsonObject()
    capabilities.Add("tools", tools)
    result.Add("capabilities", capabilities)
    let serverInfo = JsonObject()
    serverInfo.Add("name", JsonValue.Create("fsassay-mcp"))
    serverInfo.Add("version", JsonValue.Create("1.0.0"))
    result.Add("serverInfo", serverInfo)
    sendResponse id result

let handleToolsList (id: JsonNode) =
    let result = JsonObject()
    let toolsArray = JsonArray()
    let analyzeTool = JsonObject()
    analyzeTool.Add("name", JsonValue.Create("analyze_fsharp"))
    analyzeTool.Add("description", JsonValue.Create("Run FsAssay on an F# project to detect anti-patterns and compositional code smells."))
    
    let inputSchema = JsonObject()
    inputSchema.Add("type", JsonValue.Create("object"))
    let props = JsonObject()
    let projectPath = JsonObject()
    projectPath.Add("type", JsonValue.Create("string"))
    projectPath.Add("description", JsonValue.Create("Absolute path to the .fsproj file or directory containing it."))
    props.Add("projectPath", projectPath)
    inputSchema.Add("properties", props)
    let required = JsonArray()
    required.Add(JsonValue.Create("projectPath"))
    inputSchema.Add("required", required)
    
    analyzeTool.Add("inputSchema", inputSchema)
    toolsArray.Add(analyzeTool)
    result.Add("tools", toolsArray)
    sendResponse id result

let handleToolsCall (id: JsonNode) (paramsNode: JsonNode) =
    try
        let nameNode = paramsNode.["name"]
        if isNull nameNode || nameNode.GetValue<string>() <> "analyze_fsharp" then
            sendError id -32601 "Tool not found"
        else
            let argsNode = paramsNode.["arguments"]
            let projectPath = argsNode.["projectPath"].GetValue<string>()
            let results = FsAssay.Runner.Orchestrator.analyzeProject projectPath |> Async.RunSynchronously
            
            // Format results as JSON text
            let sb = System.Text.StringBuilder()
            sb.AppendLine("FsAssay Analysis Results:") |> ignore
            for res in results do
                sb.AppendLine(sprintf "- [%s] %s at %s:%d" (res.Severity.ToString()) res.Message res.Range.FileName res.Range.StartLine) |> ignore
                
            let result = JsonObject()
            let contentArray = JsonArray()
            let contentObj = JsonObject()
            contentObj.Add("type", JsonValue.Create("text"))
            contentObj.Add("text", JsonValue.Create(sb.ToString()))
            contentArray.Add(contentObj)
            result.Add("content", contentArray)
            sendResponse id result
    with ex ->
        sendError id -32000 (ex.Message)

let run () =
    try
        let reader = new StreamReader(Console.OpenStandardInput())
        while not reader.EndOfStream do
            let line = reader.ReadLine()
            if not (String.IsNullOrWhiteSpace(line)) then
                try
                    let json = JsonNode.Parse(line)
                    let id = json.["id"]
                    let methodNode = json.["method"]
                    if not (isNull methodNode) then
                        let method = methodNode.GetValue<string>()
                        match method with
                        | "initialize" -> handleInitialize id
                        | "tools/list" -> handleToolsList id
                        | "tools/call" -> handleToolsCall id (json.["params"])
                        | "notifications/initialized" -> ()
                        | _ -> sendError id -32601 "Method not found"
                with ex ->
                    sendError null -32700 "Parse error"
    with _ -> ()
