// File: src/NetworkChecker/Core/VersionInfo.cs
namespace NetworkChecker.Core;

public static class VersionInfo
{
    public static string Version
        => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}