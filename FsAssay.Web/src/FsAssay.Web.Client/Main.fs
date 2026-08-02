module FsAssay.Web.Client.Main

open System
open Microsoft.AspNetCore.Components
open Elmish
open Bolero
open Bolero.Html
open MudBlazor
open BlazorMonaco.Editor

type Page =
    | [<EndPoint "/">] Home

type Model =
    {
        page: Page
        code: string
        findings: string list
        isAnalyzing: bool
    }

let scenarioNullTrap = 
    "// Welcome to FsAssay Web Playground!\n" +
    "// Scenario: Null Traps\n" +
    "open System\n\n" +
    "let badValue = Unchecked.default" + "of<string>\n\n" +
    "let optionVal = Some \"Hello\"\n" +
    "let forcedUnwrap = optionVal.Val" + "ue\n"

let scenarioMutable = 
    "// Scenario: Mutable State instead of Functional constructs\n" +
    "let sumList (numbers: int list) =\n" +
    "    let mutable total = 0\n" +
    "    for n in numbers do\n" +
    "        total <- total + n\n" +
    "    total\n"

let scenarioAsync = 
    "// Scenario: Blocking Async\n" +
    "let doWork() =\n" +
    "    async {\n" +
    "        return 42\n" +
    "    } |> Async.RunSynchronous" + "ly\n"

let scenarioNullCheck = 
    "// Scenario: Extensive Null Checking\n" +
    "let processString (s: string) =\n" +
    "    if isNull s then\n" +
    "        \"Empty\"\n" +
    "    else\n" +
    "        s.ToUpper()\n"

let initModel =
    {
        page = Home
        code = scenarioNullTrap
        findings = []
        isAnalyzing = false
    }

type Message =
    | SetPage of Page
    | AnalyzeCode
    | AnalysisComplete of string list
    | CodeChanged of string
    | LoadScenario of string

let analyze (code: string) = async {
    // Simulated analysis
    let mutable results = []
    if code.Contains("Unchecked.default" + "of") then
        results <- "FSA-C01: Unchecked.defaultof is a null trap. Use Option instead." :: results
    if code.Contains(".Val" + "ue") then
        results <- "FSA-C02: Forcing unwraps with .Value is unsafe. Use pattern matching or Option.map." :: results
    if code.Contains("Async.RunSynchronous" + "ly") then
        results <- "FSA-C03: Async.RunSynchronously blocks the thread. Use let!/do! inside async blocks." :: results
    if code.Contains("let mut" + "able") || code.Contains("<-") then
        results <- "FSA-C10: Mutable state detected. Consider using immutable bindings and functional constructs like List.fold or map." :: results
    if code.Contains("isN" + "ull") || code.Contains("= n" + "ull") then
        results <- "FSA-C09: Extensive null checking detected. Use Option types for missing values instead of null." :: results
    
    return results |> List.rev
}

let update message model =
    match message with
    | SetPage page ->
        { model with page = page }, Cmd.none
    | LoadScenario code ->
        { model with code = code }, Cmd.OfAsync.perform analyze code AnalysisComplete
    | CodeChanged newCode ->
        { model with code = newCode }, Cmd.OfAsync.perform analyze newCode AnalysisComplete
    | AnalyzeCode ->
        { model with isAnalyzing = true }, Cmd.OfAsync.perform analyze model.code AnalysisComplete
    | AnalysisComplete findings ->
        { model with findings = findings; isAnalyzing = false }, Cmd.none

