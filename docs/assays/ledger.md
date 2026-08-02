# FsAssay Rule Ledger

| Rule Code | Description | Status |
|---|---|---|
| FSA-C01 | Unchecked.defaultof<_> in Non-Interop Code | Implemented |
| FSA-C02 | Option.get / .Value Without Guard | Implemented |
| FSA-C03 | Async.RunSynchronously in Library Code | Implemented |
| FSA-C04 | IDisposable Disposed Before Async Runs | Delegated |
| FSA-C05 | Incomplete Pattern Match on DU | Delegated |
| FSA-C06 | failwith / invalidArg / raise in Public API | Implemented |
| FSA-C07 | Non-Tail Recursion in let rec | Proposed |
| FSA-C08 | Seq.length on Infinite Sequences | Implemented |
| FSA-C09 | Null Checking (isNull / = null) Instead of Option | Implemented |
| FSA-C10 | Mutable State Instead of Functional Constructs | Implemented |
| FSA-C11 | Use _.Property shorthand for lambdas (F# 8+) | Proposed |
| FSA-C12 | Use nested record updates (F# 8+) | Proposed |
| FSA-C13 | Missing [<TailCall>] attribute on recursive function | Proposed |
| FSA-C14 | Evasion: Use of ref cells or Dictionary to bypass mutability rules | Proposed |
| FSA-C15 | Catalogue Violation: Direct use of known effectful sink in core logic | Implemented |
| FSA-C16 | Catalogue Violation: Direct use of known mutable collection | Proposed |
| FSA-S01 | Hard-Coded Credentials / Secrets | Implemented |
| FSA-S02 | Path Traversal in File Operations | Implemented |
| FSA-S03 | Swallowed Exceptions | Implemented |
| FSA-S04 | async { ... } Missing return | Proposed |
| FSA-S05 | Task.Result / .Wait() Blocking Calls | Implemented |
| FSA-ML01 | Raw array mutation in core ML logic. Use pure Tensors. | Proposed |
| FSA-ML02 | OOP Inheritance in ML Model. Use pure DUs/Records. | Proposed |
| FSA-B01 | Mutable state / arrays detected outside 'shell' profile. | Proposed |
| FSA-1301 | EF Core DbContext leakage outside shell/oracle profile | Proposed |
| FSA-1402 | Argu ParseResults leakage outside cli/shell profile | Proposed |
| FSA-F01 | No Throwing in Core | Proposed |
| FSA-F02 | Total Pattern Matching | Proposed |
| FSA-F03 | Enforce Result Binding over Imperative Checks | Proposed |
| FSA-F04 | No Implicit Unit Sequences in Core | Implemented |
| FSA-F05 | Domain Signature Purity | Proposed |
| FSA-F06 | Total Immutable Enforcement | Proposed |
| FSA-F07 | Ban Classes in Domain | Proposed |
| FSA-F08 | Effectful or impure operation detected inside a computation expression | Implemented |
| FSA-E01 | No Public Classes/Inheritance in API | Proposed |
| FSA-E02 | No Hidden Exceptions in API | Proposed |
| FSA-E03 | No C# Delegates (Action/Func) in API | Proposed |
| FSA-E04 | No Leaked Mutability in API | Proposed |
| FSA-M01 | Struct DU contains reference fields | Proposed |
| FSA-M03 | Unit-of-measure loss via implicit cast | Proposed |
| FSA-M04 | Active pattern partiality without fallback | Proposed |
| FSA-AI10 | Magic numbers: numeric literals > 1 in non-test code | Implemented |
| FSA-AI07 | Overly Generic: more than 5 generic parameters in a function/method | Implemented |

## Summary
- **Implemented**: 16
- **Delegated**: 2
- **Proposed**: 25
- **Dummy**: 0
- **Prototype**: 0
