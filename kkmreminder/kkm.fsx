#r "../packages/FSharp.Data/lib/net40/FSharp.Data.dll"
#r "../packages/FSharpx.Collections/lib/net40/FSharpx.Collections.dll"
#r "../packages/FSharpx.Extras/lib/net45/FSharpx.Extras.dll"
#load "common.fsx"

open FSharp.Data
open System
open System.IO
open System.Net
open System.Text.RegularExpressions
open FSharpx
open FSharpx.Option

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

let private getTicketInfo html = maybe {
    let! price = getPrice html
    let! ticketType = getTicketType html
    let! startDate = getStartDate html 
    let! endDate = getEndDate html
    return 
      { price = price
        ticketType = ticketType
        startDate = startDate
        endDate = endDate } }

let downloadTicketInformation userInfo = async {
    let url = getUrl userInfo DateTime.Now
    let! response = Http.AsyncRequestString url
    return getTicketInfo response }