using System.Text.Json;
using SimT.Contracts.Audio;
using SimT.Contracts.Scenes;
using SimT.Host.Wpf.Audio;

namespace SimT.Architecture.Tests;

public sealed class AudioTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "simt-audio-test-" + Guid.NewGuid().ToString("N"));

    public AudioTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Catalogue_missing_key_file_and_invalid_json_are_silent()
    {
        Assert.Null(new AudioCatalog(_root).Find(AudioBus.Sfx, "ui.click"));
        File.WriteAllText(Path.Combine(_root, "audio.json"), "invalid json");
        Assert.Null(new AudioCatalog(_root).Find(AudioBus.Music, "city"));
        File.WriteAllText(Path.Combine(_root, "audio.json"), """
            {"sfx":{"ui.click":"missing.ogg", "unsafe":"../outside.ogg"}, "music":null}
            """);
        var catalog = new AudioCatalog(_root);
        Assert.Null(catalog.Find(AudioBus.Sfx, "ui.click"));
        Assert.Null(catalog.Find(AudioBus.Sfx, "unknown"));
        Assert.Null(catalog.Find(AudioBus.Sfx, "unsafe"));
        Assert.Null(catalog.Find(AudioBus.Music, "city"));
    }

    [Fact]
    public void Catalogue_routes_identical_keys_to_their_bus_and_rejects_existing_outside_file()
    {
        var outside = Path.GetTempFileName();
        try
        {
            File.WriteAllText(Path.Combine(_root, "music.ogg"), "fixture");
            File.WriteAllText(Path.Combine(_root, "ambience.ogg"), "fixture");
            File.WriteAllText(Path.Combine(_root, "audio.json"), JsonSerializer.Serialize(new
            {
                music = new Dictionary<string, string> { ["same"] = "music.ogg" },
                ambience = new Dictionary<string, string> { ["same"] = "ambience.ogg" },
                sfx = new Dictionary<string, string> { ["outside"] = outside }
            }));
            var catalog = new AudioCatalog(_root);
            Assert.Equal(Path.Combine(_root, "music.ogg"), catalog.Find(AudioBus.Music, "same"));
            Assert.Equal(Path.Combine(_root, "ambience.ogg"), catalog.Find(AudioBus.Ambience, "same"));
            Assert.Null(catalog.Find(AudioBus.Sfx, "outside"));
        }
        finally { File.Delete(outside); }
    }

    [Fact]
    public void Settings_roundtrip_all_buses_and_mute_without_losing_levels()
    {
        var file = Path.Combine(_root, "profile", "audio.json");
        var original = new AudioSettings { Master = .73f, Music = .42f, Sfx = .37f, Ambience = .29f, Muted = true };
        original.Save(file);
        var loaded = AudioSettings.Load(file);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(loaded));
        var engine = new RecordingAudio();
        loaded.Apply(engine);
        Assert.Equal(0, engine.GetVolume(AudioBus.Master));
        Assert.Equal(.42f, engine.GetVolume(AudioBus.Music));
        loaded.Muted = false;
        loaded.Apply(engine);
        Assert.Equal(.73f, engine.GetVolume(AudioBus.Master));
    }

    [Fact]
    public void Settings_defaults_corruption_and_out_of_range_values_are_safe()
    {
        var file = Path.Combine(_root, "audio.json");
        var defaults = AudioSettings.Load(file);
        Assert.Equal(.6f, defaults.Music);
        Assert.Equal(.5f, defaults.Ambience);
        Assert.Equal(.8f, defaults.Sfx);
        File.WriteAllText(file, "broken");
        Assert.Equal(1, AudioSettings.Load(file).Master);
        File.WriteAllText(file, "{\"Master\":-2,\"Music\":3}");
        Assert.Equal(0, AudioSettings.Load(file).Master);
        Assert.Equal(1, AudioSettings.Load(file).Music);
        Assert.Equal(0, AudioSettings.Clamp(float.NaN));
    }

    [Fact]
    public void Scene_policy_selects_title_city_map_duel_event_and_debate_music()
    {
        Assert.Equal("city", AudioScenePolicy.Select(null).Music);
        Assert.Equal("city", AudioScenePolicy.Select(JsonSerializer.Deserialize<CityScene>("{}")! with { Season = "spring", WeatherEffect = "none" }).Music);
        Assert.Equal("map", AudioScenePolicy.Select(JsonSerializer.Deserialize<MapScene>("{}")).Music);
        Assert.Equal("duel", AudioScenePolicy.Select(JsonSerializer.Deserialize<DuelScene>("{}")).Music);
        Assert.Equal("city", AudioScenePolicy.Select(JsonSerializer.Deserialize<DebateScene>("{}")).Music);
        var evt = JsonSerializer.Deserialize<EventScene>("{}");
        Assert.Equal("event", AudioScenePolicy.Select(evt).Music);
        Assert.Empty(AudioScenePolicy.Select(evt).Ambience);
        Assert.Equal(["war"], AudioScenePolicy.Select(evt, hasWar: true).Ambience);
    }

    [Fact]
    public void Late_debate_layers_drums_while_keeping_city_music_and_stops_at_finish()
    {
        var scene = JsonSerializer.Deserialize<DebateScene>("{}")! with { Turn = 4, MaxTurns = 5 };
        Assert.Equal("city", AudioScenePolicy.Select(scene).Music);
        Assert.Equal(["war"], AudioScenePolicy.Select(scene).Ambience);
        Assert.Empty(AudioScenePolicy.Select(scene with { IsFinished = true }).Ambience);
        Assert.Empty(AudioScenePolicy.Select(scene with { Turn = 1 }).Ambience);
    }

    [Theory]
    [InlineData("Mùa Xuân", "none", "birds")]
    [InlineData("Mùa Hạ", "rain", "birds,rain")]
    [InlineData("Mùa Đông", "snow", "wind")]
    [InlineData("Mùa Thu", "rain", "rain")]
    [InlineData("autumn", "none", "")]
    public void City_ambience_layers_season_weather_and_optional_market(string season, string weather, string expected)
    {
        var layers = AudioScenePolicy.CityAmbience(season, weather);
        Assert.Equal("ambience.market", layers[0]);
        Assert.Equal(expected, string.Join(",", layers.Skip(1)));
    }

    [Fact]
    public void Audio_packages_are_declared_only_by_the_host()
    {
        foreach (var project in RepoLayout.Sources(".csproj").Where(p => !p.Contains("_wpftmp", StringComparison.OrdinalIgnoreCase)))
        {
            var source = File.ReadAllText(project);
            if (Path.GetFileName(project) == "SimT.Host.Wpf.csproj")
            {
                Assert.Contains("PackageReference Include=\"NAudio\"", source);
                Assert.Contains("PackageReference Include=\"NVorbis\"", source);
            }
            else
            {
                Assert.DoesNotContain("PackageReference Include=\"NAudio\"", source);
                Assert.DoesNotContain("PackageReference Include=\"NVorbis\"", source);
            }
        }
    }

    private sealed class RecordingAudio : IAudioEngine
    {
        private readonly Dictionary<AudioBus, float> _levels = new();
        public void SetVolume(AudioBus bus, float volume) => _levels[bus] = volume;
        public float GetVolume(AudioBus bus) => _levels.GetValueOrDefault(bus);
        public void PlaySfx(string cueKey, float pitch = 1, float volume = 1) { }
        public void PlayMusic(string trackKey, bool loop = true, float fadeDurationSeconds = 1) { }
        public void StopMusic(float fadeDurationSeconds = .5f) { }
    }
}
