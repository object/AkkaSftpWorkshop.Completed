[<AutoOpen>]
module SftpActors
    open System
    open Akka
    open Akka.FSharp
    open SftpClient
    open Utils

    type SftpCommand =
        | ListDirectory of Url
        | UploadFile of UncPath * Url
        | DownloadFile of UncPath * Url

    [<Literal>]
    let private ConnectionTimeoutInSeconds = 10.

    let sftpActor (clientFactory : IClientFactory) (mailbox: Actor<_>) =

        let setReceiveTimeout () =
            mailbox.Context.SetReceiveTimeout(Nullable<TimeSpan>(TimeSpan.FromSeconds(ConnectionTimeoutInSeconds)))
            DateTimeOffset.Now

        let resetReceiveTimeout () =
            mailbox.Context.SetReceiveTimeout(Nullable())

        let fileStreamProvider = clientFactory.CreateFileStreamProvider()

        let rec disconnected () = 
            actor {
                let! (message : obj) = mailbox.Receive ()
                match message with
                | :? SftpCommand as command -> 
                    mailbox.Stash ()
                    let connection = clientFactory.CreateSftpClient()
                    connection.Connect()
                    mailbox.UnstashAll ()
                    return! connected (connection, DateTimeOffset.Now)

                | _ ->
                    cprintfn ConsoleColor.Yellow "Sftp: invalid operation in disconnected state: %A" message

                return! disconnected ()
            } 
        and connected (connection : ISftpClient, idleFromTime : DateTimeOffset) = 
            actor {
                let! (message : obj) = mailbox.Receive ()
                resetReceiveTimeout ()
                match message with
                | :? SftpCommand as command -> 
                    match command with
                    | ListDirectory remotePath -> 
                        let result = 
                            try
                                connection.ListDirectory(remotePath.Value, noProgressCallback) 
                                |> List.ofSeq
                            with
                            | ex -> List.empty
                        mailbox.Sender() <! result

                    | UploadFile (localPath, remotePath) -> 
                        ensureParentDirectoryExists connection (remotePath.Value)
                        let stream = fileStreamProvider.OpenRead(localPath.Value)
                        connection.UploadFile(stream, remotePath.Value, noProgressCallback)

                    | DownloadFile (localPath, remotePath) -> 
                        let stream = fileStreamProvider.OpenWrite(localPath.Value)
                        connection.DownloadFile(remotePath.Value, stream, noProgressCallback)

                    return! connected (connection, setReceiveTimeout ())

                | :? Actor.ReceiveTimeout ->
                    if (DateTimeOffset.Now - idleFromTime > TimeSpan.FromSeconds(ConnectionTimeoutInSeconds)) then
                        resetReceiveTimeout ()
                        connection.Disconnect()
                        connection.Dispose()
                        return! disconnected ()

                | _ ->
                    cprintfn ConsoleColor.Yellow "Sftp: invalid operation in connected state: %A" message

                return! connected (connection, DateTimeOffset.Now)
            } 

        disconnected ()
