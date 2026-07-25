# FsAssay Remediation — A Formal Discharge Specification

**Target** `CanonFlowFoundation/FSharpAssay` · **Baseline** `8fefc05` · **Mode** law-verified, gate-driven.
**Synthesis** of three independent reviews (Claude, GPT, Qwen); every imported claim re-verified against `8fefc05` except `L₈`, stated as an obligation.

This document is not advice. It is a set of **laws** `L₁…L₈` the system must satisfy, a set of **witnessed violations** `¬Lᵢ` (each with its locus), and a set of **proof obligations** `Oᵢ ⊢ Lᵢ` each discharged only by a mechanical witness (a test or shell predicate). Suppression, exclusion, and `continue-on-error` are **not** discharges.

---

## 0. Objects and notation

```
ℛ            the rule set                         (Rule DU in FsAssay.Analyzers/Domain.fs)
Σ : ℛ → 𝕊   the code map r ↦ r.Code               (injective; 𝕊 = string codes)
𝒞 = im(Σ)   the closed code alphabet
σ : ℛ → Status    Status = {Proposed, Dummy, Prototype, Delegated, Implemented}
sev : ℛ → {Minor, Major, Critical}
det : ℛ → (Source → 𝒫(Located))   the detector; Located = ℛ × Range
ρ₀           the null range (StartLine = 0)
mkLocated r ρ = None  iff  ρ.StartLine = 0        (AstUtils.fs:11)
```

**Evaluation triple.** A scan over units `U` returns `⟨C,S,F⟩`, `C ⊍ S ⊍ F = U` (Completed/Skipped/Failed); findings are defined only over `C`.
**Verdict domain.** `𝕍 = {Pass, Fail, Inconclusive, ToolFailure}` (§4).
**Effective firing.** `fires(r) ⟺ ∃ s. det(r)(s) ≠ ∅ ∧ ∀(_,ρ)∈det(r)(s). ρ.StartLine ≥ 1`. Distinguish **declared** `σ(r)=Implemented` from **effective** `σ(r)=Implemented ∧ fires(r)`.

---

## 1. Laws (the constitution as axioms)

> **L₁ — Taxonomy closure.** For every checked-in artifact `a`: `codes(a) ⊆ 𝒞`.

> **L₂ — Heuristics cannot block.** `verdict(s)=Fail ⟹ ∃r. fire(r,s) ∧ σ(r)∈{Implemented,Delegated} ∧ sev(r)∈{Major,Critical}`. `σ(r)=Prototype ⟹ r` contributes at most `Inconclusive`.

> **L₃ — Location soundness.** `∀(r,ρ)∈det(r)(s). ρ.StartLine ≥ 1`. Corollary: `mkLocated=None` must never be the mechanism by which a real finding disappears.

> **L₄ — Evidence completeness (absence ≠ cleanliness).** `verdict=Pass ⟹ S=∅ ∧ F=∅`; `F≠∅ ⟹ verdict⊒ToolFailure`; `S≠∅ ⟹ verdict⊒Inconclusive`. The triple `⟨C,S,F⟩` is surfaced by **both** CLI and MCP.

> **L₅ — Status honesty (declared ⟹ effective).** `σ(r)=Implemented ⟹ fires(r) ∧ det(r)` is not empty, not marker-text-keyed, has a positive **and** negative specimen, and locates at a real range. Delegation is honest: `σ(r)=Delegated d ⟹ d` is loaded and invoked.

> **L₆ — Reflexivity.** `A ⊨ A`: the analyzer's own source satisfies every rule it marks `Implemented`, **without** blanket `SuppressMessage` or `.fsassayrc` exclusion of authored code. `FsAssay.Analyzers/**` contains no `mutable`+`<-`, no `ref`-cell accumulation, no `@`-in-fold, no `try … with _ -> _`, no `printfn`. The **only** admissible suppression is the irreducible FCS-symbol-throws boundary, carrying a `Justification`.

> **L₇ — Single generator.** `∃! Σ⃗ : ℛ → RuleRecord` s.t. registry, ledger, `docs/rules/*`, `--list`, SARIF `rules[]`, and specimen `EXPECT` sets are projections `π∘Σ⃗`. One tool version `v` everywhere.

> **L₈ — Metric partiality.** `precision = tp/(tp+fp)` is undefined when `tp+fp=0` (↦ `Inconclusive`/`⊥`, never `1.0`); likewise `recall`.

