module Specimens.StylishOrderProcessor

type EmailAddress = private EmailAddress of string

module EmailAddress =
    let create (email: string) =
        if email.Contains("@") then Ok (EmailAddress email)
        else Error "Invalid email"

type CustomerOrder = {
    Email: EmailAddress
    Items: string list
}

type ProcessResult =
    | Success
    | Invalid of string

let processOrder (inputOpt: string option) : ProcessResult =
    match inputOpt with
    | Some input ->
        match EmailAddress.create input with
        | Ok email -> 
            let order = { Email = email; Items = [] }
            Success
        | Error err -> Invalid err
    | None -> Invalid "No input"
