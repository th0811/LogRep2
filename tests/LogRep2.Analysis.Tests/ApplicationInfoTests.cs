namespace FFXI_LogAnalyzer.Tests;

public class ApplicationInfoTests
{
    [Fact]
    public void ApplicationInfo_HasExpectedName()
    {
        Assert.Equal("FFXI_LogAnalyzer", FFXI_LogAnalyzer.Core.ApplicationInfo.Name);
    }
}
