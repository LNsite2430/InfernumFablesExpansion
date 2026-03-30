using CalamityFables.Core;
using CalamityFables.Helpers;
using InfernumFablesExpansion.NPCs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumFablesExpansion.Systems;

public sealed class GenericBossIntroPresentationSystem : ModSystem
{
    private const int EntryDuration = 22;
    private const int ReleaseDuration = 42;
    private static BossIntroCard? activeCard;
    private static int focusedNpcWhoAmI = -1;
    private static int activeCardTimer;
    private static int entryTimer;
    private static int releaseTimer;
    private static int holdTimer;
    private static int compositionDirection = 1;
    private static Vector2 entryAnchorStart;
    private static Vector2 entryFocusStart;
    private static Vector2 lastAnchorPosition;
    private static Vector2 lastFocusPosition;

    public static bool IsBlackBandActive =>
        focusedNpcWhoAmI >= 0 &&
        activeCard is not null &&
        ReferenceEquals(BossIntroScreens.currentCard, activeCard);

    public static int BlackBandAge => IsBlackBandActive ? activeCardTimer : 0;

    public static bool IsBlackBandActiveFor(NPC npc)
    {
        if (!IsBlackBandActive || !npc.active || !npc.boss || npc.life <= 0)
            return false;

        if (focusedNpcWhoAmI < 0 || focusedNpcWhoAmI >= Main.maxNPCs)
            return false;

        NPC focusedNpc = Main.npc[focusedNpcWhoAmI];
        if (!focusedNpc.active || !focusedNpc.boss || focusedNpc.life <= 0)
            return false;

        bool hasFocusedIndependentId = GenericBossDisplayNameResolver.TryGetIndependentBossId(focusedNpc, out string focusedIndependentId);
        bool hasNpcIndependentId = GenericBossDisplayNameResolver.TryGetIndependentBossId(npc, out string npcIndependentId);
        if (hasFocusedIndependentId || hasNpcIndependentId)
            return hasFocusedIndependentId && hasNpcIndependentId && focusedIndependentId == npcIndependentId;

        int focusedRoot = focusedNpc.realLife >= 0 ? focusedNpc.realLife : focusedNpc.whoAmI;
        int npcRoot = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
        if (focusedNpc.realLife >= 0 || npc.realLife >= 0)
            return focusedRoot == npcRoot;

        string focusedDisplayName = GenericBossDisplayNameResolver.GetDisplayName(focusedNpc);
        string otherDisplayName = GenericBossDisplayNameResolver.GetDisplayName(npc);
        return string.Equals(focusedDisplayName, otherDisplayName, System.StringComparison.OrdinalIgnoreCase);
    }

    public static void Begin(int npcWhoAmI, bool flipped, Color primary, Color secondary, BossIntroCard card)
    {
        activeCard = card;
        focusedNpcWhoAmI = npcWhoAmI;
        activeCardTimer = 0;
        entryTimer = 0;
        releaseTimer = ReleaseDuration;
        holdTimer = 32;
        compositionDirection = flipped ? -1 : 1;
        Player player = Main.LocalPlayer;
        bool canChainTransition =
            lastAnchorPosition != Vector2.Zero &&
            lastFocusPosition != Vector2.Zero &&
            CameraManager.PanMagnet.PanProgress > 0.05f;

        if (canChainTransition)
        {
            entryAnchorStart = lastAnchorPosition;
            entryFocusStart = lastFocusPosition;
        }
        else
        {
            entryAnchorStart = player.active ? player.Center : Vector2.Zero;
            entryFocusStart = entryAnchorStart;
            lastAnchorPosition = entryAnchorStart;
            lastFocusPosition = entryFocusStart;
        }

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
        activeCard = null;
        focusedNpcWhoAmI = -1;
        activeCardTimer = 0;
        entryTimer = 0;
        releaseTimer = 0;
        holdTimer = 0;
        compositionDirection = 1;
        entryAnchorStart = Vector2.Zero;
        entryFocusStart = Vector2.Zero;
        lastAnchorPosition = Vector2.Zero;
        lastFocusPosition = Vector2.Zero;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.netMode == NetmodeID.Server)
            return;

        if (focusedNpcWhoAmI < 0)
            return;

        bool hasCard = activeCard is not null && ReferenceEquals(BossIntroScreens.currentCard, activeCard);
        bool hasNpc = focusedNpcWhoAmI < Main.maxNPCs && Main.npc[focusedNpcWhoAmI].active && Main.npc[focusedNpcWhoAmI].life > 0;

