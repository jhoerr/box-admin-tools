// Learn more about F# at http://fsharp.org

open System
open Box.V2
open Box.V2.Config
open Box.V2.Exceptions
open Box.V2.JWTAuth
open Box.V2.Models
open Argu
open Newtonsoft.Json

open Common

type CLIArguments =
    | [<Mandatory>] Config of path:string
    | [<Mandatory>] LogFile of path:string
    | DryRun

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Config _ -> "specify a path to a Box JWT json.config file."
            | LogFile _ -> "specify a path to a logfile."
            | DryRun -> "only print what would happen (do not actually change anything)."

let _1KB = 2.0 ** 10.0
let _1MB = 2.0 ** 20.0
let _1GB = 2.0 ** 30.0
let _1TB = 2.0 ** 40.0
let _4TB = 4.0 * _1TB
let _5TB = 5.0 * _1TB
let _10TB = 10.0 * _1TB

type UpdateResult =
| Success
| Error of string

let reportRow = sprintf "%s,%f,%s,%f,%s,%f,%s,%s"

let updateQuota (client:BoxClient) id quota = async { 
    try
        let! _ = 
            BoxUserRequest(Id=id, SpaceAmount=Nullable(quota))
            |> client.UsersManager.UpdateUserInformationAsync
            |> Async.AwaitTask
        return Success                
    with 
    | :? BoxException as ex -> 
        return sprintf "Error when modifying quota: (%d) %s. %s " (int ex.StatusCode) ex.Error.Description ex.Error.Message |> Error
}

let isNonPerson (bu:BoxUser) = 
    bu.TrackingCodes  
    |> Seq.exists (fun tc -> tc.Name="PrimaryStatus") 
    |> not

let accountIsOverSized (bu:BoxUser) = (bu.SpaceUsed.Value |> float) > _10TB

let fmt size =
    let pprint = sprintf "%.1f %s"
    if size > _1TB then pprint (size/_1TB) "TB"
    else if size > _1GB then pprint (size/_1GB) "GB"
    else if size > _1MB then pprint (size/_1MB) "MB"
    else pprint (size/_1KB) "KB"

let adjustQuota dryRun (client:BoxClient) (bu:BoxUser) = async { 
    let spaceUsed = (bu.SpaceUsed.Value |> float)
    let currentQuota = bu.SpaceAmount.Value |> float
    
    let reportRow newQuota msg = reportRow bu.Login spaceUsed (fmt spaceUsed) currentQuota (fmt currentQuota) newQuota (fmt newQuota) msg
    let reportNonPersonAccount () = 
        let trackingCodesRaw = 
            bu.TrackingCodes
            |> Seq.map (fun tc -> sprintf "%s: %s" tc.Name tc.Value)
            |> String.concat "; "
        let trackingCodes = if String.IsNullOrWhiteSpace(trackingCodesRaw) then "(none)" else sprintf "[ %s ]" trackingCodesRaw
        reportRow currentQuota (sprintf "Quota not modified: this appears to be a non-person account. Tracking Codes: %s" trackingCodes)
    let reportOversizedAccount () = reportRow currentQuota "Quota not modified: the space used by this account currently exceeds 10TB." 
    let reportCorrectQuota newQuota = reportRow newQuota "Quota not modified: the current quota is correct."
    let reportUpdateQuota newQuota = reportRow newQuota "Quota modified."
    let reportError err = reportRow currentQuota err

    if isNonPerson bu
    then return reportNonPersonAccount()
    else if accountIsOverSized bu
    then return reportOversizedAccount()
    else 
        let newQuota = if spaceUsed < _4TB then _5TB else _10TB
        if newQuota = currentQuota
        then return reportCorrectQuota newQuota
        else if dryRun
        then return reportUpdateQuota newQuota
        else  
            let! updateResult = updateQuota client bu.Id newQuota
            match updateResult with
            | Success -> return reportUpdateQuota newQuota
            | Error(err) -> return reportError err
}

let writeLogFileHeader logFile = 
    let header = "login,space used (bytes),space used (formatted),old quota (bytes),old quota (formatted),new quota (bytes), new quota (formatted),note\r\n"
    System.IO.File.WriteAllText(logFile, header)

let log logfile results =
    let text = 
        results 
        |> String.concat "\r\n"
        |> sprintf "%s\r\n"
    System.IO.File.AppendAllText(logfile, text)

let await fn = fn |> Async.AwaitTask |> Async.RunSynchronously

let fields = ["id"; "login"; "space_amount"; "space_used"; "tracking_codes"]
let limit = 1000u

let run config logfile dryrun = async {
    try
        logfile |> writeLogFileHeader 
        let client = getAuthenticatedClient config Admin
        let mutable page = 0u
        let mutable hasMore = true
        while hasMore do
            // let users = client.UsersManager.GetEnterpriseUsersAsync(filterTerm="jhoerr@iu.edu", fields=fields) |> await
            let users = client.UsersManager.GetEnterpriseUsersAsync(limit=limit, offset=page*limit, fields=fields) |> await
            let! results = 
                users.Entries
                |> Seq.map (adjustQuota dryrun client)
                |> Async.Parallel
            log logfile results
            page <- page + 1u
            hasMore <- int (page*limit) < users.TotalCount && users.Entries.Count > 0
            // hasMore <- int (page*limit) < 1000 && users.Entries.Count > 0
            let processedCount = if hasMore then int (page*limit) else users.TotalCount
            printfn "[%s] Processed %d/%d users..." (DateTime.Now.ToString("HH:mm:ss")) processedCount users.TotalCount
        printfn "Done!"    
        return 0
    with
    | exn -> 
        printfn "Quota update failed with exception:\n%A" exn
        return 1
}

[<EntryPoint>]
let main argv =
    
    let parser = ArgumentParser.Create<CLIArguments>().Parse(argv)
    let config = parser.GetResult Config
    let logfile = parser.GetResult LogFile
    let dryRun = parser.Contains DryRun
    
    printfn "****************************************************************"
    printfn "Config file: %s" config
    printfn "Log file: %s" logfile
    printfn "Dry run: %b" dryRun
    printfn "****************************************************************"

    run config logfile dryRun
    |> Async.RunSynchronously
