using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        // Serialized test data is in en-US format. Ensure that the tests run in this culture to avoid formatting
        // related false negatives.
        // TODO: Fix test data to be culture agnostic.
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
    }
}