[<AutoOpen>]
module SftpActors
    open System
    open Akka.FSharp
    open SftpClient

    type SftpCommand =
        | Connect
        | Disconnect
        | ListDirectory of Url

    let sftpActor (clientFactory : IClientFactory) (mailbox: Actor<_>) =

        let rec disconnected () = 
            actor {
                let! message = mailbox.Receive ()
                match message with
                | Connect -> 
                    let connection = clientFactory.CreateSftpClient()
                    connection.Connect()
                    return! connected (connection)

                | _ ->
                    cprintfn ConsoleColor.Yellow "Sftp: invalid operation in disconnected state: %A" message

                return! disconnected ()
            } 
        and connected (connection) = 
            actor {
                let! message = mailbox.Receive ()
                match message with
                | Disconnect -> 
                    connection.Disconnect()
                    connection.Dispose()
                    return! disconnected ()

                | ListDirectory remotePath -> 
                    let result = 
                        try
                            connection.ListDirectory(remotePath.Value, noProgressCallback) 
                            |> List.ofSeq
                        with
                        | ex -> List.empty
                    mailbox.Sender() <! result

                | _ ->
                    cprintfn ConsoleColor.Yellow "Sftp: invalid operation in connected state: %A" message

                return! connected (connection)
            } 

        disconnected ()
