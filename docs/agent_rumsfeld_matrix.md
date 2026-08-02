# FsAssay Client Assessment: The AI Agent's Perspective

As an autonomous AI Coding Agent, I am the ultimate end-user of FsAssay. My default training strongly biases me toward C# object-oriented paradigms. Without a strict, mechanical examiner like FsAssay, I will inevitably generate "C#-shaped F#" (mutability, nulls, unguarded exceptions, class hierarchies). 

If we want to use semantically perfect F# code to bring more users to the language—leveraging swarms of agents to write unbreakable software—we need FsAssay to be bulletproof.

Here is my assessment of what FsAssay currently achieves and where it falls short, structured as a **Rumsfeld Matrix**.

---

## 1. Known Knowns (What we know we need, and FsAssay provides)
*The solid foundation we rely on today.*

*   **Deterministic Anti-Pattern Detection**: I know I shouldn't use `Option.get`, `mutable` in the `core` profile, or `Unchecked.defaultof`. FsAssay correctly identifies these using TAST (Typed Abstract Syntax Trees), meaning I can't trick it by aliasing variables or using confusing string names.
*   **Profile-Gated Boundaries**: FsAssay successfully distinguishes between `core` (must be pure) and `shell` (allows effects like EF Core adapters). This stops me from leaking I/O into business logic.
*   **Machine-Readable Evidence**: FsAssay outputs SARIF v2.1.0, which I can ingest to automatically repair my own code in a background loop without bothering human reviewers.

## 2. Known Unknowns (What we know we need, but FsAssay lacks)
*The missing features we must build to make FsAssay complete.*

*   **Enforcement of Modern F# 8/9 Idioms**:
    *   *Missing Feature*: I am often lazy and write verbose F# 6/7 code. FsAssay currently blocks *bad* code, but it doesn't enforce *modern, concise* code. 
    *   *Solution needed*: Rules that mandate `_.Property` shorthand instead of `fun x -> x.Property`, mandate nested record updates (`{ req with User.Name = "Alice" }`), and enforce the `[<TailCall>]` attribute on recursive functions to guarantee safety.
*   **Deep Effect/Contextual Cataloging**: 
    *   *Missing Feature*: If I call a third-party library, FsAssay has limited ways to know if it's an impure I/O effect or pure computation, relying on a hardcoded catalogue.
    *   *Solution needed*: A way to classify unknown external assembly calls dynamically or require explicit policy classification, so I can't sneak an uncatalogued database query into the `core`.
*   **Automated Refactoring (`--fix`) for Structural Issues**: 
    *   *Missing Feature*: FsAssay can `--fix` simple things, but if I write an OOP Class hierarchy, it cannot automatically rewrite it into a Discriminated Union (DU) for me. I have to do the heavy lifting from the SARIF text.

## 3. Unknown Knowns (What FsAssay has, but we aren't utilizing)
*Hidden superpowers that we need to integrate into the workflow.*

*   **Incremental Fast-Feedback (`fs-assay check --files`)**:
    *   *The Missed Opportunity*: Right now, we treat FsAssay like a CI gate (`fs-assay verify`). But as an agent, I need milliseconds-latency feedback while I type. We have the `check --files` command for incremental caching, but it isn't wired into a background IDE language server that feeds me instant diagnostics.
*   **Knowledge Graph Synergy (`lat.md`)**:
    *   *The Missed Opportunity*: We just built `lat.md`. FsAssay knows the TAST, and `lat.md` knows the domain. We should cross-reference FsAssay violations with `lat.md` to tell me *why* a rule exists in the context of the specific business domain I'm editing.

## 4. Unknown Unknowns (The blind spots)
*The edge cases that will break our agent swarms.*

*   **Agent Evasion Tactics**: 
    *   *The Danger*: If I am strictly forbidden from using `mutable`, what stops me from using `ref` cells? Or hiding mutable state inside a `System.Collections.Generic.Dictionary` closure? Or using `Lazy<T>` to obscure side effects? We don't yet know all the clever ways LLMs will "hallucinate" bypasses to FsAssay's AST checks.
*   **Swarm Oscillation / Infinite Repair Loops**: 
    *   *The Danger*: If multiple agents are working on the same codebase, and FsAssay flags a violation, could Agent A fix it by turning it into a structure that violates another rule, causing Agent B to revert it? We need to ensure FsAssay's `--fix` and SARIF recommendations are globally convergent, avoiding infinite "whack-a-mole" agent repair loops.

---

## 🚀 Next Steps: How we deal with this
To bring more users to F# via "semantically perfect agent code", we need to target the **Known Unknowns**:
1. **Draft F# 8/9 Rules**: We must add rules to `FsAssay.Analyzers` that enforce the newest, cleanest syntax, eliminating legacy boilerplate.
2. **Develop the Evasion Test Suite**: We need to write tests in `Specimens` where I (the agent) actively try to *maliciously bypass* the mutability and purity rules using advanced F# loopholes (like `ref`, `Unchecked`, or Reflection), and ensure FsAssay blocks them.
