using System;
using System.IO;
using System.Threading;

namespace Communicator.Canvas;

/// <summary>
/// A mock service simulating external processing algorithms.
/// </summary>
public static class ProcessingService
{
    /// <summary>
    /// Mocks a function that takes a serialized shape and returns a "regularized" version.
    /// Logic: It attempts to "straighten" the shape by resetting color to Black and Thickness to 5.0,
    /// simulating a correction algorithm.
    /// </summary>
    public static string RegularizeShape(string inputJson)
    {
        // Simulating processing time
        Thread.Sleep(100);

        // In a real scenario, this would parse geometry and fix lines.
        // Here, we just modify the JSON string to force a "Regularized" style.
        // We naively replace color/thickness to simulate a change.

        // NOTE: This is string manipulation for the sake of the "Black Box" simulation.
        string outputJson = inputJson;

        // 1. Force Color to Black (simulating a standard ink)
        // Regex or simple replace could be used, but for safety in this mock, 
        // we assume the deserializer handles the logic, but the requirement said 
        // input=string, output=string.

        // Let's cheat slightly for the mock: we will assume the VM handles the logic 
        // if we return the same string, but let's try to actually modify the thickness in the string.
        if (outputJson.Contains("\"Thickness\":"))
        {
            // Simple hacky replace to prove the string changed
            outputJson = outputJson.Replace("\"Thickness\": 2", "\"Thickness\": 5");
            outputJson = outputJson.Replace("\"Thickness\":2", "\"Thickness\":5");
        }

        return outputJson;
    }

    /// <summary>
    /// Mocks an AI vision analysis of the canvas image.
    /// </summary>
    public static string AnalyzeCanvasImage(string imagePath)
    {
        if (!File.Exists(imagePath)) return "Error: Image file not found.";

        // Simulating processing time
        Thread.Sleep(500);

        FileInfo fi = new FileInfo(imagePath);
        return $"[Analysis Result] Image captured at {DateTime.Now:T}. \n" +
               $"Size: {fi.Length / 1024} KB. \n" +
               $"Detection: The drawing appears to contain geometric shapes. \n" +
               $"Recommendation: Try using the 'Regularize' tool to clean up freehand lines.";
    }
}
