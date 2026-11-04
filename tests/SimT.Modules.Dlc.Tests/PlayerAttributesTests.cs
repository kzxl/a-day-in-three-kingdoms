using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class PlayerAttributesTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IPlayerAttributes PlayerAttrs(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

    [Fact]
    public void Protagonist_starts_with_canonical_twelve_attributes()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var a = attrs.Attributes;
        Assert.Equal(55, a.War);
        Assert.Equal(50, a.Ldr);
        Assert.Equal(60, a.Int);
        Assert.Equal(50, a.Pol);
        Assert.Equal(55, a.Dip);
        Assert.Equal(65, a.Com); // Merchant focus
        Assert.Equal(55, a.Log);
        Assert.Equal(50, a.Cra);
        Assert.Equal(60, a.Con); // Constitution
        Assert.Equal(65, a.Wil); // Willpower
        Assert.Equal(60, a.Prc); // Perception
        Assert.Equal(70, a.Lck); // Fortune
    }

    [Fact]
    public void Protagonist_vitals_are_derived_dynamically()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var v = attrs.Vitals;
        Assert.Equal(100, v.Health);
        Assert.Equal(0, v.Fatigue);
        Assert.Equal(100, v.Morale);
        Assert.Equal(0, v.Stress);

        // MaxHP = 80 + Con*1.5 + War*0.5 = 80 + 90 + 27 = 197
        Assert.True(v.MaxHp > 150);

        // MaxStamina = 70 + Con*1.2 + War*0.3 + Health/10 = 70 + 72 + 16 + 10 = 168
        Assert.True(v.MaxStamina > 150);
        Assert.Equal(v.MaxStamina, v.CurrentStamina);
    }

    [Fact]
    public void Applying_fatigue_and_resting_mutates_vitals_correctly()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        // Work spends 35 fatigue
        attrs.ApplyFatigue(35);
        Assert.Equal(35, attrs.Vitals.Fatigue);
        Assert.Equal(attrs.Vitals.MaxStamina - 35, attrs.Vitals.CurrentStamina);

        // 2 days of rest recovers 40 fatigue
        attrs.Rest(2);
        Assert.Equal(0, attrs.Vitals.Fatigue);
        Assert.Equal(attrs.Vitals.MaxStamina, attrs.Vitals.CurrentStamina);
    }

    [Fact]
    public void Gaining_exp_levels_up_stat_when_crossing_hundred()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var comBefore = attrs.Attributes.Com;

        // Gain 150 COM EXP: should gain +1 COM and 50 residual EXP
        attrs.AddExp("com", 150);

        Assert.Equal(comBefore + 1, attrs.Attributes.Com);
    }

    [Fact]
    public void Protagonist_starts_with_honorable_social_standing()
    {
        using var kernel = _host.Boot();
        var attrs = PlayerAttrs(kernel);

        var s = attrs.Social;
        Assert.Equal(100, s.Fame);
        Assert.Equal(50, s.Virtue);
        Assert.Equal(50, s.Credibility);
    }
}
