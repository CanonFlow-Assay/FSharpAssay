# Playground convergence — phase 1

M5 already demonstrates the useful portable shape: a closed F# domain outcome,
Application ports, Shell transport mapping, behavior/architecture tests and an
isolated package consumer. Decision Assist and application assessment remain
separate from FsAssay's raw authority.

The next candidate is a versioned transport-fact boundary inside a new, bounded
Shell test: parsing/trust/deduplication stay outside the pure decision. Run it
only through the existing SDK 11.0.100 and M5 qualification flow. Do not turn an
Inconclusive native receipt into Pass, broaden rule admission, or copy the C#
serialization/time samples without an F# application requirement.

## Phase 2 specimen (`playground/m5/TransportFacts.fsx`)

Run the versioned transport-fact shell boundary over the M5 `Domain -> Application -> Shell` pipeline on `.NET SDK 11.0.100`:

```sh
dotnet fsi --warnaserror+ --exec playground/m5/TransportFacts.fsx
```