---

## 1½. Partial credit — what already holds  *(Qwen axis; verified)*

The remediation is not from zero. These are discharged or in progress and must not regress:

- **`Status` taxonomy exists and gates output.** `RuleStatus = Proposed|Dummy|Prototype|Delegated|Implemented`; `Dummy|Proposed ↦ None`. The honesty *mechanism* is present — `L₅` fails only because several arms are mislabelled, not because the field is absent.
- **Exit-code machinery exists** (`0/1/2/3`); compiler errors become missing evidence, not a clean pass, in the main CLI path. `L₄` is half-built.
- **Right patterns, where used:** `FSA-C05` delegates incomplete-match truth to compiler diagnostic `FS0025` (reuse compiler truth — exemplary); `FSA-C02` uses typed symbol identity for `Option.get`; `FSA-C01` inspects typed `DefaultValue`/`Const`. These three are the trust-slice seed (§5).
- **Rich diagnostic type** (`CodeSnippet, Explanation, DocLink, RelatedRules`) and **severity tiers** exist (even if the gate ignores them — `L₂`).
- **The test harness executes** (38 Expecto cases through FCS `ParseAndCheckFileInProject`), including negatives and fault-injection — a real gate once `L₃`/`L₅`/gate-reality land.
- **The module-graph *builder* is real** (`buildGraph`/`detectCycles`/`checkLayer`); only its *emission* is broken (`L₃`, V1).
- **The AI-rule *category* is a genuine differentiator** — no mainstream linter detects LLM-key leakage, missing `max_tokens`, prompt-injection surface, raw-LLM-string domain leakage. The **thesis** is the moat; the **implementation** is not yet trustworthy (`L₅`), so the moat is realized only after these rules go structural.

Consensus across the three reviews: **thesis ≈ 9/10, trust semantics ≈ 2/10.** The score spread (Qwen 7.2 vs GPT ~2) is explained almost entirely by whether the reviewer verified `Implemented ⟹ fires` or trusted the `Status` field. That predicate *is* `L₅`.

---

## 2. Rule census and the honesty gap  *(Qwen census + Claude/GPT verification)*

Declared status distribution (`Domain.fs` Status member), `|ℛ| = 93`:

| Status | Count | Share |
|---|---|---|
| Implemented (declared) | 38 | 41% |
| Prototype | 33 | 35% |
| Dummy | 21 | 23% |
| Delegated | 1 | 1% |

**Effective correction.** ≥9 declared-`Implemented` rules do **not** fire:

```
range0-dropped (Graph.fs → mkLocated None):  FSA2017, FSA-ARCH01, FSA-ARCH02, FSA-SEC13, FSA-TDD01, FSA-TDD04   (also FSA-TDD02 via Library.fs)
empty body:                                   FSA2016  (calculateDepth = [], Graph.fs:295)
marker-text-keyed:                            FSA-C07  (text.Contains("Non"+"Tail"), Visitor.fs:184)
no detector:                                  FSA-SEC10 (Critical; no mkLocated FSASEC10 anywhere)
```

$$\text{effective Implemented} = \frac{38-9}{93} \approx 31\% \quad(\text{vs } 41\%\text{ declared})$$

The 10-point gap is exactly the `¬L₅` set. Remediation targets the gap first, not the count.

---

## 3. Witnessed violations at `8fefc05`

