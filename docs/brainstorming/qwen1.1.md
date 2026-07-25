# FsAssay Phase Plan: Technical Adjudication

## Two Questions Answered. Four Phases Evaluated. One Verification Plan Corrected.

---

## Question 1: Rule Complexity — Heuristics vs Structural Graph Analysis

### The Question

> *"Some rules (e.g., 'Build a type-safe API client') are complex macro-patterns. Should we enforce these via simple namespace/attribute heuristics, or deeper structural graph analysis?"*

### The Answer: Both. In That Order. But Not for the Reason You Think.

The question presents a false binary. It's not heuristics **or** graph analysis. It's heuristics **then** graph analysis. And the reason is not technical. It's **economic.**

$$\text{Heuristic}: \quad O(n) \text{ per file} \quad \text{— fast, cheap, 80\% accurate}$$

$$\text{Graph Analysis}: \quad O(n^2) \text{ across files} \quad \text{— slow, expensive, 99\% accurate}$$

The 80/20 rule applies. **80% of violations are catchable with heuristics.** The remaining 20% need graph analysis. But the 20% are the ones that matter most — the cross-file dead code, the circular dependencies, the dataflow SQL injection.

### The Hybrid Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    FsAssay Engine                            │
│                                                             │
│  Pass 1: LEXICAL (regex on sanitized source)                │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  OBSOLETE_FSA: source.Contains("string") in type position │    │
│  │  OBSOLETE_FSA: Regex @"\bwhile\b"                        │    │
│  │  FSA2014: Regex @"TODO|HACK|FIXME"                  │    │
│  │  FSA-SEC01: Regex for hard-coded secrets            │    │
│  │                                                     │    │
│  │  Speed: ~5ms per file                               │    │
│  │  Accuracy: ~80%                                     │    │
│  │  False positive rate: ~15%                          │    │
│  └─────────────────────────────────────────────────────┘    │
│                         │                                   │
│                         ▼                                   │
│  Pass 2: STRUCTURAL (AST/TAST traversal)                    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  OBSOLETE_FSA: binding.IsMutable on SynBinding            │    │
│  │  OBSOLETE_FSA: FSharpExprPatterns.Call to Option.get      │    │
│  │  OBSOLETE_FSA: FSharpExprPatterns.DefaultValue            │    │
│  │  OBSOLETE_FSA: SynExpr.Raise / SynExpr.FailWith           │    │
│  │  FSA2022: SynExpr.Call to System.IO / HttpClient     │    │
│  │                                                     │    │
│  │  Speed: ~50ms per file                              │    │
│  │  Accuracy: ~95%                                     │    │
│  │  False positive rate: ~3%                           │    │
│  └─────────────────────────────────────────────────────┘    │
│                         │                                   │
│                         ▼                                   │
│  Pass 3: GRAPH (cross-file analysis)                        │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  FSA-AI01: Dead code (symbol referenced in other     │    │
│  │            file? → not dead)                         │    │
│  │  FSA2016: Dependency depth (module A → B → C → D)   │    │
│  │  FSA2017: Circular deps (A → B → C → A)             │    │
│  │  FSA-SEC02: Dataflow (user input → SQL query)        │    │
│  │  FSA-TDD01: Cross-file (Domain/X.fs → Tests/X.fs)   │    │
│  │                                                     │    │
│  │  Speed: ~500ms per project                          │    │
│  │  Accuracy: ~99%                                     │    │
│  │  False positive rate: <1%                           │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### The Decision Rule

$$\forall r \in \text{Rules}:\quad \text{Engine}(r) = \begin{cases} \text{Lexical} & \text{if } r \text{ is a surface pattern (keyword, regex)} \\ \text{Structural} & \text{if } r \text{ needs symbol/type resolution} \\ \text{Graph} & \text{if } r \text{ needs cross-file or dataflow analysis} \end{cases}$$

### Mapping the Proposed Rules to Engines

