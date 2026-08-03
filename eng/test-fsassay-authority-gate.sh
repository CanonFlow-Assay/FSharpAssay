#!/usr/bin/env bash
set -euo pipefail

root=$(git rev-parse --show-toplevel)
gate="$root/eng/fsassay-authority-gate.sh"
workspace=$(mktemp -d)
trap 'find "$workspace" -depth -delete' EXIT

pass="$workspace/pass.json"
jq -n '{
  schemaVersion:"fsassay-authority-receipt/1.1.0", tool:"FsAssay", toolVersion:"1.0.4",
  candidate:{kind:"commit",commitSha:("a"*40),approvedHeadSha:("a"*40),treeSha:("b"*40),dirty:false,syntheticMergeSha:"not-applicable",packageSha256:"not-applicable",repositoryRelativeTarget:"Sample.slnx"},
  policy:{status:"loaded",path:"fsassay-policy.lock.json",sha256:("c"*64),error:"",snapshot:{
    policySchemaVersion:"fsassay-policy/1.1.0",evidenceSchemaVersion:"fsassay-authority-receipt/1.1.0",authorityContractVersion:"authority-contract/1.0.0",shapeContractVersion:"fsharp-shape/1.0.0",toolVersion:"1.0.4",evaluationDate:"2026-08-03",
    enabledProfiles:["core"],approvedBlockingRules:[],advisoryRules:[],experimentalRules:["FSA-C02"],prototypeRules:[],dummyRules:[],deprecatedRules:[],removedRules:[],requiredProjectClasses:["core"],requiredTargetFrameworks:["net10.0"],
    requiredTests:[{id:"tests",project:"Tests/Tests.fsproj",minimumPassed:1}],baseline:{identity:"none",reviewedBy:"",reviewedOn:"",records:[]},exceptions:[]}},
  toolchain:{sdkVersion:"10.0.301",runtimeVersion:"10.0.9",fsharpCompilerServiceVersion:"43.12.201.0"},
  outcome:"Pass",authoritative:true,reasons:[],
  counts:{projectsDiscovered:1,projectsLoaded:1,projectsFailed:0,projectsSkipped:0,projectsUnsupported:0,eligibleFiles:1,analyzedFiles:1,compilerIncompleteFiles:0},
  projects:[{path:"Core/Core.fsproj",projectClass:"core",targetFrameworks:["net10.0"],status:"loaded",reason:""}],
  sources:[{path:"Core/Domain.fs",disposition:"analyzed",reason:""}],
  tests:[{id:"tests",project:"Tests/Tests.fsproj",status:"passed",passed:1,failed:0,skipped:0}],
  rules:[{ruleId:"FSA-C02",authorityClass:"experimental",status:"completed",evidenceAvailable:true,findingCount:0}],findings:[],
  appliedBaselineRecords:[],appliedSuppressions:[],policyErrors:[],evidenceErrors:[],missingEvidence:[],toolFailures:[]
}' >"$pass"

expect_exit() {
  local name="$1" expected="$2" file="$3"
  set +e
  "$gate" "$file" >"$workspace/$name.stdout" 2>"$workspace/$name.stderr"
  local actual=$?
  set -e
  if [[ "$actual" -ne "$expected" ]]; then
    echo "$name: expected exit $expected, got $actual" >&2
    cat "$workspace/$name.stderr" >&2
    exit 1
  fi
}

mutate() {
  local name="$1" filter="$2"
  jq "$filter" "$pass" >"$workspace/$name.json"
}

expect_exit pass 0 "$pass"
summary_path="$workspace/explicit-summary.md"
"$gate" "$pass" "$summary_path" >/dev/null
grep -Fxq 'Authority: PASS — merge allowed' "$summary_path"

mutate fail '.outcome="Fail" | .authoritative=false | .reasons=[{code:"blocking-finding",detail:"blocking finding remains"}]'
expect_exit fail 1 "$workspace/fail.json"
mutate authoritative_fail '.outcome="Fail" | .authoritative=true | .reasons=[{code:"blocking-finding",detail:"blocking finding remains"}]'
expect_exit authoritative_fail 1 "$workspace/authoritative_fail.json"
mutate tool_failure '.outcome="ToolFailure" | .authoritative=false | .reasons=[{code:"tool-failure",detail:"workspace host failed"}] | .toolFailures=["workspace host failed"]'
expect_exit tool_failure 1 "$workspace/tool_failure.json"

jq '.outcome="Inconclusive" | .authoritative=false |
  .reasons=[{code:"project-unsupported",detail:"2 unsupported projects"},{code:"required-test-not-run",detail:"required test notRun"}] |
  .counts.projectsDiscovered=3 | .counts.projectsUnsupported=2 |
  .projects += [{path:"ShellA/ShellA.fsproj",projectClass:"shell",targetFrameworks:["net10.0"],status:"unsupported",reason:"unsupported"},{path:"ShellB/ShellB.fsproj",projectClass:"shell",targetFrameworks:["net10.0"],status:"unsupported",reason:"unsupported"}] |
  .tests[0].status="notRun" | .tests[0].passed=0 |
  .findings=[range(0;563)|{ruleId:"FSA-C02",path:"Core/Domain.fs",symbol:"sample",line:(.+1),column:0,message:"experimental observation",fingerprint:("d"*64),authorityClass:"experimental"}] |
  .rules[0].findingCount=563' "$pass" >"$workspace/inconclusive.json"
