using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class MessageTimeParserTests
{
    [Fact]
    public void TryParse_ParsesMinutePrecision()
    {
        var parsed = new MessageTimeParser().TryParse("[21:35]", out var messageTime);

        Assert.True(parsed);
        Assert.Equal(21, messageTime.Hour);
        Assert.Equal(35, messageTime.Minute);
        Assert.Equal(0, messageTime.Second);
        Assert.Equal(MessageTimePrecision.Minute, messageTime.Precision);
    }

    [Fact]
    public void TryParse_ParsesSecondPrecision()
    {
        var parsed = new MessageTimeParser().TryParse("[21:35:42]", out var messageTime);

        Assert.True(parsed);
        Assert.Equal(21, messageTime.Hour);
        Assert.Equal(35, messageTime.Minute);
        Assert.Equal(42, messageTime.Second);
        Assert.Equal(MessageTimePrecision.Second, messageTime.Precision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("21:35")]
    [InlineData("[24:00]")]
    [InlineData("[21:60]")]
    [InlineData("[21:35:60]")]
    public void TryParse_ReturnsFalseForInvalidText(string text)
    {
        var parsed = new MessageTimeParser().TryParse(text, out _);

        Assert.False(parsed);
    }
}
