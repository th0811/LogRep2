using System.Globalization;
using System.Text.RegularExpressions;
using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class PartyTimelineBuilder
{
    private readonly ActionGroupParser _parser = new(new DefaultAnalysisRuleSet());
    private readonly MagicLogClassifier _magic = new();

    public PartyTimeline Build(
        IReadOnlyList<ICanonicalRecord> includedRecords,
        IReadOnlyList<ICanonicalRecord>? timeRecords = null,
        CancellationToken cancellationToken = default)
    {
        var events = new List<PartyTimelineEvent>();
        var warnings = new List<string>();
        var times = (timeRecords ?? includedRecords)
            .Where(record => !string.IsNullOrWhiteSpace(record.MessageTimeText) && record.Order is not null)
            .GroupBy(record => record.SessionId ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.Order).ToArray());
        var missingGroups = includedRecords.Count(record => string.IsNullOrWhiteSpace(record.EventGroup)
            || string.IsNullOrWhiteSpace(record.SessionId));
        if (missingGroups > 0)
        {
            warnings.Add($"セッションIDまたはevent_groupのない{missingGroups:N0}行は行動の対応付け対象外です。");
        }

        foreach (var group in new ActionGroupBuilder().Build(includedRecords))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsedLines = group.Records.Select(record =>
            {
                var single = new ActionGroup(group.Key, [record]);
                return new Declaration(record, _parser.Parse(single), _magic.TryParseCastStart(single, out _, out _));
            }).ToArray();
            var declarations = parsedLines.Where(item => item.Action.ActionType is ActionType.WeaponSkill or ActionType.Ability or ActionType.Magic
                && !string.IsNullOrWhiteSpace(item.Action.Actor)
                && !string.IsNullOrWhiteSpace(item.Action.ActionName)).ToArray();
            if (declarations.Length == 0)
            {
                continue;
            }

            // 同じグループでも行動候補が異なる場合は、結果を推測で帰属させません。
            if (declarations.Select(item => (item.Action.Actor, item.Action.ActionName)).Distinct().Count() > 1
                || parsedLines.Any(item => item.Action.ActionType is ActionType.NormalAttack or ActionType.NormalAttackCritical))
            {
                warnings.Add($"{group.ActionGroupKey}: 複数の行動候補があるためタイムラインから除外しました。");
                continue;
            }

            var executed = declarations.FirstOrDefault(item => !item.IsStart);
            var interrupted = group.VisibleTexts.Any(text => text.Contains("詠唱中断", StringComparison.Ordinal)
                || text.Contains("詠唱を中断", StringComparison.Ordinal));
            if (executed is null && !interrupted)
            {
                continue;
            }

            var declaration = executed ?? declarations[0];
            var anchor = interrupted && executed is null
                ? group.Records.First(record => (record.VisibleText ?? string.Empty).Contains("中断", StringComparison.Ordinal))
                : declaration.Record;
            var results = new List<TimelineTargetResult>();
            var chainSection = false;
            foreach (var record in group.Records)
            {
                var text = record.VisibleText ?? string.Empty;
                if (text.Contains("連携", StringComparison.Ordinal))
                {
                    chainSection = true;
                    continue;
                }
                if (declarations.Any(item => ReferenceEquals(item.Record, record) && !item.IsStart))
                {
                    chainSection = false;
                }
                if (chainSection || executed is null)
                {
                    continue;
                }
                // 結果行の対象と数値を一緒に保持し、連携や通常攻撃の数値は混ぜません。
                var match = DamageResultRegex().Match(text);
                if (match.Success && int.TryParse(match.Groups["damage"].Value,
                    NumberStyles.None, CultureInfo.InvariantCulture, out var damage))
                {
                    results.Add(new TimelineTargetResult(match.Groups["target"].Value, damage));
                }
            }

            var status = interrupted && executed is null ? "中断"
                : group.VisibleTexts.Any(text => text.Contains("使用失敗", StringComparison.Ordinal)
                    || text.Contains("発動失敗", StringComparison.Ordinal)) ? "失敗"
                : results.Count > 0 ? "ダメージ確認"
                : new HitStatusClassifier().Classify(group, ParsedDamageResult.None) switch
                {
                    HitStatus.Hit => "効果確認",
                    HitStatus.Miss => "ミス・効果なし",
                    _ => "実行・結果未確認",
                };
            ICanonicalRecord? time = null;
            if (times.TryGetValue(group.SessionId, out var sessionTimes) && anchor.Order is { } order)
            {
                // 二分探索で直前の時刻付きログを参照します。発生時刻の補間はしません。
                var low = 0;
                var high = sessionTimes.Length;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    if (sessionTimes[middle].Order <= order) low = middle + 1;
                    else high = middle;
                }
                if (low > 0) time = sessionTimes[low - 1];
            }
            events.Add(new PartyTimelineEvent(group.SessionId, group.EventGroup, anchor.Order,
                declaration.Action.Actor!, declaration.Action.ActionName!, declaration.Action.ActionType,
                status, executed is not null, time?.MessageTimeText, time?.Order,
                results, group.Records.Select(record => record.Record).ToArray()));
        }

        return new PartyTimeline(events.OrderBy(item => item.Order ?? long.MaxValue).ToArray(), warnings);
    }

    private sealed record Declaration(ActionGroupRecord Record, ParsedAction Action, bool IsStart);

    [GeneratedRegex(@"^\s*→?(?<target>[^→、]+?)(?:に、|は、)(?<damage>\d+)(?:ダメージ|HP吸収)[。！!]?$", RegexOptions.CultureInvariant)]
    private static partial Regex DamageResultRegex();
}
