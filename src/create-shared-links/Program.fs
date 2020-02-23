open Box.V2
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
    | [<Mandatory>] LogFile of path:string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config _ -> "specify a path to a Box JWT json.config file."
            | UserId _ -> "specify a user account id (use 'admin' for the service account)"
            | FolderId _ -> "specify a parent folder id."
            | LogFile _ -> "specify a path to a log file"


let createSharedLink (client:BoxClient) (file:BoxItem) = async {
    let request = BoxSharedLinkRequest(Access=System.Nullable<BoxSharedLinkAccessType>(BoxSharedLinkAccessType.``open``))
    let doit request = async {
        let! result = client.FilesManager.CreateSharedLinkAsync(file.Id, request) |> Async.AwaitTask
        return sprintf """"%s", %s""" file.Name result.SharedLink.Url
    }
    try return! doit request
    with exn -> 
        printfn "Retrying due to do error: %s" exn.Message
        return! doit request
}

let getFolderItems (client:BoxClient) id limit page = async {
    return!
        client.FoldersManager.GetFolderItemsAsync(id, limit, page*limit) 
        |> Async.AwaitTask
}

let doForAllFiles fn (items:BoxCollection<BoxItem>)  = async {
    return!
        items.Entries
        |> Seq.filter (fun i -> i.Type = "file")
        |> Seq.map fn
        |> Async.Parallel
}

let createSharedLinks (client:BoxClient) folderId = async {
    let getNextFolderItems = getFolderItems client folderId
    let createSharedLink = createSharedLink client
    let limit = 100
    let mutable page = 0
    let mutable processed = 0
    let mutable hasMore = true
    let mutable logLines = ["file name,shared link"]
    while hasMore do
        let! items = getNextFolderItems limit page
        let! results = items |> doForAllFiles createSharedLink
        logLines <- results |> Array.toList |> List.append logLines
        page <- page + 1
        processed <- processed + items.Entries.Count
        printfn "Processed page %d (%d/%d items)" page processed items.TotalCount
        hasMore <- processed < items.TotalCount && items.Entries.Count > 0
    return logLines    
}

[<EntryPoint>]
let main argv =
    
    // Parse command line arguments
    let parser = ArgumentParser.Create<CLIArguments>().Parse(argv)
    let config = parser.GetResult Config
    let userId = parser.GetResult UserId
    let folderId = parser.GetResult FolderId
    let logFile = parser.GetResult LogFile

    // Get a client that can interact with Box 
    let client = 
        userId
        |> authType
        |> getAuthenticatedClient config

    // Create the shared links and capture the results
    let logLines =
        createSharedLinks client folderId
        |> Async.RunSynchronously

    // Log the results
    logLines |> writeLog logFile

    printfn "Done!"
    
    0