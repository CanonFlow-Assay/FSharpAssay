#!/usr/bin/env bash
set -euo pipefail

receipt="${1:-}"
summary_path="${2:-${FSASSAY_AUTHORITY_SUMMARY:-${GITHUB_STEP_SUMMARY:-}}}"

if [[ $# -lt 1 || $# -gt 2 || -z "$receipt" ]]; then
  echo "usage: $0 <authority-receipt.json> [summary-path]" >&2
  exit 64
fi

emit_summary() {
  local value="$1"
  printf '%s' "$value"
  if [[ -n "$summary_path" ]]; then
    printf '%s' "$value" >>"$summary_path"
  fi
}

invalid() {
  local detail="$1"
  local invalid_summary
  printf -v invalid_summary 'Authority: INVALID EVIDENCE — merge blocked\nReasons: %s\nObservations: unavailable — informational\n' "$detail"
  emit_summary "$invalid_summary"
  echo "FsAssay authority evidence invalid: $detail" >&2
  exit 2
}

[[ -f "$receipt" ]] || invalid "receipt is missing: $receipt"
[[ -s "$receipt" ]] || invalid "receipt is empty: $receipt"
jq -e . "$receipt" >/dev/null 2>&1 || invalid "receipt is not valid JSON"

structural_filter=$(cat <<'JQ'
def exactkeys($expected): type == "object" and ((keys | sort) == ($expected | sort));
def nonempty: type == "string" and length > 0;
def strings: type == "array" and all(.[]; type == "string") and length == (unique | length);
def nat: type == "number" and floor == . and . >= 0;
def sha40: type == "string" and test("^[0-9a-fA-F]{40}$");
def sha64: type == "string" and test("^[0-9a-fA-F]{64}$");
def relative: nonempty and (startswith("/") | not) and (test("^[A-Za-z]:[\\\\/]") | not) and (startswith("../") | not);
def date: type == "string" and test("^[0-9]{4}-[0-9]{2}-[0-9]{2}$");
def authority_class: . == "blocking" or . == "advisory" or . == "experimental" or . == "prototype" or . == "dummy" or . == "deprecated" or . == "removed";
def baseline_record:
  exactkeys(["id","ruleId","fingerprint","relativePath","symbol","owner","rationale","disposition","createdOn","expiresOn","policyVersion"]) and
  (.id | nonempty) and (.ruleId | nonempty) and (.fingerprint | sha64) and (.relativePath | relative) and
  (.symbol | nonempty) and (.owner | nonempty) and (.rationale | nonempty) and
  (.disposition == "accepted" or .disposition == "resolved") and (.createdOn | date) and
  (.expiresOn | type == "string") and .policyVersion == "fsassay-policy/1.1.0";
def policy_exception:
  exactkeys(["id","category","relativePath","symbol","owner","reason","createdOn","expiresOn","shapeClauses"]) and
  (.id | nonempty) and
  (.category == "hosting" or .category == "serialization" or .category == "persistence" or .category == "ui" or .category == "dependency-injection" or .category == "interoperability") and
  (.relativePath | relative) and (.symbol | nonempty) and (.owner | nonempty) and (.reason | nonempty) and
  (.createdOn | date) and (.expiresOn | type == "string") and (.shapeClauses | strings);

exactkeys(["schemaVersion","tool","toolVersion","candidate","policy","toolchain","outcome","authoritative","reasons","counts","projects","sources","tests","rules","findings","appliedBaselineRecords","appliedSuppressions","policyErrors","evidenceErrors","missingEvidence","toolFailures"]) and
.schemaVersion == "fsassay-authority-receipt/1.1.0" and .tool == "FsAssay" and (.toolVersion | nonempty) and
(.outcome == "Pass" or .outcome == "Fail" or .outcome == "Inconclusive" or .outcome == "ToolFailure") and
(.authoritative | type == "boolean") and
(.candidate | exactkeys(["kind","commitSha","approvedHeadSha","treeSha","dirty","syntheticMergeSha","packageSha256","repositoryRelativeTarget"])) and
(.candidate.kind == "commit" or .candidate.kind == "dirty-worktree" or .candidate.kind == "synthetic-merge" or .candidate.kind == "package" or .candidate.kind == "unversioned") and
((.candidate.commitSha | sha40) or .candidate.commitSha == "unavailable") and
((.candidate.approvedHeadSha | sha40) or .candidate.approvedHeadSha == "unavailable") and
((.candidate.treeSha | sha40) or .candidate.treeSha == "unavailable") and
(.candidate.dirty | type == "boolean") and
((.candidate.syntheticMergeSha | sha40) or .candidate.syntheticMergeSha == "not-applicable") and
((.candidate.packageSha256 | sha64) or .candidate.packageSha256 == "not-applicable") and
(.candidate.repositoryRelativeTarget | relative) and
(.policy | exactkeys(["status","path","sha256","snapshot","error"])) and
(.policy.status == "loaded" or .policy.status == "invalid" or .policy.status == "unavailable") and
(.policy.path | relative) and (((.policy.sha256 | sha64) or .policy.sha256 == "invalid" or .policy.sha256 == "unavailable")) and
(.policy.error | type == "string") and
(.policy.snapshot | exactkeys(["policySchemaVersion","evidenceSchemaVersion","authorityContractVersion","shapeContractVersion","toolVersion","evaluationDate","enabledProfiles","approvedBlockingRules","advisoryRules","experimentalRules","prototypeRules","dummyRules","deprecatedRules","removedRules","requiredProjectClasses","requiredTargetFrameworks","requiredTests","baseline","exceptions"])) and
.policy.snapshot.policySchemaVersion == "fsassay-policy/1.1.0" and
.policy.snapshot.evidenceSchemaVersion == "fsassay-authority-receipt/1.1.0" and
.policy.snapshot.authorityContractVersion == "authority-contract/1.0.0" and
.policy.snapshot.shapeContractVersion == "fsharp-shape/1.0.0" and
(.policy.snapshot.toolVersion | nonempty) and (.policy.snapshot.evaluationDate | date) and
(.policy.snapshot.enabledProfiles | strings) and (.policy.snapshot.approvedBlockingRules | strings) and
(.policy.snapshot.advisoryRules | strings) and (.policy.snapshot.experimentalRules | strings) and
(.policy.snapshot.prototypeRules | strings) and (.policy.snapshot.dummyRules | strings) and
(.policy.snapshot.deprecatedRules | strings) and (.policy.snapshot.removedRules | strings) and
(.policy.snapshot.requiredProjectClasses | strings) and (.policy.snapshot.requiredTargetFrameworks | strings) and
(.policy.snapshot.requiredTests | type == "array") and all(.policy.snapshot.requiredTests[];
  exactkeys(["id","project","minimumPassed"]) and (.id | nonempty) and (.project | relative) and (.minimumPassed | nat) and .minimumPassed > 0) and
(.policy.snapshot.baseline | exactkeys(["identity","reviewedBy","reviewedOn","records"])) and
(.policy.snapshot.baseline.identity | nonempty) and (.policy.snapshot.baseline.reviewedBy | type == "string") and
(.policy.snapshot.baseline.reviewedOn | type == "string") and (.policy.snapshot.baseline.records | type == "array") and
all(.policy.snapshot.baseline.records[]; baseline_record) and
(.policy.snapshot.exceptions | type == "array") and all(.policy.snapshot.exceptions[]; policy_exception) and
(.toolchain | exactkeys(["sdkVersion","runtimeVersion","fsharpCompilerServiceVersion"])) and
(.toolchain.sdkVersion | nonempty) and (.toolchain.runtimeVersion | nonempty) and (.toolchain.fsharpCompilerServiceVersion | nonempty) and
(.reasons | type == "array") and all(.reasons[]; exactkeys(["code","detail"]) and (.code | nonempty) and (.detail | nonempty)) and
(.counts | exactkeys(["projectsDiscovered","projectsLoaded","projectsFailed","projectsSkipped","projectsUnsupported","eligibleFiles","analyzedFiles","compilerIncompleteFiles"])) and
all(.counts[]; nat) and
(.projects | type == "array") and all(.projects[];
  exactkeys(["path","projectClass","targetFrameworks","status","reason"]) and (.path | relative) and (.projectClass | nonempty) and
  (.targetFrameworks | strings and length > 0) and
  (.status == "loaded" or .status == "failed" or .status == "skipped" or .status == "unsupported") and (.reason | type == "string")) and
(.sources | type == "array") and all(.sources[];
  exactkeys(["path","disposition","reason"]) and (.path | relative) and
  (.disposition == "analyzed" or .disposition == "compiler-incomplete" or .disposition == "generated-excluded" or .disposition == "policy-excluded") and (.reason | type == "string")) and
(.tests | type == "array") and all(.tests[];
  exactkeys(["id","project","status","passed","failed","skipped"]) and (.id | nonempty) and (.project | relative) and
  (.status == "passed" or .status == "failed" or .status == "notRun" or .status == "skipped") and
  (.passed | nat) and (.failed | nat) and (.skipped | nat)) and
(.rules | type == "array") and all(.rules[];
  exactkeys(["ruleId","authorityClass","status","evidenceAvailable","findingCount"]) and (.ruleId | nonempty) and
  (.authorityClass | authority_class) and (.status == "completed" or .status == "incomplete" or .status == "unavailable") and
  (.evidenceAvailable | type == "boolean") and (.findingCount | nat)) and
(.findings | type == "array") and all(.findings[];
  exactkeys(["ruleId","path","symbol","line","column","message","fingerprint","authorityClass"]) and
  (.ruleId | nonempty) and (.path | relative) and (.symbol | nonempty) and (.line | nat) and .line > 0 and
  (.column | nat) and (.message | nonempty) and (.fingerprint | sha64) and (.authorityClass | authority_class)) and
(.appliedBaselineRecords | strings) and (.appliedSuppressions | type == "array" and length == 0) and
(.policyErrors | strings) and (.evidenceErrors | strings) and (.missingEvidence | strings) and (.toolFailures | strings)
JQ
)

jq -e "$structural_filter" "$receipt" >/dev/null 2>&1 || invalid "unsupported or incomplete receipt structure"

outcome=$(jq -r '.outcome' "$receipt")
authoritative=$(jq -r '.authoritative' "$receipt")
if [[ "$outcome" = "Pass" && "$authoritative" != "true" ]] ||
   [[ "$outcome" = "Inconclusive" && "$authoritative" != "false" ]] ||
   [[ "$outcome" = "ToolFailure" && "$authoritative" != "false" ]]; then
  invalid "outcome and authoritative flag contradict the four-state contract"
fi

pass_filter=$(cat <<'JQ'
def sha40: type == "string" and test("^[0-9a-fA-F]{40}$");
def sha64: type == "string" and test("^[0-9a-fA-F]{64}$");
. as $r |
($r.policy.snapshot as $p |
  ([ $p.approvedBlockingRules[] | {ruleId:., authorityClass:"blocking"} ] +
   [ $p.advisoryRules[] | {ruleId:., authorityClass:"advisory"} ] +
   [ $p.experimentalRules[] | {ruleId:., authorityClass:"experimental"} ] +
   [ $p.prototypeRules[] | {ruleId:., authorityClass:"prototype"} ] +
   [ $p.dummyRules[] | {ruleId:., authorityClass:"dummy"} ] +
   [ $p.deprecatedRules[] | {ruleId:., authorityClass:"deprecated"} ] +
   [ $p.removedRules[] | {ruleId:., authorityClass:"removed"} ])) as $policyRules |
$r.outcome == "Pass" and $r.authoritative == true and
($r.candidate.commitSha | sha40) and ($r.candidate.approvedHeadSha | sha40) and ($r.candidate.treeSha | sha40) and $r.candidate.dirty == false and
((($r.candidate.kind == "commit") and $r.candidate.commitSha == $r.candidate.approvedHeadSha and
   $r.candidate.syntheticMergeSha == "not-applicable" and $r.candidate.packageSha256 == "not-applicable") or
 (($r.candidate.kind == "synthetic-merge") and ($r.candidate.syntheticMergeSha | sha40) and
   $r.candidate.syntheticMergeSha == $r.candidate.commitSha and $r.candidate.packageSha256 == "not-applicable") or
 (($r.candidate.kind == "package") and ($r.candidate.packageSha256 | sha64) and
   (($r.candidate.syntheticMergeSha == "not-applicable" and $r.candidate.commitSha == $r.candidate.approvedHeadSha) or
    (($r.candidate.syntheticMergeSha | sha40) and $r.candidate.syntheticMergeSha == $r.candidate.commitSha)))) and
$r.policy.status == "loaded" and ($r.policy.sha256 | sha64) and $r.policy.error == "" and
$r.policy.snapshot.toolVersion == $r.toolVersion and
($r.reasons | length) == 0 and ($r.policyErrors | length) == 0 and ($r.evidenceErrors | length) == 0 and
($r.missingEvidence | length) == 0 and ($r.toolFailures | length) == 0 and
$r.counts.projectsDiscovered == ($r.projects | length) and
$r.counts.projectsLoaded == ([$r.projects[] | select(.status == "loaded")] | length) and
$r.counts.projectsFailed == ([$r.projects[] | select(.status == "failed")] | length) and
$r.counts.projectsSkipped == ([$r.projects[] | select(.status == "skipped")] | length) and
$r.counts.projectsUnsupported == ([$r.projects[] | select(.status == "unsupported")] | length) and
$r.counts.projectsDiscovered > 0 and $r.counts.projectsLoaded > 0 and $r.counts.projectsFailed == 0 and
$r.counts.projectsSkipped == 0 and $r.counts.projectsUnsupported == 0 and
$r.counts.analyzedFiles == ([$r.sources[] | select(.disposition == "analyzed")] | length) and
$r.counts.compilerIncompleteFiles == ([$r.sources[] | select(.disposition == "compiler-incomplete")] | length) and
$r.counts.eligibleFiles == ($r.counts.analyzedFiles + $r.counts.compilerIncompleteFiles) and
$r.counts.eligibleFiles > 0 and $r.counts.analyzedFiles == $r.counts.eligibleFiles and $r.counts.compilerIncompleteFiles == 0 and
all($r.projects[]; .status == "loaded") and all($r.sources[]; .disposition != "compiler-incomplete") and
([$r.policy.snapshot.requiredProjectClasses[] as $class | any($r.projects[]; .status == "loaded" and .projectClass == $class)] | all) and
([$r.policy.snapshot.requiredTargetFrameworks[] as $tf | any($r.projects[].targetFrameworks[]; . == $tf)] | all) and
all($r.tests[]; .status == "passed" and .failed == 0) and
([$r.policy.snapshot.requiredTests[] as $required |
  any($r.tests[]; .id == $required.id and .project == $required.project and .status == "passed" and
    .passed >= $required.minimumPassed and .failed == 0)] | all) and
($policyRules | length) == ($policyRules | unique_by(.ruleId) | length) and
($r.rules | length) == ($r.rules | unique_by(.ruleId) | length) and
($r.rules | length) == ($policyRules | length) and
([$policyRules[] as $policyRule |
  any($r.rules[]; .ruleId == $policyRule.ruleId and .authorityClass == $policyRule.authorityClass)] | all) and
([($r.policy.snapshot.approvedBlockingRules + $r.policy.snapshot.advisoryRules)[] as $ruleId |
  any($r.rules[]; .ruleId == $ruleId and .status == "completed" and .evidenceAvailable == true)] | all) and
([$r.rules[] as $rule |
  ([$r.findings[] | select(.ruleId == $rule.ruleId and .authorityClass == $rule.authorityClass)] | length) == $rule.findingCount] | all) and
([$r.findings[] as $finding |
  any($r.rules[]; .ruleId == $finding.ruleId and .authorityClass == $finding.authorityClass)] | all) and
all($r.findings[]; .authorityClass != "blocking") and
all($r.rules[] | select(.authorityClass == "blocking"); .findingCount == 0)
JQ
)

if [[ "$outcome" = "Pass" ]]; then
  jq -e "$pass_filter" "$receipt" >/dev/null 2>&1 || invalid "Pass receipt contains incomplete, missing, failed, or blocking evidence"
fi

case "$outcome" in
  Pass) authority_line="Authority: PASS — merge allowed" ;;
  Fail) authority_line="Authority: FAIL — merge blocked" ;;
  Inconclusive) authority_line="Authority: INCONCLUSIVE — merge blocked" ;;
  ToolFailure) authority_line="Authority: TOOL FAILURE — merge blocked" ;;
esac

reasons=$(jq -r 'if (.reasons | length) == 0 then "none" else [.reasons[].detail] | join(", ") end' "$receipt")
observations=$(jq -r '
  [.findings[] | select(.authorityClass != "blocking")]
  | if length == 0 then "0"
    else group_by(.authorityClass)
      | map("\(length) \(.[0].authorityClass)")
      | join(", ")
    end
' "$receipt")
printf -v summary '%s\nReasons: %s\nObservations: %s — informational\n' "$authority_line" "$reasons" "$observations"
emit_summary "$summary"

[[ "$outcome" = "Pass" ]] && exit 0
exit 1
