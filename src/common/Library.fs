module Common

open System
open Box.V2
open Box.V2.Config
open Box.V2.Exceptions
open Box.V2.JWTAuth
open Box.V2.Models

type BoxAccountId = string

type AuthType = 
    | Admin
    | Account of BoxAccountId

// Who are we doing this as?
let authType userId = 
    match userId with
    | "admin" -> Admin
    | _ -> Account userId

/// Authenticate to Box and get a Client object that can interact
/// with Box content.
let getAuthenticatedClient config authType =
    use stream = config |> System.IO.File.OpenRead
    let auth = stream |> BoxConfig.CreateFromJsonFile |> BoxJWTAuth
    match authType with
    | Admin -> 
        let adminToken = auth.AdminToken() 
        auth.AdminClient(adminToken)
    | Account(id) -> 
        let userToken = auth.UserToken(id)
        auth.UserClient(userToken, id)

/// Write a sequence of strings to a log file, one per line.
let writeLog logfile results =
    let text = 
        results 
        |> String.concat "\r\n"
        |> sprintf "%s\r\n"
    System.IO.File.AppendAllText(logfile, text)
