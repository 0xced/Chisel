namespace Chisel.Tests;

/// <summary>
/// The possible application publish modes for the TestApp.
/// See also the <a href="https://learn.microsoft.com/en-us/dotnet/core/deploying/">.NET application publishing overview</a> documentation.
/// </summary>
public enum PublishMode
{
    /// <summary>
    /// Standard app publish, all dlls and related files are copied along the main executable.
    /// </summary>
    Standard,

    /// <summary>
    /// Publish a single file as a framework-dependent binary.
    /// </summary>
    SingleFile,
}
