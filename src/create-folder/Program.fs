open Box.V2.Config
open Box.V2.JWTAuth
open Box.V2.Models
open Argu

open Common

/// Command line arguments
type CLIArguments =
    | [<Mandatory>] Config of path:string
    | [<Mandatory>] UserId of id:string
    | [<Mandatory>] FolderId of id:string
    | [<Mandatory>] Name of name:string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config _ -> "specify a path to a Box JWT json.config file."
            | UserId _ -> "specify a user account id (use 'admin' for the service account)"
            | FolderId _ -> "specify a parent folder id."
            | Name _ -> "specify a name for the new folder."

[<EntryPoint>]
let main argv =
    
    // Parse command line arguments
    let parser = ArgumentParser.Create<CLIArguments>().Parse(argv)
    let config = parser.GetResult Config
    let userId = parser.GetResult UserId
    let folderId = parser.GetResult FolderId
    let name = parser.GetResult Name

    // Who are we doing this as?
    let authType = 
        match userId with
        | "admin" -> Admin
        | _ -> Account userId

    // Get a client that can interact with Box 
    let client = getAuthenticatedClient config authType

    // Create the requested folder
    let folder = 
        BoxFolderRequest(Name=name, Parent = BoxRequestEntity(Id=folderId))
        |> client.FoldersManager.CreateAsync
        |> Async.AwaitTask 
        |> Async.RunSynchronously

    printfn "Created folder '%s' with id '%s'" folder.Name folder.Id
    
    0