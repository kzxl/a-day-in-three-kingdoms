using SimT.Contracts;
using SimT.Contracts.Time;

namespace SimT.Core.Tests;

/// <summary>
/// The calendar is 12 months of 30 days with no leap day, so it has its own codec. These tests
/// pin the format, the loose inputs a player might type, and the boundaries DateTime would get
/// wrong.
/// </summary>
public sealed class GameDateCodecTests
{
    [Fact]
    public void Format_is_zero_padded_and_dash_separated()
    {
        Assert.Equal("184-01-01", GameDateCodec.Format(new GameDate(184, 1, 1)));
        Assert.Equal("184-12-30", GameDateCodec.Format(new GameDate(184, 12, 30)));
        Assert.Equal("7-05-02", GameDateCodec.Format(new GameDate(7, 5, 2)));
    }

    [Theory]
    [InlineData("184-01-01")]
    [InlineData("184-1-1")]
    [InlineData("184/01/01")]
    [InlineData("184.1.1")]
    [InlineData(" 184-1-1 ")]
    public void Parse_accepts_the_canonical_form_and_loose_separators(string text)
    {
        Assert.True(GameDateCodec.TryParse(text, out var date));
        Assert.Equal(new GameDate(184, 1, 1), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("184-01")]
    [InlineData("184-01-01-01")]
    [InlineData("0000-01-01")]
    [InlineData("184-00-01")]
    [InlineData("184-13-01")]
    [InlineData("184-01-00")]
    [InlineData("184-01-31")]
    [InlineData("abc-01-01")]
    [InlineData("-184-01-01")]
    [InlineData("184--1")]
    public void Parse_rejects_dates_this_calendar_cannot_hold(string text)
    {
        Assert.False(GameDateCodec.TryParse(text, out _));
        var error = Assert.Throws<SimException>(() => GameDateCodec.Parse(text));
        Assert.Equal("date.invalid", error.Code);
    }

    [Fact]
    public void Round_trip_through_text_keeps_the_date()
    {
        var original = new GameDate(184, 12, 30);
        var parsed = GameDateCodec.Parse(GameDateCodec.Format(original));

        Assert.Equal(original, parsed);
        Assert.Equal(original.DayIndex, parsed.DayIndex);
    }

    [Fact]
    public void Adding_a_day_across_the_year_end_follows_the_360_day_calendar()
    {
        // DateTime would answer 184-12-31 here; this calendar has no such day.
        Assert.Equal(new GameDate(185, 1, 1), new GameDate(184, 12, 30).AddDays(1));
        Assert.Equal(new GameDate(184, 1, 2), new GameDate(184, 1, 1).AddDays(1));
    }

    [Fact]
    public void Create_rejects_impossible_dates()
    {
        Assert.Throws<SimException>(() => GameDate.Create(184, 2, 31));
        Assert.Throws<SimException>(() => GameDate.Create(0, 1, 1));
        Assert.Equal(new GameDate(184, 2, 30), GameDate.Create(184, 2, 30));
    }

    [Fact]
    public void The_long_form_names_the_month_in_vietnamese()
    {
        Assert.Equal("Ngày 2 tháng Năm, năm 184", GameDateCodec.DescribeVi(new GameDate(184, 5, 2)));
        Assert.Equal("Ngày 30 tháng Chạp, năm 184", GameDateCodec.DescribeVi(new GameDate(184, 12, 30)));
    }

    [Fact]
    public void The_header_line_names_the_season()
    {
        Assert.Equal("Xuân · Tháng Giêng · năm 184", GameDateCodec.DescribeSeason(new GameDate(184, 1, 1)));
        Assert.Equal("Thu · Tháng Chín · năm 184", GameDateCodec.DescribeSeason(new GameDate(184, 9, 10)));
        Assert.Equal("Đông · Tháng Chạp · năm 184", GameDateCodec.DescribeSeason(new GameDate(184, 12, 1)));
    }

    [Fact]
    public void The_short_form_is_the_vietnamese_table_date()
    {
        Assert.Equal("02/05/184", GameDateCodec.ShortVi(new GameDate(184, 5, 2)));
        Assert.Equal("27/01/184", GameDateCodec.ShortVi(new GameDate(184, 1, 27)));
    }

    [Fact]
    public void Every_month_name_is_defined()
    {
        for (var month = 1; month <= GameDate.MonthsPerYear; month++)
        {
            Assert.False(string.IsNullOrWhiteSpace(GameDateCodec.MonthNameVi(month)));
        }
    }
}
