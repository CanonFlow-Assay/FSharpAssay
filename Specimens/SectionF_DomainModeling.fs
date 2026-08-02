namespace FsAssay.Specimens.SectionF

open System

module DomainModeling =
    // FSA2027 / OBSOLETE_FSA — Primitive Obsession
    // EXPECT: REMOVED
    type EmailAddress = string

    // FSA2030 — Boolean Flag Parameters
    let processOrder (isPriority: bool) (sendReceipt: bool) (expressDelivery: bool) = ()