| # | Violates | Witness (verified) |
|---|----------|--------------------|
| V1 | L₃, L₅ | **Project/graph tier inert.** FSA2017/SEC13/TDD01/TDD04/ARCH01/ARCH02 built at `Range.range0` (`Graph.fs:227,237,257,273,307,312`) → dropped by `mkLocated`. `calculateDepth=[]` (`:295`, FSA2016). All declared `Implemented`. |
| V2 | L₂, L₄ | **Verdict vestigial.** `AssayVerdict` declared (`Runner/Domain.fs:26`), never constructed/serialized. Gate = `elif totalViolations > 0 then BlockingFinding` (`Program.fs:338`); `score = max 0 (100 − 5·n)` (`Output.fs:108`). Count-based, severity/status-blind. |
| V3 | L₂, L₇ | **Flagship off by default.** `not (sups ∋ "PROFILE:core") ∧ code="FSA-C02"` suppresses `Option.get` unless the file carries `[<Profile("core")>]` (`Suppression.fs:42`); `--profile core` never injects it. CLI help advertises `interop/cli/etl`, absent from the `Profile` union ⟹ fall through to `Core`. |
| V4 | L₅ | **Status ≠ reality.** FSA-C07 = `Contains("Non"+"Tail")`; FSA-SEC10 no detector; FSA-C04 "Delegated" to a never-loaded analyzer; all 20 `FSA-LINT*` → `FSALINT01` (`LintDelegation.fs:16`, *"map everything to FSALINT01"*), `LintResult.Failure -> ()` (`:36`). |
| V5 | L₄ | **MCP silent-clean.** `McpServer.fs:79` returns violations only; `S`/`F` unsurfaced ⟹ unanalysable project ↦ empty ↦ clean. |
| V6 | L₄, L₆ | **Failures swallowed.** Plugin load failure `Console.WriteLine`+ignore (`PluginLoader.fs:52`); lint failure discarded (V4). |
| V7 | L₁, L₇ | **≥3 divergent taxonomies.** Live `FSA-C0x/S/SEC/TDD`; `Demonstration.md`/roundtrip/all `Specimens EXPECT:` use `FSA1001–1101` (∉𝒞); `out.json` emits `FSA2020/2023` (∉𝒞); `ratecard.md` mislabels `FSA2016` "Unsafe Casts". README badge **MIT** vs `LICENSE` **Apache-2.0**. Version `0.1.0` (README `:7`, `Program.fs:75`) vs `1.0.0` (`McpServer.fs:43`, `Output.fs:84`). |
| V8 | L₅, L₂ | **Substring theatre in "TAST" rules.** Verdicts fall to `GetSubTextFromRange(…).Contains(…)`: FSA-AI14 unless `Contains("try")` (defeated by "country"/"retry"); FSA-AI19 iff `Contains("input")`; FSA-SEC12 iff `Contains("email")`; FSA-C09 on `op_Equality` iff `Contains("null")`. FSA-AI10 fires on any literal `> 1.0` outside a hardcoded list ⟹ flags **π (3.14159)**, **e (2.71828)**; list is not configurable. |
| V9 | L₆ | **Analyzer violates its own creed.** `let mutable f=[]`+`f <- f @ …` (FSA-C10/P01); `ref` cells threaded through recursion — `hasProperty=ref false` (`Library.fs:51`), `assertionsCount:int ref`, `.Value <-` (`Visitor.fs:78,276`), and a throwaway `let dummyRef = ref 0` (`:298`) to satisfy a signature (FSA-C10/C14); `try … with _ -> ""` (FSA-S03); 4× `printfn "DEBUG…"` (`Suppression.fs:17,21`, `Visitor.fs:29,192`); ~30 `SuppressMessage` (`Library.fs`×22, `Domain.fs`×8) + `.fsassayrc` excludes authored code; **0** FsCheck `[<Property>]` tests while FSA-TDD02 mandates them. |
| V10 | (gate) | **Trust gate not a gate.** `FsAssay.Tests` is `OutputType=Exe` Expecto, no test adapter ⟹ `dotnet test` green without running the 38 assertions; `fsassay.yml` passes target **before** options though `Target` is `MainCommand;Last` ⟹ Argu exit 64, masked by `continue-on-error`, SARIF never produced, enforcement skipped. |
| V11 | L₇ | **Docs exist but are not `RuleRecord` projections.** `docs/rules/FSA-*.md` are generated, but omit Status/Mechanism/Certainty/Applicability/Evidence/Examples (not `π∘Σ⃗`). |
| V12 | (hygiene) | Tracked `node_modules`, duplicated `.wasm/.br/.gz` + generated output; checkout > 190 MB. Type Gym 4/32 challenges. |

---

## 4. The verdict lattice to implement

