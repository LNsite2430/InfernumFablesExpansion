using Terraria;
using Terraria.ModLoader;

namespace InfernumFablesExpansion.Systems;

public sealed class GenericBossIntroPlayerProtection : ModPlayer
{
    private int introProtectionTimer;

    public override void ResetEffects()
    {
        if (GenericBossIntroPresentationSystem.IsBlackBandActive)
            introProtectionTimer = 12;
        else if (introProtectionTimer > 0)
            introProtectionTimer--;
    }

    public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
    {
        if (introProtectionTimer > 0 && npc.boss)
            return false;

        return true;
    }

    public override bool CanBeHitByProjectile(Projectile proj)
    {
        if (introProtectionTimer <= 0)
            return true;

        if (proj.hostile)
            return false;

        return true;
    }
}
