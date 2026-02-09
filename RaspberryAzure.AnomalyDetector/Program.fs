/// <summary>
/// The main module for the RaspberryAzure.AnomalyDetector program.
/// This module serves as the entry point for the application.
/// </summary>
module RaspberryAzure.AnomalyDetector.Program

let getAnomalies (data: float list) =
    let numbers = data
    let mean = numbers |> List.averageBy float
    let stddev =
        numbers
        |> List.averageBy (fun x -> let d = float x - mean in d * d)
        |> sqrt
    
    // Active patterns to differentiate between anomalies and regular data
    let (|Anomaly|RegularData|) input = if abs (float input - mean) > 2.0 * stddev then Anomaly else RegularData
    
    numbers
        |> List.filter (fun x ->
                        match x with
                        | Anomaly -> true
                        | RegularData -> false)

[<EntryPoint>]
let main argv =
    printfn "RaspberryAzure.AnomalyDetector is running."
    0
