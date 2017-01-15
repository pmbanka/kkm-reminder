#r "System"
#r "System.IO"
#r "System.Net"
#r "System.Text.RegularExpressions"
#load "../paket-files/include-scripts/net45/include.main.group.fsx"
#load "common.fsx"
#load "kkm.fsx"

open System
open System.Diagnostics
open Chessie.ErrorHandling

module Email =
    open System
    open System.IO
    open System.Net
    open System.Net.Mail
    open Chessie.ErrorHandling

    let sendEmail config subject body = asyncTrial {
        use msg = new MailMessage (config.sourceEmail, config.targetEmail, subject, body) 
        use client = new SmtpClient (config.smtpHost, 587)
        client.UseDefaultCredentials <- false
        client.Credentials <- new NetworkCredential (config.smtpUsername, config.smtpPassword)
        client.EnableSsl <- true
        try
            do! client.SendMailAsync msg |> Async.AwaitTask
            return ()
        with
        | ex -> return! fail ("Could not send email: " + ex.Message) |> resultToAsync }

module Config =
    open System
    open System.IO
    open FSharp.Data
    open Chessie.ErrorHandling

    [<Literal>]
    let CfgLocation = __SOURCE_DIRECTORY__ + "/secrets.example.json"
    type JsonCfg = JsonProvider<CfgLocation>

    let fromFile (path:string) = 
        try
            let json = JsonCfg.Load path
            let cfg = { 
                email = { 
                        smtpHost = json.SmtpHost
                        smtpUsername = json.SmtpUsername
                        smtpPassword = json.SmtpPassword
                        sourceEmail = json.SourceEmail
                        targetEmail = json.TargetEmail }
                user = { id = json.UserId; cardNumber = json.CardNumber }
                forceMail = json.ForceMail } 
            pass cfg
        with ex -> fail <| "Config could not be read: " + ex.Message
            
    let fromEnv () = trial {
        let getEnv name = 
            Environment.GetEnvironmentVariable name 
            |> Option.ofObj 
            |> failIfNone (name + " config env variable not found")
        let! h = getEnv "SMTP_HOST"
        let! u = getEnv "SMTP_USERNAME"
        let! p = getEnv "SMTP_PASSWORD"
        let! s = getEnv "SOURCE_EMAIL"
        let! t = getEnv "TARGET_EMAIL"
        let! id = getEnv "USER_ID"
        let! num = getEnv "CARD_NUMBER"
        let! fm = getEnv "FORCE_MAIL"
        return {
            email = { 
                    smtpHost = h
                    smtpUsername = u
                    smtpPassword = p
                    sourceEmail = s
                    targetEmail = t }
            user = { id = id; cardNumber = num }
            forceMail = fm = "TRUE" } }

    let inline private orElse fallback value =
        match value with
        | Ok _ -> value
        | Bad _ -> fallback

    let getConfig fallbackFilePath =
        fromEnv ()
        |> orElse (fromFile fallbackFilePath)

let sendReminder ticket config = asyncTrial {
    let format (d:DateTime) = d.ToString ("dd.MM.yyyy (dddd)")
    let currentTime = 
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId (DateTime.UtcNow, "Central European Standard Time")
    let diff = (ticket.endDate.Date - currentTime.Date).TotalDays |> int
    let isShoppingTime = diff <= 3 || config.forceMail
    if isShoppingTime then
        let subject = sprintf "Your KKM ticket expires in %d days" diff
        let body = 
            sprintf "Current ticket is valid since %s until %s\n\nToday is %s" 
                (format ticket.startDate) (format ticket.endDate) (format DateTime.Today)
        do! Email.sendEmail config.email subject body
    else
        do! warn "Sending email skipped" () |> resultToAsync }

let runImplAsync = asyncTrial {
    let! cfg = Config.getConfig (__SOURCE_DIRECTORY__ + "/secrets.json")
    let! ticket = Kkm.downloadTicketInformation cfg.user
    do! sendReminder ticket cfg }

let runImpl printFn = async {
    let! result = runImplAsync |> Async.ofAsyncResult
    match result with
    | Pass  _        -> printFn "Ok"
    | Warn (_, log)  -> printFn "Warning:"
                        for msg in log do printFn ("   " + msg)
    | Fail  errors   -> printFn "Error:"
                        for msg in errors do printFn ("   " + msg) }

let Run (timer: TimerInfo, log: TraceWriter) =
    runImpl log.Info |> Async.StartAsTask