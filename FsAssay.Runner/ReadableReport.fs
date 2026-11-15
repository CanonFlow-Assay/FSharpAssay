namespace FsAssay.Runner

open System
open System.Net
open System.Text
open FsAssay.Analyzers.Domain

module ReadableReport =
    let private singleLine (value: string) = value |> Seq.map (fun c -> if Char.IsControl c then ' ' else c) |> Seq.toArray |> String
    let private markdown (value: string) = WebUtility.HtmlEncode(value).Replace("\\", "\\\\").Replace("|", "\\|").Replace("`", "\\`").Replace("[", "\\[").Replace("]", "\\]").Replace("*", "\\*").Replace("_", "\\_")
    let nextAction (receipt: Authority.AuthorityReceipt) =
        match receipt.outcome with
        | "ToolFailure" -> "Resolve the reported tool or evidence-integrity failure, then rerun verification."
        | "Fail" -> "Repair the failed required tests or admitted checks; retain the failed evidence and rerun verification."
        | "Inconclusive" -> "Supply the missing required evidence and rerun verification. Experimental observations do not complete evidence."
        | _ -> "Configured requirements completed for this scope. Review remaining experimental observations and application limits."
    let lines (receipt: Authority.AuthorityReceipt) (runs: Verification.TestRun list) =
        [
            yield "FsAssay " + receipt.outcome + " | authoritative=" + string receipt.authoritative
            yield sprintf "Projects: %d discovered, %d workspace-loaded, %d supported | Sources: %d analyzed" receipt.counts.projectsDiscovered receipt.counts.projectsWorkspaceLoaded receipt.counts.projectsSupported receipt.counts.analyzedFiles
            yield "Next action: " + nextAction receipt
            for reason in receipt.reasons do yield reason.code + ": " + reason.detail
            for test in receipt.tests do yield sprintf "Test %s: %s (%d passed, %d failed, %d skipped)" test.id test.status test.passed test.failed test.skipped
            for run in runs do
                for detail in run.failureDetails do yield "Failed test " + detail.testName + ": " + (if detail.message = "" then "assertion detail unavailable" else detail.message)
                if run.failureDetailsTruncated then yield "Additional failure details were truncated."
            for finding in receipt.findings do
                yield sprintf "%s(%d,%d): %s [%s] %s" finding.path finding.line (finding.column + 1) finding.ruleId finding.authorityClass finding.message
            if receipt.findings.Length = 0 then yield "No rule observations in this scope; required evidence determines the outcome."
        ]
    let text receipt runs = lines receipt runs |> List.map singleLine |> String.concat "\n"
    let markdownReport receipt runs invocation =
        "# FsAssay verification report\n\n" +
        (lines receipt runs |> List.map (singleLine >> markdown) |> String.concat "\n\n") +
        "\n\n## Repeat invocation\n\n" + markdown invocation + "\n"
    let html receipt runs invocation =
        let paragraphs = lines receipt runs |> List.map (fun line -> "<p>" + WebUtility.HtmlEncode line + "</p>") |> String.concat "\n"
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>FsAssay verification report</title><style>body{font:16px system-ui;max-width:80ch;margin:3rem auto;padding:0 1rem;background:#10202a;color:#e2edf3}p{overflow-wrap:anywhere;white-space:pre-wrap}</style></head><body><h1>FsAssay verification report</h1>" + paragraphs + "<h2>Repeat invocation</h2><p>" + WebUtility.HtmlEncode invocation + "</p></body></html>"
    let msbuild (receipt: Authority.AuthorityReceipt) =
        [
            for reason in receipt.reasons do
                yield sprintf "fsassay : %s FSA-EVIDENCE: %s" (if receipt.outcome = "Fail" || receipt.outcome = "ToolFailure" then "error" else "warning") (singleLine reason.detail)
            for finding in receipt.findings do
                yield sprintf "%s(%d,%d): message %s: [%s] %s" (singleLine finding.path) finding.line (finding.column + 1) finding.ruleId finding.authorityClass (singleLine finding.message)
        ] |> String.concat "\n"
