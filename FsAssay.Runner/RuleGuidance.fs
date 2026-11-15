namespace FsAssay.Runner

module RuleGuidance =
    let explain code =
        match code with
        | "FSA-C02" ->
            "Replace partial Option.get/.Value with pattern matching or Option.map/bind. A guard in another branch or helper is not a proof this detector understands. Preserve the None behavior and test callers.",
            "match value with Some item -> Ok item | None -> Error MissingValue"
        | "FSA-C05" ->
            "The F# compiler supplies incomplete-pattern diagnostic FS0025. Treat it as an error in projects requiring total decisions. A wildcard can hide future DU cases; review it separately. Compiler-incomplete source is unavailable evidence.",
            "match outcome with Accepted value -> value | Rejected reason -> handle reason"
        | "FSA2022" | "FSA-C15" ->
            "Keep domain decisions independent of known filesystem/network sinks. Pass values or core-owned functions from an explicit shell adapter. The detector recognizes selected symbols, not every transitive effect. MemoryStream is not external I/O.",
            "let decide now policy input = (* pure decision using explicit inputs *) input"
        | "FSA-C10" | "FSA-C14" | "FSA-C16" ->
            "Prefer immutable public domain values. Local mutation in a measured algorithm or framework adapter can be justified; moving code into shell cannot waive required evidence. Mutable exposure and deep immutability need separate review.",
            "let updated = { original with Status = Approved }"
        | "FSA-SEC09" | "FSA-SEC10" | "FSA-S01" | "FSA-AI12" ->
            "Source heuristics cannot establish dependency vulnerability or secret absence. Use the independent pinned offline SecurityAssay receipt for declared source and lock bytes. Never copy a real secret into a specimen or failure message.",
            "Use declared F# sources, packages.lock.json, pinned scanners and an explicit evaluation time."
        | "FSA-SEC08" | "FSA-SEC11" | "FSA-SEC12" | "FSA-SEC13" | "FSA-S02" ->
            "This observation is a bounded heuristic, not an authorization, signing, privacy, SSRF or path-safety proof. Validate trust boundaries and execute application-specific hostile-input tests. An attribute or name alone is insufficient.",
            "Test rejected input and allowed input at the actual adapter boundary."
        | "FSA-TDD01" | "FSA-TDD02" | "FSA-TDD03" | "FSA-TDD04" ->
            "File names, property attributes, assertion counts and commit order are observations about test style. They do not establish test quality or correctness. Keep useful grouped assertions and qualify named behavioral requirements.",
            "Register stable test identities for boundary cases, failure cases and architecture constraints."
        | "FSA-F07" | "FSA-E01" | "FSA-E03" | "FSA-ML02" ->
            "F# supports classes, objects and interop. This catalogue entry does not establish a universal prohibition; use the reviewed Shape contract and actual framework requirements. Do not rewrite working public APIs on catalogue severity alone.",
            "Choose records, DUs, functions or classes according to the contract and measured needs."
        | _ ->
            "Review the exact symbol, caller contract and framework boundary. This catalogue observation is not admitted authority. Preview suggested edits and preserve application assertions before a fresh full verification.",
            "Use fsassay explain <RULE>, retain the failed receipt, repair source, then fsassay verify <target>."
