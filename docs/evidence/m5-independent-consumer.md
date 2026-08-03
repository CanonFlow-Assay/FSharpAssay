# M5 independent consumer exercise

Target: `https://github.com/alma-oss/fenvironment-model` at `bbf188efe6435f76e62c95889e4249a976b1ef8d`, MIT licensed. The disposable clone used .NET SDK 10.0.301 and Paket/FAKE as documented. `./build.sh Tests` exited 0: compiler and lint passed and Expecto reported 8 passed, 0 failed. The only package observation was the target's existing NU1901 advisory. The baseline is target evidence, not FsAssay evidence.

The source tree was hashed before and after the exercise and remained at Git tree `1aaaf0c6f1a2a2402648510a3ee19f30d784835f`. No target branch, commit or pull request was created.

FsAssay was installed from a local package built from the M5 candidate, never from public NuGet. Its observer result is recorded as non-authoritative. Required tests cannot be ingested by 1.0.4 and any workspace/compiler incompleteness remains a reason against authority; zero findings would not be called clean.

## Rejected count-reduction proposal

A proposed patch would replace `Environment`'s explicit custom equality/comparison representation with structural defaults. That may reduce style/shape findings, but it can change equality, ordering, public API and Fable behavior. The proposal was rejected without adoption. The representative rejected diff is preserved in `m5-rejected-refactor.patch`; the disposable target was restored and its Git status verified clean.

This one repository does not qualify other SDKs, generators, runners or application shapes. The exercise is observation evidence only, not release authority or an endorsement by the target maintainers.
