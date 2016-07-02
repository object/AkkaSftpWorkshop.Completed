module ClientFactory

    open SftpClient
    open SshNetClient
    open LocalFileClient

    let createClientFactory () =
        LocalFileClientFactory("sftp", "", 0<s/MB>)

    let createClientFactoryWithTransferDelay (transferDelay) =
        LocalFileClientFactory("sftp", "", transferDelay)
