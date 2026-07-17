using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class ActorNameClassifierTests
{
    [Theory]
    [InlineData("aminon", "Aminon")]
    [InlineData("AMINON", "Aminon")]
    [InlineData("aMiNoN", "Aminon")]
    public void NormalizePcName_NormalizesToFfxiPcNameStyle(
        string input,
        string expected)
    {
        Assert.Equal(expected, ActorNameClassifier.NormalizePcName(input));
    }

    [Theory]
    [InlineData("Aminon", true)]
    [InlineData("A", true)]
    [InlineData("Abcdefghijklmno", true)]
    [InlineData("Abcdefghijklmnop", false)]
    [InlineData("Ami Non", false)]
    [InlineData("Aminon1", false)]
    [InlineData("Aminon-", false)]
    public void IsPcNameCandidate_UsesAlphabetOnlyAndMax15Characters(
        string actor,
        bool expected)
    {
        Assert.Equal(expected, ActorNameClassifier.IsPcNameCandidate(actor));
    }

    [Fact]
    public void Classify_PrioritizesNpcRegistration()
    {
        var classifier = new ActorNameClassifier();

        var actual = classifier.Classify(
            "Aminon",
            ["Aminon"],
            ["Aminon"]);

        Assert.Equal(ActorNameKind.RegisteredNpc, actual);
    }

    [Fact]
    public void Classify_UsesPcRegistrationBeforeCandidate()
    {
        var classifier = new ActorNameClassifier();

        var actual = classifier.Classify(
            "aminon",
            ["Aminon"],
            []);

        Assert.Equal(ActorNameKind.RegisteredPc, actual);
    }

    [Fact]
    public void Classify_AllowsNpcNamesThatAreNotPcNameFormat()
    {
        var classifier = new ActorNameClassifier();

        var actual = classifier.Classify(
            "Goblin Smith",
            [],
            ["Goblin Smith"]);

        Assert.Equal(ActorNameKind.RegisteredNpc, actual);
    }
}
