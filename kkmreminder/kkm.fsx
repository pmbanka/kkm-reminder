#load "../paket-files/include-scripts/net45/include.main.group.fsx"
#load "common.fsx"

open FSharp.Data
open System
open System.IO
open System.Net
open System.Text.RegularExpressions
open Chessie.ErrorHandling

let private getPrice html =
    match html with
    | Regex "<div>Cena.*(\d\d,\d\d zł).*</div>" [ price ] -> Trial.pass price
    | _ -> warn "Price unavailable" "Unavailable"

let private getTicketType html = 
    match html with
    | Regex "<div>Rodzaj biletu.*<b>(.*)</b>.*</div>" [ ticketType ] -> Trial.pass ticketType
    | _ -> warn "Ticket type unavailable" "Unavailable"

let private getDate regex html =
    let date = 
        match html with
        | Regex regex [ startDate ] -> Some startDate
        | _ -> None
    Option.bind DateTime.TryParseOption date
    |> Trial.failIfNone "Could not parse date"

let private getStartDate = getDate "<div>Data początku ważno\u015Bci.*<b>(.*)</b>.*</div>"

let private getEndDate = getDate "<div>Data końca ważno\u015Bci.*<b>(.*)</b>.*</div>"

let private getUrl userInfo (requestDate:DateTime) =
    let dateString = requestDate.ToString ("yyyy-MM-dd")
    sprintf "http://www.kkm.krakow.pl/pl/sprawdz-waznosc-biletow-zapisanych-na-karcie/index,1.html?cityCardType=0&&dateValidity=%s&identityNumber=%s&cityCardNumber=%s&sprawdz_kkm=Sprawd%%C5%%BA" 
        dateString
        userInfo.id
        userInfo.cardNumber

let private getTicketInfo html = trial {
    let! price = getPrice html
    let! ticketType = getTicketType html
    let! startDate = getStartDate html 
    let! endDate = getEndDate html
    return { 
        price = price
        ticketType = ticketType
        startDate = startDate
        endDate = endDate } }

let downloadTicketInformation userInfo = asyncTrial {
    let url = getUrl userInfo DateTime.CESNow
    let! response = 
        Http.AsyncRequestString url
        |> Async.Catch
        |> Async.map (function | Choice1Of2 s -> Trial.pass s | Choice2Of2 ex -> Trial.fail ex.Message)
    let! ticket =
        response
        |> Trial.bind getTicketInfo
    return ticket }