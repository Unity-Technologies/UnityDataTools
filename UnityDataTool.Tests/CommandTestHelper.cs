using System;
using System.IO;
using System.Threading.Tasks;

namespace UnityDataTools.UnityDataTool.Tests;

internal static class CommandTestHelper
{
    public static async Task<(
        int exitCode,
        string stdout,
        string stderr)> RunAsync(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();

        try
        {
            Console.SetOut(stdoutWriter);
            Console.SetError(stderrWriter);

            int exitCode = await Program.Main(args);

            return (
                exitCode,
                stdoutWriter.ToString(),
                stderrWriter.ToString()
            );
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}