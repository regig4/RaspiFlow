using System.Diagnostics;

namespace RaspberryAzure.AgentAnalyzer;

public static class Utilities
{
    public static string RunFSharpScript(string scriptPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"fsi {scriptPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"F# script execution failed: {error}");
        }

        return output;
    }
}