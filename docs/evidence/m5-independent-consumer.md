# M5 independent consumer exercise

Target: `https://github.com/alma-oss/fenvironment-model` at `bbf188efe6435f76e62c95889e4249a976b1ef8d`, MIT licensed. The disposable clone used .NET SDK 10.0.301 and Paket/FAKE as documented. `./build.sh Tests` exited 0: compiler and lint passed and Expecto reported 8 passed, 0 failed. The only package observation was the target's existing NU1901 advisory. The baseline is target evidence, not FsAssay evidence.

The source tree was hashed before and after the exercise and remained at Git tree `1aaaf0c6f1a2a2402648510a3ee19f30d784835f`. No target branch, commit or pull request was created.

FsAssay was installed from a local package built from the M5 candidate, never from public NuGet. An untracked observation policy was added for the run and removed afterward. The observer exited 2 with `Inconclusive`, `authoritative: false`, 5/5 projects loaded, 20/20 eligible files analyzed, no compiler-incomplete files, no tool failures and the required test honestly reported `notRun`. It reported 165 non-authoritative findings: FSA-AI10 x11, FSA-AI11 x16, FSA-ARCH02 x2, FSA-C01 x3, FSA-C03 x1, FSA-C06 x2, FSA-C09 x3, FSA-C15 x3, FSA-F04 x89, FSA-P02 x1, FSA-TDD01 x3, FSA-TDD02 x2, FSA-TDD03 x3 and FSA2022 x26. These counts require human adjudication and are not evidence of success. Zero findings without complete evidence would not be called clean.

## Rejected count-reduction proposal

A one-line proposal added `RequireQualifiedAccess` to the public `EnvironmentNumber` union. It was applied in the disposable clone and the observer count fell from 165 to 149, including FSA-AI11 falling from 16 to 4. That reduction was unsafe and misleading: it broke unqualified case consumers and caused two source files to become compiler-incomplete. The inverse patch was then applied. The exact rejected diff is preserved in `m5-rejected-refactor.patch`; afterward HEAD remained `bbf188efe6435f76e62c95889e4249a976b1ef8d`, tree remained `1aaaf0c6f1a2a2402648510a3ee19f30d784835f`, and both tracked and untracked status were clean.

This one repository does not qualify other SDKs, generators, runners or application shapes. The exercise is observation evidence only, not release authority or an endorsement by the target maintainers.
