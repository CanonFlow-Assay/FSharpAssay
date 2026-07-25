---
document_id: CFF-FSA-ASSAY-001
title: Formal Assay of FSharpAssay — Detector Soundness, Evidence Integrity, and Verdict Justification
format: CanonFlow Format (CFF)
status: Draft for adoption
governs: CanonFlowFoundation/FSharpAssay
audit_basis_commit: 1dff9dd ("feat: enforce FsAssay Moat rules in TAST and extend strategy docs")
audit_method: static read of full working tree; no .NET execution performed
toolchain_of_record: dotnet 10.0.9 / fsc 43.12.201.0 (per out-toolchain.json, not reproduced)
supersedes: none
---

# CFF-FSA-ASSAY-001

## §0 Purpose and standing

FsAssay's product claim is not "we find bugs." It is **"our verdict is justified."**
A tool whose purpose is preventing false confidence is destroyed by exactly one
class of defect: a verdict that outruns its evidence. This document formalises
the conditions under which an FsAssay verdict is justified, measures HEAD against
them, and states the discharge obligations.

Everything below is stated as definitions, theorems and proof obligations rather
than opinions, because the subject matter is a proof system and criticism of a
proof system should itself be checkable.

---

## §1 Notation

| Symbol | Meaning |
|---|---|
| $\Sigma$ | the set of F# source programs |
| $\mathrm{text}(p)$ | the raw source text of $p$ |
| $\mathcal{T}(p)$ | the typed tree (TAST) of $p$, $\bot$ when $p$ does not type-check |
| $L$ | the set of source locations (ranges) |
| $R$ | the rule catalogue, $\lvert R \rvert = 37$ at HEAD |
| $d_r : \Sigma \to \mathcal{P}(L)$ | the **detector** implementing rule $r$ |
| $O_r : \Sigma \to \mathcal{P}(L)$ | the **oracle** — the true extension of $r$ |
| $\mathrm{Ext}(r)$ | $\{(p,\ell) : \ell \in O_r(p)\}$, the rule's true extension |
| $\sqsubseteq$ | evidence-strength order on rule status |
| $\mu_r$ | a marker literal (e.g. `M01Dummy`) |
| $\rho$ | the stdout rendering function of the runner |

---

## §2 Axioms (restated from the existing constitution)

These are not new. They are lifted from `FSharpAssay.md` and `Gpt1.md` and given
symbols so that violations become provable rather than arguable.

**A1 — Compiler truth.** A finding is admissible only if it is derived from
compiler-computed facts (TAST, symbols, diagnostics), not from lexical accident.

**A2 — Domain restriction.** $\mathrm{Dom}(\text{FsAssay}) = \{p \in \Sigma : \mathcal{T}(p) \neq \bot\}$.
The orchestrator returns `Skipped CompilerErrors` otherwise.

**A3 — Honest inconclusiveness.** `Inconclusive` and `ToolFailure` are first-class
outcomes. *Absence of evidence must never silently become proof.*

**A4 — Safe fix.** An SDK `Fix` is emitted only when the edit is local and exact,
`FromText` matches the analysed snapshot, the transformation is
semantics-preserving, it cannot change public API or failure policy, the fixed
file compiles with the finding gone, and no new blocking finding appears
(`FSharpAssay.md` §11, clauses 1–6).

**A5 — Evidence reproducibility.** Any artefact presented as evidence must be
regenerable from HEAD by the declared toolchain.

**A6 — Verdict justification.** A verdict $v \in \{\text{Pass},\text{Fail},\text{Inconclusive},\text{ToolFailure}\}$
is justified only if it factors through the finding set:
$v = g \circ F$ where $F : \Sigma \to \mathcal{P}(\text{Finding})$.

**A7 — Severity honesty.** The severity attached to a finding is a function of the
rule's discharged evidence, not of its identifier.

---

## §3 Definitions

**D1 (Soundness).** $d_r$ is *sound* iff $\forall p.\ d_r(p) \subseteq O_r(p)$. No false positives.

**D2 (Completeness).** $d_r$ is *complete* iff $\forall p.\ O_r(p) \subseteq d_r(p)$. No false negatives.

**D3 (Vacuity).** $d_r$ is *vacuous on corpus $C$* iff $\forall p \in C.\ d_r(p) = \varnothing$.