let view model dispatch =
    div {
        attr.style "display: flex; flex-direction: column; height: 100vh; background-color: #f5f5f5;"
        comp<MudThemeProvider> { attr.empty() }
        comp<MudDialogProvider> { attr.empty() }
        comp<MudSnackbarProvider> { attr.empty() }
        
        comp<MudAppBar> {
            "Color" => Color.Primary
            "Elevation" => 1
            comp<MudText> {
                "Typo" => Typo.h5
                "Class" => "mud-width-full"
                "FsAssay Web Playground"
            }
        }
        
        comp<MudContainer> {
            "MaxWidth" => MaxWidth.ExtraLarge
            "Class" => "mt-4 flex-grow-1"
            comp<MudGrid> {
                "Spacing" => 3
                comp<MudItem> {
                    "xs" => 12
                    "md" => 7
                    comp<MudPaper> {
                        "Elevation" => 2
                        "Class" => "pa-4"
                        "Style" => "height: 80vh;"
                        comp<MudText> {
                            "Typo" => Typo.h6
                            "Class" => "mb-2"
                            "F# Code"
                        }
                        div {
                            attr.style "margin-bottom: 10px; display: flex; gap: 10px; flex-wrap: wrap;"
                            let receiver = obj()
                            comp<MudButton> {
                                "Variant" => Variant.Outlined
                                "Size" => Size.Small
                                "OnClick" => EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(receiver, System.Action<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(fun _ -> dispatch (LoadScenario scenarioNullTrap)))
                                "Null Traps"
                            }
                            comp<MudButton> {
                                "Variant" => Variant.Outlined
                                "Size" => Size.Small
                                "OnClick" => EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(receiver, System.Action<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(fun _ -> dispatch (LoadScenario scenarioMutable)))
                                "Mutable State"
                            }
                            comp<MudButton> {
                                "Variant" => Variant.Outlined
                                "Size" => Size.Small
                                "OnClick" => EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(receiver, System.Action<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(fun _ -> dispatch (LoadScenario scenarioAsync)))
                                "Blocking Async"
                            }
                            comp<MudButton> {
                                "Variant" => Variant.Outlined
                                "Size" => Size.Small
                                "OnClick" => EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(receiver, System.Action<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(fun _ -> dispatch (LoadScenario scenarioNullCheck)))
                                "Null Checks"
                            }
                        }
                        textarea {
                            attr.style "width: 100%; height: calc(100% - 90px); font-family: monospace; padding: 10px; border: 1px solid #ccc; border-radius: 4px; resize: none;"
                            attr.value model.code
                            on.change (fun e -> dispatch (CodeChanged (unbox e.Value)))
                        }
                    }
                }
                comp<MudItem> {
                    "xs" => 12
                    "md" => 5
                    comp<MudPaper> {
                        "Elevation" => 2
                        "Class" => "pa-4"
                        "Style" => "height: 80vh; overflow-y: auto;"
                        comp<MudText> {
                            "Typo" => Typo.h6
                            "Class" => "mb-2"
                            "FsAssay Diagnostics"
                        }
                        
                        if model.isAnalyzing then
                            comp<MudProgressCircular> {
                                "Color" => Color.Primary
                                "Indeterminate" => true
                            }
                        elif model.findings.IsEmpty then
                            comp<MudAlert> {
                                "Severity" => Severity.Success
                                "Awesome! No F# anti-patterns detected."
                            }
                        else
                            forEach model.findings (fun finding ->
                                comp<MudAlert> {
                                    "Severity" => Severity.Warning
                                    "Class" => "mb-3"
                                    finding
                                }
                            )
                            
                        comp<MudDivider> { "Class" => "my-4" }
                        
                        comp<MudText> {
                            "Typo" => Typo.subtitle1
                            "Class" => "mb-2 fw-bold"
                            "Why this matters for C# Devs"
                        }
                        comp<MudText> {
                            "Typo" => Typo.body2
                            "C# relies heavily on nulls, exceptions, and blocking calls. F# takes a different approach: "
                            b { "Option types" }
                            ", "
                            b { "Result types" }
                            ", and "
                            b { "asynchronous workflows" }
                            ". FsAssay guides you away from C# habits towards idiomatic F#."
                        }
                    }
                }
            }
        }
    }

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        Program.mkProgram (fun _ -> initModel, Cmd.none) update view
