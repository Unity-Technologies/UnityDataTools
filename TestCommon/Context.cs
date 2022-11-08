using NUnit.Framework;

namespace UnityDataTools.TestCommon;

public class Context
{
    public string UnityDataFolder { get; }
    public string UnityDataVersion { get; }
    public string TestDataFolder { get; }
    public string ExpectedDataFolder { get; }
    public ExpectedData ExpectedData { get; } = new();

    private static Dictionary<string, List<Context>> m_Cache = new();

    private Context(string folder)
    {
        var di = new DirectoryInfo(folder);
        
        UnityDataFolder = folder;
        UnityDataVersion = di.Name;
        TestDataFolder = di.Parent.FullName;
        ExpectedDataFolder = Path.Combine(di.Parent.Parent.FullName, "ExpectedData", UnityDataVersion);
    }

    public static IEnumerable<Context> GetAll()
    {
        if (m_Cache.TryGetValue(TestContext.CurrentContext.TestDirectory, out var cases))
        {
            return cases;
        }

        cases = new List<Context>();
        m_Cache[TestContext.CurrentContext.TestDirectory] = cases;
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data")))
        {
            cases.Add(new Context(folder));
        }

        return cases;
    }

    public override string ToString()
    {
        // To show up nicely in the test report.
        return UnityDataVersion;
    }
}
