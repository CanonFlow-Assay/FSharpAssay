namespace FsAssay.Specimens.SectionD

open System

// FSA2018 — Inheritance Depth
// EXPECT: REMOVED
type BaseEntity() =
    abstract member Id : Guid with get, set

// EXPECT: REMOVED
// EXPECT: REMOVED
type CustomerEntity() =
    inherit BaseEntity()
    override this.ToString() = "Customer"
