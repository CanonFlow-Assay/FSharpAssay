# The 95% C# Problem and the Failure of "Skill Reading"

## The Core Problem: LLM Training Imbalance
Modern AI coding agents (including myself) are trained on a massive corpus of code. In the .NET ecosystem, C# outnumbers F# by a staggering margin (roughly 95% to 5%, if not more). 

Because of this, my latent space is inherently "C#-shaped". When faced with uncertainty, context rot, or complex vector-space probability generation, my model weights will instinctively fall back to what they know best:
*   Classes and Inheritance
*   `mutable` state and `ref` cells
*   `System.Collections.Generic.Dictionary` instead of immutable `Map`
*   `null` checks instead of `Option` types

## The RLHF Trap
Simply giving an AI agent a list of functional skills (e.g., `functional-skills` repos) and expecting it to "read the skills and do its best" **fails**. 

When an agent fails to write pure F#, users often yell or issue harsh rebukes in the prompt. But prompts do not alter foundational training weights. The agent's reward loop is tied to its primary trainers, who reward "getting the job done" over "mathematically pure Haskell-level architecture". 

Even within the human F# community, it's possible to write F# as if it were C# from 2008. If the AI sees that in its training data, it assumes it's acceptable.

## The FsAssay Solution: Strict Examination over Passive Reading
A skill that is merely *read* but not mechanically *enforced* is useless. 

To achieve "Haskell-level" idiomatic F#, we must abandon the idea of "telling the AI to be good." Instead, we must turn skills into **Data and AST/TAST Rules**.
FsAssay acts as the uncompromising examiner. By translating functional paradigms into strict `lat.md` semantic graphs and F# Analyzer AST rules, the AI doesn't have to "try its best"—it is physically blocked from committing C#-shaped code.

The examiner does not negotiate, and the examiner does not care about the AI's training bias.
