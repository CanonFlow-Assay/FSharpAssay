namespace Refresh.Shell

open System
open System.Net
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.Data.Sqlite
open Refresh.Domain

[<CLIMutable>]
type Response = { reference: string; status: string; reason: string }

module Wire =
    let encode decision =
        let response =
            match decision with
            | Decision.Approved(reference,_) -> {reference=reference;status="approved";reason=""}
            | Decision.Rejected reason -> {reference="";status="rejected";reason=reason}
        JsonSerializer.Serialize response

/// SQLite unique keys and a transaction enforce replay consistency at the persistence boundary.
type Store(databasePath: string) =
    let connect () =
        let connection = new SqliteConnection(SqliteConnectionStringBuilder(DataSource=databasePath,Pooling=false).ToString())
        connection.Open()
        connection
    do
        use connection = connect()
        use command = connection.CreateCommand()
        command.CommandText <- "CREATE TABLE IF NOT EXISTS approvals(reference TEXT PRIMARY KEY, amount TEXT NOT NULL, response TEXT NOT NULL);"
        command.ExecuteNonQuery() |> ignore
    member _.Record(reference: string, amount: decimal, decision: Decision, cancellation: CancellationToken) =
        cancellation.ThrowIfCancellationRequested()
        use connection = connect()
        use transaction = connection.BeginTransaction()
        use query = connection.CreateCommand()
        query.Transaction <- transaction
        query.CommandText <- "SELECT amount,response FROM approvals WHERE reference=$reference"
        query.Parameters.AddWithValue("$reference",reference) |> ignore
        use reader = query.ExecuteReader()
        let existing = if reader.Read() then Some(reader.GetString(0),reader.GetString(1)) else None
        reader.Close()
        let amountText = amount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        match existing with
        | Some(previous,response) when previous = amountText -> response
        | Some _ -> invalidOp "idempotency-conflict"
        | None ->
            cancellation.ThrowIfCancellationRequested()
            let response = Wire.encode decision
            use insert = connection.CreateCommand()
            insert.Transaction <- transaction
            insert.CommandText <- "INSERT INTO approvals(reference,amount,response) VALUES($reference,$amount,$response)"
            insert.Parameters.AddWithValue("$reference",reference) |> ignore
            insert.Parameters.AddWithValue("$amount",amountText) |> ignore
            insert.Parameters.AddWithValue("$response",response) |> ignore
            insert.ExecuteNonQuery() |> ignore
            cancellation.ThrowIfCancellationRequested()
            transaction.Commit()
            response
    member _.EngineVersion =
        use connection = connect()
        use query = connection.CreateCommand()
        query.CommandText <- "SELECT sqlite_version()"
        Version.Parse(string (query.ExecuteScalar()))
    member _.Count =
        use connection = connect()
        use query = connection.CreateCommand()
        query.CommandText <- "SELECT COUNT(*) FROM approvals"
        Convert.ToInt32(query.ExecuteScalar())

module Http =
    /// One bounded loopback request for the qualification fixture; no production hosting claim.
    let serveOnce (listener: HttpListener) (response: string) (cancellation: CancellationToken) = task {
        let! context = listener.GetContextAsync().WaitAsync(cancellation)
        let bytes = Encoding.UTF8.GetBytes response
        context.Response.ContentType <- "application/json"
        context.Response.ContentLength64 <- int64 bytes.Length
        do! context.Response.OutputStream.WriteAsync(bytes.AsMemory(),cancellation)
        context.Response.Close()
    }
