namespace SimT.Contracts.Time;

/// <summary>
/// The one place that turns a date into text and back.
///
/// A dedicated codec exists because this calendar is not the Gregorian one: years have twelve
/// 30-day months and no leap day, so <see cref="DateTime"/> would reject valid game dates and,
/// worse, silently produce a different date when a day is added across a year boundary
/// (184-12-30 + 1 is 185-01-01 here, 184-12-31 for DateTime). Keeping the format in one type
/// also means the save file, the ledger and the screen cannot drift apart.
/// </summary>
public static class GameDateCodec
{
    /// <summary>Canonical on-disk form: dash separated, month and day zero-padded, year plain.</summary>
    public const string CanonicalFormat = "YYYY-MM-DD";

    private static readonly char[] Separators = ['-', '/', '.'];

    private static readonly string[] MonthNames =
    [
        "Giêng", "Hai", "Ba", "Tư", "Năm", "Sáu", "Bảy", "Tám", "Chín", "Mười", "Mười Một", "Chạp"
    ];

    public static string Format(GameDate date) => $"{date.Year}-{date.Month:00}-{date.Day:00}";

    /// <summary>
    /// Accepts the canonical form and the loose forms people type: <c>184-5-2</c>,
    /// <c>184/05/02</c>, <c>184.5.2</c>. Month and day must be in range for this calendar.
    /// </summary>
    public static bool TryParse(string? text, out GameDate date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split(Separators, StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var day))
        {
            return false;
        }

        if (year < 1 || month is < 1 or > GameDate.MonthsPerYear || day is < 1 or > GameDate.DaysPerMonth)
        {
            return false;
        }

        date = new GameDate(year, month, day);
        return true;
    }

    public static GameDate Parse(string? text) =>
        TryParse(text, out var date)
            ? date
            : throw new SimException("date.invalid", $"'{text}' is not a valid date; expected {CanonicalFormat} (month 1-12, day 1-30).");

    /// <summary>Vietnamese table form: "02/05/184" — what a player reads in a list.</summary>
    public static string ShortVi(GameDate date) => $"{date.Day:00}/{date.Month:00}/{date.Year}";

    /// <summary>Long form for the screen: "Ngày 2 tháng Năm, năm 184".</summary>
    public static string DescribeVi(GameDate date) =>
        $"Ngày {date.Day} tháng {MonthNames[date.Month - 1]}, năm {date.Year}";

    public static string MonthNameVi(int month) =>
        month is >= 1 and <= GameDate.MonthsPerYear ? MonthNames[month - 1] : month.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string SeasonVi(int month) => month switch
    {
        <= 3 => "Xuân",
        <= 6 => "Hạ",
        <= 9 => "Thu",
        _ => "Đông"
    };

    public static string SeasonKey(int month) => month switch
    {
        <= 3 => "spring",
        <= 6 => "summer",
        <= 9 => "autumn",
        _ => "winter"
    };

    /// <summary>"Xuân · Tháng Hai · năm 184" — the header line of the shell.</summary>
    public static string DescribeSeason(GameDate date) =>
        $"{SeasonVi(date.Month)} · Tháng {MonthNameVi(date.Month)} · năm {date.Year}";

    /// <summary>Historical Han / Three Kingdoms reign era title.</summary>
    public static string EraVi(int year) => year switch
    {
        <= 183 => "Đông Hán · Quang Hòa",
        184 => "Đông Hán · Trung Bình Nguyên Niên",
        <= 189 => $"Đông Hán · Trung Bình Năm {year - 183}",
        190 => "Đông Hán · Sơ Bình Nguyên Niên",
        <= 193 => $"Đông Hán · Sơ Bình Năm {year - 189}",
        194 => "Đông Hán · Hưng Bình Nguyên Niên",
        195 => "Đông Hán · Hưng Bình Năm 2",
        196 => "Đông Hán · Kiến An Nguyên Niên",
        <= 220 => $"Đông Hán · Kiến An Năm {year - 195}",
        _ => $"Tam Quốc · Năm {year}"
    };

    /// <summary>Seasonal visual icon glyph.</summary>
    public static string SeasonGlyph(int month) => month switch
    {
        <= 3 => "🌸",
        <= 6 => "☀️",
        <= 9 => "🍂",
        _ => "❄️"
    };
}
