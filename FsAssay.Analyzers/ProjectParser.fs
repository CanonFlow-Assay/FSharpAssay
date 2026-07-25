module FsAssay.Analyzers.ProjectParser

open System
open System.IO
open System.Xml.Linq
open FsAssay.Analyzers.Domain

let vulnerablePackages =
    Set.ofList [
        "Vulnerable.Package"
        "InsecureLib"
        "BannedComponent"
    ]

let parseProjectFile (filePath: string) : Violation list =
    if not (File.Exists(filePath)) then [] // EXPECT: FSA2022
    else
        try
            let doc = XDocument.Load(filePath)
            
            let packageRefs = doc.Descendants(XName.Get("PackageReference"))
            
            let mutable findings = []
            
            // To provide ranges for XML, it's tricky without a line-preserving parser.
            // We'll just return range.Zero for simplicity or attempt to parse line info.
            let dummyText = FSharp.Compiler.Text.SourceText.ofString ""
            
            for pref in packageRefs do // EXPECT: FSA-P02 // EXPECT: FSA-F04
                let includeAttr = pref.Attribute(XName.Get("Include"))
                if includeAttr <> null then // EXPECT: FSA-C01
                    let name = includeAttr.Value
                    if vulnerablePackages.Contains(name) then
                        match AstUtils.mkLocated FSASEC09 FSharp.Compiler.Text.Range.range0 with
                        | Some loc ->
                            match toViolation dummyText loc with
                            | Some v -> findings <- v :: findings // EXPECT: FSA-C10
                            | None -> ()
                        | None -> ()
            findings
        with _ ->
            []
