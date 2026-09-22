using System.Runtime.CompilerServices;
using VerifyTests;

namespace Chisel.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void ConfigureVerify() => VerifierSettings.FixNewlinesOnRead();
}