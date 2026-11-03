namespace SimT.Contracts.Time;

/// <summary>
/// Calendar date inside the simulation. 30-day months, 12-month years, already fixed by
/// the legacy configuration; the numbers live here as constants and are cross-checked
/// against <c>assets/config/game-constants.json</c> by an architecture test.
/// </summary>
public readonly record struct GameDate(int Year, int Month, int Day) : IComparable<GameDate>
{
    public const int DaysPerMonth = 30;
    public const int MonthsPerYear = 12;
    public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

    public static GameDate Start { get; } = new(1, 1, 1);

    public long DayIndex => ((long)(Year - 1) * DaysPerYear) + ((Month - 1) * DaysPerMonth) + (Day - 1);

    public bool IsMonthStart => Day == 1;

    public bool IsYearStart => IsMonthStart && Month == 1;

    public static GameDate FromDayIndex(long index)
    {
        if (index < 0)
        {
            throw new SimException("date.negative", $"Day index {index} is before campaign start.");
        }

        var year = (int)(index / DaysPerYear) + 1;
        var rest = (int)(index % DaysPerYear);
        return new GameDate(year, (rest / DaysPerMonth) + 1, (rest % DaysPerMonth) + 1);
    }

    public GameDate AddDays(int days) => FromDayIndex(DayIndex + days);

    public GameDate AddMonths(int months)
    {
        var total = (Year * MonthsPerYear) + (Month - 1) + months;
        return new GameDate(total / MonthsPerYear, (total % MonthsPerYear) + 1, Math.Min(Day, DaysPerMonth));
    }

    public int CompareTo(GameDate other) => DayIndex.CompareTo(other.DayIndex);

    public static bool operator <(GameDate left, GameDate right) => left.DayIndex < right.DayIndex;

    public static bool operator >(GameDate left, GameDate right) => left.DayIndex > right.DayIndex;

    public static bool operator <=(GameDate left, GameDate right) => left.DayIndex <= right.DayIndex;

    public static bool operator >=(GameDate left, GameDate right) => left.DayIndex >= right.DayIndex;

    public override string ToString() => $"{Year:0000}-{Month:00}-{Day:00}";
}
