module RaspberryAzure.AnomalyDetector.Tests

open System
open Newtonsoft.Json
open Xunit

module AnomalyDetectorTests =
    [<Fact>]
    let ``Test anomaly detection with sample data`` () =
        let sampleData = """{"data": -1}{"data": -4}{"data": -6}{"data": -10}{"data": -12}{"data": -9}{"data": -11}{"data": -16}"""
        let numbers =
            sampleData.Split([|'}'|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.choose (fun entry ->
                try Some(JsonConvert.DeserializeObject<{| data: int |}>(entry + "}").data)
                with ex ->
                    Console.WriteLine($"Failed to deserialize entry: {entry}. Error: {ex.Message}")
                    None) // Log deserialization errors

        let mean = numbers |> Array.averageBy float
        let stddev =
            numbers
            |> Array.averageBy (fun x -> let d = float x - mean in d * d)
            |> sqrt

        let anomalies =
            numbers
            |> Array.filter (fun x -> abs (float x - mean) > 2.0 * stddev)

        Console.WriteLine($"Numbers: {numbers}")
        Console.WriteLine($"Mean: {mean}, StdDev: {stddev}")
        Console.WriteLine($"Anomalies: {anomalies}")

        Assert.Equal<int[]>([| -16 |], anomalies)

    [<Fact>]
    let ``Test empty data`` () =
        let sampleData = """{}"""
        let numbers =
            sampleData.Split([|'}'|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.choose (fun entry ->
                try Some(JsonConvert.DeserializeObject<{| data: int |}>(entry + "}").data)
                with ex ->
                    Console.WriteLine($"Failed to deserialize entry: {entry}. Error: {ex.Message}")
                    None) // Log deserialization errors

        Console.WriteLine($"Numbers: {numbers}")
        Assert.Empty(numbers)