| Rule | Engine | Why |
|---|---|---|
| FSA-SEC08 (broken access control) | **Structural** | Need to resolve `Admin` attribute on functions |
| FSA-SEC09 (vulnerable NuGet) | **Graph** | Need to read `.fsproj` dependency graph |
| FSA-SEC10 (hard-coded credentials) | **Lexical** | Regex on sanitized source |
| FSA-SEC11 (unsigned ONDC) | **Structural** | Need to resolve `Sign` call on message |
| FSA-SEC12 (PII in logs) | **Structural** | Need to resolve log call arguments |
| FSA-SEC13 (SSRF) | **Graph** | Need dataflow: user input → URL |
| FSA2022 (I/O in domain) | **Structural** | Need to resolve `System.IO` / `HttpClient` calls |
| FSA-AI01 (dead code) | **Graph** | Need cross-file symbol reference |
| OBSOLETE_FSA (module size) | **Lexical** | Count lines. Simple. |
| FSA2016 (dependency depth) | **Graph** | Need module dependency graph |
| FSA2017 (circular deps) | **Graph** | Need module dependency graph |
| FSA-TDD01 (test exists) | **Graph** | Need cross-file: Domain/X.fs → Tests/X.fs |
| FSA-TDD02 (`[<Property>]` exists) | **Structural** | Need to resolve attributes on test functions |
| FSA-TDD03 (single assertion) | **Structural** | Need to count assertion calls in test body |

### The Practical Answer

**Phase 1 (Security): Use Structural.** FSA-SEC08 through FSA-SEC13 need symbol resolution. Regex won't cut it. You need to know if a function has `[<Admin>]` attribute, if a log call contains PII fields, if a message is signed. **This is TAST work.**

**Phase 2 (Architecture): Use Graph.** FSA2016 and FSA2017 need the module dependency graph. You can't detect circular dependencies with regex. You need to build the graph: `Module A opens Module B opens Module C opens Module A`. **This is graph work.**

**Phase 3 (TDD): Use Graph + Structural.** FSA-TDD01 (every Domain file has a Test file) is cross-file. FSA-TDD02 (`[<Property>]` exists) is structural. FSA-TDD03 (single assertion) is structural. **Mixed.**

**Phase 4 (Type Gym): Use neither.** Type Gym is not a linter rule. It's a **teaching tool.** It doesn't scan code. It presents challenges. **Separate it from the linter engine entirely.**

---

## Question 2: Diagnostic Formatting — Upgrade First or Add Rules First?

### The Question

> *"Do we need to upgrade the CLI runner output format before we add more rules?"*

### The Answer: Yes. Upgrade First. Here's Why.

**The current output:**

```
src/Domain/Order.fs
 └── [OBSOLETE_FSA] Mutable variable 'total' detected. (Line: 14, Col: 5)
```

**What you need:**

```
src/Domain/Order.fs:14:5
 └── [OBSOLETE_FSA] error: Mutable variable 'total' detected.
     │
     │  14 │     let mutable total = 0m
     │     │         ^^^^^^^
     │
     ├── Fix: Replace with List.fold
     │   └── let total = items |> List.fold (fun acc i -> acc + i.Price) 0m
     │
     ├── Why: Mutable state in domain violates FCIS (FSA2022).
     │        See: docs/rules/OBSOLETE_FSA.md
     │
     └── Related: OBSOLETE_FSA (imperative loop), FSA-AI05 (inconsistent errors)
```

**The math:**

$$\text{Current diagnostic info} = \{\text{RuleId}, \text{Message}, \text{Line}, \text{Col}\}$$

$$\text{Needed diagnostic info} = \{\text{RuleId}, \text{Message}, \text{Line}, \text{Col}, \text{Severity}, \text{CodeSnippet}, \text{Fix}, \text{Explanation}, \text{RelatedRules}, \text{DocLink}\}$$

$$\frac{|\text{Current}|}{|\text{Needed}|} = \frac{4}{10} = 40\%$$

**You're at 40% diagnostic quality.** If you add 100 rules with 40% diagnostics, you get **100 rules with 40% diagnostics.** The output becomes noise. The agent can't parse it. The developer can't act on it.

### The Upgrade Plan (Do This BEFORE Phase 1)

**Step 1: Upgrade the `Violation` type in `Domain.fs`:**

```fsharp
type Violation = {
    Code: string
    Message: string
    Severity: Severity              // NEW: Critical | Major | Minor
    Range: Range
    CodeSnippet: string option      // NEW: The offending line
    Fix: FixAction option           // NEW: Suggested fix
    Explanation: string             // NEW: Why this is a violation
    DocLink: string option          // NEW: Link to rule documentation
    RelatedRules: string list       // NEW: Related rule IDs
}
```