        if (!hasCard || !hasNpc)
        {
            ReleaseCamera();
            return;
        }

        NPC npc = Main.npc[focusedNpcWhoAmI];
        Vector2 targetAnchor = npc.Center;
        Vector2 targetFocus = GetComposedFocusPosition(npc);
        float entryProgress = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(entryTimer / (float)EntryDuration, 0f, 1f));
        Vector2 appliedAnchor = Vector2.Lerp(entryAnchorStart, targetAnchor, entryProgress);
        Vector2 appliedFocus = Vector2.Lerp(entryFocusStart, targetFocus, entryProgress);
        lastAnchorPosition = appliedAnchor;
        lastFocusPosition = appliedFocus;

        bool applied = CameraManager.PanMagnet.SetMagnetPositionAndImmunityForEveryone(
            appliedAnchor,
            appliedFocus,
            30000,
            1.4f,
            FablesUtils.PolyInOutEasing,
            4f);

        if (applied)
        {
            CameraManager.PanMagnet.PanProgress = MathHelper.Clamp(
                MathHelper.Lerp(CameraManager.PanMagnet.PanProgress, 1f, 0.06f + entryProgress * 0.08f),
                0f,
                1f);
        }

        if (entryTimer < EntryDuration)
            entryTimer++;

        activeCardTimer++;
    }

    private static void ReleaseCamera()
    {
        if (GenericBossNameIntroGlobalNPC.HasPendingIntro())
        {
            bool applied = CameraManager.PanMagnet.SetMagnetPositionAndImmunityForEveryone(
                lastAnchorPosition,
                lastFocusPosition,
                30000,
                1.15f,
                FablesUtils.PolyInOutEasing,
                4f);

            if (applied)
            {
                CameraManager.PanMagnet.PanProgress = MathHelper.Clamp(
                    MathHelper.Lerp(CameraManager.PanMagnet.PanProgress, 1f, 0.08f),
                    0f,
                    1f);
            }

            return;
        }

        if (holdTimer > 0)
        {
            holdTimer--;

            if (focusedNpcWhoAmI >= 0 && focusedNpcWhoAmI < Main.maxNPCs)
            {
                NPC npc = Main.npc[focusedNpcWhoAmI];
                if (npc.active && npc.life > 0)
                {
                    Vector2 focus = GetComposedFocusPosition(npc);
                    lastAnchorPosition = npc.Center;
                    lastFocusPosition = focus;
                    bool applied = CameraManager.PanMagnet.SetMagnetPositionAndImmunityForEveryone(
                        npc.Center,
                        focus,
                        30000,
                        1.2f,
                        FablesUtils.PolyInOutEasing,
                        4f);

                    if (applied)
                    {
                        CameraManager.PanMagnet.PanProgress = MathHelper.Clamp(
                            CameraManager.PanMagnet.PanProgress + 0.03f,
                            0f,
                            1f);
                    }
                }
            }

            return;
        }

        Player player = Main.LocalPlayer;
        if (player.active && !player.dead)
        {
            float releaseProgress = 1f - releaseTimer / (float)ReleaseDuration;
            float smoothedProgress = MathHelper.SmoothStep(0f, 1f, releaseProgress);
            Vector2 releaseAnchor = Vector2.Lerp(lastAnchorPosition, player.Center, smoothedProgress);
            Vector2 releaseFocus = Vector2.Lerp(lastFocusPosition, player.Center, smoothedProgress);

            bool applied = CameraManager.PanMagnet.SetMagnetPositionAndImmunityForEveryone(
                releaseAnchor,
                releaseFocus,
                70,
                1.05f,
                FablesUtils.PolyInOutEasing,
                4f);

            if (applied)
            {
                CameraManager.PanMagnet.PanProgress = MathHelper.Clamp(
                    MathHelper.Lerp(CameraManager.PanMagnet.PanProgress, 0f, 0.065f + smoothedProgress * 0.08f),
                    0.02f,
                    1f);
            }
        }

        releaseTimer--;
        if (releaseTimer <= 0)
            Clear();
    }

    private static Vector2 GetComposedFocusPosition(NPC npc)
    {
        if (npc.type is NPCID.WallofFlesh or NPCID.WallofFleshEye)
        {
            Player player = Main.LocalPlayer;
            Vector2 midpoint = Vector2.Lerp(npc.Center, player.Center, 0.3f);
            return midpoint + new Vector2(0f, -12f);
        }

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
