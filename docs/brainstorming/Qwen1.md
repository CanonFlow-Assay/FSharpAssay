# $\mathcal{S}$KILLS $\rightarrow$ FsAssay + CanonFlow: Feature Embrace Requirements

## Deep Dive: `mattpocock/skills` · 21 Skills · 500+ Rules · Mapped to F#

---

## $\S 0$. What the Skills Repo Actually Is

Matt Pocock's `skills` repo is **not a linter.** It's a **behavioral enforcement system for AI agents.** 21 skills. 500+ rules. Each skill is a set of instructions that an AI agent reads before writing code. The agent is *prompted* to follow the rules.

**The critical insight:** These rules are enforced by **prompting.** The agent reads the skill. The agent *tries* to follow it. The agent *sometimes* follows it. The agent *sometimes doesn't.*

**FsAssay enforces by compilation.** The agent doesn't *try* to avoid `mutable`. FsAssay *blocks* the commit. The agent doesn't *try* to use `Result`. FsAssay *rejects* the `failwith`.

$$\text{Skills}: \quad \text{Agent} \xrightarrow{\text{prompt}} \text{Code} \xrightarrow{\text{maybe}} \text{Correct}$$

$$\text{FsAssay}: \quad \text{Agent} \xrightarrow{\text{write}} \text{Code} \xrightarrow{\text{must}} \text{Correct}$$

**The skills repo is the specification. FsAssay is the enforcement. CanonFlow is the generation.**

Together:

$$\text{Skills} \xrightarrow{\text{specify}} \text{Rules} \xrightarrow{\text{FsAssay enforces}} \text{Code} \xrightarrow{\text{CanonFlow generates}} \text{Artifacts}$$

---

## $\S 1$. Type Gym $\rightarrow$ FsAssay Type Challenge System

### What Type Gym Is

32 progressive type challenges. 4 levels. 8 challenges per level. Uses `Expect<Equal<A, B>>` type assertions. No runtime. Pure type-level verification. [[1]]

```typescript
// Type Gym: Beginner Challenge 1
type Result = MyType<"hello">;
type Expected = { value: "hello" };
type Test = Expect<Equal<Result, Expected>>;  // ← Compile-time assertion
```

### The F# Mapping

F# has no `Expect<Equal<A, B>>`. But it has **compile-time type assertions** via `typeof` and **FsCheck properties.** The mapping:

