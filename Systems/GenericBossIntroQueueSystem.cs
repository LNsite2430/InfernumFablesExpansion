using CalamityFables.Core;
using InfernumFablesExpansion.NPCs;
using Terraria.ModLoader;

namespace InfernumFablesExpansion.Systems;

public sealed class GenericBossIntroQueueSystem : ModSystem
{
    public override void PostUpdateEverything()
    {
        if (BossIntroScreens.currentCard is not null)
            return;

        GenericBossNameIntroGlobalNPC.TryPlayNextQueuedIntro();
    }
}
