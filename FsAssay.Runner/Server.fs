namespace FsAssay.Runner

open System
open System.IO
open System.Net
open FSharp.Analyzers.SDK
open FsAssay.Analyzers.Domain

module Server =
    let startLiveServer (results: (string * Violation list) list) (totalFiles: int) (port: int) =
        let listener = new HttpListener()
        let url = sprintf "http://localhost:%d/" port
        listener.Prefixes.Add(url)
        try
            listener.Start()
            printfn "\n🌐 Live FsAssay Material 5 Dashboard running at %s" url
            printfn "   Press Ctrl+C to terminate the live dashboard server.\n"
            
            let htmlFile = Path.GetTempFileName() + ".html"
            Output.writeMaterialDashboard results htmlFile
            let htmlBytes = File.ReadAllBytes(htmlFile)
            if File.Exists(htmlFile) then File.Delete(htmlFile)

            let mutable running = true
            while running do
                try
                    let ctx = listener.GetContext()
                    let resp = ctx.Response
                    resp.ContentType <- "text/html; charset=utf-8"
                    resp.ContentLength64 <- int64 htmlBytes.Length
                    resp.OutputStream.Write(htmlBytes, 0, htmlBytes.Length)
                    resp.OutputStream.Close()
                with _ -> running <- false
        with e ->
            printfn "Could not start live server on port %d: %s" port e.Message
