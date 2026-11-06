using SimT.Contracts.Playing;

namespace SimT.Contracts.Localization;

/// <summary>
/// Bridge adapters enabling dynamic multilingual and bilingual resolution for domain entities
/// without breaking backward compatibility or altering persistent schemas.
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// Resolves an NPC's display name using its unique alphanumeric code (e.g. "npc.npc_0005.name" or "npc.guan_yu.name").
    /// Falls back to the canonical NameVi if translation is not found in the active catalog.
    /// </summary>
    public static string LocalizeName(this NpcDef npc, ILocalizationService loc, bool? bilingual = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(loc);

        var key = $"npc.{npc.Code.ToLowerInvariant()}.name";
        var isBilingual = bilingual ?? loc.IsBilingualMode;

        if (isBilingual)
        {
            var res = loc.GetBilingual(key);
            if (!res.Equals(key, StringComparison.OrdinalIgnoreCase)) return res;
        }
        else
        {
            var res = loc.Get(key);
            if (!res.Equals(key, StringComparison.OrdinalIgnoreCase)) return res;
        }

        return npc.NameVi;
    }

    /// <summary>
    /// Resolves an NPC's biography in the active language with fallback.
    /// </summary>
    public static string? LocalizeBio(this NpcDef npc, ILocalizationService loc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(loc);

        var key = $"npc.{npc.Code.ToLowerInvariant()}.bio";
        var res = loc.Get(key);
        return res.Equals(key, StringComparison.OrdinalIgnoreCase) ? npc.BioVi : res;
    }

    /// <summary>
    /// Resolves a skill's display name using its code (e.g. "skill.vo_thanh.name").
    /// </summary>
    public static string LocalizeName(this NpcSkillDef skill, ILocalizationService loc, bool? bilingual = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(loc);

        var key = $"skill.{skill.Code.ToLowerInvariant()}.name";
        var isBilingual = bilingual ?? loc.IsBilingualMode;

        if (isBilingual)
        {
            var res = loc.GetBilingual(key);
            if (!res.Equals(key, StringComparison.OrdinalIgnoreCase)) return res;
        }
        else
        {
            var res = loc.Get(key);
            if (!res.Equals(key, StringComparison.OrdinalIgnoreCase)) return res;
        }

        return skill.NameVi;
    }

    /// <summary>
    /// Resolves a skill's description in the active language.
    /// </summary>
    public static string LocalizeDesc(this NpcSkillDef skill, ILocalizationService loc)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(loc);

        var key = $"skill.{skill.Code.ToLowerInvariant()}.desc";
        var res = loc.Get(key);
        return res.Equals(key, StringComparison.OrdinalIgnoreCase) ? skill.Description : res;
    }

    /// <summary>
    /// Resolves a citadel/settlement's display name using its ID or code (e.g. "citadel.1.name" or "citadel.luoyang.name").
    /// </summary>
    public static string LocalizeName(this CitadelDef citadel, ILocalizationService loc, bool? bilingual = null)
    {
        ArgumentNullException.ThrowIfNull(citadel);
        ArgumentNullException.ThrowIfNull(loc);

        var keyById = $"citadel.{citadel.Id}.name";
        var isBilingual = bilingual ?? loc.IsBilingualMode;

        if (isBilingual)
        {
            var res = loc.GetBilingual(keyById);
            if (!res.Equals(keyById, StringComparison.OrdinalIgnoreCase)) return res;
        }
        else
        {
            var res = loc.Get(keyById);
            if (!res.Equals(keyById, StringComparison.OrdinalIgnoreCase)) return res;
        }

        return citadel.NameVi;
    }

    /// <summary>
    /// Resolves an item's display name using its code (e.g. "item.gao.name").
    /// </summary>
    public static string LocalizeName(this ItemDef item, ILocalizationService loc, bool? bilingual = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(loc);

        var key = $"item.{item.Code.ToLowerInvariant()}.name";
        var isBilingual = bilingual ?? loc.IsBilingualMode;

        if (isBilingual)
        {
            var res = loc.GetBilingual(key);
            if (!res.Equals(key, StringComparison.OrdinalIgnoreCase)) return res;
        }
        else
        {
            var res = loc.Get(key);
            if (!res.Equals(key, StringComparison.OrdinalIgnoreCase)) return res;
        }

        return item.NameVi;
    }
}
