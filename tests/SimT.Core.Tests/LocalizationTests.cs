using SimT.Contracts.Localization;
using SimT.Core.Localization;
using Xunit;

namespace SimT.Core.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void Default_locale_resolves_vietnamese_with_fallback()
    {
        var loc = new LocalizationService("vi");

        Assert.Equal("vi", loc.CurrentLocale);
        Assert.Equal("SimT — Nhất Nhật Tam Quốc", loc.Get("app.title"));
        Assert.Equal("Mùa Xuân", loc.Get("time.spring"));

        // Non-existent key returns key itself
        Assert.Equal("unknown.key", loc.Get("unknown.key"));
    }

    [Fact]
    public void Switching_locale_changes_active_translation_and_fires_event()
    {
        var loc = new LocalizationService("vi");
        LanguageInfo? firedLang = null;
        loc.LanguageChanged += lang => firedLang = lang;

        var switched = loc.SetLocale("en");
        Assert.True(switched);
        Assert.Equal("en", loc.CurrentLocale);
        Assert.NotNull(firedLang);
        Assert.Equal("en", firedLang.Code);

        Assert.Equal("SimT — A Day in the Three Kingdoms", loc.Get("app.title"));
        Assert.Equal("Day", loc.Get("time.day"));
        Assert.Equal("Spring", loc.Get("time.spring"));
    }

    [Fact]
    public void Dynamic_bilingual_mode_concatenates_distinct_translations()
    {
        var loc = new LocalizationService("vi")
        {
            IsBilingualMode = true,
            SecondaryLocale = "en"
        };

        loc.RegisterStrings("vi", new Dictionary<string, string>
        {
            ["hero.guanyu"] = "Quan Vũ",
            ["skill.blade"] = "Yển Nguyệt Đao"
        });

        loc.RegisterStrings("en", new Dictionary<string, string>
        {
            ["hero.guanyu"] = "Guan Yu",
            ["skill.blade"] = "Crescent Blade"
        });

        var bilingualHero = loc.GetBilingual("hero.guanyu");
        Assert.Equal("Quan Vũ · Guan Yu", bilingualHero);

        var bilingualSkill = loc.GetBilingual("skill.blade");
        Assert.Equal("Yển Nguyệt Đao · Crescent Blade", bilingualSkill);

        // When bilingual mode is false, returns primary only
        loc.IsBilingualMode = false;
        Assert.Equal("Quan Vũ", loc.GetBilingual("hero.guanyu"));
    }

    [Fact]
    public void Subdirectory_modular_loading_merges_multiple_json_files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "simt_lang_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create languages/ja/manifest.json
            var jaDir = Path.Combine(tempDir, "ja");
            Directory.CreateDirectory(jaDir);

            File.WriteAllText(Path.Combine(jaDir, "manifest.json"), """
                {
                  "code": "ja",
                  "displayName": "日本語",
                  "nativeName": "日本語"
                }
                """);

            // Create languages/ja/core.json
            File.WriteAllText(Path.Combine(jaDir, "core.json"), """
                {
                  "app.title": "三国志の一日",
                  "time.day": "日"
                }
                """);

            // Create languages/ja/module.json
            File.WriteAllText(Path.Combine(jaDir, "module.json"), """
                {
                  "vault.name": "両替商・銭荘",
                  "item.silver": "銀両"
                }
                """);

            var loc = new LocalizationService("vi");
            var count = loc.LoadPackagesFromDirectory(tempDir);
            Assert.True(count >= 1);

            // Switch to dynamically loaded Japanese
            var switched = loc.SetLocale("ja");
            Assert.True(switched);
            Assert.Equal("ja", loc.CurrentLocale);

            // Both core.json and module.json keys must be available in the same package!
            Assert.Equal("三国志の一日", loc.Get("app.title"));
            Assert.Equal("両替商・銭荘", loc.Get("vault.name"));
            Assert.Equal("銀両", loc.Get("item.silver"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
