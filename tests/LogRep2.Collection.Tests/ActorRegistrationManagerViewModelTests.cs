using FFXI_LogAnalyzer.App;

namespace FfxiTempLogCollector.Tests;

public sealed class ActorRegistrationManagerViewModelTests
{
    [Fact]
    public void 既存NPC名と追加入力をPC名と同じ規則で正規化する()
    {
        AnalyzerSettings? saved = null;
        var viewModel = new ActorRegistrationManagerViewModel(
            new AnalyzerSettings
            {
                KnownNpcNames = ["gOBLIN SMITH"],
            },
            settings => saved = settings);

        Assert.Equal(["Goblin smith"], viewModel.NpcNames);

        viewModel.NpcNameInput = "  oRC WARRIOR  ";
        viewModel.AddNpcNameCommand.Execute(null);

        Assert.NotNull(saved);
        Assert.Equal(
            ["Goblin smith", "Orc warrior"],
            saved.KnownNpcNames);
    }
}
