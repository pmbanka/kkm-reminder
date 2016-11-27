#r "System"
#r "System.IO"
#r "System.Net"
#r "System.Net.Mail"
#r "System.Text.RegularExpressions"
#load "../paket-files/include-scripts/net46/include.main.group.fsx"
#load "kkm.fsx"

open System
open System.IO

module Email =
    open System
    open System.IO
    open System.Net
    open System.Net.Mail
    let sendEmail (emailUsername:string) (emailPassword:string) fromAddress toAddress subject body =
        use msg = new MailMessage(fromAddress, toAddress, subject, body)     
        use client = new SmtpClient("smtp.gmail.com", 587)
        client.Credentials <- new NetworkCredential(emailUsername, emailPassword)
        client.EnableSsl <- true
        client.Send msg

module Config =
    open System
    open System.IO
    open FSharp.Data
    open FSharp.Data.JsonExtensions
    let getValue fallbackFilePath key =
        let fromEnv = Environment.GetEnvironmentVariable key
        if isNull fromEnv then
            let text = File.ReadAllText fallbackFilePath
            let json = JsonValue.Parse text
            match json.TryGetProperty key with
            | Some x -> x.AsString ()
            | None -> failwithf "%s property not found in %s" key fallbackFilePath
        else
            fromEnv

type RunResult = | TicketNotFound | NoNeedToRemind | ReminderSent 

let RunImpl () =
    let getConfig = Config.getValue ".secrets"
    let sendEmail = 
        Email.sendEmail (getConfig "EMAIL_USERNAME") (getConfig "EMAIL_PASSWORD") 
                        (getConfig "SOURCE_EMAIL") (getConfig "TARGET_EMAIL")
    let format (d:DateTime) = d.ToString ("dd.MM.yyyy (dddd)")
    let user = Kkm.createUserInfo (getConfig "USER_ID") (getConfig "CARD_NUMBER")
    let ticket = Kkm.downloadTicketInformation user
    let result = 
        match ticket with
        | Some t -> 
            let diff = (t.endDate.Date - DateTime.Today.Date).TotalDays |> int
            let isShoppingTime = diff <= 3 || (getConfig "FORCE_MAIL") = "TRUE"
            if isShoppingTime then
                let subject = sprintf "Your KKM ticket expires in %d days" diff
                let body = 
                    sprintf "Current ticket is valid since %s until %s\n\nToday is %s" 
                        (format t.startDate) (format t.endDate) (format DateTime.Today)
                sendEmail subject body
                RunResult.ReminderSent
            else
                RunResult.NoNeedToRemind
        | None -> RunResult.TicketNotFound
    printfn "Run result: %A" result

let Run (timer: TimerInfo) =
    RunImpl ()