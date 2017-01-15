[<AutoOpen>]
module Common
open System
open System.Net.Mail
open System.Text.RegularExpressions

type DateTime with
    static member TryParseOption str =
        match DateTime.TryParse str with
        | true, d -> Some d
        | _ -> None

type Int32 with
    static member TryParseOption str = 
        match Int32.TryParse str with
        | true, i -> Some i
        | _ -> None

type UserInfo = { 
    id: string
    cardNumber: string }

type EmailInfo = { 
    smtpHost: string
    smtpUsername: string
    smtpPassword: string
    sourceEmail: string
    targetEmail: string }

type Config = { 
    email: EmailInfo
    user: UserInfo
    forceMail: bool }

type Ticket = { 
    price: string 
    startDate: DateTime
    endDate: DateTime
    ticketType: string }

type RunResult = TicketNotFound | NoNeedToRemind | ReminderSent

let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None