```
𝕍:   Pass ⊑ Inconclusive ⊑ Fail ⊑ ToolFailure       join ⊔ = max (worst wins)
exit : 𝕍 → ℤ    Pass↦0  Fail↦1  Inconclusive↦2  ToolFailure↦3
```
```fsharp
let contributes (f: Finding) : 𝕍 =
    match σ f.Rule, sev f.Rule with
    | (Implemented | Delegated), (Critical | Major) -> Fail          // L2
    | (Implemented | Delegated), Minor              -> Inconclusive
    | Prototype, _                                  -> Inconclusive   // heuristics never Fail
    | _                                            -> Pass

let verdictOf (findings: Finding list) (e: Eval) : 𝕍 =
    (findings |> List.map contributes |> List.fold (⊔) Pass)
    ⊔ (if not e.Failed.IsEmpty then ToolFailure                       // L4
       elif not e.Skipped.IsEmpty then Inconclusive else Pass)
```
By construction `verdict=Pass ⟹ (no Major/Critical Implemented finding) ∧ S=∅ ∧ F=∅` — i.e. `L₂ ∧ L₄`.

---

## 5. Proof obligations

Order `≺`: `O₁ ≺ *`; `{O₂,O₅} ≺ O₄`; `O₅ ≺ O₇`; `O₆(a–d) ≺ O₆(e′)`.

**O₁ ⊢ L₁** — taxonomy closure. Guard test `EXPECT ⊆ 𝒞`; rewrite/delete every `EXPECT:`; regenerate/delete `out.json,out.sarif,ratecard.md,dashboard.html,material.html,Demonstration.md,roundtrip` from the current binary in one run; purge dead `FSA1001` suppressions in `Runner/Domain.fs`.
Discharge: `! grep -rEn "FSA100[0-9]|FSA1101|FSA2020|FSA2023" --include=*.{fs,md,json,sarif,html} .` ∧ `dotnet test --filter L1`.

**O₂ ⊢ L₃** — real ranges. Thread the module/decl `range` into `Graph.buildGraph`; replace every `range0` in `Graph.fs`; implement `calculateDepth` or downgrade FSA2016 (O₅). Property `∀v. v.Range.StartLine ≥ 1` + known-positive corpus per project rule (guards the regression).
Discharge: `dotnet test --filter L3` ∧ `! grep -n range0 FsAssay.Analyzers/Graph.fs`.

**O₃ ⊢ L₄** — evidence completeness. `Orchestrator.analyzeProject` returns `⟨C,S,F⟩`; `McpServer` serializes all three; plugin/lint load failure ∈ `F` (closes V5,V6).
Discharge: tests `unparseable ⟹ Failed≠∅ ∧ verdict≠Pass`, `MCP result ∋ "failed"`.

**O₄ ⊢ L₂** — verdict algebra (§4). Delete `totalViolations>0` gate and the `mutable totalViolations`; `score` counts only `Fail`-eligible findings.
Discharge: `Prototype-only ⟹ verdict∈{Pass,Inconclusive}`; `one Critical Implemented ⟹ Fail`.

**O₅ ⊢ L₅** — status honesty. Reclassify to truth: FSA2016→Dummy, FSA-C07→Dummy/Prototype, FSA-SEC10→Dummy, FSA-C04→Prototype until its analyzer loads; give each LINT a distinct code + surface `Failure`; forbid marker-text detectors for `Implemented`.
Discharge: `∀r. σ(r)=Implemented ⟹ fires(r) ∧ hasPositive r ∧ hasNegative r`; `! grep -rn 'Contains("Non" + "Tail")\|map .* FSALINT01' FsAssay.Analyzers/`.

**O₆ ⊢ L₆** — reflexivity. (a) fold-rewrite every `mutable f`/`f <- f @ …` → pure concat; **replace the `ref` cells** (`hasProperty`, `assertionsCount`, `dummyRef`) by threading accumulators as return values or a `fold` state (they are not irreducible); (b) total symbol helpers isolate the FCS-throws boundary in one ADR'd module — the **sole** surviving suppression, carrying `Justification="FCS symbol resolution is a genuine partial boundary"`; (c) delete the 4 `printfn`; (d) add FsCheck properties (O₂/O₄); **(e′, after a–d)** delete the `SuppressMessage` walls and authored-code exclusions.
Reference fold for the `Call` branch:
```fsharp
| FSharpExprPatterns.Call(objOpt, func, _, _, args) ->
    let name, logical, decl = symbolName func, symbolLogical func, symbolDecl func   // total
    let full = if decl <> "" then decl + "." + logical else name
    let here =
        [ (name = "Microsoft.FSharp.Core.Option.get" && not (isSuppressed sups "FSA-C02")), FSAC02
          (logical = "RunSynchronously"               && not (isSuppressed sups "FSA-C03")), FSAC03
          (Catalogue.isEffectful full                 && not (isSuppressed sups "FSA-C15")), FSAC15 ]
        |> List.choose (fun (fired, r) -> if fired then mkLocated r expr.Range else None)
    here @ (objOpt |> Option.map (fun o -> visitExpr o sups …) |> Option.defaultValue [])
         @ (args |> List.collect (fun a -> visitExpr a sups …))
```
Discharge: `! grep -rnE "mutable |ref false|ref 0|<- .*@|with _ ->|printfn" FsAssay.Analyzers/`; `grep -rc SuppressMessage FsAssay.Analyzers/*.fs` single-digit, each ADR-linked; `dotnet test --filter Property`.

