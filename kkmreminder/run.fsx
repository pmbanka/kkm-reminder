#r "System"
#r "System.IO"
#r "System.Net"
#r "System.Text.RegularExpressions"
#r "System.Xml.Linq" 
#r "../packages/FSharpx.Collections/lib/net40/FSharpx.Collections.dll"
#r "../packages/FSharpx.Extras/lib/net45/FSharpx.Extras.dll"
#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#load "common.fsx"
#load "kkm.fsx"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open System.Diagnostics
open FSharpx
open FSharpx.Option

module Email =
    open System
    open System.IO
    open System.Net
    open System.Net.Mail

    let sendEmail config subject body = async {
        use msg = new MailMessage (config.sourceEmail, config.targetEmail, subject, body) 
        use client = new SmtpClient (config.smtpHost, 587)
        client.UseDefaultCredentials <- false
        client.Credentials <- new NetworkCredential (config.smtpUsername, config.smtpPassword)
        client.EnableSsl <- true
        do! client.SendMailAsync msg |> Async.AwaitTask }

module Config =
    open System
    open System.IO
    open FSharp.Data
    open FSharpx
    open FSharpx.Option

    type JsonCfg = JsonProvider<"secrets.example.json">

    let fromFile (path:string) = 
        try
            let json = JsonCfg.Load path
            let cfg = 
              { email = 
                  { smtpHost = json.SmtpHost
                    smtpUsername = json.SmtpUsername
                    smtpPassword = json.SmtpPassword
                    sourceEmail = json.SourceEmail
                    targetEmail = json.TargetEmail }
                user = { id = json.UserId; cardNumber = json.CardNumber }
                forceMail = json.ForceMail } 
            Some cfg
        with ex -> None
            
    let fromEnv () = maybe {
        let getEnv = Environment.GetEnvironmentVariable >> Option.ofObj
        let! h = getEnv "SMTP_HOST"
        let! u = getEnv "SMTP_USERNAME"
        let! p = getEnv "SMTP_PASSWORD"
        let! s = getEnv "SOURCE_EMAIL"
        let! t = getEnv "TARGET_EMAIL"
        let! id = getEnv "USER_ID"
        let! num = getEnv "CARD_NUMBER"
        let! fm = getEnv "FORCE_MAIL"
        return 
          { email = 
              { smtpHost = h
                smtpUsername = u
                smtpPassword = p
                sourceEmail = s
                targetEmail = t }
            user = { id = id; cardNumber = num }
            forceMail = fm = "TRUE" } }

    let getConfig fallbackFilePath =
        fromEnv ()
        |> Option.orElse (fromFile fallbackFilePath)


let sendReminder ticket config = async {
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
        return RunResult.ReminderSent
    else
        return RunResult.NoNeedToRemind }

let runImpl log = async {
    try
        let cfg = 
            Config.getConfig "../secrets.json" 
            |> Option.getOrFail "Could not get config"

        let! ticket = Kkm.downloadTicketInformation cfg.user
        let! result = async {
            match ticket with
            | Some t -> return! sendReminder t cfg
            | None -> return RunResult.TicketNotFound }
        log <| sprintf "%A" result
    with ex -> log ex.Message }

let Run (timer: TimerInfo, log: TraceWriter) =
    runImpl log.Info |> Async.StartAsTask