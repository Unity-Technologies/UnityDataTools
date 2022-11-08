using NUnit.Framework;

namespace UnityDataTools.TestCommon;

[TestFixtureSource(typeof(Context), nameof(Context.GetAll))]
public class TestForAllVersions
{
    protected Context Context;
    
    public TestForAllVersions(Context context)
    {
        Context = context;
    }

    [OneTimeSetUp]
    public void LoadExpectedData()
    {
        Context.ExpectedData.Load(Context.ExpectedDataFolder);
    }
}
