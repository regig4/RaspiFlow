/// <summary>
/// The main module for the RaspberryAzure.AnomalyDetector program.
/// This module serves as the entry point for the application.
/// </summary>
module RaspberryAzure.AnomalyDetector.Program

open System
open System.IO

let getAnomalies (data: float list) =
    //{"data": -1}{"data": -4}{"data": -6}{"data": -10}{"data": -12}{"data": -9}{"data": -11}{"data": -16}{"data": -12}{"data": -8}{"data": -6}{"data": -10}{"data": -11}{"data": -13}{"data": -9}{"data": -9}{"data": -10}{"data": -9}{"data": -13}{"data": -18}{"data": -14}{"data": -18}{"data": -17}{"data": -16}{"data": -16}{"data": -21}{"data": -21}{"data": -22}{"data": -24}{"data": -20}{"data": -21}{"data": -20}{"data": -24}{"data": -23}{"data": -22}{"data": -20}{"data": -25}{"data": -23}{"data": -24}{"data": -22}{"data": -23}{"data": -21}{"data": -26}{"data": -27}{"data": -31}{"data": -32}{"data": -36}{"data": -39}{"data": -40}{"data": -38}{"data": -40}{"data": -40}{"data": -37}{"data": -38}{"data": -42}{"data": 18}{"data": 15}{"data": 15}{"data": 15}{"data": 13}{"data": 8}{"data": 11}{"data": 10}{"data": 10}{"data": 10}{"data": 9}{"data": 10}{"data": 6}{"data": 2}{"data": 3}{"data": 1}{"data": -4}{"data": -5}{"data": -9}{"data": -11}{"data": -14}{"data": -14}{"data": -15}{"data": -16}{"data": -21}{"data": -19}{"data": -22}
    // let text = File.ReadAllText("tmp23.txt")
    // let text = File.ReadAllText("data.txt")
    // let text = data
    
    // old solution passing serialized json:
    /// Extracts an array of integers from a given text using a regular expression.
    /// The regular expression matches patterns in the format: `{"data": <number>}`
    /// where `<number>` can be a positive or negative integer.
    /// 
    /// Returns:
    /// - An array of integers extracted from the text.
    /// 
    /// Parameters:
    /// - `text`: The input string containing the data to be parsed.
    // let numbers =
    //     System.Text.RegularExpressions.Regex.Matches(text, @"\{""data"":\s*(-?\d+)\,")
    //     |> Seq.cast<System.Text.RegularExpressions.Match>
    //     |> Seq.map (fun m -> double m.Groups.[1].Value)
    //     |> Seq.toArray

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