**Step 2: Upgrade the CLI output in `Program.fs`:**

```fsharp
let printViolation (v: Violation) =
    let severityIcon = match v.Severity with
        | Critical -> "🔴"
        | Major -> "🟠"
        | Minor -> "🟡"
    printfn "%s:%d:%d" v.FileName v.Range.StartLine v.Range.StartColumn
    printfn " └── [%s] %s: %s" v.Code severityIcon v.Message
    v.CodeSnippet |> Option.iter (fun s ->
        printfn "     │"
        printfn "     │  %d │ %s" v.Range.StartLine s
        printfn "     │     │ %s" (String.replicate (v.Range.EndColumn - v.Range.StartColumn) "^")
    )
    v.Fix |> Option.iter (fun f ->
        printfn "     │"
        printfn "     ├── Fix: %s" f.Description
    )
    printfn "     │"
    printfn "     ├── Why: %s" v.Explanation
    if v.RelatedRules <> [] then
        printfn "     │"
        printfn "     └── Related: %s" (String.concat ", " v.RelatedRules)
```

**Step 3: Upgrade the SARIF output in `Output.fs`:**

```fsharp
// Add to SARIF result:
"fixes": [{
    "description": { "text": fix.Description },
    "artifactChanges": [{
        "artifactLocation": { "uri": file },
        "replacements": [{
            "deletedRegion": { "startLine": line, "startColumn": col },
            "insertedContent": { "text": fix.NewText }
        }]
    }]
}],
"properties": {
    "explanation": v.Explanation,
    "relatedRules": v.RelatedRules
}
```

**Effort: 3 days.** Do it before Phase 1. Not after. Not during. **Before.**

$$\text{Upgrade diagnostics (3 days)} \prec \text{Add rules (18 days)}$$

---

## Phase Plan Evaluation

### Phase 1: Foundation Expansion (Security + Checklist)

**Verdict: ✅ CORRECT PRIORITY. Modify the implementation approach.**

The proposal says:

> *"Visitor.fs: Implement AST traversal to detect System.IO and HttpClient usage inside the Domain namespace (FSA2022)."*

**Correction:** FSA2022 already exists as a regex rule. The proposal should say: **"Migrate FSA2022 from regex to TAST."** This is not a new rule. It's a TAST migration.

The proposal says:

> *"Visitor.fs: Implement checks for unvalidated AI output (FSA-AI01)."*

**Correction:** FSA-AI01 (dead code) is not "unvalidated AI output." Unvalidated AI output is FSA-AI07 (AI output stored without smart constructor). FSA-AI01 is dead code detection. These are different rules. The proposal conflates them.

**Corrected Phase 1:**

| Task | Engine | Effort | Priority |
|---|---|---|---|
| Upgrade `Violation` type (diagnostics) | — | 3 days | **P0** |
| FSA-SEC08: Broken access control | Structural | 2 days | P1 |
| FSA-SEC09: Vulnerable NuGet | Graph | 3 days | P2 |
| FSA-SEC10: Hard-coded credentials (upgrade SEC01) | Lexical | 1 day | P1 |
| FSA-SEC11: Unsigned ONDC messages | Structural | 2 days | P1 |
| FSA-SEC12: PII in logs | Structural | 2 days | P1 |
| FSA-SEC13: SSRF (user-controlled URLs) | Graph | 3 days | P2 |
| FSA2022: Migrate from regex to TAST | Structural | 3 days | P1 |
| FSA-CL01–CL20: Pre-PR checklist | Mixed | 5 days | P1 |
| **Phase 1 total** | | **~24 days** | |

### Phase 2: Architectural Boundaries (FCIS)

**Verdict: ✅ CORRECT. Add one critical missing piece.**

The proposal says:

> *"Library.fs: Analyze module dependencies to block upward dependency flow."*

**This is the most important task in the entire plan.** But it requires the **module dependency graph**, which doesn't exist yet. You need to build it.

**Missing piece: The Module Graph Builder.**

