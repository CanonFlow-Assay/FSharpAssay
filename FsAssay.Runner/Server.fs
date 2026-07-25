namespace FsAssay.Runner

open System
open System.IO
open System.Net
open FSharp.Analyzers.SDK
open FsAssay.Analyzers.Domain

module Server =
    let startLiveServer (results: (string * Violation list) list) (totalFiles: int) (port: int) =
        let listener = new HttpListener() // EXPECT: FSA2022
        let url = sprintf "http://localhost:%d/" port
        listener.Prefixes.Add(url) // EXPECT: FSA2022 // EXPECT: FSA-F04
        try
            listener.Start() // EXPECT: FSA2022 // EXPECT: FSA-F04
            printfn "\n🌐 Live FsAssay Material 5 Dashboard running at %s" url // EXPECT: FSA-F04
            printfn "   Press Ctrl+C to terminate the live dashboard server.\n" // EXPECT: FSA-F04
            
            let htmlFile = Path.GetTempFileName() + ".html" // EXPECT: FSA2022
            Output.writeMaterialDashboard results htmlFile // EXPECT: FSA-F04
            let htmlBytes = File.ReadAllBytes(htmlFile) // EXPECT: FSA2022
            if File.Exists(htmlFile) then File.Delete(htmlFile) // EXPECT: FSA2022 // EXPECT: FSA-F04 // EXPECT: FSA-C15

            let mutable running = true
            while running do
                try
                    let ctx = listener.GetContext() // EXPECT: FSA2022
                    let resp = ctx.Response // EXPECT: FSA2022
                    resp.ContentType <- "text/html; charset=utf-8" // EXPECT: FSA2022 // EXPECT: FSA-F04
                    resp.ContentLength64 <- int64 htmlBytes.Length // EXPECT: FSA2022 // EXPECT: FSA-F04
                    resp.OutputStream.Write(htmlBytes, 0, htmlBytes.Length) // EXPECT: FSA2022 // EXPECT: FSA-F04
                    resp.OutputStream.Close() // EXPECT: FSA2022
                with _ -> running <- false // EXPECT: FSA-C10
        with e ->
            printfn "Could not start live server on port %d: %s" port e.Message
