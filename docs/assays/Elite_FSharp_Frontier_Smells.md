# Brainstorm: Enforcing "Elite" F# (Haskellish/OCamlish) via Code Smells

F#'s greatest strength—being a multi-paradigm, .NET-integrated hybrid—is also its greatest weakness. It allows developers to write "C# with `let` bindings." 

To write "Elite" F# (closer to Haskell/OCaml), we must overcome F#'s lack of advanced type system features like Higher-Kinded Types (HKTs), Generalized Algebraic Data Types (GADTs), and native Typeclasses. 

Since the compiler won't enforce these elite concepts for us, **FsAssay must capture the "missed opportunity" to use their F# equivalents as code smells.**

Here is a brainstorm of how `FsAssay` can detect when a developer is failing to write Elite F#, and what the F# equivalent rule should be.

---

## 1. The Monad / Typeclass Gap
F# does not have Typeclasses or HKTs, meaning we can't write `fmap` or `bind` over a generic `M<a>`. Instead, F# uses **Computation Expressions (CEs)** and **Record Dictionary Passing**.

*   **Smell: Nested Pattern Matching on Options/Results (The "Pyramid of Doom")**
    *   *Detection:* AST reveals a `match` expression inside another `match` expression, both matching on `Option` or `Result`.
    *   *Elite F# Solution:* Use a Computation Expression (`result { ... }` or `option { ... }`).
    *   *Proposed Rule:* **FSA2001: Missing Computation Expression** - "Deeply nested match statements on monadic types detected. Use a Computation Expression to flatten the flow."

*   **Smell: Heavy Interfaces / Abstract Classes**
    *   *Detection:* Defining an `interface` with 1-3 methods.
    *   *Elite F# Solution:* Emulate Haskell Typeclasses via "Dictionary Passing" (passing a record of functions).
    *   *Proposed Rule:* **FSA2002: Service Interface Obsession** - "Replace this small interface with a Record of function signatures for easier composition and partial application."

## 2. The GADT / Advanced Types Gap
F# lacks GADTs to tie type safety to specific constructor states. The F# alternative is using **Phantom Types** or breaking states into distinct **Discriminated Unions (DUs)**.

*   **Smell: "Stringly" or "Intly" Typed IDs (Primitive Obsession)**
    *   *Detection:* Functions accepting multiple parameters of the same primitive type (e.g., `let process (orderId: int) (customerId: int)`).
    *   *Elite F# Solution:* Use Single-Case DUs or Phantom Types to distinguish them (`type OrderId = OrderId of int`).
    *   *Proposed Rule:* **FSA2003: Signature Blindness** - "Consecutive primitive arguments of the same type detected. Wrap them in Single-Case DUs to prevent accidental swapping."

*   **Smell: Implicit State Machines via Flags**
    *   *Detection:* A Record or Class containing boolean flags like `IsPaid`, `IsShipped`, or stringly status fields.
    *   *Elite F# Solution:* Make illegal states unrepresentable using DUs where each state is a discrete type (e.g., `type Order = | Unpaid of UnpaidData | Paid of PaidData`).
    *   *Proposed Rule:* **FSA2004: Flag-Based State Machine** - "Do not use boolean flags to track state. Encode the state machine explicitly as a Discriminated Union."

## 3. The Purity / IO Gap
Haskell forces purity by confining side-effects to the `IO` monad. F# allows side-effects anywhere.

*   **Smell: Hidden Side Effects**
    *   *Detection:* A function that does not return `unit`, does not explicitly use `Async` or `Task`, yet calls external dependencies like `System.IO.File`, `Console.WriteLine`, or an `HttpClient`.
    *   *Elite F# Solution:* Push side effects to the boundary (Functional Core, Imperative Shell). If side effects must happen, the function signature should clearly indicate it (e.g., returning `Async<Result<T, Error>>`).
    *   *Proposed Rule:* **FSA2005: Impure Core** - "Function performs I/O but returns a pure value. Push I/O to the system boundaries or wrap the return type in Async/Task to signal the side effect."

## 4. The Functional Pipelining Gap
In Haskell, point-free style and function composition are natural. In F#, developers from OOP backgrounds often nest function calls inside parentheses.

*   **Smell: Deep Function Nesting**
    *   *Detection:* AST reveals nested function applications `f (g (h x))`.
    *   *Elite F# Solution:* The Forward Pipe operator `|>`.
    *   *Proposed Rule:* **FSA2006: Nested Function Application** - "Avoid deep parenthesis nesting. Refactor using the forward pipe operator (`x |> h |> g |> f`) for better readability."

*   **Smell: Complex If/Elif Chains on Domain Data**
    *   *Detection:* A large `if/elif/else` block doing property extraction and boolean logic.
    *   *Elite F# Solution:* F#'s unique **Active Patterns**.
    *   *Proposed Rule:* **FSA2007: Missed Active Pattern** - "Complex conditional logic detected. Refactor into an Active Pattern to maintain expression-oriented pattern matching."

## Summary
By actively scanning for these "C#-ish/Hybrid" crutches, `FsAssay` can act as an uncompromising mentor. It forces the developer to simulate a Haskell/OCaml-like environment in F# by pushing them toward Computation Expressions, Phantom Types, DUs, and Dictionary Passing.
