namespace FsAssay.Specimens.SectionG

open System
open System.Threading.Tasks

module ConcurrencyAndSharedState =
    // FSA2033 — Redundant Async Wrapper
    let pureAsyncCall () =
        async {
            // EXPECT: REMOVED
            let mutable x = 42
            return x
        }

    // FSA2042 — Sync-over-Async
    let runSyncTask (t: Task<int>) =
        t.Result
