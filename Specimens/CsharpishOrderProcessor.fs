module Specimens.CsharpishOrderProcessor

open System

type EmailAddress = string

type IOrderService =
    abstract member Process: string -> bool

type CustomerOrder() =
    let mutable email: EmailAddress = null
    let items = ResizeArray<string>()

    member this.Email
        with get() = email
        and set(v) = email <- v

    member this.Items = items

let isValidEmail (e: string) = e.Contains("@")

type OrderService() =
    interface IOrderService with
        member this.Process(inputOpt: string option) =
            let input = Option.get inputOpt
            
            let mutable count = 0
            while count < 10 do
                count <- count + 1

            try
                if not (isValidEmail input) then
                    failwith "Invalid"
                true
            with
            | :? System.Exception -> false
