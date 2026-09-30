using System.Globalization;
using System.Net;
using System.Text;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public static class PartyTimelineHtmlExporter
{
    public static string Build(PartyTimeline timeline, IReadOnlyList<string> members,
        string rangeName, string orderRange, int excludedRecordCount,
        IReadOnlyList<ActorSummary>? actorSummaries = null)
    {
        var players = members.Distinct(StringComparer.Ordinal).ToArray();
        if (players.Length == 0)
        {
            throw new ArgumentException("PTメンバーを1名以上選択してください。", nameof(members));
        }
        var selected = players.ToHashSet(StringComparer.Ordinal);
        var events = timeline.Events.Where(item => selected.Contains(item.Actor)).ToArray();
        var html = new StringBuilder("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>PT行動レポート</title><style>");
        html.Append(Styles).Append("</style></head><body><header><p class=\"eyebrow\">LogRep2 · PT行動レポート</p><h1>")
            .Append(E(rangeName)).Append("</h1><p>ログ順 ").Append(E(orderRange))
            .Append(" · 手動除外 ").Append(N(excludedRecordCount)).Append("行 · 表示行動 ")
            .Append(N(events.Length)).Append("件</p><p>PT: ").Append(E(string.Join(" / ", players)))
            .Append("</p><p>作成日時: ").Append(E(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)))
            .Append(" · バージョン: ").Append(E(typeof(PartyTimelineHtmlExporter).Assembly.GetName().Version?.ToString() ?? "不明"))
            .Append("</p><nav><a href=\"#numbers\">数値データ</a><a href=\"#timeline\">行動タイムライン</a></nav></header><main>")
            .Append("<details class=\"warnings\"><summary>表示・集計について</summary><p>選択区間を1回のボス戦として表示します。ダメージ合計と図表はWS・アビリティ・魔法を対象とし、通常攻撃・連携ダメージ・実行者不明は含めません。被ダメージは通常攻撃・魔法・連携を含む対象別のダメージ結果1件を単位に集計し、回避や結果不明を0で補完しません。回避率は魔法・連携・成否不明・分身による無効化・効果なしを除外し、回避数÷（被命中数＋回避数）で算出します。攻撃命中率は通常攻撃とWSを合算し、命中数÷（命中数＋非命中数）で算出します。判定不明は分母に含めません。WSダメージ統計はダメージを確認できた1回のWSの対象合計から算出し、ミス・結果不明を0で補完しません。縦の間隔は経過時間を表しません。時刻はログに記録された参考情報です。</p></details>");
        if (timeline.Warnings.Count > 0)
        {
            html.Append("<details class=\"warnings\"><summary>解析上の注意（区間全体）</summary><ul>");
            foreach (var warning in timeline.Warnings) html.Append("<li>").Append(E(warning)).Append("</li>");
            html.Append("</ul></details>");
        }
        html.Append("<section id=\"numbers\"><h2>PC別サマリ</h2>");
        html.Append("<div class=\"scroll\"><table><thead><tr><th>PC</th><th>ダメージ合計</th><th>攻撃命中率</th><th>WS回数</th><th>WSダメージ平均</th><th>WSダメージ最大</th><th>WSダメージ最小</th><th>被ダメージ合計</th><th>被ダメージ最大</th><th>被ダメージ最小</th><th>被ダメージ平均</th><th>回避率</th></tr></thead><tbody>");
        foreach (var player in players)
            PcSummaryRow(html, player, events.Where(item => item.Actor == player).ToArray(),
                actorSummaries?.FirstOrDefault(summary => summary.Actor == player));
        html.Append("</tbody></table></div></section><section id=\"timeline\"><h2>PT全体の行動タイムライン</h2><p>カードをクリックするとゲーム内ログを表示します。横並びは同時実行を意味しません。行動順はカード番号で確認できます。</p><div class=\"timeline-scroll\"><table class=\"timeline\" style=\"min-width:")
            .Append(145 + players.Length * 170).Append("px\"><thead><tr><th>行動順<br>参考時刻</th>");
        foreach (var player in players) html.Append("<th>").Append(E(player)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        string? previousSession = null;
        string? previousTime = null;
        foreach (var row in PartyTimelineLayout.BuildRows(events))
        {
            var first = events[row[0]];
            if (previousSession != first.SessionId)
            {
                html.Append("<tr class=\"session\"><td colspan=\"").Append(players.Length + 1).Append("\">セッション: ").Append(E(first.SessionId)).Append("</td></tr>");
                previousTime = null;
            }
            html.Append("<tr class=\"action-row\"><th>#").Append(row[0] + 1);
            if (row.Count > 1) html.Append("–").Append(row[^1] + 1);
            if (first.ReferenceTime is not null && first.ReferenceTime != previousTime)
                html.Append("<small title=\"記録行 ").Append(N(first.ReferenceTimeOrder)).Append("\">参考 ").Append(E(first.ReferenceTime)).Append("</small>");
            html.Append("</th>");
            foreach (var player in players)
            {
                html.Append("<td>");
                foreach (var index in row.Where(index => events[index].Actor == player))
                {
                    var item = events[index];
                    html.Append("<button type=\"button\" aria-haspopup=\"dialog\" aria-controls=\"log-dialog\" id=\"row-").Append(index + 1).Append("\" class=\"card ")
                        .Append(item.ActionType == ActionType.Magic ? "magic" : item.ActionType == ActionType.Ability ? "ability" : "skill")
                        .Append("\" data-log=\"event-").Append(index + 1).Append("\"><span class=\"event-number\">#").Append(index + 1).Append("</span><strong>")
                        .Append(E(item.ActionName)).Append("</strong>");
                    if (item.Damage is not null)
                        html.Append("<span>").Append(N(item.Damage)).Append(" ダメージ</span>");
                    else if (item.Status is not ("実行・結果未確認" or "効果確認"))
                        html.Append("<span>").Append(E(item.Status)).Append("</span>");
                    html.Append("</button>");
                }
                html.Append("</td>");
            }
            html.Append("</tr>");
            previousSession = first.SessionId;
            previousTime = first.ReferenceTime;
        }
        html.Append("</tbody></table></div>");
        if (events.Length == 0) html.Append("<p>選択したメンバーの表示対象行動はありません。</p>");
        html.Append("</section><section id=\"action-summary\"><h2>PC・行動別</h2><p>表示対象行動のみの集計です。平均・最大・最小は、ダメージを確認できた1回の行動の対象合計から算出します。未確認・ミスを0として補完しません。中断は使用回数に含めません。</p>");
        html.Append("<p>列見出しをクリックすると昇順・降順を切り替えます。値のない「—」は常に末尾に表示します。</p>");
        StartSummary(html, sortable: true);
        foreach (var group in events.GroupBy(item => (item.Actor, item.ActionName, item.ActionType)))
            SummaryRow(html, group.Key.Actor, group.Key.ActionName, Kind(group.Key.ActionType), group.ToArray());
        html.Append("</tbody></table></div></section></main>");
        for (var i = 0; i < events.Length; i++)
        {
            html.Append("<template id=\"event-").Append(i + 1).Append("\">");
            foreach (var record in events[i].SourceRecords)
                html.Append(E(record.VisibleText ?? string.Empty)).Append('\n');
            html.Append("</template>");
        }
        html.Append("<dialog id=\"log-dialog\" aria-label=\"ゲーム内ログ\"><form method=\"dialog\"><button autofocus>閉じる</button></form><pre id=\"log-text\"></pre></dialog><script>");
        return html.Append(Script).Append(SortScript).Append("</script></body></html>").ToString();
    }

    private static void PcSummaryRow(StringBuilder html, string actor, PartyTimelineEvent[] events, ActorSummary? summary)
    {
        var damages = events.Where(item => item.Damage.HasValue).Select(item => item.Damage!.Value).ToArray();
        var weaponSkills = events.Where(item => item.ActionType == ActionType.WeaponSkill && item.IsExecuted).ToArray();
        var wsDamages = weaponSkills.Where(item => item.Damage.HasValue).Select(item => item.Damage!.Value).ToArray();
        var hitRate = "—";
        if (summary is not null)
        {
            var wsSummaries = summary.ActionSummaries.Where(action => action.ActionType == ActionType.WeaponSkill).ToArray();
            var hits = (long)summary.NormalAttackSummary.HitCount + wsSummaries.Sum(action => (long)action.HitCount);
            var misses = (long)summary.NormalAttackSummary.MissCount + wsSummaries.Sum(action => (long)action.MissCount);
            if (hits + misses > 0)
                hitRate = (100d * hits / (hits + misses)).ToString("N2", CultureInfo.InvariantCulture) + "%";
        }
        html.Append("<tr><td>").Append(E(actor)).Append("</td><td>").Append(damages.Length == 0 ? "—" : N(damages.Sum()))
            .Append("</td><td>").Append(hitRate).Append("</td><td>").Append(N(weaponSkills.Length))
            .Append("</td><td>").Append(wsDamages.Length == 0 ? "—" : wsDamages.Average().ToString("N2", CultureInfo.InvariantCulture))
            .Append("</td><td>").Append(wsDamages.Length == 0 ? "—" : N(wsDamages.Max()))
            .Append("</td><td>").Append(wsDamages.Length == 0 ? "—" : N(wsDamages.Min()))
            .Append("</td><td>").Append(summary is null ? "—" : N(summary.IncomingDamage.TotalDamage))
            .Append("</td><td>").Append(summary?.IncomingDamage.MaxDamage is int maximum ? N(maximum) : "—")
            .Append("</td><td>").Append(summary?.IncomingDamage.MinDamage is int minimum ? N(minimum) : "—")
            .Append("</td><td>").Append(summary?.IncomingDamage.AverageDamage?.ToString("N2", CultureInfo.InvariantCulture) ?? "—")
            .Append("</td><td>").Append(summary?.EvasionRate is double rate ? (rate * 100).ToString("N2", CultureInfo.InvariantCulture) + "%" : "—")
            .Append("</td></tr>");
    }

    private static void StartSummary(StringBuilder html, bool sortable = false)
    {
        string[] columns = ["PC", "行動", "種別", "使用回数", "中断", "ダメージ確認回数", "合計", "平均", "最大", "最小"];
        html.Append("<div class=\"scroll\"><table><thead><tr>");
        for (var index = 0; index < columns.Length; index++)
        {
            if (sortable)
                html.Append("<th scope=\"col\" aria-sort=\"none\"><button type=\"button\" class=\"sort-button\" data-sort=\"")
                    .Append(index).Append("\" data-type=\"").Append(index < 3 ? "text" : "number")
                    .Append("\">").Append(columns[index]).Append("</button></th>");
            else html.Append("<th>").Append(columns[index]).Append("</th>");
        }
        html.Append("</tr></thead><tbody>");
    }

    private static void SummaryRow(StringBuilder html, string actor, string action, string kind, PartyTimelineEvent[] events)
    {
        var damages = events.Where(item => item.Damage.HasValue).Select(item => item.Damage!.Value).ToArray();
        html.Append("<tr><td>").Append(E(actor)).Append("</td><td>").Append(E(action)).Append("</td><td>").Append(kind)
            .Append("</td><td>").Append(N(events.Count(item => item.IsExecuted))).Append("</td><td>").Append(N(events.Count(item => !item.IsExecuted)))
            .Append("</td><td>").Append(N(damages.Length)).Append("</td><td>").Append(damages.Length == 0 ? "—" : N(damages.Sum()))
            .Append("</td><td>").Append(damages.Length == 0 ? "—" : damages.Average().ToString("N2", CultureInfo.InvariantCulture))
            .Append("</td><td>").Append(damages.Length == 0 ? "—" : N(damages.Max()))
            .Append("</td><td>").Append(damages.Length == 0 ? "—" : N(damages.Min())).Append("</td></tr>");
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);
    private static string N(long? value) => value?.ToString("N0", CultureInfo.InvariantCulture) ?? "—";
    private static string Kind(ActionType type) => type switch
    {
        ActionType.Magic => "魔法",
        ActionType.Ability => "アビリティ",
        ActionType.WeaponSkill => "WS",
        _ => "未分類",
    };

    private const string SortScript = """
        (() => {
            const collator = new Intl.Collator('ja', { numeric: true });
            document.addEventListener('click', event => {
                const button = event.target.closest('button[data-sort]');
                if (!button) return;
                const table = button.closest('table');
                const header = button.closest('th');
                const column = Number(button.dataset.sort);
                const direction = header.getAttribute('aria-sort') === 'ascending' ? -1 : 1;
                const numeric = button.dataset.type === 'number';
                const body = table.tBodies[0];
                const rows = Array.from(body.rows);
                const value = row => {
                    const text = row.cells[column].textContent.trim();
                    if (!numeric) return text;
                    if (text === '—' || text === '') return null;
                    const number = Number(text.replaceAll(',', ''));
                    return Number.isFinite(number) ? number : null;
                };
                rows.sort((a, b) => {
                    const left = value(a), right = value(b);
                    if (left === null) return right === null ? 0 : 1;
                    if (right === null) return -1;
                    return direction * (numeric ? left - right : collator.compare(left, right));
                });
                rows.forEach(row => body.append(row));
                table.querySelectorAll('th[aria-sort]').forEach(th => th.setAttribute('aria-sort', 'none'));
                header.setAttribute('aria-sort', direction === 1 ? 'ascending' : 'descending');
            });
        })();
        """;

    private const string Script = """
        (() => {
            const dialog = document.getElementById('log-dialog');
            const text = document.getElementById('log-text');
            const timeline = document.querySelector('.timeline-scroll');
            let opener = null;
            let position = null;
            document.addEventListener('click', event => {
                const card = event.target.closest('button[data-log]');
                if (!card) return;
                const source = document.getElementById(card.dataset.log);
                if (!source) return;
                opener = card;
                position = [window.scrollX, window.scrollY, timeline.scrollLeft, timeline.scrollTop];
                text.textContent = source.content.textContent;
                dialog.showModal();
                dialog.scrollTop = 0;
            });
            dialog.addEventListener('close', () => {
                text.textContent = '';
                if (position) {
                    window.scrollTo(position[0], position[1]);
                    timeline.scrollLeft = position[2];
                    timeline.scrollTop = position[3];
                }
                opener?.focus({ preventScroll: true });
            });
        })();
        """;

    private const string Styles = """
        :root{font-family:'Segoe UI','Yu Gothic UI',sans-serif;color:#24354b;background:#f3f6fa;font-size:14px}
        *{box-sizing:border-box}body{margin:0}header,main{max-width:1500px;margin:auto;padding:14px 24px}
        header{background:#152b46;color:white;max-width:none}header h1{font-size:28px;margin:8px 0}header p{color:#d2dfef}.eyebrow{letter-spacing:.1em;font-size:12px}
        nav{display:flex;gap:24px}nav a{color:#dceaff}a{color:#175b9a}section{margin:14px 0;background:white;padding:14px;border-radius:12px;border:1px solid #dce3ec}header p{margin:4px 0}h2{margin:0 0 8px}
        h2{font-size:21px}h3{margin-top:26px}p,aside{line-height:1.8}aside,.warnings{padding:16px;background:#e9eff7;border-radius:8px}.warnings{margin-top:12px}
        .scroll{overflow:auto}table{border-collapse:separate;border-spacing:0;width:100%;font-size:13px}th,td{text-align:left;padding:10px 12px;border-bottom:1px solid #e1e7ef;vertical-align:top}
        th{background:#edf2f8;white-space:nowrap}td.number{font-variant-numeric:tabular-nums;white-space:nowrap}summary{cursor:pointer;color:#175b9a}pre{white-space:pre-wrap;overflow-wrap:anywhere;min-width:240px;max-width:650px}
        .timeline-scroll{overflow:auto;max-height:80vh;border:1px solid #dce3ec;border-radius:8px}.timeline{table-layout:fixed;min-width:780px}.timeline th:first-child{width:145px}
        .timeline thead th{position:sticky;top:0;z-index:3}.timeline tbody th{position:sticky;left:0;z-index:1}.timeline thead th:first-child{left:0;z-index:4}
        .timeline td{padding:2px 4px;border-right:1px solid #e1e7ef}.timeline tbody th{padding:4px 8px;line-height:1.3}.timeline small{display:block;font-size:11px;font-weight:normal;margin:1px 0}
        .card{display:flex;flex-wrap:wrap;align-items:baseline;gap:2px 8px;text-decoration:none;padding:3px 6px;line-height:1.35;border-left:4px solid #3067c4;background:#eaf1ff;border-radius:4px;color:#203650;overflow-wrap:anywhere}
        button.card{font:inherit;text-align:left;width:100%;border-top:0;border-right:0;border-bottom:0;cursor:pointer}.card:focus-visible{outline:2px solid #175b9a}

        .sort-button{font:inherit;font-weight:600;color:inherit;border:0;background:transparent;padding:0;cursor:pointer}.sort-button::after{content:' ↕';color:#63748a}th[aria-sort=ascending] .sort-button::after{content:' ▲'}th[aria-sort=descending] .sort-button::after{content:' ▼'}.sort-button:focus-visible{outline:2px solid #175b9a;outline-offset:3px}
        dialog{position:fixed;inset:0 0 0 auto;margin:0;width:min(520px,100vw);height:100dvh;max-height:100dvh;max-width:100vw;border:0;padding:20px;box-shadow:-8px 0 30px #152b4633;background:#fff;color:#24354b}
        dialog::backdrop{background:#152b4622}dialog form{position:sticky;top:0;text-align:right;background:#fff;padding-bottom:12px}dialog button{padding:8px 16px;cursor:pointer}dialog pre{min-width:0;max-width:none;line-height:1.7;margin:0;font-family:inherit}
        @media(max-width:600px){dialog{inset:0;width:100vw}header,main{padding:12px}}
        .card:target{outline:2px solid #b47700;outline-offset:-2px}.event-number{opacity:.7}.card strong{font-size:12px}.card span{font-size:11px;font-variant-numeric:tabular-nums}.card.magic{border-color:#8053b0;background:#f2ebfa}.card.ability{border-color:#258574;background:#e5f4ef}
        .session td{background:#dce6f2;font-size:11px}.timeline tr:target td,tr:target>td{background:#fff2c9}details p{font-size:12px}
        @media print{.timeline-scroll{max-height:none;overflow:visible}.scroll{overflow:visible}header,main{padding:10px}section{padding:10px}.timeline thead th,.timeline tbody th{position:static}}
        """;
}
