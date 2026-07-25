# fs-assay: Implementation Plan & Deep Dive

## 1. Executive Summary

This plan outlines the architecture and execution strategy for **`fs-assay`** (F# Verification Officer). The tool is designed to be a deterministic, evidence-backed gatekeeper for AI-generated F# code. Its primary directive is to treat AI-generated code as hostile input and require strict, compiler- and analyzer-backed evidence before the code can cross a repository gate.

No Large Language Model (LLM) participates in the verdict path. The tool relies exclusively on static analysis, the F# compiler, and strict policy enforcement.

## 2. Core Architecture & Technologies

`fs-assay` acts as an opinionated release-verification application, integrating the following components:

*   **F# Compiler & `FSharp.Compiler.Service` (FCS):** The source of truth for compilation and type checking.
*   **`FSharp.Analyzers.SDK`:** Used for public AST/TAST contexts, syntax collectors, and analyzer loading. 
    *   *Crucial Decision:* It must use `RunAnalyzersSafely` to capture `Result<Message list, exn>`. Standard analyzer paths that swallow exceptions are strictly forbidden.
*   **`FsAssay.Cli`:** The orchestration layer that owns project loading, tool failure handling, multi-Target Framework Moniker (TFM) semantics, and verdict generation.
*   **`FsAssay.Analyzers`:** Custom F# rules explicitly enforcing skill-specific paradigms.
*   **Fantomas:** The standard formatting authority.
*   **`Sarif.Sdk`:** Used for standardized reporting, augmented with `fs-assay` specific properties like rule certainty and evidence links.

## 3. The Seven Laws of fs-assay

The system operates under strict constitutional laws:

*   **Law Zero (Agent/judge separation):** An AI agent cannot modify the assay policy, toolchain, or rules in the same change it is attempting to pass.
*   **Law One (Honest uncertainty):** Missing evidence, analyzer crashes, or load failures immediately result in `Inconclusive` or `ToolFailure`. They never silently default to `Pass`.
*   **Law Two (Deterministic blocking):** A finding only blocks a release if it is deterministic and explicitly configured to block in the policy. Heuristics cannot block.
*   **Law Three (Reproducible verdict):** Identical source inputs, policies, and toolchains must consistently yield the exact same verdict.
*   **Law Four (Visible suppression):** All inline ignores or baseline exclusions must be recorded, auditable, and backed by a reviewed policy.
*   **Law Five (Honest non-claim):** An `AssayPass` simply means the code meets mechanical obligations. It does not guarantee total correctness, security, or performance.
*   **Law Six (Full release authority):** Only a complete `fs-assay verify` across all defined TFMs can grant a release. Incremental editor checks (`fs-assay check`) are strictly provisional.

## 6. Implementation Phases

The roadmap is structured into structured trust boundaries rather than feature sets:

### Phase 0: Constitution, Compatibility, and Corpus
*   Pin explicit tooling versions (`toolchain-lock.json`).
*   Establish exact compatibility tests for FCS and the Analyzer SDK.
*   Implement MSBuild/ProjInfo project loading spikes.
*   Define the FSharpLint delegation criteria (rules are only delegated if they meet strict determinism and reliability gates).

### Phase 1: Vertical Trust Slice
*   Implement a foundational set of rules to validate the AST/TAST pipelines:
    *   `OBSOLETE_FSA` (`Option.get`/`.Value`) to validate TAST symbol identity.
    *   `OBSOLETE_FSA` (Null literal) to validate AST collection.
    *   `FSA1301` (EF Core scope) and `FSA1402` (Argu `ParseResults` leakage).
*   Implement rule outcomes (`Completed`, `Skipped`, `Failed`) and top-level verdicts.

### Phase 2: Verification MVP
*   Integrate remaining Tier A deterministic rules.
*   Introduce `core`, `shell`, and `oracle` profiles.
*   Implement all-TFM verification and visible suppression reporting.
*   Package `FsAssay.Analyzers` and the `fs-assay` CLI tool.

### Phase 3: Contextual Depth
*   Add advanced profiles (`etl`, `cli`, `interop`, `test`, `script`).
*   Implement the versioned effect/source catalogue to avoid heuristic guesses on unfamiliar libraries.
*   Integrate hybrid AST/TAST context indexing (e.g., matching a TAST symbol within an AST computation expression).

### Phase 4: Fast Feedback and Editor
*   Expose `EditorAnalyzer` adapters.
*   Map only 100% mechanically safe remediations to SDK `Fix` actions.
*   Implement the provisional `fs-assay check --files` command for rapid agent/developer loops.

### Phase 5: Hardening
*   Extensive fault injection (e.g., corrupt policies, truncated evidence, broken projects).
*   Benchmarking and multi-TFM load testing.
*   Ensure the tool degrades gracefully to `ToolFailure` rather than a false `Pass` under duress.

## 7. Evidence and SARIF Output

The CLI generates a comprehensive bundle:
*   `assay-run.json` & `toolchain-lock.json`
*   `compiler-diagnostics.json` & `analyzer-findings.json`
*   `findings.sarif` (SARIF output injected with custom `fs-assay` properties).
*   `suppression-report.json`

## Conclusion

`fs-assay` is not merely a linter; it is a cryptographic-style policy engine for AI development. By leaning heavily on the `FSharp.Analyzers.SDK` (via `RunAnalyzersSafely`), rejecting untrusted heuristics, and demanding explicit evidence, it constructs an environment where AI-generated F# can be trusted for production deployments.
