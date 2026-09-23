using System;
using System.IO;
using System.Threading.Tasks;

namespace UnityDataTools.UnityDataTool.Tests;

/// <summary>
/// Helpers for running UnityDataTool commands while capturing their console output.
/// </summary>
public static class ConsoleTestHelper
{
    /// <summary>
    /// Runs a command capturing stderr, where analyze prints its warnings and errors.
    /// </summary>
    public static async Task<(int exitCode, string stdErr)> RunCapturingStdErr(params string[] args)
    {
        using var sw = new StringWriter();
        var currentError = Console.Error;
        int exitCode;
        try
        {
            Console.SetError(sw);
            exitCode = await Program.Main(args);
        }
        finally
        {
            Console.SetError(currentError);
        }

        return (exitCode, sw.ToString());
    }

    /// <summary>
    /// Runs a command capturing stdout.
    /// </summary>
    public static async Task<(int exitCode, string stdOut)> RunCapturingStdOut(params string[] args)
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        int exitCode;
        try
        {
            Console.SetOut(sw);
            exitCode = await Program.Main(args);
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        return (exitCode, sw.ToString());
    }
}
