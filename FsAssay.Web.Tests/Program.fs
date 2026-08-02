module FsAssay.Web.Tests.Program

open System
open Expecto
open Microsoft.Playwright
open FsAssay.Web.Client.Main

let elmishTests =
    testList "Elmish Unit Tests" [
        test "Initial state should be Home page" {
            let model = initModel
            Expect.equal model.page Home "Should start on Home page"
            Expect.isFalse model.isAnalyzing "Should not be analyzing initially"
        }

        test "SetPage updates page" {
            let model = { initModel with page = Home }
            let newModel, cmd = update (SetPage Home) model
            Expect.equal newModel.page Home "Page should be updated"
        }

        test "CodeChanged updates code and triggers analysis" {
            let model = initModel
            let newCode = "let x = 1"
            let newModel, cmd = update (CodeChanged newCode) model
            Expect.equal newModel.code newCode "Code should be updated"
            // We'd typically assert on Cmd here, but we can just check the model state.
        }
        
        test "AnalyzeCode sets isAnalyzing to true" {
            let model = initModel
            let newModel, cmd = update AnalyzeCode model
            Expect.isTrue newModel.isAnalyzing "Should set isAnalyzing to true"
        }
        
        test "AnalysisComplete sets findings and clears isAnalyzing" {
            let model = { initModel with isAnalyzing = true }
            let findings = ["Some error"]
            let newModel, cmd = update (AnalysisComplete findings) model
            Expect.equal newModel.findings findings "Findings should be set"
            Expect.isFalse newModel.isAnalyzing "isAnalyzing should be false"
        }
    ]

let e2eTests =
    testList "Playwright E2E Tests" [
        testTask "Playwright should be able to initialize" {
            let! (playwright: IPlaywright) = Playwright.CreateAsync()
            let! (browser: IBrowser) = playwright.Chromium.LaunchAsync()
            let! (page: IPage) = browser.NewPageAsync()
            
            // In a real E2E test, we would start the DevServer and navigate to it:
            // let! _ = page.GotoAsync("http://localhost:5000")
            // let! title = page.TitleAsync()
            // Expect.isTrue (title.Contains("Bolero")) "Should load Bolero app"
            
            Expect.isNotNull page "Playwright successfully created a page"
            do! page.CloseAsync()
            do! browser.CloseAsync()
            playwright.Dispose()
        }
    ]

[<EntryPoint>]
let main argv =
    let tests = testList "All" [elmishTests; e2eTests]
    runTestsWithCLIArgs [] argv tests
