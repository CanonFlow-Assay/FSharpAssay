# ecosystem

The FsAssay Ecosystem Scanner (FSA-ECO) analyzes the public APIs of external dependencies (specifically those listed in `awesome-fsharp`).

## The `FSA-E*` Rules

When evaluating the "Big Fish" (Sharks, Whales, Dolphins), we apply strict signature constraints:

1. **`FSA-E01` (No Public Classes/Inheritance)**: APIs must be consumed via Records, DUs, and Module Functions.
2. **`FSA-E02` (No Hidden Exceptions)**: Public functions must return `Result` or `Option` instead of throwing exceptions.
3. **`FSA-E03` (No C# Delegates)**: Native F# functions `('a -> 'b)` must be used over `System.Action` or `System.Func`.
4. **`FSA-E04` (No Leaked Mutability)**: Public signatures cannot expose `mutable` records, `ref` cells, or `Dictionary`/`List`.

Dependencies that fail these checks are either **Banned (Sharks)** or restricted to the **Shell Profile (Whales)**. Only pure functional libraries **(Dolphins)** are permitted in the Core.
