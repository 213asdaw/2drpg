using System;

namespace MegaCrit.Sts2.Core.Logging;

/// <summary>Compile-time stub for STS2 logging.</summary>
public static class Log
{
    public static void Info(string message) => Console.WriteLine("[INFO] " + message);
    public static void Warn(string message) => Console.WriteLine("[WARN] " + message);
    public static void Error(string message) => Console.WriteLine("[ERROR] " + message);
    public static void Debug(string message) => Console.WriteLine("[DEBUG] " + message);
}
