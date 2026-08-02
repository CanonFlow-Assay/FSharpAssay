# FsAssay CanonFlow obligation plugin

This separately admitted plugin checks whether a generated F# model still
preserves the structural obligations declared by a CanonFlow profile.

It checks compiler-resolved union types, cases, payload types, private
representations and mapping signatures. It also checks source, manifest,
generated-artifact and suppression-audit digests, and reports wildcard matches
and unaudited `CFF-OBLnnn` suppressions.

Set `CANONFLOW_FSASSAY_OBLIGATION_PROFILE` to the absolute profile path and load
the built assembly with `fsassay --plugin`.

This plugin is not a business-semantic oracle. A clean result means only that
the admitted F# structure and evidence bindings were preserved. Compiler
failure or missing typed-tree evidence is `Inconclusive`; a requested plugin
that cannot load is `ToolFailure`.