**D4 (Marker detector).** $d_r$ is a *marker detector* iff there exists a literal
$\mu_r \notin \mathrm{Lex}(\text{F\#})$ such that
$$d_r(p) = \begin{cases} \{(1,0)\} & \mu_r \sqsubseteq \mathrm{text}(p)\\ \varnothing & \text{otherwise}\end{cases}$$

**D5 (Comment invariance).** $d_r$ is *comment-invariant* iff for every $p$ and every
comment text $c$, $\mathrm{codes}(d_r(p)) = \mathrm{codes}(d_r(\mathrm{insertComment}(p,c)))$.
A detector satisfying A1 is necessarily comment-invariant.

**D6 (Location fidelity).** $\varphi(f) = 1$ iff the syntactic witness of finding $f$ lies
within $f.\mathrm{range}$; else $0$.

**D7 (Status lattice).** $\mathrm{Status} = \{\text{Proposed},\ \text{Dummy},\ \text{Prototype},\ \text{Delegated},\ \text{Implemented}\}$
ordered by discharged evidence:
$$\text{Proposed} \sqsubset \text{Dummy} \sqsubset \text{Prototype} \sqsubset \text{Implemented}$$
with $\text{Delegated}$ incomparable (evidence discharged by an external analyzer of
declared provenance).

**D8 (Severity admissibility).**
$$\mathrm{sev}(r) = \begin{cases}\text{Error} & \sigma(r) \in \{\text{Implemented},\text{Delegated}\}\\ \text{Warning} & \sigma(r) = \text{Prototype}\\ \text{no message} & \sigma(r) \in \{\text{Dummy},\text{Proposed}\}\end{cases}$$

**D9 (Adjudication).** For rule $r$ over fixture corpus $C$ annotated with `// EXPECT:`:
$\mathrm{TP}_r, \mathrm{FP}_r, \mathrm{FN}_r$ counted against annotations.
$$\mathrm{Prec}_r = \frac{\mathrm{TP}_r}{\mathrm{TP}_r+\mathrm{FP}_r},\quad \mathrm{Rec}_r = \frac{\mathrm{TP}_r}{\mathrm{TP}_r+\mathrm{FN}_r}$$
**$\mathrm{Prec}_r$ is undefined when the prediction set is empty. It is not $1$.**
This single convention is the mathematical heart of §5.

**D10 (Registration coverage).** $\mathrm{RC} = \lvert\{r : \exists \text{ test asserting } r \text{ fires}\}\rvert / \lvert R \rvert$.
Registration coverage is **independent of** $\mathrm{Prec}$ and $\mathrm{Rec}$ and entails nothing about them.

---

## §4 Census at HEAD (measured, not asserted)

| Quantity | Value | Source |
|---|---|---|
| Rule constructors in `Rules.Rule` | **37** | `Library.fs:10–17` |
| Rules claimed by README | **42** ("42/42 Verified Tests") | `README.md:157`, `:291` |
| `testCase` in `FsAssay.Tests` | **36** | count |
| `testCase` in `FsAssay.Web.Tests` | **0** | count |
| Marker-only detectors | **23 / 37 (62.2 %)** | §5 T1 |
| Marker occurrences in `Specimens/` | **0** | grep |
| Dummy rules emitting `Severity.Error` | **22** | `Library.fs:165` |
| Rules in the "10 Moat Rules (`FSA-M01`–`FSA-M10`)" tier | **M01–M04 exist**; the other six are re-used C/S codes | `README.md:221` |
| Emitters of the `FSA1xxx`/`FSA2xxx` namespace | **0** | grep |
| `out.json` codes | `FSA2017`, `OBSOLETE_FSA`, `OBSOLETE_FSA` | unreproducible from HEAD |
| Consumers of `// EXPECT:` annotations | **0** | grep |

The three headline numbers — 42 claimed, 37 defined, 36 tested — agree with each
other nowhere. Whatever "42/42" measures, it is not the catalogue and not the suite.

The 23 marker-only detectors are:
`C05, C07, C11, C12, C13, S04, ML01, ML02, B01, F01, F02, F03, F05, F06, F07, E01, E02, E03, E04, M01, M02, M03, M04`.

---

## §5 Theorems

### T1 — Marker Vacuity
*Let $d_r$ be a marker detector (D4) with $\mu_r \notin \mathrm{Lex}(\text{F\#})$ and $\mu_r$ absent from
corpus $C$. Then $d_r$ is vacuous on $C$, $\mathrm{Rec}_r = 0$, and $\mathrm{Prec}_r$ is undefined.*

**Proof.** $\mu_r$ is not producible by any well-formed F# construct, so its
occurrence is at the author's discretion. A grep over `Specimens/` returns zero
occurrences of all 23 markers. By D4, $d_r(p) = \varnothing$ for all $p \in$ `Specimens`.
By D9 with $\mathrm{TP}=\mathrm{FP}=0$, $\mathrm{Prec}_r$ is undefined and $\mathrm{Rec}_r = 0$. $\blacksquare$

**Corollary T1.1.** The 23 marker rules have $\mathrm{Rec}=0$ against the adversarial corpus
purpose-built to exercise them. Coverage of the moat is $0$, not $10/10$.

### T2 — Test Non-Entailment
*For a marker rule $r$ whose fixture contains $\mu_r$ in a comment,
$\mathrm{Pass}(T_r) \nvdash \mathrm{Sound}(d_r)$ and $\mathrm{Pass}(T_r) \nvdash \mathrm{Complete}(d_r)$.*

**Proof.** $T_r$ asserts $\exists m \in d_r(\text{fixture}) : m.\mathrm{Code} = r$. By D4 this reduces to
$\mu_r \sqsubseteq \mathrm{text}(\text{fixture})$, which holds by construction of the fixture and is
independent of $O_r$. The predicate tested is the identity on the marker set. $\blacksquare$

**Witnesses.** `FSA-M01` is tested against `// M01Dummy trigger` + `let doSomething () = ()`
containing no struct DU. `FSA-C07` asserts non-tail-recursion detection against
`let rec doSomething () = ()`, which does not recurse. `FSA-C05` fires on a comment
while ignoring the genuinely incomplete `match 1 with | 1 -> ()` two lines below.

**Corollary T2.1.** CI green is currently *anti-evidence*: it is consistent with all 23
detectors being empty functions.

### T3 — Compile-Error Rules Have Empty Extension
*If $O_r(p) \neq \varnothing \Rightarrow \mathcal{T}(p) = \bot$, then $\mathrm{Ext}(r) \cap \mathrm{Dom}(\text{FsAssay}) = \varnothing$.*

**Proof.** Immediate from A2. $\blacksquare$

**Application.** `FSA-M02` (`[<RequireQualifiedAccess>]` violation): unqualified access to
an RQA case is a *type error*, so `ImplementationFile` is `None` and `Orchestrator`
returns `Skipped CompilerErrors`. No implementation effort can make M02 fire.
It must be **Delegated** (diagnostic normalisation) or **deleted** — never `Implemented`.

**Scope axiom to publish.** *FsAssay analyses only programs that type-check.*
Stating this once removes an entire class of proposed rules a priori.

### T4 — Order/Equality Incoherence ⟹ Lossy Deduplication
*`Located<'F>` violates the `IComparable`/`Equals` coherence law required by `Set`.*

**Proof.** `Equals` compares `Finding` and the **full** `Range`; `CompareTo` compares
`Finding`, `StartLine`, `StartColumn` only. Witness:
$a = \langle F, (l,c)\to(l,c{+}5)\rangle$, $b = \langle F, (l,c)\to(l,c{+}9)\rangle$.
Then $\mathrm{compare}(a,b) = 0$ while $a \neq b$. `Set` orders by comparison, so $a$ and $b$
collapse without equality justification. $\blacksquare$

**Consequence.** Deduplication is lossy and insertion-order dependent. Required law:
$\forall a,b.\ \mathrm{compare}(a,b) = 0 \iff a = b$ (Annex A.1).

### T5 — Location Fidelity of the Lexical Pass is Zero
*Every finding from the file-level pass has $\varphi = 0$ except by coincidence.*

**Proof.** `Library.fs:383` constructs a single range $(1,0)\to(1,0)$ and attaches it to
all file-level findings independently of the witness position. $\blacksquare$

**Composition with T4.** `analyzeDecl` emits the same rule at the body range while the
file pass emits it at $(1,0)$. The ranges differ, so `Set.union` does not collapse
them: one defect yields two findings, one of which is a fabricated location.

**Composition with A4.** A `SafeFix` anchored at $(1,0)\to(1,0)$ cannot satisfy clause 2
(`FromText` matches the snapshot at the range). Fix emission is structurally
unsound wherever the finding came from the lexical pass.

### T6 — The Scanner Verdict Does Not Factor Through Findings
*The ecosystem verdict violates A6 and is unsound in both directions.*

**Proof.** `Scanner/Program.fs` computes
$$v = \begin{cases}\text{Shark} & \exists s \in \{\texttt{"FSA-E0"},\texttt{"FSA-C"},\texttt{"FSA-F"}\} : s \sqsubseteq \rho(F(p))\\ \text{Dolphin} & \text{otherwise}\end{cases}$$
so $v = g \circ \rho \circ F$, not $g \circ F$.
*(i) False Shark:* $\rho$ interleaves banners and file paths with findings; any path
containing `FSA-C` satisfies $g$.
*(ii) False Dolphin:* the codes actually present in `out.json` are
$\{\texttt{FSA2017},\texttt{OBSOLETE_FSA},\texttt{OBSOLETE_FSA}\}$, and
$\{\texttt{FSA-E0},\texttt{FSA-C},\texttt{FSA-F}\} \cap \mathrm{prefixes} = \varnothing$.
A repository with non-empty findings is therefore declared
🐬 *Dolphin (Passed Elite F# Checks)* with exit 0. $\blacksquare$

**Additional.** Build failure currently downgrades to "source-only scan" but still
yields a verdict. By A3 a failed build must yield `Inconclusive`.

### T7 — The Emitted Fixes Violate A4
*None of the three shipped fixes satisfies the safe-fix predicate.*

**Proof by typing.** A token substitution $s \mapsto t$ preserves compilation only if $t$
inhabits $s$'s type in $s$'s context.

| From | Type | To | Type | Verdict |
|---|---|---|---|---|
| `Async.RunSynchronously` | `Async<'T> -> 'T` | `Async.AwaitTask` | `Task<'T> -> Async<'T>` | domain and codomain both differ ⟹ ¬compiles |
| `Async.Start` | `Async<unit> -> unit` | `Async.StartChild` | `Async<'T> -> Async<Async<'T>>`, legal only inside an async CE under `let!` | arity + context change ⟹ ¬compiles |
| `isNull` | `'T -> bool` (`'T : null`) | `Option.isNone` | `'T option -> bool` | domain mismatch ⟹ ¬compiles; migration changes public API ⟹ clause 4 also fails |

Clauses 3 and 5 fail for all three; clause 4 fails for the third. $\blacksquare$

**Governance violation, independent of typing.** `FSharpAssay.md` §11 states
*"The CLI does not apply fixes in v1; a future `fix` command requires a separate law
sheet, diff preview, compilation, reassay, and human acceptance."*
`Program.fs` ships `--fix` wired to `AutoFix.applyAutoFixes`. The same section
explicitly excludes `Option.get` from automatic fixes because
*"no universal semantics-preserving edit exists"* — and the constitution was correct.

**Remedy.** Demote all three to `Explain`. Remove `--fix` until the law sheet exists.

### T8 — Guard Non-Entailment
*Preceding `Option.filter` or `Option.map` does not discharge the `Option.get` obligation.*

**Proof.** `Option.map f` preserves the constructor: $\mathrm{map}\ f\ \mathrm{None} = \mathrm{None}$, so it
cannot eliminate `None`. `Option.filter p` can map $\mathrm{Some}\ x \mapsto \mathrm{None}$ when $\neg(p\,x)$, so
it *strictly weakens* the postcondition. Neither entails $\exists y.\ e = \mathrm{Some}\ y$ at the
`get` site. $\blacksquare$

**Remedy.** The honest rule is an exact-symbol ban, profile-scoped:
`core` bans resolved `Option.get` / `.Value`; `interop`/`test`/`script` configurable.
No flow-sensitive "guard recognition" until a real dataflow proof exists.
The correct symbol is `Microsoft.FSharp.Core.OptionModule.GetValue` — the
`CompiledName` — not `…OptionModule.Get` as `Qwen.md` states.

### T9 — `FSA-F04` Is a Desugaring Tautology
*The implemented predicate characterises the language, not a defect.*

**Proof.** F# statement sequencing `e1; e2` desugars to `Sequential(e1,e2)` with
$\mathrm{type}(e_1) = \mathrm{unit}$. `Library.fs:258–260` fires exactly on
$\mathrm{type}(e_1) = \mathrm{unit}$. Hence every well-typed sequenced side effect satisfies it.
Witness: `printfn "Side effect"; 5`. $\blacksquare$

**Corollary.** $\mathrm{Prec}_{F04} \to 0$ on idiomatic F#. This is the one *implemented*
rule whose false-positive rate is provably near-total.

### T10 — Severity Is a Function of the Identifier, Not the Evidence
*A7 is violated with 22 witnesses.*

**Proof.** `Library.fs:165`:
`Severity = if Code.StartsWith("FSA-S") then Warning else Error`.
Of the 23 marker rules, only `S04` carries the `FSA-S` prefix. The remaining 22
emit `Severity.Error` with $\sigma = \text{Dummy}$. $\blacksquare$

### T11 — `FSA-C04` Does Not Establish Its Hazard
*The predicate `logicalName = "Start" ∧ inTryFinally` proves none of the required conjuncts.*

The hazard requires all four of:
1. the `try/finally` originates from a `use` binding;
2. the disposable is *captured* by the started workflow;
3. the workflow *escapes* the binding's scope;
4. disposal precedes completion.

The implementation establishes none, and `"Start"` additionally matches
`Process.Start`, `Stopwatch.Start`, `Timer.Start`. The file-level variant is worse:
it conjoins `use ` and `Async.Start` **anywhere in the file**.

**Remedy.** Delegate to G-Research's production-derived `DisposedBeforeAsyncRunAnalyzer`
rather than reimplement capture/escape analysis, and record the delegation as
provenance. $\sigma(\text{C04}) = \text{Delegated}$.

### T12 — Aggregate Bias Is Toward Unearned Pass
*The composition of T1, T6 and T10 biases the system entirely in the false-negative direction.*

**Proof.** By T1 the 23 marker detectors never fire on real code, so they cannot
produce false `Fail`. By T6 the ecosystem verdict defaults to `Dolphin` for the
code namespace the runner actually emits. Therefore the residual error is
concentrated in unearned `Pass`. $\blacksquare$

**This is the maximally adverse failure mode for this product.** A tool that
over-reports is annoying; a tool built to prevent false confidence that
manufactures unearned `Pass` verdicts inverts its own thesis. By A3 the correct
output for an undischarged rule is `Inconclusive`, and `Inconclusive` is precisely
the outcome the current design cannot reach.

### T13 — The Falsifier Is the Missing Component
*The only instrument that would detect T1, T2 and T9 is the one that is unimplemented.*

`Program.fs:36` advertises `--adjudicate` as *"evaluate Precision/Recall against
`// EXPECT` comments."* `Program.fs:105` uses the flag solely to suppress printing.
Nothing parses `// EXPECT:`; the Specimens' annotations have zero consumers.

The system has a rich claim surface and no falsifier. Under A3 this is the
highest-leverage single defect, because discharging it makes T1/T2/T9 impossible
to recur silently.

---

## §6 Security finding (highest severity)

**S-1 — Arbitrary code execution on the examiner host.**

`FsAssay.Scanner` clones an arbitrary user-supplied repository and executes
`dotnet build -c Release` inside it. MSBuild targets, custom tasks, SDK props and
build-time tooling from an untrusted repository then execute with the scanner
process's privileges. This is remote code execution by design, on a tool whose
marketing invites pointing it at other people's repositories.

Required controls before any publication:

| # | Control |
|---|---|
| 1 | Default to source-only parsing; never build unknown code on the host |
| 2 | If compilation is required, use an ephemeral hardened container |
| 3 | No host mounts, no credentials, no ambient cloud identity |
| 4 | Network disabled after clone and reviewed restore |
| 5 | CPU / memory / process-count / wall-clock limits |
| 6 | Temporary directory destroyed on every exit path |
| 7 | Build failure ⟹ `Inconclusive`, never `Dolphin` (A3) |
| 8 | Verdict from structured results and exit codes, never `scanOut.Contains` (A6, T6) |

**Positioning.** "Hunting repositories", "Shark = failed purity", "weaponised CLI",
"the bad parts" — this register turns an assurance tool into a purity tribunal and
will cost community adoption before the engineering is judged on merit. Reframe as
an *ecosystem compatibility and architectural-profile study*. The examiner should
be dispassionate; the current vocabulary is not.

---

## §7 Evidence integrity findings

**E-1 — Unreproducible committed evidence.** `out.json` carries paths rooted at
`/root/FSharpAssay/…` and codes (`FSA2017/2020/2023`) that **no source in the tree
emits**. `scan.fsx` hardcodes `/root/.nuget/…` and `/root/fsharp-realworld`.
Violates A5.

**E-2 — Dead identifier namespace.** `FSA1xxx`/`FSA2xxx` appears in Specimen
comments, `ratecard.md`, `out.json` and four `SuppressMessage` attributes — and in
no emitter. Consequently `AutoFix.fs`, which dispatches on `OBSOLETE_FSA/1003/1004/1009`,
is **unreachable code in its entirety**.

**E-3 — Identifier contradiction.** `README.md:221` announces
*"Tier 6: The 10 Moat Rules (`FSA-M01` – `FSA-M10`)"*; only `M01`–`M04` exist, and
the tier's own table fills the remaining six slots with re-used `C`/`S` codes.

**E-4 — Registration coverage presented as correctness.** "42/42 Verified Tests"
(`README.md:157`) is not registration coverage either: 37 rules, 36 tests, 42 claimed.

**E-5 — Dangling citations.** `Qwen.md` carries `[[9]] [[11]] [[12]] [[14]] [[22]] [[23]] [[28]] [[29]] [[31]] [[126]]`
with no bibliography anywhere in the repository. Unsupported assertions requiring
measurement, citation, or demotion to hypothesis:
*"LLMs generate these constantly"*, *"no other tool catches all ten"*,
*"diff-only, under two seconds"*, *"all ten are production-proven"*,
*"impossible to write for any other language"*, and
`README.md:257` *"integrates natively into … JetBrains Rider"*.

**E-6 — Negative dogfooding.** `Library.fs` uses `let mutable f = []` with `f <- f @ …`
in ~20 sites — the exact pattern of `FSA-C10`, plus quadratic append — inside the
enforcement kernel. `Scanner` self-suppresses `C10`/`S05`; `Orchestrator`
self-suppresses `FSA2017`/`C01`; `Domain.fs` suppresses `OBSOLETE_FSA` four times, a code
that cannot fire. ⚠️ A C#-shaped accumulator in the organ that certifies F# purity
is the single most quotable defect in the repository.

---

## §8 Rule status assignment $\sigma$

Applying D7/D8 to HEAD. **No rule may ship at `Error` until its row reaches
`Implemented` or `Delegated`.**

| Status | Count | Rules |
|---|---|---|
| **Dummy** (marker only — must emit no message) | 23 | `C05 C07 C11 C12 C13 S04 ML01 ML02 B01 F01 F02 F03 F05 F06 F07 E01 E02 E03 E04 M01 M02 M03 M04` |
| **Prototype** (real TAST path, unvalidated precision) | 13 | `C01 C02 C03 C06 C08 C09 C10 C14 S01 S02 S03 S05 F04` |
| **Delegated** (external analyzer of record) | 1 | `C04` → G-Research `DisposedBeforeAsyncRunAnalyzer` |
| **Implemented** | **0** | — |
| **Retire** (empty extension by T3) | 1 | `M02` |

Zero rules currently qualify as `Implemented`. That is the honest headline, and
publishing it is a stronger signal of trustworthiness than "42/42".

### Rule-level corrections carried forward

| Rule | Current framing | Corrected framing |
|---|---|---|
| `C05` incomplete match | reimplement exhaustiveness | **promote FS0025**; audit suppression; link the missing case to the DU change. Never count patterns — wildcards, OR-patterns, guards, nesting and active patterns make counting unsound |
| `C07` non-tail recursion | "will stack-overflow" | "not in tail position; **may** exhaust the stack for unbounded or sufficiently deep input" — advise by default, block only under an explicit stack-safety policy |
| `C13` `[<TailCall>]` | "compiler doesn't enforce it" | the attribute expresses an expectation and the compiler **does** emit a diagnostic when violated; it does not change runtime behaviour. Promote the diagnostic |
| `C02` `Option.get` | guard recognition | exact-symbol ban, profile-scoped (T8) |
| `S04` async without return | flag every missing `return` | `Async<unit>` is common and often intentional. Flag only *proven discarded meaningful values* |
| `S05` `Task.Result` | "will deadlock" | "blocks a thread; **may** deadlock under a synchronisation context and can cause thread-pool starvation" |
| `M01` struct DU ref fields | "boxes on every use" | **false** — a value type may hold reference fields without being boxed; boxing occurs on conversion to `obj`/interface or in certain generic contexts. Replace with observed-boxing-site or copy-size analysis. Also: a multi-case struct DU requires unique field names across all cases (FS3204), so `Qwen.md`'s example does not compile |
| `M03` measure loss | block all erasure | erasure is legitimate at serialisation, formatting and interop boundaries; `float measuredValue` is documented. Block in `core`, advise in `shell`/`interop`, prefer named conversion functions so erasure is reviewable |
| `M04` active-pattern partiality | novel TAST moat | syntax in `Qwen.md` is wrong: partial is `(\|Positive\|_\|)`; `(\|Positive\|)` is a **total** single-case pattern, so the stated example does not throw. F# 9 also permits `bool`-returning partial patterns, so an `Option` return-type test is not a detector. Correct positioning: promote FS0025 and audit suppressions |

### `Qwen.md` errata (blocking adoption as canonical)

Beyond the rule corrections above:

1. `Option.get` raises `ArgumentException`, not `KeyNotFoundException` (that is `Map.find`). Stated wrong twice.
2. Rule 5's example does not compile: `let! r = httpClient.GetAsync(url)` inside `async {}` requires `Async.AwaitTask`, and a body terminating in `let!` with no continuation is a syntax error.
3. Rule 1's example does not compile: `base` is a reserved keyword and requires backticks as a field label.
4. Fabricated FCS API surface: `SynMatchExpr`, `SynExpr.Use`, `SynExpr.UnionCase`, `SynPat.NamedActivePattern` do not exist. Real: `SynExpr.Match`, `SynExpr.LetOrUse(isUse = true, …)`, `SynPat.LongIdent`.
5. Category error: the thesis is "only the TAST knows", yet six of ten detection sketches walk the **untyped** syntax tree. They answer different questions and the document conflates them throughout.
6. Rule 8 is unreachable by construction (T3) yet marked ✅ in the moat table.
7. The "No other tool catches all 10" table asserts ✅ ten times; by the document's own definitions FsAssay currently catches **zero**. This table is the principal reputational exposure if the repository is publicised.

**Disposition.** `Qwen.md` is retained as `docs/research/` with a status banner
(*Research proposal — not canonical; see CFF-FSA-ASSAY-001 §8*), corrected facts,
and a real bibliography. It must not be cited as a specification.

---

## §9 The actual moat

Individual rules are copyable in an afternoon. The defensible asset is the **meet**:

$$\mathrm{Moat} = \bigwedge \{\text{CompilerTruth},\ \text{ExactSymbolCertainty},\ \text{ProfileScopedPolicy},\ \text{HonestInconclusive},\ \text{SuppressionAudit},\ \text{SafeRemediation},\ \text{ReproducibleEvidence},\ \text{AdversarialCorpus},\ \text{BoundedRepairLoop}\}$$

Two properties follow.

**Trust is multiplicative, not additive.** The value of the meet is not $\sum$ of the
conjuncts: one unsound conjunct sends the justification of every verdict to $\bot$.
Twenty-two `Dummy` rules emitting `Error` are sufficient to collapse it.

**The conjuncts are individually copyable; the discipline is not.** Nobody can
fast-follow *"we publish our precision, recall and inconclusive rate per rule, per
release, regenerated by CI."* That sentence is the moat. Ten rule ideas are not.

---

## §10 Discharge obligations (gates)

Ordered by leverage. $G_1$ alone falsifies T1, T2 and T9 mechanically.

**G1 — Comment invariance property.** Assert D5 over the whole catalogue via FsCheck
(Annex A.2). Every one of the 23 marker detectors fails immediately. This is the
cheapest possible falsifier and it is decisive.

**G2 — Implement `--adjudicate` for real.** Parse `// EXPECT: <code>` from `Specimens/`,
compute $\mathrm{TP}/\mathrm{FP}/\mathrm{FN}$ per rule, treat empty prediction sets as **undefined precision**
(never 1), and fail CI when any enabled rule has $\mathrm{TP} = 0$. ~60 lines.

**G3 — Status-gated severity.** Make `Severity` a total function of $\sigma$ (Annex A.4),
not of the code prefix. 22 rules stop emitting `Error` the moment this lands.

**G4 — Contain the scanner.** Ship §6 controls 1–8, or unship the scanner. Until then
it must not be documented as usable against third-party repositories.

**G5 — Demote the fixes.** All three to `Explain`; remove `--fix` until the law sheet
in `FSharpAssay.md` §11 exists.

**G6 — Purge or regenerate evidence.** `out.json`, `ratecard.md`, `material.html`,
`dashboard.html`, `scan.fsx` are either produced by CI from HEAD or they leave the
repository. Replace "42/42" with a CI-generated table.

**G7 — The trust slice.** Freeze UI, scanner and new-rule expansion. Take exactly
three rules to `Implemented` with positive **and** negative fixtures:
`C02` (exact-symbol `OptionModule.GetValue`), `C05` (FS0025 promotion),
`C04` (G-Research delegation). Three unquestionable rules make the remaining
thirty-four credible. Thirty-seven claimed rules with zero discharged make none of
them credible.

---

## Annex A — Executable laws

Strict F#: DUs over classes, `Result` over exceptions, no mutation, FsCheck + Expecto.

```fsharp
module FsAssay.Laws

open Expecto
open FsCheck
open FsAssay.Analyzers

/// A.1 — Set membership requires coherent order and equality.
/// Falsifies the current Located<'F> implementation.
let ``compare zero iff equal`` (a: Rules.Located<Rules.Rule>) (b: Rules.Located<Rules.Rule>) =
    (compare a b = 0) = (a = b)

/// A.2 — G1. A detector obeying A1 cannot observe comments.
/// Falsifies all 23 marker detectors on the first shrink.
let private codesOf (ms: Message list) =
    ms |> List.map (fun m -> m.Code) |> List.sort

let ``detection is invariant under comment insertion``
        (Fixture source) (CommentText comment) =
    let baseline  = source |> Assay.run |> codesOf
    let perturbed = source |> Perturb.insertComment comment |> Assay.run |> codesOf
    baseline = perturbed

/// A.3 — No finding may carry a degenerate range.
let ``no finding is anchored at the origin`` (Fixture source) =
    source
    |> Assay.run
    |> List.forall (fun m ->
        not (m.Range.StartLine = 1
             && m.Range.StartColumn = 0
             && m.Range.EndLine = 1
             && m.Range.EndColumn = 0))

/// A.4 — G3. Severity is a total function of discharged evidence (D8).
type Status =
    | Proposed
    | Dummy
    | Prototype
    | Delegated of analyzerOfRecord: string
    | Implemented

type Emission =
    | Emit of Severity
    | Withhold of reason: string

let emissionFor (status: Status) : Emission =
    match status with
    | Implemented
    | Delegated _ -> Emit Severity.Error
    | Prototype   -> Emit Severity.Warning
    | Dummy
    | Proposed    -> Withhold "undischarged evidence: rule must not produce a Message"

/// A.5 — G2. Precision is undefined on an empty prediction set.
type Adjudication =
    { Rule: Rules.Rule
      TruePositives: int
      FalsePositives: int
      FalseNegatives: int }

module Adjudication =

    let precision (a: Adjudication) : Result<float, string> =
        match a.TruePositives + a.FalsePositives with
        | 0 -> Error "undefined: empty prediction set"
        | d -> Ok (float a.TruePositives / float d)

    let recall (a: Adjudication) : Result<float, string> =
        match a.TruePositives + a.FalseNegatives with
        | 0 -> Error "undefined: no annotated instances"
        | d -> Ok (float a.TruePositives / float d)

    /// CI gate: an enabled rule that never fires on the adversarial corpus
    /// is unproven, regardless of its unit tests.
    let gate (a: Adjudication) : Result<Adjudication, string> =
        if a.TruePositives = 0 then
            Error $"%A{a.Rule}: zero true positives against Specimens — status cannot exceed Dummy"
        else Ok a
```

---

## Annex B — Provenance of findings

CFF requires that a document's claims carry their source.

| Finding | Origin | Note |
|---|---|---|
| Marker vacuity; 23/37 census; zero markers in `Specimens` | Claude static audit | T1 |
| Test non-entailment | Claude | T2 |
| `--adjudicate` unimplemented (falsifier gap) | Claude | T13 |
| `Located` order/equality incoherence | Claude | T4 |
| Fabricated `(1,0)` ranges; duplicate findings | Claude | T5 |
| Scanner verdict does not factor through findings | Claude | T6 |
| Dead `FSA1xxx/2xxx` namespace; `AutoFix` unreachable; `out.json` unreproducible | Claude | E-1, E-2 |
| Negative dogfooding (mutable kernel, self-suppression) | Claude | E-6 |
| CLI ships `--fix` contra `FSharpAssay.md` §11 | Claude | T7, governance |
| **Scanner arbitrary code execution** | **GPT review** | S-1 — highest severity; missed by Claude audit |
| **Auto-fixes violate the safe-fix constitution** | **GPT review** | T7 typing table |
| `Option.filter`/`map` non-entailment | GPT review | T8 |
| `FSA-F04` flags legitimate sequencing | GPT review | T9 |
| `C04` four-conjunct decomposition; delegate to G-Research | GPT review | T11 |
| Certainty calibration (`Task.Result`, non-tail recursion, measure erasure) | GPT review | §8 table |
| `FSA-M01`–`M10` identifier contradiction | GPT review | E-3 |
| Registration coverage ≠ rule correctness | GPT review | D10, E-4 |
| Status taxonomy (Implemented/Prototype/Dummy/Proposed/Delegated) | GPT review | formalised as D7 lattice |
| "The moat is the conjunction, not the rules" | GPT review | §9 |
| Community-tone risk | GPT review | §6 |
| Boxing claim false; `(\|Positive\|_\|)` syntax; dangling citations; promote FS0025 | Both, independently | corroborated |
| `Option.get` raises `ArgumentException`; `CompiledName` is `GetValue`; Rule 8 unreachable; Rule 5 and Rule 7 examples do not compile; fabricated FCS API names; Syn/TAST category error | Claude | §8 errata |

**Corrections applied to the GPT review during merge.** Rule constructors are **37**,
not 42 — 42 is the README's claim and matches nothing. `FSA-F04` is **not**
marker-only; it has a genuine (over-broad) TAST path, which is what makes T9 worth
stating. The "six additional web tests" could not be verified:
`FsAssay.Web.Tests/Program.fs` contains zero `testCase` occurrences.

---

## Closing

The thesis survives every finding in this document. *A better author never
eliminates the need for an independent examiner* is correct, and FsAssay is the
right asset to build now.

But by T12 the examiner currently manufactures unearned `Pass` verdicts, and by
T13 the instrument that would have revealed this is the one component left
unimplemented. The repository is moving faster than its evidence — which is the
one failure mode this particular product cannot survive, because its entire
value proposition is the prevention of exactly that.

Discharge $G_1$ and $G_2$ first. They are cheap, they are mechanical, and together
they make every claim in `README.md` either true or loudly false.