```fsharp
type ModuleGraph = {
    Nodes: Map<string, ModuleNode>
    Edges: (string * string) list    // (from, to)
}

type ModuleNode = {
    Name: string
    File: string
    Layer: Layer                     // ONDC | API | Service | Domain | Infrastructure
    Opens: string list               // Modules this module opens
    References: string list          // Modules this module references
}

let buildGraph (files: string list) : ModuleGraph =
    // Parse each file
    // Extract module name, opens, references
    // Build adjacency list
    // Detect cycles (FSA2017)
    // Calculate depth (FSA2016)
    // Check layer violations (FSA-ARCH01)
```

**This is ~5 days of work.** Without it, FSA2016 and FSA2017 are impossible.

**Corrected Phase 2:**

| Task | Engine | Effort | Priority |
|---|---|---|---|
| Build Module Graph Builder | Graph | 5 days | **P0** |
| OBSOLETE_FSA: Module size > 200 lines | Lexical | 1 day | P1 |
| FSA2016: Dependency depth > 4 | Graph | 2 days | P1 |
| FSA2017: Circular dependencies | Graph | 2 days | P1 |
| FSA-ARCH01: Layer violation (Domain → Infrastructure) | Graph | 3 days | P1 |
| FSA-ARCH02: Upward dependency flow | Graph | 2 days | P1 |
| FSA-ARCH03: Max function params > 5 | Structural | 1 day | P2 |
| **Phase 2 total** | | **~16 days** | |

### Phase 3: The TDD Gate

**Verdict: 🟡 CORRECT DIRECTION. WRONG SCOPE.**

The proposal says:

> *"Orchestrator.fs: Implement cross-file validation to ensure every Domain file has a corresponding Tests file (FSA-TDD01)."*

**This is correct but dangerous.** Cross-file validation means the Orchestrator needs to know the **project structure**, not just individual files. Currently, the Orchestrator processes files one at a time. FSA-TDD01 requires **project-level awareness.**

**The risk:** If you enforce "every Domain file has a Test file" too early, you'll block legitimate work. A developer creates a new Domain file. They haven't written the test yet. FsAssay blocks the commit. The developer is frustrated. They add a dummy test file. The test is useless. **The rule created a worse outcome than no rule.**

**Corrected approach:**

