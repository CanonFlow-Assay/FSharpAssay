module FsAssay.TypeGym.Program

open System
open System.IO
open FsAssay.TypeGym.Challenges

let clear () = Console.Clear()

let printHeader () =
    Console.ForegroundColor <- ConsoleColor.Cyan
    printfn "========================================"
    printfn "           FsAssay Type Gym             "
    printfn "========================================"
    Console.ResetColor()

let rec runChallenge (challenge: Challenge) =
    clear ()
    printHeader ()
    Console.ForegroundColor <- ConsoleColor.Yellow
    printfn "Challenge [%s]: %s" challenge.Id challenge.Title
    Console.ResetColor()
    printfn "%s\n" challenge.Description
    
    let tempFile = Path.Combine(Path.GetTempPath(), sprintf "%s.fs" challenge.Id)
    if not (File.Exists(tempFile)) then
        File.WriteAllText(tempFile, challenge.InitialCode)
        
    printfn "I have created a template file for you at:"
    Console.ForegroundColor <- ConsoleColor.Cyan
    printfn "%s" tempFile
    Console.ResetColor()
    
    printfn "\nPlease edit this file to solve the challenge."
    printfn "Press [Enter] when you are ready to verify, or type 'Q' to quit this challenge."
    
    let input = Console.ReadLine()
    if input.Trim().ToUpper() = "Q" then
        ()
    else
        let code = File.ReadAllText(tempFile)
        match challenge.Verify code with
        | Ok msg ->
            Console.ForegroundColor <- ConsoleColor.Green
            printfn "\nSUCCESS: %s" msg
            Console.ResetColor()
            printfn "Press [Enter] to return to the menu..."
            Console.ReadLine() |> ignore
        | Error err ->
            Console.ForegroundColor <- ConsoleColor.Red
            printfn "\nFAILED: %s" err
            Console.ResetColor()
            printfn "Press [Enter] to try again, or 'Q' to abort..."
            let retry = Console.ReadLine()
            if retry.Trim().ToUpper() <> "Q" then
                runChallenge challenge

let rec mainMenu () =
    clear ()
    printHeader ()
    printfn "Select a challenge to begin:"
    
    let challenges = Challenges.allChallenges
    for i in 0 .. challenges.Length - 1 do
        let c = challenges.[i]
        printfn " %d. [%s] %s" (i + 1) (string c.Difficulty) c.Title
        
    printfn " Q. Quit"
    printfn ""
    printf "> "
    
    let input = Console.ReadLine()
    if input.Trim().ToUpper() = "Q" then
        0
    else
        match Int32.TryParse(input.Trim()) with
        | true, n when n >= 1 && n <= challenges.Length ->
            runChallenge challenges.[n - 1]
            mainMenu ()
        | _ ->
            printfn "Invalid choice. Press [Enter] to try again."
            Console.ReadLine() |> ignore
            mainMenu ()

[<EntryPoint>]
let main argv =
    mainMenu ()
