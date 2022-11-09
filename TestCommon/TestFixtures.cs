using NUnit.Framework;

namespace UnityDataTools.TestCommon;

public class BaseTestFixture
{
    protected Context Context { get; }

    private static Dictionary<string, List<Context>> m_Cache = new();
    
    public BaseTestFixture(Context context)
    {
        Context = context;
    }

    protected virtual void OnLoadExpectedData(Context context)
    {
    }

    [OneTimeSetUp]
    public void LoadExpectedData()
    {
        OnLoadExpectedData(Context);
        Context.ExpectedData.Load(Context.ExpectedDataFolder);
    }
    
    protected static IEnumerable<Context> GetContexts(string dataFolder)
    {
        if (m_Cache.TryGetValue(dataFolder, out var cases))
        {
            return cases;
        }

        cases = new List<Context>();
        m_Cache[TestContext.CurrentContext.TestDirectory] = cases;
        
        var subfolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", dataFolder);
        
        foreach (var folder in Directory.EnumerateDirectories(subfolder))
        {
            cases.Add(new Context(folder));
        }

        return cases;
    }
}

[TestFixtureSource(typeof(AssetBundleTestFixture), nameof(GetContexts))]
public class AssetBundleTestFixture : BaseTestFixture
{
    public AssetBundleTestFixture(Context context) : base(context)
    {
    }
    
    public static IEnumerable<Context> GetContexts()
    {
        return BaseTestFixture.GetContexts("AssetBundles");
    }
}

[TestFixtureSource(typeof(PlayerDataTestFixture), nameof(GetContexts))]
public class PlayerDataTestFixture : BaseTestFixture
{
    public PlayerDataTestFixture(Context context) : base(context)
    {
    }
    
    public static IEnumerable<Context> GetContexts()
    {
        return BaseTestFixture.GetContexts("PlayerData");
    }
}
