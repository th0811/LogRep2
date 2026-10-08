using System.Collections;
using System.ComponentModel;
using System.Windows.Data;
using System.Xml.Linq;
using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class AnalysisResultSortingTests
{
    [Fact]
    public void 分析結果の全数値列を表示文字列ではなく数値で昇順降順に並べる()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LogRep2.sln")))
                    directory = directory.Parent;
                Assert.NotNull(directory);
                var xaml = XDocument.Load(Path.Combine(directory.FullName, "src/LogRep2.App/Analyzer/AnalysisResultView.xaml"));
                XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                var tabs = xaml.Descendants(ns + "TabItem").Take(3).ToArray();
                object[][] rows =
                [
                    new[] { Actor(2), Actor(10), Actor(1000) },
                    new[] { Action(2), Action(10), Action(1000) },
                    new[] { Points(2), Points(10), Points(1000) },
                ];
                var count = 0;
                for (var i = 0; i < tabs.Length; i++)
                {
                    var columns = tabs[i].Descendants(ns + "DataGridTextColumn")
                        .Where(column => (string?)column.Attribute("ElementStyle") == "{StaticResource NumericCellStyle}");
                    foreach (var column in columns)
                    {
                        var path = (string?)column.Attribute("SortMemberPath");
                        Assert.False(string.IsNullOrEmpty(path), $"数値列「{column.Attribute("Header")?.Value}」にソート指定がありません。");
                        var view = new ListCollectionView(new ArrayList(new[] { rows[i][2], rows[i][0], rows[i][1] }));
                        view.SortDescriptions.Add(new SortDescription(path!, ListSortDirection.Ascending));
                        Assert.Equal(rows[i], view.Cast<object>());
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new SortDescription(path!, ListSortDirection.Descending));
                        Assert.Equal(rows[i].Reverse(), view.Cast<object>());
                        count++;
                    }
                }
                Assert.Equal(19, count);
                CheckNull(new ActorSummaryViewModel(Actor(0).Summary with { Dps = null }), Actor(2), "Summary.Dps");
                CheckNull(new ActionSummaryViewModel(Action(0).Summary with { Damage = DamageStatistics.Empty }), Action(2), "Summary.Damage.AverageDamage");
                CheckNull(new LevelingPointSummaryViewModel(new("不明", 0, 0, null)), Points(2), "Summary.PointsPerHour");
            }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "ソートテストが制限時間内に完了しませんでした。");
        Assert.Null(error);
    }

    private static void CheckNull(object missing, object number, string path)
    {
        var view = new ListCollectionView(new ArrayList(new[] { number, missing }));
        view.SortDescriptions.Add(new SortDescription(path, ListSortDirection.Ascending));
        Assert.Equal(new[] { missing, number }, view.Cast<object>());
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(path, ListSortDirection.Descending));
        Assert.Equal(new[] { number, missing }, view.Cast<object>());
    }

    private static ActorSummaryViewModel Actor(int value) => new(new ActorSummary(
        "キャラクター", value, value + 0.25, TimeConfidence.Exact, value / 10000.0, value / 10000.0,
        value, value, value, value,
        new(value, value, value, value / 10000.0, value / 10000.0), [])
    { IncomingDamage = new([value]), EvadeCount = value, IncomingHitCount = 10000 - value });

    private static ActionSummaryViewModel Action(int value) => new(new(
        "キャラクター", "技", ActionType.WeaponSkill, value, value, value, value, value / 10000.0, new([value])));

    private static LevelingPointSummaryViewModel Points(int value) => new(new("経験値", value, value, value + 0.25));
}
