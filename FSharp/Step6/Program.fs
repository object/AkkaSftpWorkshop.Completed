﻿module Application

    open System
    open System.IO
    open Akka
    open Akka.FSharp
    open ClientFactory

    let printInstuctions () =
        printfn "Time to write mode code! In step 6 you will implement replace synchronous implementation file transfer commands with asynchronous,"
        printfn "so it should be possible to cancel pending transfer. Then you implement Cancel command."
        printfn "Once the actor is property implemented the program should display the following messages:"
        printfn ""
        cprintfn ConsoleColor.Cyan "SSH.NET: Connecting..."
        cprintfn ConsoleColor.Green "SSH.NET: Connected."
        cprintfn ConsoleColor.Cyan "SSH.NET: Checking if directory <directory name> exists..."
        cprintfn ConsoleColor.Green "SSH.NET: Directory <directory name> exists."
        cprintfn ConsoleColor.Cyan "SSH.NET: Beginning uploading of file <file name>..."
        cprintfn ConsoleColor.Green "SSH.NET: File <file name> is being uploaded."
        cprintfn ConsoleColor.Cyan "SSH.NET: Requesting cancel..."
        cprintfn ConsoleColor.Green "SSH.NET: Cancel requested."
        cprintfn ConsoleColor.Cyan "SSH.NET: Ending uploading of file <file name>..."
        cprintfn ConsoleColor.Green "SSH.NET: Ended file upload."
        printfn "    pause for about 10 seconds"
        cprintfn ConsoleColor.Cyan "SSH.NET: Disconnecting..."
        cprintfn ConsoleColor.Green "SSH.NET: Disconnected."
        printfn ""

    let run () =
        let clientFactory = createClientFactoryWithTransferDelay(10<s/MB>)
        let system = System.create "system" <| Configuration.load ()
        let sftp = spawn system "sftp" <| sftpActor clientFactory

        let baseDir = AppDomain.CurrentDomain.BaseDirectory
        let localPath = Path.Combine(baseDir, @"Wire.dll")
        let remotePath = "/test/12345.dll"
        sftp <! UploadFile (UncPath localPath, Url remotePath)
        Async.Sleep 2000 |> Async.RunSynchronously
        sftp <! Cancel remotePath
        printfn ""

    [<EntryPoint>]
    let main argv = 

        printInstuctions ()
        waitForInput "Press any key to start the actor system and validate the implementation."

        run ()

        waitForInput ""
        0

