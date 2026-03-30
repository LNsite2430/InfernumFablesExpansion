using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace InfernumFablesExpansion;

public sealed class InfernumFablesExpansionConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [ReloadRequired]
    [DefaultValue(IntroCoverageMode.AllBossesOn)]
    public IntroCoverageMode IntroCoverage { get; set; } = IntroCoverageMode.AllBossesOn;

    public static bool ShouldPlayGenericIntroFor(NPC npc)
    {
        IntroCoverageMode mode = ModContent.GetInstance<InfernumFablesExpansionConfig>().IntroCoverage;
        return mode switch
        {
            IntroCoverageMode.AllBossesOff => false,
            IntroCoverageMode.CalamityAndInfernumOnly => IsCalamityOrInfernumBoss(npc),
            _ => true
        };
    }

    public static bool ShouldSuppressAllCustomIntros()
    {
        return ModContent.GetInstance<InfernumFablesExpansionConfig>().IntroCoverage == IntroCoverageMode.AllBossesOff;
    }

    private static bool IsCalamityOrInfernumBoss(NPC npc)
    {
        string modName = npc.ModNPC?.Mod?.Name ?? "";
        return modName is "CalamityMod" or "InfernumMode";
    }
}

public enum IntroCoverageMode
{
    CalamityAndInfernumOnly,
    AllBossesOn,
    AllBossesOff
}
