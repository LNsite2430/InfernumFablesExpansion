using CalamityFables.Core;
using InfernumFablesExpansion.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumFablesExpansion.NPCs;

public sealed class GenericBossNameIntroGlobalNPC : GlobalNPC
{
    private static readonly Color DefaultAccentColor = new(90, 140, 255);
    private static readonly System.Collections.Generic.Dictionary<int, CharacteristicColorPair> DominantColorCache = new();
    private static readonly System.Collections.Generic.List<EncounterClaim> EncounterClaims = new();
    private const int EncounterClaimLifetime = 300;
    public override bool InstancePerEntity => true;

    internal static void ClearColorCache()
    {
        DominantColorCache.Clear();
    }

    internal static void ClearEncounterCache()
    {
        EncounterClaims.Clear();
    }

    private bool playedIntro;
    private int introTimer;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
    {
        return entity.boss;
    }

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        playedIntro = false;
        introTimer = 0;
    }

    public override void OnKill(NPC npc)
    {
        playedIntro = false;
    }

    public override void PostAI(NPC npc)
    {
        if (Main.dedServ || Main.netMode == NetmodeID.Server)
            return;

        if (!npc.active || !npc.boss || npc.life <= 0 || playedIntro)
            return;

        if (InfernumFablesReflection.HasCustomIntro(npc.type))
            return;

        if (ShouldSkipSegmentIntro(npc))
            return;

        if (IsClaimedEncounter(npc))
            return;

        if (!IsPrimaryIntroSource(npc))
            return;

        if (BossIntroScreens.currentCard is not null)
            return;

        if (!CanPlayIntro(npc))
            return;

        ClaimEncounter(npc);
        CharacteristicColorPair colors = GetCharacteristicBossColors(npc);
        bool flipped;
        BossIntroScreens.currentCard = CreateCard(npc, colors, out flipped);
        GenericBossIntroPresentationSystem.Begin(npc.whoAmI, flipped, colors.Primary, colors.Secondary);
        playedIntro = true;
    }

    private bool CanPlayIntro(NPC npc)
    {
        CharacteristicColorPair colors = GetCharacteristicBossColors(npc);
        GenericBossAnimationProfileSelector.AnimationProfile profile = GenericBossAnimationProfileSelector.Select(npc, colors.Primary);

        if (npc.Opacity <= 0.08f)
            return false;

        if (npc.target < 0 || npc.target >= Main.maxPlayers)
            return false;

        Player target = Main.player[npc.target];
        if (!target.active || target.dead)
            return false;

        if (npc.Distance(target.Center) > 12000f)
            return false;

        if (!IsBossReadyForIntro(npc, target))
            return false;

        introTimer++;
        return introTimer >= profile.DelayTicks;
    }

    private static BossIntroCard CreateCard(NPC npc, CharacteristicColorPair colors, out bool flipped)
    {
        flipped = npc.Center.X < Main.LocalPlayer.Center.X;
        string displayName = GenericBossDisplayNameResolver.GetDisplayName(npc);
        Color accentColor = colors.Primary;
        GenericBossAnimationProfileSelector.AnimationProfile profile = GenericBossAnimationProfileSelector.Select(npc, accentColor);

        Color edgeColor = Color.Lerp(accentColor, Color.Black, 0.35f);
        Color brightColor = Color.Lerp(colors.Primary, Color.White, profile.NameBrighten * 0.35f);
        Color secondaryColor = Color.Lerp(colors.Secondary, Color.White, profile.NameSecondaryBrighten * 0.35f);
        Color titleColor = Color.Lerp(new Color(225, 230, 255), brightColor, 0.22f) * profile.TitleBrightness;
        string title = InfernumIntroLookup.TryGetTitle(npc, out string infernumTitle)
            ? infernumTitle
            : GenericBossTitleGenerator.GetTitle(npc, accentColor);

        return new BossIntroCard(
            bossNameFunction: () => displayName,
            bossTitle: title,
            duration: profile.Duration,
            flipped: flipped,
            edgeColor: edgeColor * profile.EdgeOpacity,
            titleColor: titleColor,
            nameColorChroma1: brightColor,
            nameColorChroma2: secondaryColor)
        {
            music = null,
            slant = 0.30f,
            shiftDown = 0.08f
        };
    }

    private static CharacteristicColorPair GetCharacteristicBossColors(NPC npc)
    {
        if (DominantColorCache.TryGetValue(npc.type, out CharacteristicColorPair cachedColor))
            return cachedColor;

        CharacteristicColorPair dominantColors = new(DefaultAccentColor, Color.Lerp(DefaultAccentColor, Color.White, 0.3f));

        try
        {
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Color[] pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);

            var buckets = new System.Collections.Generic.Dictionary<int, BucketData>();

            foreach (Color pixel in pixels)
            {
                if (pixel.A < 32)
                    continue;

                int maxChannel = System.Math.Max(pixel.R, System.Math.Max(pixel.G, pixel.B));
                int minChannel = System.Math.Min(pixel.R, System.Math.Min(pixel.G, pixel.B));

                if (maxChannel <= 28)
                    continue;

                if (minChannel >= 227)
                    continue;

                int bucketR = pixel.R / 24;
                int bucketG = pixel.G / 24;
                int bucketB = pixel.B / 24;
                int key = (bucketR << 16) | (bucketG << 8) | bucketB;

                if (!buckets.TryGetValue(key, out BucketData bucket))
                    bucket = new BucketData();

                bucket.Count++;
                bucket.R += pixel.R;
                bucket.G += pixel.G;
                bucket.B += pixel.B;
                buckets[key] = bucket;
            }

            var rankedBuckets = new System.Collections.Generic.List<RankedBucket>();

            foreach (BucketData bucket in buckets.Values)
            {
                Color averageColor = new(
                    bucket.R / bucket.Count,
                    bucket.G / bucket.Count,
                    bucket.B / bucket.Count);

                float score = CalculateCharacteristicScore(averageColor, bucket.Count);
                if (score <= 0f)
                    continue;

                rankedBuckets.Add(new RankedBucket
                {
                    Color = averageColor,
                    Score = score
                });
            }

            rankedBuckets.Sort((left, right) => right.Score.CompareTo(left.Score));

            if (rankedBuckets.Count > 0)
            {
                Color primary = rankedBuckets[0].Color;
                Color secondary = primary;
                float bestSecondaryScore = float.MinValue;

                for (int i = 1; i < rankedBuckets.Count; i++)
                {
                    RankedBucket candidate = rankedBuckets[i];
                    float distinctness = CalculateColorDistance(primary, candidate.Color);
                    float pairScore = candidate.Score * (0.55f + distinctness * 1.35f);

                    if (distinctness < 0.16f)
                        pairScore *= 0.25f;

                    if (pairScore <= bestSecondaryScore)
                        continue;

                    bestSecondaryScore = pairScore;
                    secondary = candidate.Color;
                }

                if (bestSecondaryScore <= 0f || CalculateColorDistance(primary, secondary) < 0.1f)
                    secondary = Color.Lerp(primary, Color.White, 0.25f);

                dominantColors = new CharacteristicColorPair(primary, secondary);
            }
        }
        catch
        {
            dominantColors = new CharacteristicColorPair(DefaultAccentColor, Color.Lerp(DefaultAccentColor, Color.White, 0.3f));
        }

        DominantColorCache[npc.type] = dominantColors;
        return dominantColors;
    }

    private static float CalculateCharacteristicScore(Color color, int count)
    {
        Vector3 hsl = Main.rgbToHsl(color);
        float saturation = hsl.Y;
        float lightness = hsl.Z;

        float saturationWeight = MathHelper.Lerp(0.25f, 2.3f, saturation);
        float lightnessDistance = System.MathF.Abs(lightness - 0.5f);
        float lightnessWeight = MathHelper.Clamp(1.18f - lightnessDistance * 1.7f, 0.2f, 1.18f);
        float chroma = (System.Math.Max(color.R, System.Math.Max(color.G, color.B)) -
                        System.Math.Min(color.R, System.Math.Min(color.G, color.B))) / 255f;
        float chromaWeight = MathHelper.Lerp(0.35f, 1.65f, chroma);

        return count * saturationWeight * lightnessWeight * chromaWeight;
    }

    private static float CalculateColorDistance(Color left, Color right)
    {
        Vector3 leftVector = left.ToVector3();
        Vector3 rightVector = right.ToVector3();
        return Vector3.Distance(leftVector, rightVector) / System.MathF.Sqrt(3f);
    }

    private static bool IsPrimaryIntroSource(NPC npc)
    {
        if (npc.realLife >= 0)
            return npc.realLife == npc.whoAmI;

        string displayName = GenericBossDisplayNameResolver.GetDisplayName(npc);
        return FindDisplayNameLeader(npc, displayName) == npc.whoAmI;
    }

    private static int FindDisplayNameLeader(NPC npc, string displayName)
    {
        int leader = npc.whoAmI;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (!BelongsToSameDisplayNameGroup(npc, other, displayName))
                continue;

            if (other.whoAmI < leader)
                leader = other.whoAmI;
        }

        return leader;
    }

    private static bool IsClaimedEncounter(NPC npc)
    {
        PruneExpiredClaims();

        string encounterKey = GetEncounterKey(npc);
        if (string.IsNullOrWhiteSpace(encounterKey))
            return false;

        foreach (EncounterClaim claim in EncounterClaims)
        {
            if (!string.Equals(claim.Key, encounterKey, System.StringComparison.Ordinal))
                continue;

            return true;
        }

        return false;
    }

    private void ClaimEncounter(NPC npc)
    {
        string encounterKey = GetEncounterKey(npc);
        if (string.IsNullOrWhiteSpace(encounterKey))
            return;

        int members = 0;
        string displayName = GenericBossDisplayNameResolver.GetDisplayName(npc);

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (!BelongsToSameDisplayNameGroup(npc, other, displayName))
                continue;

            members++;

            GenericBossNameIntroGlobalNPC globalNpc = other.GetGlobalNPC<GenericBossNameIntroGlobalNPC>();
            globalNpc.playedIntro = true;
            globalNpc.introTimer = 0;
        }

        if (members <= 0)
            return;

        EncounterClaims.Add(new EncounterClaim
        {
            Key = encounterKey,
            ExpireTime = Main.GameUpdateCount + EncounterClaimLifetime
        });
    }

    private static bool BelongsToSameDisplayNameGroup(NPC source, NPC other, string displayName)
    {
        if (!other.active || !other.boss || other.life <= 0)
            return false;

        if (InfernumFablesReflection.HasCustomIntro(other.type))
            return false;

        if (source.target >= 0 && other.target >= 0 && source.target != other.target)
            return false;

        int sourceRoot = source.realLife >= 0 ? source.realLife : source.whoAmI;
        int otherRoot = other.realLife >= 0 ? other.realLife : other.whoAmI;
        if (source.realLife >= 0 || other.realLife >= 0)
            return sourceRoot == otherRoot;

        string otherDisplayName = GenericBossDisplayNameResolver.GetDisplayName(other);
        return string.Equals(displayName, otherDisplayName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetEncounterKey(NPC npc)
    {
        if (npc.realLife >= 0)
            return $"reallife:{npc.realLife}";

        string displayName = GenericBossDisplayNameResolver.GetDisplayName(npc);
        if (string.IsNullOrWhiteSpace(displayName))
            return "";

        return $"name:{npc.target}:{displayName.ToLowerInvariant()}";
    }

    private static bool ShouldSkipSegmentIntro(NPC npc)
    {
        if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
            return true;

        if (!LooksLikeSegmentName(npc.FullName, out string rootName))
            return false;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (!other.active || !other.boss || other.life <= 0 || other.whoAmI == npc.whoAmI)
                continue;

            string otherDisplayName = GenericBossDisplayNameResolver.GetDisplayName(other);
            if (!string.Equals(otherDisplayName, rootName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (npc.target >= 0 && other.target >= 0 && npc.target != other.target)
                continue;

            return true;
        }

        return false;
    }

    private static bool LooksLikeSegmentName(string fullName, out string rootName)
    {
        rootName = "";

        string normalized = NormalizeSpacing(fullName);
        string lower = normalized.ToLowerInvariant();

        string[] markers =
        {
            "'s core",
            "'s hand",
            "'s head",
            "'s body",
            "'s tail",
            "'s eye",
            "'s arm",
            "'s claw",
            "'s fist",
            "'s cannon",
            "'s laser",
            "'s gauntlet",
            " core",
            " hand",
            " head",
            " body",
            " tail",
            " eye",
            " arm",
            " claw",
            " fist",
            " cannon",
            " laser",
            " gauntlet"
        };

        foreach (string marker in markers)
        {
            if (!lower.EndsWith(marker))
                continue;

            string candidate = normalized[..^marker.Length].TrimEnd(' ', '\'');
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            rootName = candidate;
            return true;
        }

        return false;
    }

    private static string NormalizeSpacing(string value)
    {
        return string.Join(" ", value.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsBossReadyForIntro(NPC npc, Player target)
    {
        Rectangle paddedScreen = new(
            (int)Main.screenPosition.X - 120,
            (int)Main.screenPosition.Y - 80,
            Main.screenWidth + 240,
            Main.screenHeight + 160);

        if (!paddedScreen.Intersects(npc.Hitbox))
            return false;

        if (npc.velocity.Length() > 14f)
            return false;

        if ((npc.width >= 220 || npc.height >= 220) &&
            System.MathF.Abs(npc.Center.X - target.Center.X) > Main.screenWidth * 0.65f)
        {
            return false;
        }

        return true;
    }

    private static void PruneExpiredClaims()
    {
        ulong now = Main.GameUpdateCount;
        for (int i = EncounterClaims.Count - 1; i >= 0; i--)
        {
            if (EncounterClaims[i].ExpireTime <= now)
                EncounterClaims.RemoveAt(i);
        }
    }

    private sealed class BucketData
    {
        public int Count;
        public int R;
        public int G;
        public int B;
    }

    private sealed class RankedBucket
    {
        public Color Color;
        public float Score;
    }

    private readonly record struct CharacteristicColorPair(Color Primary, Color Secondary);

    private sealed class EncounterClaim
    {
        public string Key = "";
        public ulong ExpireTime;
    }
}
