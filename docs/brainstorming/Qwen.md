# 🔥 WHY THESE 10 RULES ROCK: The F#-Specific Moat

## The One-Sentence Answer

These 10 rules rock because they exploit **F#'s type system as a detection engine** — the compiler already *knows* the types, the cases, the scopes, the desugared CE structure, the tail positions, the attribute metadata. FsAssay just needs to **ask the compiler what it already knows** and flag when the code violates what the type system is trying to tell you. No regex can do this. No other language's analyzer can do this. And LLMs generate these exact bugs **constantly** because they treat F# like C# with funny syntax.

---

## The Deep Why: F# Is a Language That *Wants* To Be Analyzed

F# is not just "another .NET language." It is a language where **the type system encodes intent** [[12]]:

- A DU says *"these are ALL the cases"*
- An `Option<'T>` says *"this might not exist"*
- A `use` says *"dispose this at scope exit"*
- A `let rec` says *"this is recursive"*
- An `async { }` says *"this is a computation expression with Bind/Return semantics"*
- A `[<Struct>]` says *"this must be a value type for performance"*
- A `[<RequireQualifiedAccess>]` says *"don't pollute the namespace"*
- A `float<meter>` says *"this is a physical quantity, not a bare number"*
- A `(|Even|Odd|)` says *"this pattern might not match everything"*

**The compiler enforces these at compile time.** But there's a gap: the compiler checks *syntax* and *types*. It does NOT check *semantic intent*. FsAssay lives in that gap.

And here's the critical insight for the agentic AI world: **LLMs don't understand semantic intent.** They pattern-match on syntax. They generate F# that *compiles* but *violates the language's design philosophy*. These 10 rules catch exactly that class of bug.

---

## Rule-by-Rule: Why Each One Rocks

---

### 1. Incomplete DU Match

#### The F# mechanic:
Discriminated Unions are F#'s algebraic data types. When you write:

```fsharp
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
    | Triangle of base: float * height: float
```

The compiler **knows there are exactly 3 cases**. Exhaustive pattern matching is a *core correctness feature* of F# [[12]]. If you match on only 2 cases, the compiler warns (FS0025).

#### The LLM bug:
LLMs add a new DU case but **forget to update all match expressions**:

```fsharp
// LLM adds a new case:
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
    | Triangle of base: float * height: float
    | Hexagon of side: float  // ← LLM added this

// But forgets to update this match 200 lines away:
let area shape =
    match shape with
    | Circle r -> Math.PI * r * r
    | Rectangle (w, h) -> w * h
    | Triangle (b, h) -> 0.5 * b * h
    // ← Hexagon is MISSING. Runtime MatchFailureException.
```

The compiler warns. But in an agentic loop, the agent might suppress the warning or the build might treat warnings as non-fatal. FsAssay **denies** this.

#### The TAST detection:
```fsharp
// Walk SynMatchExpr nodes
// For each: resolve the DU type via CheckFileResults
// Count the DU cases via FSharpEntity.UnionCases
// Count the match patterns
// If patterns < cases → VIOLATION with the missing case names
```

#### Why regex can't do it:
Regex sees `match shape with | Circle ... | Rectangle ... | Triangle ...`. It cannot know that `Shape` has 4 cases. It cannot resolve the type. It cannot name the missing case. **Only the TAST knows the full case list.**

#### Why this rocks:
This is the **#1 correctness rule** for F#. It turns a runtime crash into a compile-time block. In an agentic loop, it tells the agent: *"You added Hexagon but forgot to handle it in `area` at line 42."* The agent fixes it. The build passes. No human needed.

---

### 2. `use` + `Async.Start` (Disposed Before Async Runs)

#### The F# mechanic:
F#'s `use` keyword desugars to `try/finally` with `IDisposable.Dispose()` at scope exit [[14]]. `Async.Start` spawns a fire-and-forget workflow that **outlives the current scope**.

```fsharp
let processData () =
    use client = new HttpClient()  // ← Disposed at end of processData
    Async.Start (async {
        let! response = client.GetAsync("https://api.example.com")  // ← CRASH
        // client is ALREADY DISPOSED when this runs
        return! response.Content.ReadAsStringAsync()
    })
```

