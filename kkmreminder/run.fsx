open System
open System.Configuration

let Run(timer: TimerInfo, log: TraceWriter) =
    let text = sprintf "F# timer function processed at '%s'" (DateTime.Now.ToString ())
    log.Info (text)
