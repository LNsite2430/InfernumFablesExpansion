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
    private static readonly System.Collections.Generic.List<QueuedIntro> PendingIntros = new();
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

    internal static void ClearPendingIntros()
    {
        PendingIntros.Clear();
    }

    internal static bool HasPendingIntro()
    {
        return PendingIntros.Count > 0;
    }

    private bool playedIntro;
    private bool queuedIntro;
    private int introTimer;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
    {
        return entity.boss;
    }

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        playedIntro = false;
        queuedIntro = false;
        introTimer = 0;
    }

    public override void OnKill(NPC npc)
    {
        playedIntro = false;
        queuedIntro = false;
    }

    public override bool PreAI(NPC npc)
    {
        if (!ShouldPauseBossAIDuringGenericIntro(npc))
            return true;

        npc.velocity = Vector2.Zero;
        npc.frameCounter = 0D;
        return false;
    }

    public override void PostAI(NPC npc)
    {
        if (Main.dedServ || Main.netMode == NetmodeID.Server)
            return;

        if (!npc.active || !npc.boss || npc.life <= 0 || playedIntro)
            return;

        if (!InfernumFablesExpansionConfig.ShouldPlayGenericIntroFor(npc))
            return;

        if (InfernumFablesReflection.HasCustomIntro(npc.type))
            return;

        if (ShouldSkipSegmentIntro(npc))
            return;

        if (IsClaimedEncounter(npc))
            return;

        if (!IsPrimaryIntroSource(npc))
            return;

        if (!CanPlayIntro(npc))
            return;

        if (BossIntroScreens.currentCard is not null)
        {
            QueueIntro(npc);
            return;
        }

        PlayIntro(npc);
    }

    public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
    {
        if (ShouldSuppressBossDamageDuringGenericIntro(npc))
            return false;

        return true;
    }

    private bool CanPlayIntro(NPC npc)
    {
        if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
            npc.TargetClosest(false);

        if (npc.target < 0 || npc.target >= Main.maxPlayers)
            return false;

        introTimer++;
        return introTimer >= GetRequiredIntroDelay(npc);
    }

    private static BossIntroCard CreateCard(NPC npc, CharacteristicColorPair colors, bool compactMode, out bool flipped)
    {
        flipped = npc.Center.X < Main.LocalPlayer.Center.X;
        Color accentColor = colors.Primary;
        GenericBossAnimationProfileSelector.AnimationProfile profile = GenericBossAnimationProfileSelector.Select(npc, accentColor);
        int duration = compactMode
            ? System.Math.Max(64, (int)(profile.Duration * 0.7f))
            : profile.Duration;

        Color edgeBaseColor = Color.Lerp(colors.Primary, colors.Secondary, 0.35f);
        Color edgeColor = Color.Lerp(edgeBaseColor, Color.Black, 0.12f);
        Color brightColor = Color.Lerp(colors.Primary, Color.White, profile.NameBrighten * 0.35f);
        Color secondaryColor = Color.Lerp(colors.Secondary, Color.White, profile.NameSecondaryBrighten * 0.35f);
        Color titleColor = Color.Lerp(new Color(225, 230, 255), brightColor, 0.22f) * profile.TitleBrightness;
        string title = InfernumIntroLookup.TryGetTitle(npc, out string infernumTitle)
            ? infernumTitle
            : GenericBossTitleGenerator.GetTitle(npc, accentColor);
        string displayName = GenericBossDisplayNameResolver.GetDisplayName(npc);

        return new BossIntroCard(
            bossNameFunction: () => displayName,
            bossTitle: title,
            duration: duration,
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

    internal static void TryPlayNextQueuedIntro()
    {
        if (BossIntroScreens.currentCard is not null)
            return;

        PruneExpiredClaims();

        for (int i = PendingIntros.Count - 1; i >= 0; i--)
        {
            QueuedIntro pending = PendingIntros[i];
            if (pending.ExpireTime > Main.GameUpdateCount)
                continue;

            PendingIntros.RemoveAt(i);
        }

        while (PendingIntros.Count > 0)
        {
            QueuedIntro pending = PendingIntros[0];
            PendingIntros.RemoveAt(0);

            if (pending.NpcWhoAmI < 0 || pending.NpcWhoAmI >= Main.maxNPCs)
                continue;

            NPC npc = Main.npc[pending.NpcWhoAmI];
            if (!npc.active || !npc.boss || npc.life <= 0)
                continue;

            GenericBossNameIntroGlobalNPC globalNpc = npc.GetGlobalNPC<GenericBossNameIntroGlobalNPC>();
            globalNpc.queuedIntro = false;
            globalNpc.playedIntro = true;

            BossIntroScreens.currentCard = pending.Card;
            GenericBossIntroPresentationSystem.Begin(pending.NpcWhoAmI, pending.Flipped, pending.Primary, pending.Secondary, pending.Card);
            return;
        }
    }

    private static CharacteristicColorPair GetCharacteristicBossColors(NPC npc)
    {
        if (DominantColorCache.TryGetValue(npc.type, out CharacteristicColorPair cachedColor))
            return cachedColor;

        if (TryGetSpecialBossColors(npc, out CharacteristicColorPair specialColors))
        {
            DominantColorCache[npc.type] = specialColors;
            return specialColors;
        }

        CharacteristicColorPair dominantColors = new(DefaultAccentColor, Color.Lerp(DefaultAccentColor, Color.White, 0.3f));
        bool extractedSuccessfully = false;

        try
        {
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Color[] pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);

            var buckets = new System.Collections.Generic.Dictionary<int, BucketData>();

            foreach (Color pixel in pixels)
            {
                if (pixel.A < 16)
                    continue;

                int maxChannel = System.Math.Max(pixel.R, System.Math.Max(pixel.G, pixel.B));
                int minChannel = System.Math.Min(pixel.R, System.Math.Min(pixel.G, pixel.B));

                if (maxChannel <= 14)
                    continue;

                if (maxChannel >= 250 && minChannel >= 238)
                    continue;

                float weight = CalculatePixelWeight(pixel);
                if (weight <= 0.01f)
                    continue;

                int bucketR = pixel.R / 18;
                int bucketG = pixel.G / 18;
                int bucketB = pixel.B / 18;
                int key = (bucketR << 16) | (bucketG << 8) | bucketB;

                if (!buckets.TryGetValue(key, out BucketData bucket))
                    bucket = new BucketData();

                bucket.Weight += weight;
                bucket.R += pixel.R * weight;
                bucket.G += pixel.G * weight;
                bucket.B += pixel.B * weight;
                buckets[key] = bucket;
            }

            var rankedBuckets = new System.Collections.Generic.List<RankedBucket>();

            foreach (BucketData bucket in buckets.Values)
            {
                if (bucket.Weight <= 0.01f)
                    continue;

                Color averageColor = new(
                    (int)(bucket.R / bucket.Weight),
                    (int)(bucket.G / bucket.Weight),
                    (int)(bucket.B / bucket.Weight));

                float score = CalculateCharacteristicScore(averageColor, bucket.Weight);
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
                extractedSuccessfully = true;
            }
        }
        catch
        {
            dominantColors = new CharacteristicColorPair(DefaultAccentColor, Color.Lerp(DefaultAccentColor, Color.White, 0.3f));
        }

        if (extractedSuccessfully)
            DominantColorCache[npc.type] = dominantColors;

        return dominantColors;
    }

    private static bool TryGetSpecialBossColors(NPC npc, out CharacteristicColorPair colors)
    {
        string probe = $"{npc.ModNPC?.Name} {npc.FullName}".ToLowerInvariant();

        if (npc.type is NPCID.WallofFlesh or NPCID.WallofFleshEye)
        {
            colors = new CharacteristicColorPair(
                new Color(214, 86, 78),
                new Color(255, 186, 92));
            return true;
        }

        if (GenericBossDisplayNameResolver.TryGetIndependentBossId(npc, out string bossId))
        {
            switch (bossId)
            {
                case "ares":
                    colors = new CharacteristicColorPair(
                        new Color(222, 92, 82),
                        new Color(255, 184, 112));
                    return true;

                case "artemis":
                    colors = new CharacteristicColorPair(
                        new Color(108, 255, 176),
                        new Color(122, 214, 255));
                    return true;

                case "apollo":
                    colors = new CharacteristicColorPair(
                        new Color(255, 154, 88),
                        new Color(255, 226, 132));
                    return true;

                case "thanatos":
                    colors = new CharacteristicColorPair(
                        new Color(255, 118, 222),
                        new Color(132, 176, 255));
                    return true;

                case "retinazer":
                    colors = new CharacteristicColorPair(
                        new Color(255, 86, 96),
                        new Color(255, 176, 92));
                    return true;

                case "spazmatism":
                    colors = new CharacteristicColorPair(
                        new Color(110, 255, 118),
                        new Color(192, 255, 132));
                    return true;
            }
        }

        if (npc.type is NPCID.Retinazer || probe.Contains("retinazer"))
        {
            colors = new CharacteristicColorPair(
                new Color(255, 86, 96),
                new Color(255, 176, 92));
            return true;
        }

        if (npc.type is NPCID.Spazmatism || probe.Contains("spazmatism"))
        {
            colors = new CharacteristicColorPair(
                new Color(110, 255, 118),
                new Color(192, 255, 132));
            return true;
        }

        colors = default;
        return false;
    }

    private static float CalculatePixelWeight(Color color)
    {
        Vector3 hsl = Main.rgbToHsl(color);
        float saturation = hsl.Y;
        float lightness = hsl.Z;
        float alphaWeight = color.A / 255f;

        int maxChannel = System.Math.Max(color.R, System.Math.Max(color.G, color.B));
        int minChannel = System.Math.Min(color.R, System.Math.Min(color.G, color.B));
        float chroma = (maxChannel - minChannel) / 255f;

        float saturationWeight = MathHelper.Lerp(0.4f, 1.15f, saturation);
        float chromaWeight = MathHelper.Lerp(0.55f, 1.2f, chroma);
        float lightnessWeight = MathHelper.Clamp(1.08f - System.MathF.Abs(lightness - 0.52f) * 1.1f, 0.45f, 1.08f);

        return alphaWeight * saturationWeight * chromaWeight * lightnessWeight;
    }

    private static float CalculateCharacteristicScore(Color color, float weight)
    {
        Vector3 hsl = Main.rgbToHsl(color);
        float saturation = hsl.Y;
        float lightness = hsl.Z;

        float saturationWeight = MathHelper.Lerp(0.45f, 2.1f, saturation);
        float lightnessDistance = System.MathF.Abs(lightness - 0.5f);
        float lightnessWeight = MathHelper.Clamp(1.16f - lightnessDistance * 1.4f, 0.35f, 1.16f);
        float chroma = (System.Math.Max(color.R, System.Math.Max(color.G, color.B)) -
                        System.Math.Min(color.R, System.Math.Min(color.G, color.B))) / 255f;
        float chromaWeight = MathHelper.Lerp(0.55f, 1.55f, chroma);

        return weight * saturationWeight * lightnessWeight * chromaWeight;
    }

    private static float CalculateColorDistance(Color left, Color right)
    {
        Vector3 leftVector = left.ToVector3();
        Vector3 rightVector = right.ToVector3();
        return Vector3.Distance(leftVector, rightVector) / System.MathF.Sqrt(3f);
    }

    private static bool IsPrimaryIntroSource(NPC npc)
    {
        if (GenericBossDisplayNameResolver.TryGetIndependentBossId(npc, out _))
        {
            string independentDisplayName = GenericBossDisplayNameResolver.GetDisplayName(npc);
            return FindDisplayNameLeader(npc, independentDisplayName) == npc.whoAmI;
        }

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

        bool hasSourceIndependentId = GenericBossDisplayNameResolver.TryGetIndependentBossId(source, out string sourceIndependentId);
        bool hasOtherIndependentId = GenericBossDisplayNameResolver.TryGetIndependentBossId(other, out string otherIndependentId);
        if (hasSourceIndependentId || hasOtherIndependentId)
            return hasSourceIndependentId && hasOtherIndependentId && sourceIndependentId == otherIndependentId;

        int sourceRoot = source.realLife >= 0 ? source.realLife : source.whoAmI;
        int otherRoot = other.realLife >= 0 ? other.realLife : other.whoAmI;
        if (source.realLife >= 0 || other.realLife >= 0)
            return sourceRoot == otherRoot;

        string otherDisplayName = GenericBossDisplayNameResolver.GetDisplayName(other);
        return string.Equals(displayName, otherDisplayName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetEncounterKey(NPC npc)
    {
        if (GenericBossDisplayNameResolver.TryGetIndependentBossId(npc, out string independentBossId))
            return $"boss:{npc.target}:{independentBossId}";

        if (npc.realLife >= 0)
            return $"reallife:{npc.realLife}";

        string displayName = GenericBossDisplayNameResolver.GetDisplayName(npc);
        if (string.IsNullOrWhiteSpace(displayName))
            return "";

        return $"name:{npc.target}:{displayName.ToLowerInvariant()}";
    }

    private static bool ShouldSkipSegmentIntro(NPC npc)
    {
        if (GenericBossDisplayNameResolver.TryGetIndependentBossId(npc, out _))
            return false;

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
        bool isExoMech = IsExoMech(npc);

        if (npc.velocity.Length() > (isExoMech ? 26f : 14f))
            return false;

        if (!isExoMech &&
            (npc.width >= 220 || npc.height >= 220) &&
            System.MathF.Abs(npc.Center.X - target.Center.X) > Main.screenWidth * 0.65f)
        {
            return false;
        }

        return true;
    }

    private static bool ShouldSuppressBossDamageDuringGenericIntro(NPC npc)
    {
        return npc.active &&
               npc.boss &&
               npc.life > 0 &&
               GenericBossIntroPresentationSystem.IsBlackBandActiveFor(npc);
    }

    private static bool ShouldPauseBossAIDuringGenericIntro(NPC npc)
    {
        if (!ShouldSuppressBossDamageDuringGenericIntro(npc))
            return false;

        return GenericBossIntroPresentationSystem.BlackBandAge <= GetAIPauseDuration(npc);
    }

    private static bool IsExoMech(NPC npc)
    {
        return GenericBossDisplayNameResolver.TryGetIndependentBossId(npc, out string bossId) &&
               bossId is "ares" or "artemis" or "apollo" or "thanatos" or "exomechdusa";
    }

    private static int GetRequiredIntroDelay(NPC npc)
    {
        if (IsExoMech(npc))
        {
            CharacteristicColorPair colors = GetCharacteristicBossColors(npc);
            GenericBossAnimationProfileSelector.AnimationProfile profile = GenericBossAnimationProfileSelector.Select(npc, colors.Primary);
            return System.Math.Max(12, profile.DelayTicks);
        }

        return 1;
    }

    private static int GetAIPauseDuration(NPC npc)
    {
        CharacteristicColorPair colors = GetCharacteristicBossColors(npc);
        GenericBossAnimationProfileSelector.AnimationProfile profile = GenericBossAnimationProfileSelector.Select(npc, colors.Primary);
        int profileBasedPause = profile.Duration / 4;
        return System.Math.Clamp(profileBasedPause, 16, 30);
    }

    private void QueueIntro(NPC npc)
    {
        if (queuedIntro)
            return;

        string encounterKey = GetEncounterKey(npc);
        if (string.IsNullOrWhiteSpace(encounterKey))
            return;

        foreach (QueuedIntro pending in PendingIntros)
        {
            if (string.Equals(pending.EncounterKey, encounterKey, System.StringComparison.Ordinal))
                return;
        }

        ClaimEncounter(npc);
        CharacteristicColorPair colors = GetCharacteristicBossColors(npc);
        bool flipped;
        BossIntroCard card = CreateCard(npc, colors, compactMode: true, out flipped);

        PendingIntros.Add(new QueuedIntro
        {
            NpcWhoAmI = npc.whoAmI,
            EncounterKey = encounterKey,
            Card = card,
            Flipped = flipped,
            Primary = colors.Primary,
            Secondary = colors.Secondary,
            ExpireTime = Main.GameUpdateCount + 600
        });

        queuedIntro = true;
        playedIntro = true;
    }

    private void PlayIntro(NPC npc)
    {
        ClaimEncounter(npc);
        CharacteristicColorPair colors = GetCharacteristicBossColors(npc);
        bool flipped;
        BossIntroCard card = CreateCard(npc, colors, compactMode: false, out flipped);
        BossIntroScreens.currentCard = card;
        GenericBossIntroPresentationSystem.Begin(npc.whoAmI, flipped, colors.Primary, colors.Secondary, card);
        queuedIntro = false;
        playedIntro = true;
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
        public float Weight;
        public float R;
        public float G;
        public float B;
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

    private sealed class QueuedIntro
    {
        public int NpcWhoAmI;
        public string EncounterKey = "";
        public BossIntroCard Card = null!;
        public bool Flipped;
        public Color Primary;
        public Color Secondary;
        public ulong ExpireTime;
    }
}
