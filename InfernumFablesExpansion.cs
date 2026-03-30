using InfernumFablesExpansion.NPCs;
using InfernumFablesExpansion.Systems;
using Terraria.ModLoader;

namespace InfernumFablesExpansion;

public sealed class InfernumFablesExpansion : Mod
{
    public override void Load()
    {
        InfernumFablesReflection.Load();
    }

    public override void Unload()
    {
        InfernumIntroLookup.Clear();
        InfernumFablesReflection.Unload();
        GenericBossIntroPresentationSystem.Clear();
        GenericBossNameIntroGlobalNPC.ClearEncounterCache();
        GenericBossNameIntroGlobalNPC.ClearPendingIntros();
        GenericBossNameIntroGlobalNPC.ClearColorCache();
    }
}