**O₇ ⊢ L₇** — single generator. Define `RuleRecord = {Code;Status;Certainty;Disposition;Mechanism;Profiles;RequiredEvidence;Suppression;Positive;Negative;Precision;Recall;Doc}`; generate registry/ledger/`docs/rules/*`/`--list`/SARIF `rules[]` from it (upgrades V11 docs to full projections). Fix profiles: `--profile p` injects `PROFILE:p` scan-wide (closes V3); align `Profile` union with CLI help. One version `v`.
Discharge: `--profile core ⟹ FSA-C02 fires without any attribute`; `grep -rhoE "[0-9]+\.[0-9]+\.[0-9]+" README.md FsAssay.Runner/*.fs | sort -u | wc -l = 1`.

**O₈ ⊢ L₈** — metric partiality. `precision tp fp = if tp+fp=0 then None else Some(float tp/float(tp+fp))`; adjudicator renders `None` as `undefined`/`Inconclusive`. Discharge: `precision 0 0 = None`.

**O₉** — gate reality + hygiene. Make `dotnet run --project FsAssay.Tests` authoritative (or add a test adapter); fix `fsassay.yml` arg order + drop `continue-on-error`; `.gitignore`/untrack `node_modules`, `*.wasm/.br/.gz`, generated output. Discharge: self-assay produces `results.sarif` and exits per §4; `git ls-files | grep -c node_modules = 0`.

---

## 6. Trust-slice milestone `M⋆`  (before promoting any Prototype)

Freeze scope to the verified-good seed `{FSA-C02, FSA-C05, FSA2022}`; **add FSA-AI12** (LLM-key leakage — the smallest structural member of the moat category, `Const` prefix match, no substring dependence). For each `r`:
`positive ∧ negative ∧ suppression ∧ profile ∧ compiler-error corpora ∧ TP/FP/FN adjudicated ∧ deterministic {JSON,SARIF} ∧ evidence ⟨C,S,F⟩`.
No rule leaves `Prototype` until `M⋆` holds. Surface area is not discharged evidence.

---

## 7. GATE-FINAL

$$\Omega \;\triangleq\; L_1\wedge L_2\wedge L_3\wedge L_4\wedge L_5\wedge L_6\wedge L_7\wedge L_8 \wedge M^\star$$

`Ω` holds iff every `Oᵢ` discharge and `M⋆` are green in a single CI run, and the regenerated `ratecard.md` reports the score self-assay actually produces (no README divergence).

**Ledger.** One line per obligation → `REMEDIATION_LEDGER.md`: `Oᵢ | Lᵢ | commit | discharge-cmd | result | ⟨|C|,|S|,|F|⟩ | ts`. Ledger + regenerated SARIF + CI log = acceptance bundle. No `Oᵢ` is `Pass` without its ledger line.

---

### Appendix A — verified TAST surface (credit, Qwen)
The visitor is genuine typed-tree traversal (not the old regex script): `Call, DefaultValue, Const, ValueSet, Sequential, TryWith, WhileLoop, NewObject, Coerce, LetRec, Entity, MemberOrFunctionOrValue`. Where it degrades to `GetSubTextFromRange(...).Contains` (V8) the rule is at most `Prototype` under `L₅` until promoted to symbol/argument-expression structure.

### Appendix B — delegation
`O₁,O₈,O₉` and the mechanical half of `O₅,O₇` are executor-safe (contract = the discharge). `O₂` (real ranges), `O₄` (verdict algebra), `O₆` (fold + ref-elimination + reflexivity) are architect-owned. Do not begin `O₆(e′)` until `O₆(a–d)` is green, else the analyzer fails on its own un-refactored body and the path of least resistance re-enters `¬L₆`.