expect_exit inconclusive 1 "$workspace/inconclusive.json"
cat >"$workspace/expected-summary.txt" <<'EOF'
Authority: INCONCLUSIVE — merge blocked
Reasons: 2 unsupported projects, required test notRun
Observations: 563 experimental — informational
EOF
cmp "$workspace/expected-summary.txt" "$workspace/inconclusive.stdout"

set +e
"$gate" "$workspace/missing.json" >/dev/null 2>&1
test $? -eq 2
set -e
: >"$workspace/empty.json"
expect_exit empty 2 "$workspace/empty.json"
printf '{invalid' >"$workspace/invalid.json"
expect_exit invalid 2 "$workspace/invalid.json"
printf 'null\n' >"$workspace/null.json"
expect_exit null 2 "$workspace/null.json"

mutate missing_top 'del(.counts)'
expect_exit missing_top 2 "$workspace/missing_top.json"
mutate missing_outcome 'del(.outcome)'
expect_exit missing_outcome 2 "$workspace/missing_outcome.json"
mutate wrong_outcome '.outcome=7'
expect_exit wrong_outcome 2 "$workspace/wrong_outcome.json"
mutate missing_authority 'del(.authoritative)'
expect_exit missing_authority 2 "$workspace/missing_authority.json"
mutate wrong_authority '.authoritative="true"'
expect_exit wrong_authority 2 "$workspace/wrong_authority.json"
mutate nonpass_true '.outcome="Inconclusive" | .authoritative=true'
expect_exit nonpass_true 2 "$workspace/nonpass_true.json"
mutate pass_false '.authoritative=false'
expect_exit pass_false 2 "$workspace/pass_false.json"

mutate pass_reasons '.reasons=[{code:"unexpected",detail:"unexpected"}]'
expect_exit pass_reasons 2 "$workspace/pass_reasons.json"
mutate pass_policy_error '.policyErrors=["bad policy"]'
expect_exit pass_policy_error 2 "$workspace/pass_policy_error.json"
mutate pass_evidence_error '.evidenceErrors=["bad evidence"]'
expect_exit pass_evidence_error 2 "$workspace/pass_evidence_error.json"
mutate pass_missing '.missingEvidence=["missing"]'
expect_exit pass_missing 2 "$workspace/pass_missing.json"
mutate pass_tool_failure '.toolFailures=["failed"]'
expect_exit pass_tool_failure 2 "$workspace/pass_tool_failure.json"
mutate candidate_incomplete '.candidate.commitSha="unavailable"'
expect_exit candidate_incomplete 2 "$workspace/candidate_incomplete.json"
mutate policy_incomplete '.policy.status="unavailable" | .policy.sha256="unavailable"'
expect_exit policy_incomplete 2 "$workspace/policy_incomplete.json"
mutate zero_loaded '.counts.projectsLoaded=0 | .projects=[]'
expect_exit zero_loaded 2 "$workspace/zero_loaded.json"
mutate project_count_drift '.counts.projectsDiscovered=2'
expect_exit project_count_drift 2 "$workspace/project_count_drift.json"
mutate project_failed '.counts.projectsLoaded=0 | .counts.projectsFailed=1 | .projects[0].status="failed"'
expect_exit project_failed 2 "$workspace/project_failed.json"
mutate source_incomplete '.counts.compilerIncompleteFiles=1 | .sources[0].disposition="compiler-incomplete"'
expect_exit source_incomplete 2 "$workspace/source_incomplete.json"
mutate test_not_run '.tests[0].status="notRun" | .tests[0].passed=0'
expect_exit test_not_run 2 "$workspace/test_not_run.json"
mutate blocking_finding '.findings=[{ruleId:"FSA-BLOCK",path:"Core/Domain.fs",symbol:"sample",line:1,column:0,message:"blocking",fingerprint:("e"*64),authorityClass:"blocking"}] | .rules[0].findingCount=1'
expect_exit blocking_finding 2 "$workspace/blocking_finding.json"
mutate rule_count_shift '.rules[0].findingCount=1'
expect_exit rule_count_shift 2 "$workspace/rule_count_shift.json"
mutate unknown_finding '.findings=[{ruleId:"FSA-UNKNOWN",path:"Core/Domain.fs",symbol:"sample",line:1,column:0,message:"unknown",fingerprint:("f"*64),authorityClass:"experimental"}]'
expect_exit unknown_finding 2 "$workspace/unknown_finding.json"
mutate duplicate_policy_rule '.policy.snapshot.prototypeRules=["FSA-C02"]'
expect_exit duplicate_policy_rule 2 "$workspace/duplicate_policy_rule.json"

github_summary="$workspace/github-summary.md"
GITHUB_STEP_SUMMARY="$github_summary" "$gate" "$workspace/inconclusive.json" >/dev/null || test $? -eq 1
cmp "$workspace/expected-summary.txt" "$github_summary"

echo "FsAssay authority gate regression passed"
