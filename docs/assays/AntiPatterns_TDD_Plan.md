# Anti-Patterns & TDD Plan: C#-ish F# vs. fs-assay

When C# developers write F# without understanding functional paradigms (e.g., as taught in *F# in Action*), they inadvertently write "C#-ish F#". This document defines the most common C#-ish anti-patterns and details a Test-Driven Development (TDD) plan for `fs-assay` to reliably detect and reject them.

---

## 1. The Anti-Patterns

### Anti-Pattern 1: Mutable State via Classes
**The C# mindset:** Data is modeled as classes with getters and setters.
**The "C#-ish F#":**
```fsharp
type Order() =
    let mutable total = 0.0m
    member this.Total 
        with get() = total
        and set(v) = total <- v
```
**Why it fails `fs-assay`:** Relies on `mutable` variables and class inheritance instead of immutable records (`type Order = { Total: decimal }`).

### Anti-Pattern 2: Null for Missing Data
**The C# mindset:** Missing objects are `null`.
**The "C#-ish F#":**
```fsharp
let findCustomer (id: int) : Customer =
    if id < 0 then null // explicit null literal
    else new Customer()
```
**Why it fails `fs-assay`:** F# idiomatic code uses `Option<'T>`. Explicit `null` literals violate rule **OBSOLETE_FSA (Forbidden Null Literal)**.

### Anti-Pattern 3: Exceptions for Control Flow
**The C# mindset:** Validate data; if it's invalid, `throw new ArgumentException()`.
**The "C#-ish F#":**
```fsharp
let validateEmail (email: string) =
    if not (email.Contains("@")) then
        raise (System.ArgumentException("Invalid email"))
    email
```
**Why it fails `fs-assay`:** Exceptions break type safety for control flow. Idiomatic F# uses `Result<string, DomainError>`. This triggers **OBSOLETE_FSA (Exception Control Flow)**.

### Anti-Pattern 4: Partial Unsafe Access (`Option.get`)
**The C# mindset:** "I know this value is here, just give me the value." (Like `Nullable<T>.Value`).
**The "C#-ish F#":**
```fsharp
let processCustomer (customerOpt: Customer option) =
    let customer = customerOpt.Value // or Option.get customerOpt
    printfn "%s" customer.Name
```
**Why it fails `fs-assay`:** `.Value` and `Option.get` can throw runtime exceptions. `fs-assay` enforces total functions and triggers **OBSOLETE_FSA (Partial Access)**.

### Anti-Pattern 5: Blocking on Async (`Task.Result`)
**The C# mindset:** "I need this value now, I'll just call `.Result`."
**The "C#-ish F#":**
```fsharp
let getCustomerData () =
    let task = fetchFromDbAsync()
    let data = task.Result // Blocking the thread!
    data
```
**Why it fails `fs-assay`:** Calling `.Result` or `.Wait()` blocks threads and causes deadlocks. Idiomatic F# requires `let!` inside a `task` or `async` block. This violates **OBSOLETE_FSA (Blocking Thread)**.

---

## 2. TDD Strategy for `fs-assay`

To prove `fs-assay` works, we will write our analyzer tests using a strict TDD loop. The tests will feed "C#-ish F#" code into the analyzer and assert that the correct rules are triggered.

### Testing Framework Setup
We will use `Expecto` to write our analyzer tests.

### Test Example: Disproving `Option.get` (OBSOLETE_FSA)
```fsharp
testCase "fs-assay detects Option.get and fails the build" <| fun _ ->
    // 1. Arrange: The hostile C#-ish code
    let sourceCode = """
    module BadCode
    let doSomething (x: int option) =
        let v = x.Value // Anti-pattern!
        v + 1
    """

    // 2. Act: Run the fs-assay engine over the AST/TAST
    let results = runFsAssay sourceCode

    // 3. Assert: fs-assay caught the exact violation
    let hasPartialAccessViolation = 
        results |> List.exists (fun r -> r.RuleId = "OBSOLETE_FSA")
    
    Expect.isTrue hasPartialAccessViolation "Expected OBSOLETE_FSA (Partial Access) to be triggered by .Value"
```

### Test Example: Disproving Exceptions (OBSOLETE_FSA)
```fsharp
testCase "fs-assay detects raise/throw and demands Result instead" <| fun _ ->
    // 1. Arrange
    let sourceCode = """
    module BadDomain
    let validate x =
        if x < 0 then failwith "Negative" // Anti-pattern!
        x
    """

    // 2. Act
    let results = runFsAssay sourceCode

    // 3. Assert
    let hasExceptionViolation = 
        results |> List.exists (fun r -> r.RuleId = "OBSOLETE_FSA")
    
    Expect.isTrue hasExceptionViolation "Expected OBSOLETE_FSA (Exception Control Flow) to be triggered by failwith"
```

## Summary
By building `fs-assay` through these specific TDD loops, we guarantee that the tool successfully operates as a "Verification Officer". It acts as an impassable gate against C# paradigms sneaking into an F# codebase.
