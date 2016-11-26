module Kkm

open System
open System.IO
open System.Net
open System.Text.RegularExpressions

type System.DateTime with 
    static member TryParseOption str =
        match DateTime.TryParse str with
        | true, r -> Some(r)
        | _ -> None

type Ticket = 
  { price: string; 
    startDate: DateTime; 
    endDate: DateTime
    ticketType: string }

type UserInfo = { id: string; cardNumber: string }

let createUserInfo id cardNumber =
    { id = id; cardNumber = cardNumber }

let private (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None

let private getPrice html =
    match html with
    | Regex "<div>Cena.*(\d\d,\d\d zł).*</div>" [ price ] -> Some price
    | _ -> None

let private getTicketType html = 
    match html with
    | Regex "<div>Rodzaj biletu.*<b>(.*)</b>.*</div>" [ price ] -> Some price
    | _ -> None

let private getDate regex html =
    let date = 
        match html with
        | Regex regex [ startDate ] -> Some startDate
        | _ -> None
    Option.bind DateTime.TryParseOption date

let private getStartDate = getDate "<div>Data początku ważno\u015Bci.*<b>(.*)</b>.*</div>"

let private getEndDate = getDate "<div>Data końca ważno\u015Bci.*<b>(.*)</b>.*</div>"

let private getUrl userInfo (requestDate:DateTime) =
    let dateString = requestDate.ToString ("yyyy-MM-dd")
    sprintf "http://www.kkm.krakow.pl/pl/sprawdz-waznosc-biletow-zapisanych-na-karcie/index,1.html?cityCardType=0&&dateValidity=%s&identityNumber=%s&cityCardNumber=%s&sprawdz_kkm=Sprawd%%C5%%BA" 
        dateString
        userInfo.id
        userInfo.cardNumber

let private getStringResponse (url:string) =
    let req = HttpWebRequest.Create(url) :?> HttpWebRequest 
    req.ProtocolVersion <- HttpVersion.Version10
    req.Method <- "GET"
    let resp = req.GetResponse() 
    let stream = resp.GetResponseStream() 
    let reader = new StreamReader(stream) 
    let html = reader.ReadToEnd()
    html

let private getTicketInfo html =
    let price = defaultArg (getPrice html) "unknown"
    let ticketType = defaultArg (getTicketType html) "unknown"
    let startDate = getStartDate html 
    let endDate = getEndDate html
    match startDate, endDate with
    | Some s, Some e -> 
        Some { price = price; ticketType = ticketType; startDate = s; endDate = e }
    | _ -> None

let downloadTicketInformation userInfo = 
    let url = getUrl userInfo DateTime.Now
    let response = getStringResponse url
    getTicketInfo response
