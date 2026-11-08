using SimT.Contracts.Data;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    private IReadOnlyDictionary<string, IReadOnlyList<string>> ReadEventExpressions(string code)
    {
        var rows = _db.Query(
            "SELECT npc_code, line_index, expression FROM so_event_portrait_expression WHERE event_code = @code ORDER BY npc_code, line_index",
            r => (Npc: r.GetString(0), Index: r.GetInt32(1), Expression: r.GetString(2)), DbArgs.One("@code", code));
        return rows.GroupBy(r => r.Npc).ToDictionary(g => g.Key, g =>
        {
            var lines = Enumerable.Repeat("normal", g.Max(r => r.Index) + 1).ToArray();
            foreach (var row in g) lines[row.Index] = row.Expression;
            return (IReadOnlyList<string>)lines;
        });
    }
}