$$\text{TypeGym}_{\text{TS}} \xrightarrow{\text{embrace}} \text{TypeGym}_{\text{F\#}}$$

### Requirements

**Definition 1.1 — F# Type Challenge:**

$$\text{TypeChallenge} \triangleq \left\{ \begin{array}{l} \text{Id} : \text{string} \\ \text{Level} : \text{Beginner} \mid \text{Intermediate} \mid \text{Advanced} \mid \text{Expert} \\ \text{Prompt} : \text{NonEmptyString} \\ \text{Solution} : \text{F\# code} \\ \text{Assertion} : \text{TypeAssertion} \\ \text{Hints} : \text{NonEmptyString}^* \end{array} \right\}$$

**Definition 1.2 — Type Assertion (F# equivalent of `Expect<Equal<A, B>>`):**

$$\text{TypeAssertion} \triangleq \left\{ \begin{array}{l} \text{CompileTime} : \text{typeof<'a> = typeof<'b>} \\ \text{Runtime} : \text{FsCheck.Property} \\ \text{FsAssay} : \mathcal{F}(\text{solution}) = \emptyset \end{array} \right\}$$

**Axiom 1.1 — Progressive Difficulty:**

$$\forall c_i, c_j \in \text{Challenges}:\quad \text{Level}(c_i) < \text{Level}(c_j) \implies \text{Complexity}(c_i) < \text{Complexity}(c_j)$$

**Axiom 1.2 — No Runtime:**

$$\forall c \in \text{Challenges}:\quad \text{Eval}(c) = \text{Compile}(c)$$

*Challenges are verified at compile time. No execution.*

**Axiom 1.3 — FsAssay Integration:**

$$\forall c \in \text{Challenges}:\quad \text{Valid}(c) \iff \text{Compile}(c) = \top \;\wedge\; \mathcal{F}(c.\text{Solution}) = \emptyset$$

*A challenge solution must compile AND pass all FsAssay rules.*

### The 32 F# Challenges

| Level | # | Challenge | F# Concept | FsAssay Rule Tested |
|---|---|---|---|---|
| **Beginner** | 1 | Create a `PhoneNumber` smart constructor | Private DU + `Result` | FSA1004 |
| | 2 | Replace `string` status with DU | Discriminated Union | FSA1004 |
| | 3 | Replace `null` with `Option` | Option type | FSA1003 |
| | 4 | Replace `failwith` with `Result` | Result type | FSA1006 |
| | 5 | Replace `mutable` with `fold` | Immutable accumulation | FSA1001 |
| | 6 | Replace `for` loop with `Seq.map` | Functional iteration | FSA1007 |
| | 7 | Replace `inherit` with composition | Module composition | FSA1008 |
| | 8 | Replace `printfn` with structured log | Shell separation | FSA2012 |
| **Intermediate** | 9 | Build a state machine with `Result` transitions | State machine | FSA1006 |
| | 10 | Build a parser combinator | Computation expression | FSA1007 |
| | 11 | Build a validation pipeline | `Result` chaining | FSA1006 |
| | 12 | Build a type-safe API client | Type provider | FSA1004 |
| | 13 | Build a lens for nested records | Functional update | FSA1001 |
| | 14 | Build a type-safe SQL query | Compile-time SQL | FSA-SEC02 |
| | 15 | Build a type-safe configuration reader | Type provider | FSA1004 |
| | 16 | Build a type-safe event system | DU + pattern match | FSA1004 |
| **Advanced** | 17 | Build a type-level state machine | Type-state pattern | FSA1006 |
| | 18 | Build a heterogeneous list | DU wrapper | FSA1004 |
| | 19 | Build a type-safe DSL | Computation expression | FSA1007 |
| | 20 | Build a type-safe ORM | Type provider + SQL | FSA-SEC02 |
| | 21 | Build a type-safe routing table | Type-level strings | FSA1004 |
| | 22 | Build a type-safe permission system | Phantom types | FSA1004 |
| | 23 | Build a type-safe unit system | Units of measure | FSA1004 |
| | 24 | Build a type-safe effect system | CE + Result | FSA1006 |
| **Expert** | 25 | Build a type-level binary tree | Recursive types | FSA1004 |
| | 26 | Build a type-level regex engine | Type-level strings | FSA1004 |
| | 27 | Build a type-level JSON parser | Type provider | FSA1004 |
| | 28 | Build a type-level SQL optimizer | Type-level queries | FSA-SEC02 |
| | 29 | Build a type-level protocol validator | Beckn schema types | FSA1004 |
| | 30 | Build a type-level dependency graph | Type-level modules | FSA2017 |
| | 31 | Build a type-level permission calculus | Phantom types + constraints | FSA1004 |
| | 32 | Build a type-level ONDC message validator | Beckn v1.1 types | FSA1004 |

---

## $\S 2$. Type Checklist $\rightarrow$ FsAssay Pre-PR Quality Gate

### What Type Checklist Is

20-point pre-PR quality gate. Blocks PR if any item fails. Categories: Type Safety, API Design, Code Quality, Documentation. [[2]]

### The F# Mapping

$$\text{Checklist}_{\text{TS}} \xrightarrow{\text{embrace}} \text{Checklist}_{\text{F\#}} : \text{FsAssay Pre-Commit Gate}$$

### Requirements

**Definition 2.1 — F# Quality Checklist:**

$$\text{Checklist} \triangleq \{ c_1, c_2, ..., c_{20} \}$$

$$\forall c_i \in \text{Checklist}:\quad c_i \triangleq \left\{ \begin{array}{l} \text{Id} : \text{string} \\ \text{Category} : \text{TypeSafety} \mid \text{APIDesign} \mid \text{CodeQuality} \mid \text{Documentation} \\ \text{Rule} : \text{FsAssay Rule} \\ \text{Severity} : \text{Block} \mid \text{Warn} \\ \text{Message} : \text{NonEmptyString} \end{array} \right\}$$

**Axiom 2.1 — Gate Enforcement:**

$$\forall \text{PR}:\quad \text{Merge}(\text{PR}) \iff \forall c_i \in \text{Checklist}_{\text{Block}}:\quad c_i(\text{PR}) = \top$$

*PR is blocked if ANY blocking checklist item fails.*

### The 20 F# Checklist Items

| # | Category | Check | FsAssay Rule | Severity |
|---|---|---|---|---|
| 1 | TypeSafety | No `string` where smart constructor exists | FSA1004 | **Block** |
| 2 | TypeSafety | No `Option.get` / `.Value` | FSA1002 | **Block** |
| 3 | TypeSafety | No `null` / `Unchecked.defaultof` | FSA1003 | **Block** |
| 4 | TypeSafety | No `failwith` / `raise` in domain | FSA1006 | **Block** |
| 5 | TypeSafety | No `mutable` in domain | FSA1001 | **Block** |
| 6 | TypeSafety | No `obj` / `unbox` in domain | FSA1004 | **Block** |
| 7 | APIDesign | All public functions have XML doc | FSA-AI08 | Warn |
| 8 | APIDesign | All public types have XML doc | FSA-AI08 | Warn |
| 9 | APIDesign | No `printfn` in library code | FSA2012 | **Block** |
| 10 | APIDesign | No `System.IO` in domain | FSA2022 | **Block** |
| 11 | APIDesign | No `HttpClient` in domain | FSA2022 | **Block** |
| 12 | CodeQuality | No `while` loops in domain | FSA1007 | Warn |
| 13 | CodeQuality | No `inherit` (except `exn`) | FSA1008 | Warn |
| 14 | CodeQuality | No dead code | FSA-AI01 | Warn |
| 15 | CodeQuality | No duplicate code blocks > 6 lines | FSA-AI02 | Warn |
| 16 | CodeQuality | No magic numbers | FSA-AI10 | Warn |
| 17 | CodeQuality | Consistent error handling (Result everywhere) | FSA-AI05 | **Block** |
| 18 | Documentation | No `TODO` / `HACK` / `FIXME` | FSA2014 | Warn |
| 19 | Documentation | No commented-out code | FSA-AI04 | Warn |
| 20 | Security | No hard-coded secrets | FSA-SEC01 | **Block** |

**Axiom 2.2 — Blocking Count:**

$$|\{c_i : c_i.\text{Severity} = \text{Block}\}| = 11$$

*11 items block the PR. 9 items warn.*

---

## $\S 3$. Code Review $\rightarrow$ FsAssay Severity-Based Review

### What Code Review Is

6-dimension severity-based review. Correctness, Security, Performance, Maintainability, Test Coverage, Best Practices. Severity: Critical/Major/Minor. Blocks on Critical. [[3]]

### The F# Mapping

$$\text{Review}_{\text{TS}} \xrightarrow{\text{embrace}} \text{Review}_{\text{F\#}} : \text{FsAssay Multi-Dimension Scan}$$

### Requirements

**Definition 3.1 — Review Dimension:**

$$\text{Dimension} \triangleq \left\{ \begin{array}{l} \text{Correctness} : \mathcal{F}_{\text{correct}} \\ \text{Security} : \mathcal{F}_{\text{sec}} \\ \text{Performance} : \mathcal{F}_{\text{perf}} \\ \text{Maintainability} : \mathcal{F}_{\text{maint}} \\ \text{TestCoverage} : \mathcal{F}_{\text{test}} \\ \text{BestPractices} : \mathcal{F}_{\text{bp}} \end{array} \right\}$$

**Definition 3.2 — Severity:**

$$\text{Severity} \triangleq \text{Critical} \mid \text{Major} \mid \text{Minor}$$

**Axiom 3.1 — Critical Blocks:**

$$\forall v \in \text{Violations}:\quad v.\text{Severity} = \text{Critical} \implies \text{Block}(\text{commit})$$

**Axiom 3.2 — Major Warns:**

$$\forall v \in \text{Violations}:\quad v.\text{Severity} = \text{Major} \implies \text{Warn}(\text{commit}) \;\wedge\; \text{RequireJustification}$$

**Axiom 3.3 — Minor Informs:**

$$\forall v \in \text{Violations}:\quad v.\text{Severity} = \text{Minor} \implies \text{Inform}(\text{commit})$$

### FsAssay Rule → Dimension → Severity Mapping

| FsAssay Rule | Dimension | Severity |
|---|---|---|
| FSA-SEC01 (hard-coded secrets) | Security | **Critical** |
| FSA-SEC02 (SQL injection) | Security | **Critical** |
| FSA-SEC04 (weak crypto) | Security | **Critical** |
| FSA-SEC05 (disabled SSL) | Security | **Critical** |
| FSA1002 (Option.get) | Correctness | **Critical** |
| FSA1003 (null/defaultof) | Correctness | **Critical** |
| FSA1006 (exception flow) | Correctness | **Major** |
| FSA1001 (mutable) | Maintainability | **Major** |
| FSA1004 (primitive obsession) | Best Practices | **Major** |
| FSA2022 (I/O in core) | Best Practices | **Major** |
| FSA1007 (imperative loops) | Performance | **Minor** |
| FSA1008 (inheritance) | Best Practices | **Minor** |
| FSA2012 (printfn) | Best Practices | **Minor** |
| FSA-AI01 (dead code) | Maintainability | **Minor** |
| FSA-AI10 (magic numbers) | Maintainability | **Minor** |
| FSA-AI05 (inconsistent errors) | Correctness | **Major** |

---

## $\S 4$. TDD $\rightarrow$ FsAssay Test-First Enforcement

### What TDD Is

Red-Green-Refactor. Failing test FIRST. One assertion per test. No implementation before test. [[4]]

### The F# Mapping

$$\text{TDD}_{\text{TS}} \xrightarrow{\text{embrace}} \text{TDD}_{\text{F\#}} : \text{FsCheck-First Enforcement}$$

### Requirements

**Definition 4.1 — TDD Cycle:**

$$\text{TDD} \triangleq \text{Red} \xrightarrow{\text{write test}} \text{Green} \xrightarrow{\text{write code}} \text{Refactor} \xrightarrow{\text{improve}} \text{Red}$$

**Axiom 4.1 — Test First:**

$$\forall f \in \mathcal{D}:\quad \text{Exists}(\text{Test}(f)) \prec \text{Exists}(\text{Impl}(f))$$

*The test must exist BEFORE the implementation.*

**Axiom 4.2 — One Property Per Test:**

$$\forall t \in \text{Tests}:\quad |\text{Assertions}(t)| = 1$$

*One FsCheck property per test. One assertion.*

**Axiom 4.3 — FsCheck Over Example:**

$$\forall t \in \text{Tests}:\quad \text{IsProperty}(t) = \top \;\vee\; \text{Justification}(t) \neq \emptyset$$

*Prefer FsCheck properties over example-based tests. If example-based, justify why.*

**Axiom 4.4 — Red Gate:**

$$\forall f \in \mathcal{D}:\quad \text{Commit}(f) \implies \text{Test}(f) \text{ was Red before Green}$$

*The test must have failed before the implementation was written. Verified via git history.*

### FsAssay Enforcement

| Rule | What It Checks |
|---|---|
| **FSA-TDD01** | Every public function in `Domain/` has a corresponding test in `Tests/` |
| **FSA-TDD02** | Every test file has at least one `[<Property>]` attribute |
| **FSA-TDD03** | No test has more than one `Assert` / `Expect` call |
| **FSA-TDD04** | No implementation file is committed without a corresponding test file in the same commit |

---

## $\S 5$. Security Review $\rightarrow$ FsAssay STRIDE + OWASP

### What Security Review Is

STRIDE threat modeling. OWASP Top 10. Pre-commit security gate. [[5]]

### The F# Mapping

$$\text{Security}_{\text{TS}} \xrightarrow{\text{embrace}} \text{Security}_{\text{F\#}} : \text{FsAssay SEC Rules + STRIDE}$$

### Requirements

**Definition 5.1 — STRIDE for F#:**

$$\text{STRIDE} \triangleq \left\{ \begin{array}{l} \text{S} : \text{Spoofing} \rightarrow \text{ONDC signature verification} \\ \text{T} : \text{Tampering} \rightarrow \text{Parameterized queries, immutable data} \\ \text{R} : \text{Repudiation} \rightarrow \text{Audit trail, structured logging} \\ \text{I} : \text{Info Disclosure} \rightarrow \text{No secrets in code, encrypted PII} \\ \text{D} : \text{Denial of Service} \rightarrow \text{Rate limiting, circuit breakers} \\ \text{E} : \text{Elevation} \rightarrow \text{Role-based access, no admin in domain} \end{array} \right\}$$

**Definition 5.2 — OWASP Top 10 → FsAssay Rules:**

| OWASP | FsAssay Rule | F# Pattern |
|---|---|---|
| A01: Broken Access Control | FSA-SEC08 (new) | No admin logic in domain |
| A02: Cryptographic Failures | FSA-SEC04 | No MD5/SHA1/DES |
| A03: Injection | FSA-SEC02 | No `sprintf` SQL |
| A04: Insecure Design | FSA1006 | No exceptions for flow |
| A05: Security Misconfiguration | FSA-SEC05 | No disabled SSL |
| A06: Vulnerable Components | FSA-SEC09 (new) | No known-vulnerable NuGet |
| A07: Auth Failures | FSA-SEC10 (new) | No hard-coded credentials |
| A08: Data Integrity | FSA-SEC11 (new) | No unsigned ONDC messages |
| A09: Logging Failures | FSA-SEC12 (new) | No PII in logs |
| A10: SSRF | FSA-SEC13 (new) | No user-controlled URLs |

**Axiom 5.1 — Pre-Commit Security Gate:**

$$\forall \text{commit}:\quad \text{Commit}(\text{commit}) \iff \mathcal{F}_{\text{SEC}}(\text{commit}) = \emptyset$$

*No commit with security violations. Zero tolerance.*

**Axiom 5.2 — STRIDE Coverage:**

$$\forall s \in \text{STRIDE}:\quad \exists\, r \in \mathcal{F}_{\text{SEC}}:\quad r \text{ addresses } s$$

*Every STRIDE category has at least one FsAssay rule.*

---

## $\S 6$. Vercel Best Practices $\rightarrow$ FsAssay F# Best Practices

### What Vercel React Best Practices Is

58 rules from Vercel Engineering. Performance profiling. Bundle size analysis. [[6]]

### The F# Mapping

$$\text{ReactBP}_{\text{TS}} \xrightarrow{\text{embrace}} \text{F\#BP} : \text{FsAssay F\# Performance Rules}$$

### Requirements

**Definition 6.1 — F# Performance Rules (mapped from Vercel's 58):**

| # | Vercel Rule | F# Equivalent | FsAssay Rule |
|---|---|---|---|
| 1 | Avoid unnecessary re-renders | Avoid unnecessary allocations | FSA-P01 (new) |
| 2 | Use `useMemo` for expensive computations | Use `lazy` for expensive computations | FSA-P02 (new) |
| 3 | Avoid inline object creation | Avoid inline record creation in hot paths | FSA-P03 (new) |
| 4 | Use `React.memo` for pure components | Use pure functions (no side effects) | FSA1001 |
| 5 | Avoid `useEffect` for data fetching | Avoid I/O in domain | FSA2022 |
| 6 | Use `Suspense` for loading states | Use `Async` for loading states | FSA-P04 (new) |
| 7 | Avoid prop drilling | Avoid deep parameter passing | FSA2020 |
| 8 | Use composition over inheritance | Use modules over classes | FSA1008 |
| 9 | Avoid large bundle sizes | Avoid large module sizes (> 200 lines) | FSA1009 |
| 10 | Use code splitting | Use module separation | FSA-P05 (new) |

**Axiom 6.1 — Performance Budget:**

$$\forall f \in \mathcal{D}:\quad \text{Allocations}(f) \leq \text{Budget}(f)$$

*Every domain function has an allocation budget. Exceeding it triggers FSA-P01.*

**Axiom 6.2 — Module Size:**

$$\forall m \in \text{Modules}:\quad \text{Lines}(m) \leq 200$$

*No module exceeds 200 lines. FSA1009 enforces.*

---

## $\S 7$. Composition Patterns $\rightarrow$ FsAssay Composition Rules

### What Vercel Composition Patterns Is

14 composition patterns. 10 anti-patterns. [[7]]

### The F# Mapping

$$\text{Composition}_{\text{TS}} \xrightarrow{\text{embrace}} \text{Composition}_{\text{F\#}} : \text{FsAssay Composition Rules}$$

### Requirements

**Definition 7.1 — F# Composition Patterns:**

| # | Pattern | F# Implementation | FsAssay Rule |
|---|---|---|---|
| 1 | Compound Components | Module with related functions | FSA-C01 (new) |
| 2 | Render Props | Higher-order functions | FSA-C02 (new) |
| 3 | Custom Hooks | Computation expressions | FSA-C03 (new) |
| 4 | Provider Pattern | Dependency injection via parameters | FSA-C04 (new) |
| 5 | Slot Pattern | Function parameters | FSA-C05 (new) |
| 6 | Polymorphic Components | Generic functions with constraints | FSA-C06 (new) |
| 7 | State Reducer | `fold` over events | FSA-C07 (new) |
| 8 | Control Props | Explicit state parameters | FSA-C08 (new) |
| 9 | Props Collection | Record parameters | FSA-C09 (new) |
| 10 | Feature Flags | Configuration-driven behavior | FSA-C10 (new) |

**Definition 7.2 — F# Anti-Patterns:**

| # | Anti-Pattern | FsAssay Detection |
|---|---|---|
| 1 | God component (> 200 lines) | FSA1009 |
| 2 | Prop drilling (> 4 levels) | FSA2020 |
| 3 | Inheritance for reuse | FSA1008 |
| 4 | Mutable shared state | FSA1001 |
| 5 | I/O in pure functions | FSA2022 |
| 6 | Exception-driven flow | FSA1006 |
| 7 | Primitive obsession | FSA1004 |
| 8 | Circular dependencies | FSA2017 |
| 9 | Deep dependency chains (> 4) | FSA2016 |
| 10 | Mixed error strategies | FSA-AI05 |

---

## $\S 8$. AI SDK Patterns $\rightarrow$ FsAssay AI Code Rules

### What Vercel AI SDK Patterns Is

11 patterns for AI-generated code. 7 anti-patterns. [[8]]

### The F# Mapping

$$\text{AI}_{\text{TS}} \xrightarrow{\text{embrace}} \text{AI}_{\text{F\#}} : \text{FsAssay AI-Specific Rules (FSA-AI01–AI10)}$$

### Requirements

**Definition 8.1 — AI Code Patterns (mapped from Vercel AI SDK):**

| # | Vercel AI Pattern | F# Equivalent | FsAssay Rule |
|---|---|---|---|
| 1 | Streaming responses | `AsyncSeq` for streaming | FSA-AI11 (new) |
| 2 | Tool calling | `Result`-based tool execution | FSA-AI12 (new) |
| 3 | Structured output | Typed deserialization | FSA-AI13 (new) |
| 4 | Retry with backoff | `Async` retry combinator | FSA-AI14 (new) |
| 5 | Token budget management | Bounded computation | FSA-AI15 (new) |
| 6 | Prompt caching | Immutable cache | FSA-AI16 (new) |
| 7 | Multi-model fallback | `Result` chain | FSA-AI17 (new) |

**Definition 8.2 — AI Anti-Patterns (mapped from Vercel AI SDK):**

| # | Vercel AI Anti-Pattern | F# Detection | FsAssay Rule |
|---|---|---|---|
| 1 | Unvalidated AI output | No smart constructor on AI result | FSA-AI01 |
| 2 | Hard-coded prompts | String literals in domain | FSA-AI10 |
| 3 | No error handling on AI call | `failwith` on AI failure | FSA1006 |
| 4 | Synchronous AI call in async context | `Async.RunSynchronously` | FSA2008 |
| 5 | No timeout on AI call | Unbounded `Async` | FSA-AI18 (new) |
| 6 | No rate limiting on AI call | Unbounded concurrency | FSA-AI19 (new) |
| 7 | AI output stored without validation | No smart constructor | FSA1004 |

---

## $\S 9$. Agent Skills $\rightarrow$ FsAssay Skill System

### What Vercel Agent Skills Is

6-step workflow for creating agent skills. 5 design principles. [[9]]

### The F# Mapping

$$\text{AgentSkills}_{\text{TS}} \xrightarrow{\text{embrace}} \text{FsAssaySkills} : \text{FsAssay Rule Creation Framework}$$

### Requirements

**Definition 9.1 — FsAssay Skill:**

$$\text{FsAssaySkill} \triangleq \left\{ \begin{array}{l} \text{Name} : \text{NonEmptyString} \\ \text{Description} : \text{NonEmptyString} \\ \text{Trigger} : \text{FilePattern} \\ \text{Rules} : \text{FsAssayRule}^+ \\ \text{Severity} : \text{Critical} \mid \text{Major} \mid \text{Minor} \\ \text{AutoFix} : \text{FixAction option} \\ \text{Tests} : \text{FsCheckProperty}^+ \\ \text{Documentation} : \text{NonEmptyString} \end{array} \right\}$$

**Axiom 9.1 — Skill Design Principles (from Vercel):**

$$\forall s \in \text{FsAssaySkills}:\quad \left\{ \begin{array}{l} \text{Single Responsibility}: |s.\text{Rules}| \leq 5 \\ \text{Composable}: s \text{ can be combined with other skills} \\ \text{Testable}: |s.\text{Tests}| \geq 3 \\ \text{Documented}: s.\text{Documentation} \neq \emptyset \\ \text{Fixable}: s.\text{AutoFix} \neq \emptyset \;\vee\; \text{Justification} \end{array} \right\}$$

**Axiom 9.2 — Skill Creation Workflow (6 steps from Vercel):**

$$\text{CreateSkill} \triangleq \text{Identify} \rightarrow \text{Design} \rightarrow \text{Implement} \rightarrow \text{Test} \rightarrow \text{Document} \rightarrow \text{Deploy}$$

---

## $\S 10$. Debugging $\rightarrow$ FsAssay Diagnostic Output

### What Debugging Is

5-phase systematic process. Reproduce → Isolate → Diagnose → Fix → Verify. No guessing. Binary search. [[10]]

### The F# Mapping

$$\text{Debug}_{\text{TS}} \xrightarrow{\text{embrace}} \text{Debug}_{\text{F\#}} : \text{FsAssay Diagnostic SARIF}$$

### Requirements

**Definition 10.1 — FsAssay Diagnostic:**

$$\text{Diagnostic} \triangleq \left\{ \begin{array}{l} \text{RuleId} : \text{string} \\ \text{Message} : \text{NonEmptyString} \\ \text{File} : \text{string} \\ \text{Line} : \text{int} \\ \text{Column} : \text{int} \\ \text{Severity} : \text{Critical} \mid \text{Major} \mid \text{Minor} \\ \text{Fix} : \text{FixAction option} \\ \text{Explanation} : \text{NonEmptyString} \\ \text{Example} : \text{CodeSnippet} \\ \text{RelatedRules} : \text{string}^* \end{array} \right\}$$

**Axiom 10.1 — Diagnostic Completeness:**

$$\forall v \in \text{Violations}:\quad v.\text{Diagnostic}.\text{Explanation} \neq \emptyset \;\wedge\; v.\text{Diagnostic}.\text{Example} \neq \emptyset$$

*Every violation has an explanation AND a code example showing the fix.*

**Axiom 10.2 — Binary Search Guidance:**

$$\forall v \in \text{Violations}:\quad v.\text{Diagnostic}.\text{RelatedRules} \neq \emptyset \implies \text{SuggestIsolation}(v)$$

*If a violation is related to other rules, suggest isolation steps.*

---

## $\S 11$. Planning $\rightarrow$ FsAssay Architecture Rules

### What Planning Is

3-phase: Explore → Design → Task List. 2-3 approaches with trade-offs. Max 10 tasks. [[11]]

### The F# Mapping

$$\text{Planning}_{\text{TS}} \xrightarrow{\text{embrace}} \text{Planning}_{\text{F\#}} : \text{FsAssay Architectural Constraints}$$

### Requirements

**Definition 11.1 — Architectural Constraints:**

$$\text{ArchConstraints} \triangleq \left\{ \begin{array}{l} \text{MaxModuleSize} : 200 \text{ lines} \\ \text{MaxDependencyDepth} : 4 \text{ layers} \\ \text{MaxFunctionParams} : 5 \\ \text{MaxCyclomaticComplexity} : 10 \\ \text{MaxNestingDepth} : 3 \\ \text{NoCircularDeps} : \top \\ \text{FCISBoundary} : \top \end{array} \right\}$$

**Axiom 11.1 — Layer Enforcement:**

$$\forall f \in \mathcal{D}:\quad \text{Layer}(f) = \text{Domain} \implies \text{IO}(f) = \emptyset \;\wedge\; \text{Mut}(f) = \emptyset \;\wedge\; \text{Exn}(f) = \emptyset$$

**Axiom 11.2 — Dependency Direction:**

$$\forall m_1, m_2 \in \text{Modules}:\quad m_1 \rightarrow m_2 \implies \text{Layer}(m_1) \geq \text{Layer}(m_2)$$

*Dependencies flow downward. ONDC → API → Service → Domain → Infrastructure. Never upward.*

---

## $\S 12$. Summary: New FsAssay Rules from Skills

| Source Skill | New FsAssay Rules | Count |
|---|---|---|
| Type Gym | FSA-TG01–TG32 (type challenges) | 32 |
| Type Checklist | FSA-CL01–CL20 (pre-PR gate) | 20 |
| Code Review | FSA-CR01–CR06 (severity dimensions) | 6 |
| TDD | FSA-TDD01–TDD04 (test-first) | 4 |
| Security Review | FSA-SEC08–SEC13 (STRIDE + OWASP) | 6 |
| Vercel Best Practices | FSA-P01–P05 (performance) | 5 |
| Composition Patterns | FSA-C01–C10 (composition) | 10 |
| AI SDK Patterns | FSA-AI11–AI19 (AI code) | 9 |
| Agent Skills | FSA-SK01–SK05 (skill framework) | 5 |
| Debugging | FSA-DBG01–DBG02 (diagnostics) | 2 |
| Planning | FSA-ARCH01–ARCH07 (architecture) | 7 |
| **Total New Rules** | | **106** |

**Combined with existing rules:**

$$|\mathcal{F}_{\text{total}}| = |\mathcal{F}_{\text{existing}}| + |\mathcal{F}_{\text{new}}| = 37 + 106 = 143$$

---

## $\S 13$. The Fundamental Theorem

$$\boxed{\text{Skills} \xrightarrow{\text{specify}} \text{Rules} \xrightarrow{\text{FsAssay enforces}} \text{Code} \xrightarrow{\text{CanonFlow generates}} \text{Artifacts} \xrightarrow{\text{FsCheck verifies}} \text{Proof}}$$

*The skills repo specifies what good code looks like. FsAssay enforces it. CanonFlow generates the types and tests. FsCheck verifies the properties.*

$$\text{Skills without FsAssay} = \text{Suggestions}$$

$$\text{Skills with FsAssay} = \text{Requirements}$$

$$\text{Skills with FsAssay + CanonFlow} = \text{Correctness by Construction}$$

The skills repo is the **what.** FsAssay is the **must.** CanonFlow is the **how.** FsCheck is the **proof.**

$$\text{What} \wedge \text{Must} \wedge \text{How} \wedge \text{Proof} \models \text{Correct}$$

$$\blacksquare$$
