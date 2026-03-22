using CalamityFables.Core;
using CalamityFables.Helpers;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumFablesExpansion.Systems;

public sealed class GenericBossIntroPresentationSystem : ModSystem
{
    private static int focusedNpcWhoAmI = -1;
    private static int releaseTimer;
    private static int holdTimer;
    private static int compositionDirection = 1;

    public static void Begin(int npcWhoAmI, bool flipped, Color primary, Color secondary)
    {
        focusedNpcWhoAmI = npcWhoAmI;
        releaseTimer = 24;
        holdTimer = 32;
        compositionDirection = flipped ? -1 : 1;

        if (npcWhoAmI < 0 || npcWhoAmI >= Main.maxNPCs)
            return;

        NPC npc = Main.npc[npcWhoAmI];
        if (!npc.active)
            return;

        SpawnIntroDust(npc, primary, secondary);
        CameraManager.Shake = System.Math.Max(CameraManager.Shake, 8f);
    }

    public static void Clear()
    {
        focusedNpcWhoAmI = -1;
        releaseTimer = 0;
        holdTimer = 0;
        compositionDirection = 1;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.netMode == NetmodeID.Server)
            return;

        if (focusedNpcWhoAmI < 0)
            return;

        bool hasCard = BossIntroScreens.currentCard is not null;
        bool hasNpc = focusedNpcWhoAmI < Main.maxNPCs && Main.npc[focusedNpcWhoAmI].active && Main.npc[focusedNpcWhoAmI].life > 0;

        if (!hasCard || !hasNpc)
        {
            ReleaseCamera();
            return;
        }

        NPC npc = Main.npc[focusedNpcWhoAmI];
        Vector2 focus = GetComposedFocusPosition(npc);

        bool applied = CameraManager.PanMagnet.SetMagnetPositionAndImmunityForEveryone(
            focus,
            focus,
            180,
            1.2f,
            FablesUtils.PolyInOutEasing,
            4f);

        if (applied)
        {
            CameraManager.PanMagnet.PanProgress = MathHelper.Clamp(
                CameraManager.PanMagnet.PanProgress + 0.065f,
                0f,
                0.9f);
        }
    }

    private static void ReleaseCamera()
    {
        if (holdTimer > 0)
        {
            holdTimer--;

            if (focusedNpcWhoAmI >= 0 && focusedNpcWhoAmI < Main.maxNPCs)
            {
                NPC npc = Main.npc[focusedNpcWhoAmI];
                if (npc.active && npc.life > 0)
                {
                    Vector2 focus = GetComposedFocusPosition(npc);
                    bool applied = CameraManager.PanMagnet.SetMagnetPositionAndImmunityForEveryone(
                        focus,
                        focus,
                        180,
                        1.08f,
                        FablesUtils.PolyInOutEasing,
                        4f);

                    if (applied)
                    {
                        CameraManager.PanMagnet.PanProgress = MathHelper.Clamp(
                            CameraManager.PanMagnet.PanProgress + 0.02f,
                            0f,
                            0.9f);
                    }
                }
            }

            return;
        }

        Player player = Main.LocalPlayer;
        if (player.active && !player.dead)
        {
            bool applied = CameraManager.PanMagnet.SetMagnetPositionAndImmunityForEveryone(
                player.Center,
                player.Center,
                70,
                0.9f,
                FablesUtils.PolyInOutEasing,
                4f);

            if (applied)
            {
                CameraManager.PanMagnet.PanProgress = MathHelper.Clamp(
                    CameraManager.PanMagnet.PanProgress - 0.045f,
                    0f,
                    1f);
            }
        }

        releaseTimer--;
        if (releaseTimer <= 0)
            Clear();
    }

    private static Vector2 GetComposedFocusPosition(NPC npc)
    {
        float horizontalOffset = MathHelper.Clamp(Main.screenWidth * 0.18f, 160f, 340f);
        float verticalOffset = MathHelper.Clamp(Main.screenHeight * 0.04f, 18f, 52f);

        return npc.Center + new Vector2(horizontalOffset * compositionDirection, verticalOffset);
    }

    private static void SpawnIntroDust(NPC npc, Color primary, Color secondary)
    {
        float radius = System.Math.Max(npc.width, npc.height) * 0.45f + 24f;

        for (int i = 0; i < 18; i++)
        {
            float angle = MathHelper.TwoPi * i / 18f;
            Vector2 direction = angle.ToRotationVector2();
            Color color = (i & 1) == 0 ? primary : secondary;

            Dust dust = Dust.NewDustPerfect(
                npc.Center + direction * radius,
                DustID.Torch,
                direction * Main.rand.NextFloat(2.5f, 5.5f),
                150,
                color,
                Main.rand.NextFloat(1.2f, 1.8f));

            dust.noGravity = true;
        }

        for (int i = 0; i < 10; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Circular(3.5f, 3.5f);
            Color color = i % 3 == 0 ? secondary : primary;

            Dust dust = Dust.NewDustPerfect(
                npc.Center,
                DustID.GemDiamond,
                velocity,
                120,
                color,
                Main.rand.NextFloat(0.9f, 1.35f));

            dust.noGravity = true;
        }
    }
}
