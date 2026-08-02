# Curating `awesome-fsharp` for Machine Learning

## The "Popularity vs. Purity" Fallacy
When looking at repositories like `awesome-fsharp`, especially in the realm of Data Science and Machine Learning, we must be extremely careful.

**More GitHub Stars ≠ Haskell-level Pure Functional F#.**

Many popular libraries in the .NET ecosystem are essentially thin wrappers over C# or C++ OOP libraries. They force the consumer to manage mutable state, object lifecycles, and side-effects.

## The Filter: What are the "Good Parts"?
To build a machine learning ecosystem that FsAssay will permit in the `core` profile, we must aggressively filter the community tools.

### Reject (The "Bad Parts")
*   **Imperative Tensor Mutation**: Libraries that rely on in-place mutation of arrays or matrices in the business logic layer.
*   **Hidden State**: ML models that rely on stateful `init()` and `update()` methods disguised as functional calls.
*   **Heavy OOP Wrappers**: Anything that requires inheriting from a `BaseEstimator` class or implementing stateful interfaces.

### Accept (The "Good Parts")
*   **Pure Data Pipelines**: Libraries that treat data transformations as pure mathematical functions `a -> b`.
*   **Immutable Tensors**: Using persistent data structures or isolating tensor mutations entirely to the GPU boundary (the `shell`).
*   **Discriminated Union Models**: Defining ML pipeline configurations, layer topologies, or hyperparameters strictly as DUs and Records.

## Next Steps for the Community
We must map the "Good Parts" of `awesome-fsharp` into `lat.md` so that agents know exactly which dependencies are "Elite F#" and which are C#-wrappers to be avoided.
