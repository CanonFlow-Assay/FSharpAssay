module FsAssay.TypeGym.Challenges

type Difficulty = 
    | Beginner 
    | Intermediate 
    | Advanced 
    | Expert

type Challenge = {
    Id: string
    Title: string
    Difficulty: Difficulty
    Description: string
    InitialCode: string
    Verify: string -> Result<string, string> // Custom verification logic (can be simple text matching for now, or FCS compilation later)
}

let beginnerChallenges = [
    {
        Id = "B01"
        Title = "Phantom Types for Email"
        Difficulty = Beginner
        Description = "Use a phantom type to distinguish between an unvalidated email string and a validated email string. Define a type `Email<'T>` where `'T` is either `Validated` or `Unvalidated`."
        InitialCode = """module TypeGym.B01

// 1. Define types Validated and Unvalidated (empty types/markers)

// 2. Define Email<'T> wrapping a string

// 3. Implement a function validate : Email<Unvalidated> -> Email<Validated> option
"""
        Verify = fun code ->
            if code.Contains("Email<") && code.Contains("Unvalidated") && code.Contains("Validated") && code.Contains("-> Email<Validated> option") then
                Ok "Excellent! You have successfully used Phantom Types to represent validation state."
            else
                Error "Your solution does not seem to define the required types or the validate function signature."
    }
    {
        Id = "B02"
        Title = "Units of Measure"
        Difficulty = Beginner
        Description = "Define Units of Measure for Meters (m) and Seconds (s). Then define a Speed type (m/s) and a function that calculates speed given distance and time."
        InitialCode = """module TypeGym.B02

// 1. Define [<Measure>] type m
// 2. Define [<Measure>] type s

// 3. Define a function calculateSpeed (distance: float<m>) (time: float<s>) = ...
"""
        Verify = fun code ->
            if code.Contains("[<Measure>] type m") && code.Contains("[<Measure>] type s") && code.Contains("float<m>") && code.Contains("float<s>") then
                Ok "Great job! Units of Measure will prevent you from accidentally adding distances to times."
            else
                Error "Ensure you have defined the `m` and `s` measures and used them in the function signature."
    }
    {
        Id = "B03"
        Title = "Single-Case Discriminated Unions"
        Difficulty = Beginner
        Description = "Wrap a primitive string in a Single-Case Discriminated Union to prevent primitive obsession. Define a `CustomerId` type."
        InitialCode = """module TypeGym.B03

// 1. Define CustomerId as a single-case DU wrapping a string.

// 2. Define a function processCustomer (id: CustomerId) = ...
"""
        Verify = fun code ->
            if code.Contains("type CustomerId = CustomerId of string") || code.Contains("type CustomerId = | CustomerId of string") then
                Ok "Perfect! Single-Case DUs provide type safety without runtime overhead."
            else
                Error "Make sure you define `CustomerId` as a DU wrapping a string."
    }
    {
        Id = "B04"
        Title = "Total Functions with Option"
        Difficulty = Beginner
        Description = "Replace a function that throws an exception with a total function returning an Option. Rewrite `divide` to return `int option`."
        InitialCode = """module TypeGym.B04

// Rewrite this function to be total (return int option) instead of throwing an exception.
let divide (x: int) (y: int) : int =
    if y = 0 then failwith "Cannot divide by zero"
    else x / y
"""
        Verify = fun code ->
            if not (code.Contains("failwith")) && code.Contains("int option") && code.Contains("Some") && code.Contains("None") then
                Ok "Awesome! Returning Options forces the caller to handle the missing case."
            else
                Error "Ensure the function returns `int option` and does not use `failwith`."
    }
]

let allChallenges = beginnerChallenges
