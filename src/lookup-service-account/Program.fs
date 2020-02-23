open Argu
open Common

/// Command line arguments
type CLIArguments =
    | [<Mandatory>] Config of path:string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config _ -> "specify a path to a Box JWT json.config file."

let box fn = async {
    try 
        return! fn |> Async.AwaitTask
    with 
    | exn -> 
        printfn "Box operation failed: %s" exn.Message
        raise exn
}

[<EntryPoint>]
let main argv =
    
    // Parse command line arguments
    let parser = ArgumentParser.Create<CLIArguments>().Parse(argv)
    let config = parser.GetResult Config

    printfn "Looking up service account..."

    // Get a client that can interact with Box 
    let client = getAuthenticatedClient config Admin

    // Create the shared links and capture the results
    let serviceAccount = 
        client.UsersManager.GetCurrentUserInformationAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously

    printfn "Id: %s" serviceAccount.Id
    printfn "Name: %s" serviceAccount.Name
    printfn "Login: %s" serviceAccount.Login

    let updated = 
        Box.V2.Models.BoxUserRequest(Id=serviceAccount.Id, Name="UDMC 3PA Process")
        |> client.UsersManager.UpdateUserInformationAsync
        |> Async.AwaitTask
        |> Async.RunSynchronously

    printfn "Id: %s" updated.Id
    printfn "Name: %s" updated.Name
    printfn "Login: %s" updated.Login
    
    0