using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class SampleSessionIntegrationTests
{
    [Fact]
    public void AnalyzeSampleSession_RunsFullPipeline()
    {
        var sessionFolder = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample_session");
        var loadResult = new SessionFolderLoader().Load(sessionFolder);

        Assert.True(loadResult.IsSuccess, string.Join(Environment.NewLine, loadResult.Errors));
        Assert.NotNull(loadResult.Session);
        Assert.Empty(loadResult.Warnings);
        Assert.Equal("sample-session", loadResult.Session.SessionInfo.SessionId);
        Assert.Equal(14, loadResult.Session.StatsInfo.CanonicalRecordsWritten);

        var recordLoadResult = new CanonicalRecordReader().Read(loadResult.Session.CanonicalRecordsPath);
        Assert.Empty(recordLoadResult.Errors);
        Assert.Empty(recordLoadResult.LineErrors);
        Assert.Equal(14, recordLoadResult.Records.Count);

        var markers = new MarkerExtractor().Extract(recordLoadResult.Records);
        Assert.Collection(
            markers,
            marker => Assert.Equal("#start", marker.MarkerKeyword),
            marker => Assert.Equal("#end", marker.MarkerKeyword));

        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.FromMarker(markers[0]),
            AnalysisEndpoint.FromMarker(markers[1]));
        var analysisRecords = new AnalysisRangeBuilder().Build(recordLoadResult.Records, selection);
        Assert.Equal(12, analysisRecords.Count);
        Assert.DoesNotContain(analysisRecords, record => record.IsMarker);

        var analysisTime = new AnalysisTimeResolver().Resolve(selection, analysisRecords);
        Assert.Equal(TimeConfidence.Exact, analysisTime.Confidence);
        Assert.Equal(30, analysisTime.DurationSeconds);
        Assert.True(analysisTime.CanCalculateDps);

        var actionGroups = new ActionGroupBuilder().Build(analysisRecords);
        Assert.Equal(8, actionGroups.Count);

        var parser = new ActionGroupParser(new DefaultAnalysisRuleSet());
        var parseResults = actionGroups
            .Select(group => parser.ParseGroup(group))
            .ToArray();
        var parsed = parseResults
            .Where(result => result.Parsed is not null)
            .Select(result => result.Parsed!)
            .ToArray();
        var unparsed = parseResults
            .Where(result => result.Unparsed is not null)
            .Select(result => result.Unparsed!)
            .ToArray();

        Assert.Equal(7, parsed.Length);
        Assert.Single(unparsed);
        Assert.Contains(parsed, action => action.ActionType == ActionType.NormalAttackCritical);
        Assert.Contains(parsed, action => action.HitStatus == HitStatus.Miss);
        Assert.Contains(parsed, action => action.HitStatus == HitStatus.Unknown);
        Assert.Contains(parsed, action => action.Damage.Damage == 0);
        Assert.Contains(parsed, action => action.Damage.DamageValues.Count > 1);

        var result = new AnalysisAggregator().Aggregate(parsed, analysisTime, unparsed);

        Assert.Equal(2, result.ActorSummaries.Count);
        Assert.Single(result.UnparsedActionGroups);
        Assert.Single(result.UnknownActionGroups);

        var xitra = Assert.Single(result.ActorSummaries, summary => summary.Actor == "Xitra");
        Assert.Equal(1117, xitra.TotalDamage);
        Assert.Equal(1117d / 30d, xitra.Dps);
        Assert.Equal(6, xitra.TotalUseCount);
        Assert.Equal(4, xitra.TotalHitCount);
        Assert.Equal(1, xitra.TotalMissCount);
        Assert.Equal(1, xitra.UnknownCount);
        Assert.Equal(0.75, xitra.NormalAttackHitRate);
        Assert.Equal(1.0 / 3.0, xitra.NormalAttackCriticalRate);

        var boro = Assert.Single(result.ActorSummaries, summary => summary.Actor == "Boro");
        Assert.Equal(200, boro.TotalDamage);
        Assert.Equal(200d / 30d, boro.Dps);

        var normalAttack = Assert.Single(
            result.ActionSummaries,
            summary => summary.Actor == "Xitra" && summary.ActionType == ActionType.NormalAttack);
        Assert.Equal(4, normalAttack.UseCount);
        Assert.Equal(2, normalAttack.HitCount);
        Assert.Equal(1, normalAttack.MissCount);
        Assert.Equal(1, normalAttack.UnknownCount);
        Assert.Equal(123, normalAttack.Damage.TotalDamage);
        Assert.Equal(123, normalAttack.Damage.MaxDamage);
        Assert.Equal(0, normalAttack.Damage.MinDamage);
        Assert.Equal(61.5, normalAttack.Damage.AverageDamage);

        var critical = Assert.Single(
            result.ActionSummaries,
            summary => summary.Actor == "Xitra" && summary.ActionType == ActionType.NormalAttackCritical);
        Assert.Equal(573, critical.Damage.TotalDamage);

        var multiTargetSkill = Assert.Single(
            result.ActionSummaries,
            summary => summary.Actor == "Xitra" && summary.ActionType == ActionType.Skill);
        Assert.Equal(1, multiTargetSkill.UseCount);
        Assert.Equal(421, multiTargetSkill.Damage.TotalDamage);
        Assert.Equal(321, multiTargetSkill.Damage.MaxDamage);
        Assert.Equal(100, multiTargetSkill.Damage.MinDamage);
        Assert.Equal(210.5, multiTargetSkill.Damage.AverageDamage);
    }
}
