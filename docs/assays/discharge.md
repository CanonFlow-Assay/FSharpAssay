# FsAssay Living Discharge Document

This document tracks the rationale and proof of correctness for rules that have been intentionally delegated or removed from the custom `FsAssay` analyzers.

## Delegated Rules

### FSA-C05: Incomplete Pattern Match on DU
**Status**: Discharged / Delegated
**Rationale**: F# compiler natively provides `FS0025: Incomplete pattern matches on this expression.` Reinventing this inside `FsAssay` as an AST traversal creates brittle edge cases and duplicate work. We delegate this entirely to the F# compiler's built-in analysis.

### FSA-C04: IDisposable Disposed Before Async Runs
**Status**: Discharged / Delegated
**Rationale**: Detecting variable capture and escape inside closures (e.g., passing a disposed reference to an asynchronous callback) requires full control-flow and escape analysis which is currently beyond the simple TAST visitor design of FsAssay. It is deferred to more robust flow-analysis tools or deferred until full escape-proof TAST graph analysis can be built.

## Removed Features

### AutoFix / Code Rewriting
**Status**: Disabled
**Rationale**: `FsAssay` is optimized for deterministic validation as a CI quality gate for AI code. Automatically applying arbitrary string-replacement fixes is non-deterministic and can corrupt valid source code. All violations must be addressed manually or by proper compiler tooling.
