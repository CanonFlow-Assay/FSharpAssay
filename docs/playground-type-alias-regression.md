# Type-alias regression from Canonflow.Samples

The imported Webuy model contains valid F# abbreviations such as `type Address = string`. Its build succeeds, but the namespace-boundary visitor previously read FCS `FSharpEntity.FullName` unconditionally. FCS throws for this abbreviation, so the packaged playground audit returned ToolFailure and retained its independently observed failed application tests.

The visitor now uses the existing `Graph.tryGetFullName` helper. When no qualified name exists, the abbreviation inherits its enclosing boundary; no guessed symbol identity is manufactured. Child declarations continue to be visited. Rule classification, policy requirements and the zero blocking/advisory admission remain unchanged.

A compiler-backed regression first failed with the reported exception. It now completes analysis and still observes the explicit `File.WriteAllText` effect in a function accepting the alias. The stable suite increases from 120 to 121 cases; all 121 passed through the direct test entry point on SDK 10.0.301. Existing historical qualification records retain their original source identities and counts.

The paired playground consumes a newly packed local candidate at this source revision rather than silently reusing another archive with the same inherited version number. No package-registry release is implied.