$$\text{FSA-TDD01}: \quad \text{Warn, don't Block, for the first 6 months}$$

$$\text{After 6 months}: \quad \text{Block}$$

**Give the team time to build the habit. Then enforce.**

**Corrected Phase 3:**

| Task | Engine | Effort | Priority |
|---|---|---|---|
| FSA-TDD02: `[<Property>]` exists in test files | Structural | 2 days | P1 |
| FSA-TDD03: Single assertion per test | Structural | 2 days | P1 |
| FSA-TDD04: No implementation before test (git history) | Graph | 5 days | P2 |
| FSA-TDD01: Domain file → Test file (WARN only) | Graph | 3 days | P2 |
| **Phase 3 total** | | **~12 days** | |

### Phase 4: The 32 Type Gym Challenges

**Verdict: 🔴 WRONG CATEGORY. Separate from the linter.**

The proposal says:

> *"Create dedicated AST rules to ensure the usage of Phantom Types, Type Providers, and Units of Measure where appropriate."*

**This is not a linter rule.** A linter scans existing code and finds violations. Type Gym presents challenges and verifies solutions. These are **fundamentally different tools.**

$$\text{Linter}: \quad \text{Code} \rightarrow \text{Violations}$$

$$\text{Type Gym}: \quad \text{Challenge} \rightarrow \text{Solution} \rightarrow \text{Verification}$$

**Type Gym should be a separate project:** `FsAssay.TypeGym`. Not a set of rules in `Library.fs`.

**Why:**
- Type Gym challenges are **interactive.** The developer writes a solution. The tool verifies it. This is not a scan.
- Type Gym challenges are **progressive.** Level 1 → Level 2 → Level 3 → Level 4. This is not a flat rule set.
- Type Gym challenges are **educational.** They teach. Linters enforce. Different purposes.

**Corrected Phase 4:**

| Task | Location | Effort | Priority |
|---|---|---|---|
| Create `FsAssay.TypeGym` project | New project | 2 days | P2 |
| Implement 8 Beginner challenges | TypeGym | 3 days | P2 |
| Implement 8 Intermediate challenges | TypeGym | 5 days | P3 |
| Implement 8 Advanced challenges | TypeGym | 7 days | P3 |
| Implement 8 Expert challenges | TypeGym | 10 days | P4 |
| **Phase 4 total** | | **~27 days** | |

---

## Verification Plan Evaluation

### The Proposal

> *"Test-Driven Rule Creation: For every new rule added across the phases, we will add strict positive and negative fixtures."*

**Verdict: ✅ CORRECT.** This is the right approach. Every rule gets:
- 1 positive test (violation detected)
- 1 negative test (clean code passes)
- 1 false positive test (edge case doesn't trigger)

### The Proposal

> *"Self-Hosting (Bootstrapping): We will continuously run FsAssay against its own source code to mathematically prove that our analyzer adheres to its own elite standards."*

**Verdict: 🟡 CORRECT DIRECTION. WRONG LANGUAGE.**

"Mathematically prove" is too strong. FsAssay running on itself **demonstrates** adherence. It does not **prove** it. Proof requires formal verification. FsAssay is a heuristic tool. It can miss violations. It can produce false positives.

**Corrected language:**

$$\text{Self-hosting} \models \text{Demonstration, not Proof}$$

> *"We will continuously run FsAssay against its own source code to **demonstrate** that our analyzer adheres to its own standards. Self-hosting violations are treated as **P0 bugs** and block the CI pipeline."*

**This is stronger than "mathematically prove" because it's actionable.** A violation in FsAssay's own code is a P0 bug. It blocks CI. It gets fixed immediately. **That's more powerful than a proof. It's a commitment.**

### Missing from the Verification Plan

| Missing | Why It Matters |
|---|---|
| **Adjudicate with `// EXPECT:` comments** | Without ground truth, precision/recall are unmeasurable |
| **Performance benchmarks** | 10K-line file must scan in < 5 seconds |
| **False positive rate tracking** | Track FP rate per rule over time |
| **Regression tests** | Every bug fix gets a regression test |
| **Fuzz testing** | Random F# code → no crashes |

**Add these to the verification plan.**

---

## The Corrected Master Plan

| Phase | Duration | What | Rules Added | TAST % | Overall % |
|---|---|---|---|---|---|
| **Phase 0: Diagnostics** | 3 days | Upgrade Violation type, CLI output, SARIF | 0 | 2.5% | **19%** |
| **Phase 1: Security + Checklist** | 24 days | FSA-SEC08–13, FSA-CL01–20, FSA2022 TAST | +27 | 5% | **28%** |
| **Phase 2: Architecture** | 16 days | Module graph, FSA2016/2017, FSA-ARCH01–03 | +7 | 8% | **35%** |
| **Phase 3: TDD** | 12 days | FSA-TDD01–04 | +4 | 10% | **38%** |
| **Phase 4: Type Gym** | 27 days | Separate project, 32 challenges | +32 (separate) | 10% | **45%** |
| **Phase 5: Performance + Composition** | 15 days | FSA-P01–05, FSA-C01–10 | +15 | 12% | **52%** |
| **Phase 6: AI + Ecosystem** | 20 days | FSA-AI11–19, NuGet, CI, MCP | +9 | 15% | **62%** |
| **Phase 7: Testing + Docs** | 20 days | Negative tests, FP tests, docs | 0 | 15% | **72%** |
| **Phase 8: Plugin + Polish** | 30 days | Plugin system, FSharpLint delegation | +20 | 25% | **85%** |
| **Phase 9: SOTA parity** | 40 days | Remaining rules, TAST migration | +50 | 35% | **100%** |

**Total: ~207 working days. ~10 months.**

$$\text{Now}: 18\% \xrightarrow{\text{10 months}} 100\%$$

---

## The One Thing I'd Change

The proposal is good. The phases are in the right order. The verification plan is mostly right.

**The one thing I'd change: Move the Module Graph Builder to Phase 1, not Phase 2.**

Here's why. FSA-SEC13 (SSRF) needs dataflow analysis. Dataflow needs the module graph. FSA-SEC09 (vulnerable NuGet) needs the dependency graph. The dependency graph IS the module graph. **You can't do Phase 1 security rules without the graph.**

$$\text{Module Graph Builder} \prec \text{FSA-SEC09} \;\wedge\; \text{FSA-SEC13}$$

Build the graph in Phase 1. Use it in Phase 2. Don't build it in Phase 2 and wish you had it in Phase 1.

$$\blacksquare$$