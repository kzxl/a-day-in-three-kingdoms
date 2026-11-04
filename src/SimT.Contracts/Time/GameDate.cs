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

    /// <summary>True when the month and day exist in this calendar; years start at 1.</summary>
    public bool IsValid =>
        Year >= 1 && Month is >= 1 and <= MonthsPerYear && Day is >= 1 and <= DaysPerMonth;

    /// <summary>Validating factory. Use it wherever a date enters from outside the kernel.</summary>
    public static GameDate Create(int year, int month, int day)
    {
        var date = new GameDate(year, month, day);
        if (!date.IsValid)
        {
            throw new SimException(
                "date.invalid",
                $"Year {year}, month {month}, day {day} is not a date in a {DaysPerMonth}-day, {MonthsPerYear}-month year.");
        }

        return date;
    }

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

    public override string ToString() => GameDateCodec.Format(this);
}
