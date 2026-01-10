using System.ComponentModel;
using System.Text.Json;
using CSnakes.Runtime;
using Microsoft.FSharp.Collections;
using Microsoft.SemanticKernel;

namespace RaspberryAzure.AgentAnalyzer;

public class BasicStatsPlugin
{
    private readonly IPythonEnvironment _pythonEnvironment;

    public BasicStatsPlugin(IPythonEnvironment  pythonEnvironment)
    {
        _pythonEnvironment = pythonEnvironment;
    }
    
    [KernelFunction("get_avg")]
    [Description("Returns the average of latest records/measurements from storage.")]
    public double GetAverage()
    {
        return Storage.GetLatestRecord().Select(r => r.Data).Average();
    }
    
    [KernelFunction("get_min")]
    [Description("Returns the minimal value of latest records/measurements from storage.")]
    public double GetMinimum()
    {
        return Storage.GetLatestRecord().Select(r => r.Data).Min();
    }
    
    [KernelFunction("get_max")]
    [Description("Returns the maximal value of latest records/measurements from storage.")]
    public double GetMaximum()
    {
        return Storage.GetLatestRecord().Select(r => r.Data).Max();
    }

    [KernelFunction("get_anomalies")]
    [Description("Returns the anomalies of latest records/measurements.")]
    public List<double> GetAnomalies()
    {
        // old solution using json serialization
        // string data = JsonSerializer.Serialize(Storage.GetLatestRecord(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        //
        // if (data is null or "null")
        //     return [];
        
        
        var data = ListModule.OfSeq(Storage.GetLatestRecord().Select(r => r.Data));
        FSharpList<double> output = AnomalyDetector.Program.getAnomalies(data);
        return output.ToList();
    }
    
    [KernelFunction("run_nn")]
    [Description("Runs test neural network in python environment and returns tuple with predicted and actual result.")]
    public (string, string) RunNeuralNetwork()
    {
        // _pythonEnvironment. 
        // return output.ToList();
        var module = _pythonEnvironment.Nn();
        var response = module.Main();
        return response;
    }
}