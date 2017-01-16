#r "System"
#r "System.IO"
#r "System.Net"
#r "System.Text.RegularExpressions"
#load "../paket-files/include-scripts/net45/include.main.group.fsx"
#load "common.fsx"
#load "kkm.fsx"
open System
open System.Diagnostics

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
        with
        | ex -> return! AsyncTrial.fail ("Could not send email: " + ex.Message) }

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
                forceMail = json.ForceMail
                warningDays = json.WarningDays } 
            Trial.pass cfg
        with ex -> Trial.fail <| "Config could not be read from json: " + ex.Message
            
    let fromEnv () = trial {
        let getEnvWithTransformation t name = 
            Environment.GetEnvironmentVariable name 
            |> Option.ofObj
            |> t
            |> Trial.failIfNone (name + " config env variable not found")
        let getEnv = getEnvWithTransformation id 
        let! h = getEnv "SMTP_HOST"
        let! u = getEnv "SMTP_USERNAME"
        let! p = getEnv "SMTP_PASSWORD"
        let! s = getEnv "SOURCE_EMAIL"
        let! t = getEnv "TARGET_EMAIL"
        let! id = getEnv "USER_ID"
        let! num = getEnv "CARD_NUMBER"
        let! fm = getEnvWithTransformation (Option.map ((=) "TRUE")) "FORCE_MAIL"
        let! wd = getEnvWithTransformation (Option.bind Int32.TryParseOption) "WARNING_DAYS"
        return {
            email = { 
                    smtpHost = h
                    smtpUsername = u
                    smtpPassword = p
                    sourceEmail = s
                    targetEmail = t }
            user = { id = id; cardNumber = num }
            forceMail = fm
            warningDays = wd } }

    let getConfig fallbackFilePath =
        fromEnv ()
        |> Trial.orElse (fromFile fallbackFilePath)

module Reminder =
    open System
    open Chessie.ErrorHandling

    let private sendReminder ticket config = asyncTrial {
        let diff = (ticket.endDate.Date - DateTime.CESNow.Date).TotalDays |> int
        let isShoppingTime = diff <= config.warningDays || config.forceMail
        if isShoppingTime then
            let format (d:DateTime) = d.ToString ("dd.MM.yyyy (dddd)")
            let subject = sprintf "Your KKM ticket expires in %d days" diff
            let body = 
                sprintf "Current ticket is valid since %s until %s\n\nToday is %s" 
                    (format ticket.startDate) (format ticket.endDate) (format DateTime.Today)
            return! Email.sendEmail config.email subject body
        else
            return! AsyncTrial.warn "Sending email skipped" () }

    let private runImplAsync = asyncTrial {
        let! cfg = Config.getConfig (__SOURCE_DIRECTORY__ + "/secrets.json")
        let! ticket = Kkm.downloadTicketInformation cfg.user
        return! sendReminder ticket cfg }

    let run printFn = async {
        let! result = runImplAsync |> Async.ofAsyncResult
        match result with
        | Pass _         -> printFn "Ok"
        | Warn (_, log)  -> printFn "Warning:"
                            for msg in log do printFn ("   " + msg)
        | Fail errors    -> printFn "Error:"
                            for msg in errors do printFn ("   " + msg) }

// Reminder.run Console.WriteLine |> Async.RunSynchronously

let Run (timer: TimerInfo, log: TraceWriter) =
    Reminder.run log.Info |> Async.StartAsTask