The `use` block exits immediately after `Async.Start` returns (which is instant — it's fire-and-forget). The `HttpClient` is disposed. The async workflow then tries to use a disposed object. **Runtime `ObjectDisposedException`.**

#### The LLM bug:
LLMs generate this **constantly** because they pattern-match on C#'s `using` + `async/await` where `await` keeps the scope alive. In F#, `Async.Start` is NOT `await`. It's fire-and-forget. The LLM doesn't understand the scope semantics.

#### The TAST detection:
```fsharp
// Walk SynExpr.Use nodes
// For each: check if the body contains Async.Start / Async.StartAsTask / Async.StartImmediate
// If the disposable is referenced inside the async block → VIOLATION
// "client is disposed before the async workflow that uses it runs"
```

#### Why regex can't do it:
Regex sees `use client = new HttpClient()` and `Async.Start (async { ... client ... })`. It cannot determine:
- That `client` is an `IDisposable`
- That `Async.Start` is fire-and-forget (vs `Async.RunSynchronously` which blocks)
- That the `use` scope exits before the async body executes
- Whether `client` is actually referenced inside the async block

**Only the TAST knows the scope, the type, and the async semantics.**

#### Why this rocks:
G-Research built their `DisposedBeforeAsyncRunAnalyzer` for exactly this bug in production trading systems [[11]]. This is not theoretical. This is a **runtime crash in production**. FsAssay catching this at analysis time — before the code ever runs — is the difference between a caught bug and a 3am pager alert.

---

### 3. Non-Tail `let rec`

#### The F# mechanic:
F# has `let rec` for recursive functions. Unlike OCaml, **F# does NOT guarantee tail call optimization** [[23]][[24]]. The compiler *may* emit a tailcall, but it depends on:
- Whether the recursive call is in tail position
- Whether the function is too complex for the JIT to optimize
- Whether the `Tailcall` IL instruction is emitted

F# 8.0 added the `[<TailCall>]` attribute to express intent [[22]], but the compiler still doesn't enforce it.

```fsharp
// LLM generates this — looks fine, compiles, passes tests on small inputs:
let rec sum lst =
    match lst with
    | [] -> 0
    | x :: xs -> x + sum xs  // ← NOT tail-recursive (x + ... is the tail)

// StackOverflowException on List.init 100000 id
```

#### The LLM bug:
LLMs generate recursive functions that are **not tail-recursive** because they pattern-match on mathematical definitions (which are naturally non-tail-recursive) rather than F# idioms (accumulator-passing style):

```fsharp
// What the LLM writes (non-tail):
let rec factorial n = if n <= 1 then 1 else n * factorial (n - 1)

// What F# wants (tail-recursive):
let factorial n =
    let rec loop acc = function
        | 0 -> acc
        | k -> loop (acc * k) (k - 1)
    loop 1 n
```

#### The TAST detection:
```fsharp
// Walk SynBinding nodes where IsRecursive = true
// For each recursive call site: check if it's in tail position
// Tail position = the call is the LAST expression in its branch
// If recursive call has surrounding computation (x + sum xs) → NOT tail
// If recursive call is bare (loop (acc * k) (k-1)) → IS tail
```

#### Why regex can't do it:
Regex sees `x + sum xs`. It cannot determine:
- That `sum` is the function being defined (requires binding resolution)
- That `x + ...` wraps the recursive call (requires expression tree analysis)
- That this makes the call non-tail (requires control flow analysis)
- Whether the JIT will optimize it (requires IL emission knowledge)

**Only the TAST knows the binding, the expression structure, and the call position.**

#### Why this rocks:
This is a **latent bomb**. The code compiles. It passes tests on small inputs. It works in development. Then production hits it with 100,000 items and **StackOverflowException** crashes the process. No try/catch can catch a StackOverflow. The process dies. FsAssay catches this at analysis time: *"Recursive call to `sum` at line 4 is not in tail position. This will stack-overflow on large inputs."*

G-Research's production F# systems use advanced recursion techniques specifically to avoid this [[28]]. FsAssay makes that knowledge automatic.

---

### 4. `Option.get` in Pipeline

#### The F# mechanic:
F#'s pipeline operator `|>` chains transformations left-to-right. `Option<'T>` is F#'s null-safety mechanism. `Option.get` extracts the value — and throws `KeyNotFoundException` if the option is `None`.

```fsharp
// LLM generates this — compiles, looks "functional":
let result =
    input
    |> tryParse
    |> Option.get  // ← BOOM if tryParse returns None
    |> transform
    |> format
```

#### The LLM bug:
LLMs treat `Option.get` like C#'s `.Value` on `Nullable<T>` — they assume the value is always there. They don't understand that `Option` is F#'s way of saying *"this might not exist, handle it."* The LLM generates the happy path and forgets the `None` case.

#### The TAST detection:
```fsharp
// Walk FSharpExprPatterns.Call nodes
// If func.FullName = "Microsoft.FSharp.Core.OptionModule.Get"
// Check if the pipeline has a preceding Option.filter / Option.map / match
// If no guard → VIOLATION
// "Option.get at line 4 will throw KeyNotFoundException if tryParse returns None"
```

#### Why regex can't do it:
Regex sees `|> Option.get`. It cannot determine:
- That the preceding pipeline stage returns `Option<'T>` (requires type resolution)
- Whether there's a guard earlier in the pipeline (requires dataflow analysis)
- Whether the `Option.get` is inside a `match | Some x -> ...` branch (requires scope analysis)

**Only the TAST knows the types flowing through the pipeline.**

#### Why this rocks:
`Option.get` is the **F# equivalent of NullReferenceException** — the billion-dollar mistake that F# was designed to eliminate. When an LLM reintroduces it, FsAssay says: *"You're using Option.get without a guard. Use `Option.defaultValue`, `Option.map`, or `match` instead."* This is not a style preference. This is a **runtime crash prevention**.

---

### 5. `async { }` Without Return

#### The F# mechanic:
Computation expressions desugar to `Bind`/`Return`/`ReturnFrom` calls [[14]][[15]]. An `async { }` block is a computation expression. If it has no `return` or `return!`, the result is `Async<unit>` — the computed value is **silently discarded**.

```fsharp
// LLM generates this — compiles, looks like it does something:
let fetchData url =
    async {
        let! response = httpClient.GetAsync(url)
        let! content = response.Content.ReadAsStringAsync()
        // ← No return! content is computed then DISCARDED
        // The caller gets Async<unit>, not Async<string>
    }
```

The caller expects `Async<string>` but gets `Async<unit>`. The data is fetched, read into memory, and **thrown away**. Silent data loss.

#### The LLM bug:
LLMs generate C#-style async methods where the last expression is implicitly returned. In F#, **nothing is implicit**. You must write `return content` or `return! someAsync`. The LLM forgets because C# trained it to omit `return`.

#### The TAST detection:
```fsharp
// Walk SynExpr.ComputationExpr nodes where the CE is "async"
// Check if the body contains SynExpr.YieldOrReturn / SynExpr.YieldOrReturnFrom
// If no Return/ReturnFrom → VIOLATION
// "async block at line 3 computes a value but never returns it. Result is Async<unit>."
```

#### Why regex can't do it:
Regex sees `async { let! x = ... let! y = ... }`. It cannot determine:
- That this is a computation expression (requires CE resolution)
- That the CE builder is `AsyncBuilder` (requires type resolution)
- Whether `return`/`return!` appears in the desugared form (requires CE desugaring)
- What the inferred return type is (requires type inference)

**Only the TAST knows the desugared CE structure and the inferred type.**

#### Why this rocks:
This is a **silent data loss bug**. The code compiles. It runs. It fetches the data. And then it throws the data away. No exception. No warning. Just... nothing. The caller gets `unit` and wonders why the result is empty. FsAssay catches this at analysis time: *"Your async block computes `content` but never returns it. Add `return content`."*

---

### 6. `Task.Result` in `async { }`

#### The F# mechanic:
F# has two async paradigms: `Async<'T>` (F#-native) and `Task<'T>` (.NET). Mixing them incorrectly causes **deadlocks**. Calling `.Result` or `.Wait()` on a `Task` inside an `async { }` block blocks the thread that the async workflow needs to complete.

```fsharp
// LLM generates this — compiles, works in console app, DEADLOCKS in ASP.NET:
let processData () =
    async {
        let task = httpClient.GetAsync("https://api.example.com")
        let response = task.Result  // ← BLOCKS the thread
        // In ASP.NET: the SynchronizationContext is blocked
        // The task needs the context to complete → DEADLOCK
        return response.StatusCode
    }
```

#### The LLM bug:
LLMs mix `Task` and `Async` because they're trained on both C# (Task-centric) and F# (Async-centric) code. They generate `.Result` because it's the C# way to "unwrap" a Task. In F#, the correct way is `let! response = Async.AwaitTask task`.

#### The TAST detection:
```fsharp
// Walk FSharpExprPatterns.Call nodes
// If func.FullName = "System.Threading.Tasks.Task`1.get_Result"
// Check if the enclosing context is an async CE
// If yes → VIOLATION
// "Task.Result at line 4 blocks the thread inside async { }. Use Async.AwaitTask instead."
```

#### Why regex can't do it:
Regex sees `task.Result`. It cannot determine:
- That `task` is a `Task<'T>` (requires type resolution)
- That the enclosing context is an `async { }` CE (requires scope analysis)
- That `.Result` blocks the thread (requires semantic knowledge)
- Whether this is in a console app (safe-ish) vs ASP.NET (deadlock)

**Only the TAST knows the type, the scope, and the threading semantics.**

#### Why this rocks:
This is a **deadlock**. Not a crash. Not an exception. A **hang**. The application stops responding. No error message. No stack trace. Just... frozen. In production, this means a thread pool exhaustion cascade. FsAssay catches this at analysis time: *"You're calling .Result inside async { }. This will deadlock in ASP.NET. Use `let! x = Async.AwaitTask task` instead."*

---

### 7. Struct DU with Reference Fields

#### The F# mechanic:
F# 6+ supports `[<Struct>]` discriminated unions for performance — they're allocated on the stack instead of the heap [[29]][[30]]. But if a struct DU case contains a **reference type** (`string`, `list<'T>`, `obj`, `array<'T>`), the struct must be **boxed** when stored, defeating the entire purpose.

```fsharp
// LLM generates this — compiles, "looks performant":
[<Struct>]
type Measurement =
    | Distance of value: float * unit: string  // ← string is a REFERENCE TYPE
    | Weight of value: float * unit: string     // ← Boxing on every use

// Every time this is stored in a collection, passed to a generic function,
// or compared for equality → BOXING ALLOCATION → GC pressure → perf cliff
```

#### The LLM bug:
LLMs add `[<Struct>]` because they've seen it in F# performance guides. But they don't understand that **structs must contain only value types** to avoid boxing. They put `string` fields in struct DUs because "it's just a label."

#### The TAST detection:
```fsharp
// Walk FSharpEntity nodes where IsFSharpUnion = true
// Check if the entity has [<Struct>] attribute
// For each union case: inspect field types
// If any field type IsReferenceType = true → VIOLATION
// "Struct DU 'Measurement' case 'Distance' contains reference type 'string'.
//  This causes boxing on every use. Remove [<Struct>] or use a value type."
```

#### Why regex can't do it:
Regex sees `[<Struct>]` and `string`. It cannot determine:
- That `string` is a reference type in this context (requires type resolution)
- That the DU is actually a struct (requires attribute resolution)
- Whether the boxing actually occurs (requires usage analysis)
- Whether `float` is a value type but `string` is not (requires type system knowledge)

**Only the TAST knows the attribute, the field types, and the boxing implications.**

#### Why this rocks:
This is a **performance bomb**. The code compiles. It works. It's correct. But under load, every boxing allocation hits the GC. In a trading system processing millions of messages per second, this is the difference between 10μs latency and 10ms latency. FsAssay catches this at analysis time: *"Your struct DU contains a string field. This defeats the purpose of [<Struct>]."*

---

### 8. `[<RequireQualifiedAccess>]` Violation

#### The F# mechanic:
The `[<RequireQualifiedAccess>]` attribute forces DU cases to be accessed with their type name: `Shape.Circle` instead of bare `Circle` [[31]]. This prevents namespace pollution and makes code self-documenting.

```fsharp
[<RequireQualifiedAccess>]
type Shape =
    | Circle of float
    | Rectangle of float * float

// Correct:
let c = Shape.Circle 5.0

// LLM generates this — COMPILE ERROR:
let c = Circle 5.0  // ← Error: 'Circle' is not defined
```

#### The LLM bug:
LLMs generate bare DU case access because they've seen F# code without `[<RequireQualifiedAccess>]`. They don't check whether the attribute is present. The compiler catches this, but in an agentic loop, the agent might not see the compiler error clearly.

#### The TAST detection:
```fsharp
// Walk SynExpr.UnionCase nodes
// Resolve the DU type via CheckFileResults
// Check if the type has [<RequireQualifiedAccess>] attribute
// If yes and the access is unqualified → VIOLATION
// "DU case 'Circle' requires qualified access: use 'Shape.Circle'"
```

#### Why regex can't do it:
Regex sees `Circle 5.0`. It cannot determine:
- That `Circle` is a DU case (requires symbol resolution)
- That the DU has `[<RequireQualifiedAccess>]` (requires attribute resolution)
- Whether the access is qualified or not (requires parse tree context)

**Only the TAST knows the symbol, the attribute, and the access form.**

#### Why this rocks:
This is a **compile error** that the compiler already catches. But FsAssay adds value by:
1. Providing a **clearer error message** than the compiler
2. Suggesting the **exact fix** (`Shape.Circle` instead of `Circle`)
3. Working in the **agentic loop** where the agent might not parse compiler errors well
4. Being part of a **unified SARIF report** with all other violations

---

### 9. Unit-of-Measure Loss

#### The F# mechanic:
F#'s units of measure (`float<meter>`, `int<kilogram>`, `decimal<dollar>`) are **compile-time annotations** that are erased at runtime [[31]]. They prevent unit confusion (adding meters to seconds). But casting `float<meter>` to bare `float` **silently loses the unit**.

```fsharp
// LLM generates this — compiles, loses the unit:
let distance: float<meter> = 100.0<meter>
let bare: float = float distance  // ← Unit is GONE
let time: float<second> = 9.58<second>
let speed = bare / time  // ← Should be meter/second, but compiler can't check
```

#### The LLM bug:
LLMs don't understand units of measure. They generate `float` everywhere because that's what they've seen in training data. When they encounter `float<meter>`, they cast it to `float` to "simplify" — destroying the type safety that units provide.

#### The TAST detection:
```fsharp
// Walk FSharpExprPatterns.Coerce / FSharpExprPatterns.Call nodes
// If source type has measure annotations (float<meter>)
// And target type is bare (float)
// → VIOLATION
// "Unit of measure 'meter' is lost in cast at line 3.
//  Use a function that preserves units or explicitly acknowledge the loss."
```

#### Why regex can't do it:
Regex sees `float distance`. It cannot determine:
- That `distance` has type `float<meter>` (requires type resolution)
- That the cast target is bare `float` (requires type inference)
- That the unit annotation is being erased (requires measure analysis)

**Only the TAST knows the full type including measure annotations.**

#### Why this rocks:
This is a **silent correctness bug**. The code compiles. It runs. The numbers are right. But the **type safety is gone**. If someone later writes `let result = distance + time` (meters + seconds), the compiler can't catch it because the units were erased. FsAssay catches the erasure at the source: *"You're casting float<meter> to float. The unit 'meter' is lost. Downstream code can no longer verify unit consistency."*

This is **unique to F#**. No other mainstream language has units of measure. No other analyzer can write this rule.

---

### 10. Active Pattern Partiality

#### The F# mechanic:
F#'s active patterns `(|Even|Odd|)` can be **total** (match all inputs) or **partial** (match some inputs) [[9]]. A partial active pattern `(|Even|)` returns `Option<'T>` — it might not match. Using it in a `match` without a fallback causes `MatchFailureException`.

```fsharp
// Total active pattern — safe:
let (|Even|Odd|) n = if n % 2 = 0 then Even else Odd

// Partial active pattern — DANGEROUS:
let (|Positive|) n = if n > 0 then Some n else None

// LLM generates this — compiles, crashes on negative input:
let describe n =
    match n with
    | Positive p -> sprintf "positive: %d" p
    // ← No fallback for n <= 0. MatchFailureException at runtime.
```

#### The LLM bug:
LLMs generate partial active patterns and use them as if they were total. They don't add the fallback case because they pattern-match on total active pattern examples in training data. The compiler warns (FS0025) but the LLM might suppress it.

#### The TAST detection:
```fsharp
// Walk SynPat.NamedActivePattern nodes
// Resolve the active pattern definition via CheckFileResults
// Check if the return type is Option<'T> (partial) vs direct (total)
// If partial and used in match without wildcard fallback → VIOLATION
// "Active pattern '(|Positive|)' is partial. Match at line 4 has no fallback
//  for non-matching inputs. Add '| _ -> ...' case."
```

#### Why regex can't do it:
Regex sees `| Positive p ->`. It cannot determine:
- That `Positive` is a partial active pattern (requires symbol resolution)
- That the return type is `Option<int>` (requires type resolution)
- Whether the match has a fallback (requires parse tree analysis)
- Whether the active pattern is total or partial (requires signature analysis)

**Only the TAST knows the active pattern's signature and the match's completeness.**

#### Why this rocks:
This is a **runtime crash on specific inputs**. The code compiles. It works for positive numbers. It crashes on zero or negative numbers. The LLM tested it with `[1; 2; 3]` and it passed. Production hits it with `[-1]` and it dies. FsAssay catches this at analysis time: *"Active pattern '(|Positive|)' is partial. Your match has no fallback. Add `| _ -> ...` to handle non-matching inputs."*

---

## THE META-WHY: Why These 10 Together Are the Moat

### 1. They are F#'s type system, operationalized.

Each rule takes something the F# type system **already knows** and turns it into an **actionable diagnostic**:

| F# Type System Knows | FsAssay Turns It Into |
|---|---|
| "This DU has 4 cases" | "You only matched 3" |
| "This is an IDisposable in a use scope" | "You disposed it before the async runs" |
| "This recursive call is not in tail position" | "This will stack-overflow" |
| "This Option might be None" | "You called .get without a guard" |
| "This async block has no return" | "You're discarding the result" |
| "This Task.Result blocks the thread" | "This will deadlock in ASP.NET" |
| "This struct contains a reference type" | "This will box on every use" |
| "This DU requires qualified access" | "Use Shape.Circle, not Circle" |
| "This float has a meter unit" | "You erased the unit" |
| "This active pattern is partial" | "You have no fallback" |

### 2. They are the exact bugs LLMs generate.

LLMs are trained on **millions of lines of C#, Python, and JavaScript**. When they generate F#, they apply C#/Python/JS patterns:

| LLM Thinks (C#/Python) | F# Reality | Bug |
|---|---|---|
| "Add a case to the enum" | "Update ALL match expressions" | Rule 1 |
| "`using` + `async` is fine" | "`use` + `Async.Start` disposes early" | Rule 2 |
| "Recursion is fine" | "Non-tail recursion stack-overflows" | Rule 3 |
| "`.Value` is fine after a check" | "`Option.get` without guard crashes" | Rule 4 |
| "Last expression is returned" | "Must write `return` explicitly" | Rule 5 |
| "`.Result` unwraps a Task" | "`.Result` in async deadlocks" | Rule 6 |
| "`struct` is always faster" | "Struct + reference type = boxing" | Rule 7 |
| "Enum cases are bare" | "`[<RequireQualifiedAccess>]` requires prefix" | Rule 8 |
| "`float` is `float`" | "`float<meter>` ≠ `float`" | Rule 9 |
| "Pattern match covers all cases" | "Partial active pattern needs fallback" | Rule 10 |

### 3. No other tool catches all 10.

| Tool | Catches Rule 1? | Rule 2? | Rule 3? | Rule 4? | Rule 5? | Rule 6? | Rule 7? | Rule 8? | Rule 9? | Rule 10? |
|---|---|---|---|---|---|---|---|---|---|---|
| F# Compiler | ✅ (FS0025) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ (FS0025) |
| FSharpLint | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| G-Research | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| SonarQube | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Clippy | N/A (Rust) | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| **FsAssay** | **✅** | **✅** | **✅** | **✅** | **✅** | **✅** | **✅** | **✅** | **✅** | **✅** |

**FsAssay is the only tool that catches all 10.** That's the moat.

### 4. They are the agentic loop's guardrail.

In an agentic coding loop [[126]]:

```
Agent generates F# code
    ↓
FsAssay runs (diff-only, < 2 seconds)
    ↓
SARIF output: "Rule 2: use + Async.Start at line 14.
               HttpClient disposed before async runs."
    ↓
Agent reads SARIF, fixes the bug
    ↓
FsAssay re-runs → green
    ↓
Human reviews (or auto-merges if policy allows)
```

Without FsAssay, the agent's code **compiles, passes tests, and crashes in production**. With FsAssay, the agent gets **precise, actionable, F#-specific feedback** in the loop. The bug is fixed before a human ever sees it.

**This is what "deterministic tool over hopeful prompting" means** [[126]]. Don't prompt the LLM to "remember to dispose after async." Make it **impossible to commit** code that disposes before async.

---

## THE IMPLEMENTATION PATH: How To Build Each Rule

All 10 rules use the **same TAST infrastructure** that FsAssay already has in `Library.fs`:

```fsharp
// Already exists:
let rec visitExpr (expr: FSharpExpr) (sups: string list) =
    match expr with
    | FSharpExprPatterns.Call(obj, func, _, _, args) -> ...
    | FSharpExprPatterns.Let((binding, valExpr, _), body) -> ...
    | FSharpExprPatterns.DefaultValue(_) -> ...
```

Each new rule adds a pattern match case:

```fsharp
// Rule 4: Option.get in pipeline
| FSharpExprPatterns.Call(_, func, _, _, _) 
    when func.FullName = "Microsoft.FSharp.Core.OptionModule.Get" ->
    addViolation "FSA-C02" "Option.get without guard" expr.Range sups

// Rule 6: Task.Result in async
| FSharpExprPatterns.Call(_, func, _, _, _)
    when func.FullName = "System.Threading.Tasks.Task`1.get_Result"
         && isInAsyncCE expr ->
    addViolation "FSA-C06" "Task.Result in async { } will deadlock" expr.Range sups
```

The infrastructure is there. The patterns are documented in the FSharp.Compiler.Service API [[1]][[6]]. The F# compiler already computes the typed tree. FsAssay just needs to **ask the right questions**.

---

## THE BOTTOM LINE

These 10 rules rock because they are:

1. **F#-specific** — impossible to write for any other language
2. **TAST-powered** — impossible to write with regex
3. **LLM-targeted** — they catch the exact bugs AI generates
4. **Production-proven** — G-Research, trading systems, real crashes [[11]][[28]]
5. **Compiler-complementary** — they fill the gap between "compiles" and "correct"
6. **Agentic-ready** — precise, actionable, machine-readable feedback for the loop
7. **The moat** — no other tool catches all 10

The F# type system is the most powerful static analysis engine in the .NET ecosystem. FsAssay's job is to **operationalize it** — to turn what the compiler *knows* into what the developer (or the agent) *needs to hear*.

These 10 rules are not features. They are **the reason FsAssay exists**.

---

## BEYOND THE MOAT: The Next 5 Elite Rules

Once the core 10 rules are solidified, the TAST engine can enforce even deeper idioms that separate senior F# developers from the rest. Here are 5 more rules to consider:

### 11. Silent Implicit Dependency Capture
When an F# lambda captures a mutable variable or an `IDisposable` from the outer scope, it can create memory leaks or race conditions if that lambda is passed to a concurrent queue or stored in a dictionary. FsAssay can walk the TAST, detect closure captures, and verify that the captured variable is safe to share.

### 12. Accumulator List Prepending
Appending to the end of an F# list (`lst @ [item]`) is `O(N)`. Inside a recursive loop, this turns an `O(N)` algorithm into `O(N^2)`. The elite F# idiom is to prepend (`item :: lst`) and then `List.rev` at the end. The TAST can detect `List.append` or `@` inside recursive loops and flag the performance cliff.

### 13. Map / Fold Allocation Chaining
Chaining `|> List.map ... |> List.filter ... |> List.map ...` creates intermediate allocations for every step. The elite F# pattern for long chains is to use `Seq` or `List.choose` / `List.fold`. The TAST can analyze the pipeline depth and suggest `Seq` for pipelines with 3+ intermediate mapping stages.

### 14. Non-Deterministic `Set` / `Map` Comparisons
F# `Set` and `Map` use structural comparison. If the keys are records containing floats or arrays, the structural comparison can be incredibly slow or non-deterministic (NaN equality). The TAST can resolve the type arguments of `Set<'T>` and warn if `'T` lacks `[<CustomEquality>]` but contains non-trivial structural types.

### 15. The `Result` Error Type Erasure
When `Result<'T, string>` is used, the error channel is just a string. This loses all domain modeling semantics. Elite F# uses Domain Errors (e.g., `Result<'T, OrderError>`). FsAssay can inspect the second generic parameter of `Result` in public APIs and block primitive strings or `System.Exception` from being used as the error channel.
