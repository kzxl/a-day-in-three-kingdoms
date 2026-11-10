using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

public sealed record SectionTabGroup(
    string Id,
    string Label,
    IReadOnlyList<int> SectionIndices,
    int TotalRows);

/// <summary>
/// Partitions dense multi-section screens into scannable functional tab groups
/// to prevent horizontal tab sprawl and vertical scroll fatigue.
/// </summary>
public static class SectionTabPartitioner
{
    public static IReadOnlyList<SectionTabGroup> BuildTabGroups(
        string screenId,
        IReadOnlyList<ScreenSection> sections)
    {
        if (sections.Count <= 1)
            return Array.Empty<SectionTabGroup>();

        if (string.Equals(screenId, "politics.court", StringComparison.Ordinal) && sections.Count > 3)
            return BuildCourtTabGroups(sections);

        if (string.Equals(screenId, "market.stall", StringComparison.Ordinal) && sections.Count > 3)
            return BuildMarketTabGroups(sections);

        if (string.Equals(screenId, "social.network", StringComparison.Ordinal) && sections.Count > 3)
            return BuildSocialTabGroups(sections);

        var classic = new List<SectionTabGroup>(sections.Count + 1)
        {
            new("all", "Tất cả", Enumerable.Range(0, sections.Count).ToArray(), sections.Sum(s => s.Rows.Count))
        };
        for (var i = 0; i < sections.Count; i++)
        {
            classic.Add(new SectionTabGroup($"sec:{i}", sections[i].Heading, [i], sections[i].Rows.Count));
        }
        return classic;
    }

    public static int FindTabIndexForKeyword(
        IReadOnlyList<SectionTabGroup> groups,
        IReadOnlyList<ScreenSection> sections,
        string keyword)
    {
        if (groups.Count == 0 || string.IsNullOrWhiteSpace(keyword))
            return 0;

        for (var g = 0; g < groups.Count; g++)
        {
            if (groups[g].Id == "all")
                continue;
            foreach (var secIdx in groups[g].SectionIndices)
            {
                if (secIdx >= 0 && secIdx < sections.Count &&
                    sections[secIdx].Heading.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return g;
                }
            }
        }
        return 0;
    }

    private static IReadOnlyList<SectionTabGroup> BuildCourtTabGroups(IReadOnlyList<ScreenSection> sections)
    {
        var hierarchy = new List<int>();
        var intrigue = new List<int>();
        var fief = new List<int>();
        var chronicle = new List<int>();

        for (var i = 0; i < sections.Count; i++)
        {
            var h = sections[i].Heading;
            if (ContainsAny(h, "Dòm Ngó", "Cây To Đón Gió", "Sóng Ngầm", "Tam Đại Thế Lực", "Ngự Sử Đài", "Thanh Sát", "Cứu Tế"))
                intrigue.Add(i);
            else if (ContainsAny(h, "Quan Chế", "Chư Hầu", "Chính Sự & Trị An"))
                hierarchy.Add(i);
            else if (ContainsAny(h, "Thái Thú", "Phong Ấp", "Chiêu Hiền", "Biến Cố", "Chiến Báo", "Đại Công Trình", "Xuất Chinh"))
                fief.Add(i);
            else
                chronicle.Add(i);
        }

        var result = new List<SectionTabGroup>();
        AddIfNonEmpty(result, sections, "court:hierarchy", "👑 Quan Chế & Chư Hầu", hierarchy);
        AddIfNonEmpty(result, sections, "court:intrigue", "🏛️ Sóng Ngầm & Quân Phiệt", intrigue);
        AddIfNonEmpty(result, sections, "court:fief", "🏰 Thái Thú Phong Ấp", fief);
        AddIfNonEmpty(result, sections, "court:chronicle", "📜 Sớ Tấu & Biên Niên", chronicle);
        result.Add(new SectionTabGroup("all", "📋 Tất cả", Enumerable.Range(0, sections.Count).ToArray(), sections.Sum(s => s.Rows.Count)));
        return result;
    }

    private static IReadOnlyList<SectionTabGroup> BuildMarketTabGroups(IReadOnlyList<ScreenSection> sections)
    {
        var bazaar = new List<int>();
        var warfare = new List<int>();
        var special = new List<int>();

        for (var i = 0; i < sections.Count; i++)
        {
            var h = sections[i].Heading;
            if (ContainsAny(h, "Lũng Đoạn", "Buôn Lậu", "Áp Tải"))
                warfare.Add(i);
            else if (ContainsAny(h, "Sạp Chuyên Doanh", "Mỏ Khoáng", "Nguồn Cung"))
                special.Add(i);
            else
                bazaar.Add(i);
        }

        var result = new List<SectionTabGroup>();
        AddIfNonEmpty(result, sections, "market:bazaar", "🏮 Sạp Hàng Thành Trấn", bazaar);
        AddIfNonEmpty(result, sections, "market:warfare", "⚔️ Thương Chiến & Buôn Lậu", warfare);
        AddIfNonEmpty(result, sections, "market:special", "⛰️ Đặc Sản & Sạp NPC", special);
        result.Add(new SectionTabGroup("all", "📋 Tất cả", Enumerable.Range(0, sections.Count).ToArray(), sections.Sum(s => s.Rows.Count)));
        return result;
    }

    private static IReadOnlyList<SectionTabGroup> BuildSocialTabGroups(IReadOnlyList<ScreenSection> sections)
    {
        var retainers = new List<int>();
        var relations = new List<int>();
        var skills = new List<int>();

        for (var i = 0; i < sections.Count; i++)
        {
            var h = sections[i].Heading;
            if (ContainsAny(h, "Kim Lan", "Gia Thần", "Dã Tâm", "Biến Cố"))
                retainers.Add(i);
            else if (ContainsAny(h, "Hòm Thư", "Danh Sĩ", "Quan Hệ", "Bằng Hữu", "Nhân Duyên"))
                relations.Add(i);
            else
                skills.Add(i);
        }

        var result = new List<SectionTabGroup>();
        AddIfNonEmpty(result, sections, "social:retainers", "🛡️ Gia Thần & Kim Lan", retainers);
        AddIfNonEmpty(result, sections, "social:relations", "✉️ Thư Tín & Nhân Sĩ", relations);
        AddIfNonEmpty(result, sections, "social:skills", "⚡ Tuyệt Kỹ & Tâm Pháp", skills);
        result.Add(new SectionTabGroup("all", "📋 Tất cả", Enumerable.Range(0, sections.Count).ToArray(), sections.Sum(s => s.Rows.Count)));
        return result;
    }

    private static void AddIfNonEmpty(
        List<SectionTabGroup> target,
        IReadOnlyList<ScreenSection> sections,
        string id,
        string label,
        List<int> indices)
    {
        if (indices.Count == 0)
            return;
        var rows = indices.Sum(idx => sections[idx].Rows.Count);
        target.Add(new SectionTabGroup(id, label, indices, rows));
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
