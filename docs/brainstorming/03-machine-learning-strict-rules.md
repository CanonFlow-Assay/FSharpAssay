# Machine Learning under FsAssay Strict Rules

If we are to bring Machine Learning into the FsAssay ecosystem, we must convert functional ML skills into strict AST rules.

## The Architecture
Machine Learning inherently requires highly optimized, often mutable operations (like matrix multiplication). However, we must preserve the user's requirement for "Haskell-level" purity.

This is solved by FsAssay's strict boundary laws.

### The Shell (Effectful, Mutable)
*   **GPU Interop**: Calls to CUDA, Torch, or ONNX runtimes.
*   **Allowed Operations**: Here, and *only* here, `mutable` arrays and imperative `for` loops are permitted for performance.
*   **FsAssay Enforcement**: These files must be explicitly tagged with `[<Profile("shell")>]`.

### The Core (Pure, Mathematical)
*   **Model Topology**: The definition of the neural network or statistical model must be defined using pure data (Records and DUs).
*   **Training Loop Logic**: The orchestration of the training loop (e.g., `Epoch -> State -> State`) must be a pure fold/reduce over an immutable state record.
*   **FsAssay Enforcement**: No mutable tensors. No hidden OOP layers. If an agent tries to use an ML library that demands mutability in the core, FsAssay's TAST analyzer will reject the assembly call as an "Uncatalogued Impure Effect".

## Turning Skills into Data
To prevent the "yelling at the AI" loop, we will codify this into `FsAssay.Analyzers`:

1.  **Rule `FSA-ML01`**: Reject direct references to `System.Array` or `float[]` mutations in the `core` profile. Enforce the use of pure functional tensor wrappers.
2.  **Rule `FSA-ML02`**: Detect and block ML libraries that require class inheritance (e.g., forcing the user to inherit from `Torch.nn.Module`). Agents must use functional composition instead.
