open Argu
open Common

/// Command line arguments
type CLIArguments =
    | [<Mandatory>] Config of path:string
    | [<Mandatory>] Login of login:string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config _ -> "specify a path to a Box JWT json.config file."
            | Login _ -> "specify a user login"

[<EntryPoint>]
let main argv =
    
    // Parse command line arguments
    let parser = ArgumentParser.Create<CLIArguments>().Parse(argv)
    let config = parser.GetResult Config
    let login = parser.GetResult Login

    printfn "Looking up '%s'..." login

    // Get a client that can interact with Box 
    let client = getAuthenticatedClient config Admin

    let logins = 
        client.UsersManager.GetEnterpriseUsersAsync(filterTerm=login)
        |> Async.AwaitTask
        |> Async.RunSynchronously

    printfn "Found %d logins matching '%s'.\n" logins.Entries.Count login

    logins.Entries
    |> Seq.map (fun l -> sprintf "%s (%s)" l.Login l.Id)
    |> Seq.iter (printfn "%s")
    
    0