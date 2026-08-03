using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Keeps Bannerlord's native political cadence tied to a campaign year
    /// instead of letting it run 4.35 times faster because the Gregorian
    /// calendar has 365 days rather than the native 84.
    /// </summary>
    internal static class DiplomacyBalanceMath
    {
        internal static bool IsEnabled
        {
            get { return DailyRateBalance.IsExtendedCalendar; }
        }

        internal static float GetNativeEquivalentDays(CampaignTime time)
        {
            return DailyRateBalance.ToNativeCalendarDays(time.ElapsedDaysUntilNow);
        }

        internal static float ScaleDecisionProposalProbability(float nativeDailyProbability)
        {
            return DailyRateBalance.ScaleDailyProbability(nativeDailyProbability);
        }
    }

    // Each eligible clan rolls for peace, war, alliance/trade, policy, and
    // annexation proposals once per campaign day. Replace only the shared
    // native probability threshold, rather than suppressing DailyTickClan as
    // a whole. This preserves all native eligibility checks, decision
    // selection, player exceptions, and daily pending-decision maintenance.
    [HarmonyPatch(typeof(KingdomDecisionProposalBehavior), "DailyTickClan")]
    internal static class KingdomDecisionProposalCadencePatch
    {
        private static readonly MethodInfo NativeProposalChanceCap = AccessTools.Method(
            typeof(MathF),
            "Min",
            new[] { typeof(float), typeof(float) });

        private static readonly MethodInfo ScaleProposalChance = AccessTools.Method(
            typeof(DiplomacyBalanceMath),
            "ScaleDecisionProposalProbability");

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            bool replaced = false;
            for (int index = 0; index < codes.Count; index++)
            {
                CodeInstruction instruction = codes[index];
                yield return instruction;

                if (replaced || NativeProposalChanceCap == null || !instruction.Calls(NativeProposalChanceCap))
                {
                    continue;
                }

                if (index + 1 >= codes.Count || codes[index + 1].opcode != OpCodes.Stloc_S)
                {
                    throw new InvalidOperationException(
                        "Could not locate the local assignment for Bannerlord's kingdom proposal probability.");
                }

                CodeInstruction storeProbability = codes[++index];
                yield return storeProbability;
                yield return new CodeInstruction(OpCodes.Ldloc_S, storeProbability.operand);
                yield return new CodeInstruction(OpCodes.Call, ScaleProposalChance);
                yield return new CodeInstruction(OpCodes.Stloc_S, storeProbability.operand);
                replaced = true;
            }

            if (!replaced)
            {
                throw new InvalidOperationException(
                    "Could not locate Bannerlord's kingdom proposal probability cap for calendar balancing.");
            }
        }
    }

    // The native candidate picker uses a 20-day post-peace limit. It first
    // filters by its own 20 calendar days, then this guard prevents a selected
    // target from bypassing the corresponding 20 native-day cooldown.
    [HarmonyPatch(typeof(KingdomDecisionProposalBehavior), "GetRandomWarDecision")]
    internal static class KingdomWarCooldownPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Clan clan, ref KingdomDecision __result)
        {
            if (!DiplomacyBalanceMath.IsEnabled || __result == null || clan == null || clan.Kingdom == null)
            {
                return;
            }

            DeclareWarDecision warDecision = __result as DeclareWarDecision;
            if (warDecision == null || warDecision.FactionToDeclareWarOn == null)
            {
                return;
            }

            StanceLink stance = clan.Kingdom.GetStanceWith(warDecision.FactionToDeclareWarOn);
            if (DiplomacyBalanceMath.GetNativeEquivalentDays(stance.PeaceDeclarationDate) <= 20f)
            {
                __result = null;
            }
        }
    }

    [HarmonyPatch(typeof(DefaultDiplomacyModel), "GetHourlyInfluenceAwardForBeingArmyMember")]
    internal static class ArmyInfluenceDiplomacyBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultDiplomacyModel), "GetHourlyInfluenceAwardForRaidingEnemyVillage")]
    internal static class RaidInfluenceDiplomacyBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultDiplomacyModel), "GetHourlyInfluenceAwardForBesiegingEnemyFortification")]
    internal static class SiegeInfluenceDiplomacyBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            DailyRateBalance.Scale(ref __result);
        }
    }

    [HarmonyPatch(typeof(DefaultDiplomacyModel), "GetScoreOfMercenaryToLeaveKingdom")]
    internal static class MercenaryTenureDiplomacyBalancePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            DefaultDiplomacyModel __instance,
            Clan mercenaryClan,
            Kingdom kingdom,
            ref float __result)
        {
            if (!DiplomacyBalanceMath.IsEnabled)
            {
                return true;
            }

            float nativeEquivalentDays = DiplomacyBalanceMath.GetNativeEquivalentDays(
                mercenaryClan.LastFactionChangeTime);
            float tenureScore = 0.005f * MathF.Min(200f, nativeEquivalentDays);
            __result = 10000f * tenureScore
                - 5000f
                - __instance.GetScoreOfMercenaryToJoinKingdom(mercenaryClan, kingdom);
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultDiplomacyModel), "GetScoreOfClanToLeaveKingdom")]
    internal static class ClanTenureDiplomacyBalancePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Clan clan, Kingdom kingdom, ref float __result)
        {
            if (!DiplomacyBalanceMath.IsEnabled)
            {
                return true;
            }

            int relationBetweenClans = FactionManager.GetRelationBetweenClans(kingdom.RulingClan, clan);
            int relationTotal = 0;
            int relatedClanCount = 0;
            foreach (Clan kingdomClan in kingdom.Clans)
            {
                relationTotal += FactionManager.GetRelationBetweenClans(clan, kingdomClan);
                relatedClanCount++;
            }

            float averageRelation = relatedClanCount > 0
                ? (float)relationTotal / relatedClanCount
                : 0f;
            float combinedRelation = MathF.Max(
                -100f,
                MathF.Min(100f, relationBetweenClans + averageRelation));
            float relationMultiplier = MathF.Min(
                2f,
                MathF.Max(
                    0.33f,
                    1f + MathF.Sqrt(MathF.Abs(combinedRelation))
                        * (combinedRelation < 0f ? -0.067f : 0.1f)));
            float cultureMultiplier = 1f + (kingdom.Culture == clan.Culture
                ? 0.15f
                : (kingdom.Leader == Hero.MainHero ? 0f : -0.15f));
            float clanSettlementValue = clan.CalculateTotalSettlementBaseValue();
            float settlementValueInKingdom = clan.CalculateTotalSettlementValueForFaction(kingdom);
            int warPartyLimit = clan.WarPartyLimit;
            float kingdomValuePerParty = 0f;
            if (!clan.IsMinorFaction)
            {
                float totalKingdomSettlementValue = 0f;
                foreach (Town town in kingdom.Fiefs)
                {
                    totalKingdomSettlementValue += town.Settlement.GetSettlementValueForFaction(kingdom);
                }

                int otherKingdomWarPartyLimit = 0;
                foreach (Clan kingdomClan in kingdom.Clans)
                {
                    if (!kingdomClan.IsUnderMercenaryService && kingdomClan != clan)
                    {
                        otherKingdomWarPartyLimit += kingdomClan.WarPartyLimit;
                    }
                }

                kingdomValuePerParty = totalKingdomSettlementValue
                    / (otherKingdomWarPartyLimit + warPartyLimit);
            }

            float reliability = HeroHelper.CalculateReliabilityConstant(clan.Leader);
            float nativeEquivalentDays = DailyRateBalance.ToNativeCalendarDays(
                (float)(CampaignTime.Now - clan.LastFactionChangeTime).ToDays);
            float tenurePenalty = 4000f * (15f - MathF.Sqrt(MathF.Min(225f, nativeEquivalentDays)));
            int townCount = 0;
            int castleCount = 0;
            foreach (Town fief in clan.Fiefs)
            {
                if (fief.IsCastle)
                {
                    castleCount++;
                }
                else
                {
                    townCount++;
                }
            }

            float fiefPenalty = -70000f - castleCount * 10000f - townCount * 30000f;
            fiefPenalty /= 0.15f;
            float score = -kingdomValuePerParty * MathF.Sqrt(warPartyLimit) * 0.15f * 0.2f
                + fiefPenalty * reliability
                - tenurePenalty;
            score *= relationMultiplier * cultureMultiplier;
            score = !(relationMultiplier < 1f) || !(clanSettlementValue - settlementValueInKingdom < 0f)
                ? score + (clanSettlementValue - settlementValueInKingdom)
                : score + relationMultiplier * (clanSettlementValue - settlementValueInKingdom);
            if (relationMultiplier < 1f)
            {
                score += (1f - relationMultiplier) * 200000f;
            }

            if (kingdom.Leader == Hero.MainHero)
            {
                score = score > 0f ? score * 0.2f : score * 5f;
            }

            __result = score + (kingdom.Leader == Hero.MainHero
                ? -1000000f * relationMultiplier
                : 0f);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class WarDurationDiplomacyBalancePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DefaultDiplomacyModel), "GetWarScale");
        }

        [HarmonyPrefix]
        private static bool Prefix(
            IFaction factionDeclaresWar,
            IFaction factionDeclaredWar,
            ref float __result)
        {
            if (!DiplomacyBalanceMath.IsEnabled)
            {
                return true;
            }

            StanceLink stance = factionDeclaresWar.GetStanceWith(factionDeclaredWar);
            if (!stance.IsAtWar)
            {
                __result = 1f;
                return false;
            }

            int casualtiesAgainstTarget = stance.GetCasualties(factionDeclaredWar);
            int casualtiesAgainstSource = stance.GetCasualties(factionDeclaresWar);
            int nativeEquivalentWarDays = MathF.Max(
                1,
                (int)DiplomacyBalanceMath.GetNativeEquivalentDays(stance.WarStartDate));
            if (nativeEquivalentWarDays <= 20)
            {
                __result = 1f;
                return false;
            }

            float warScale = MathF.Max(casualtiesAgainstTarget + casualtiesAgainstSource, 1)
                / (20f * MathF.Pow(nativeEquivalentWarDays, 1.5f));
            __result = warScale >= 1f || warScale <= 0f ? 1f : warScale;
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultDiplomacyModel), "IsPeaceSuitable")]
    internal static class PeaceSuitabilityDurationBalancePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            IFaction factionDeclaresPeace,
            IFaction factionDeclaredPeace,
            ref bool __result)
        {
            if (!DiplomacyBalanceMath.IsEnabled)
            {
                return true;
            }

            if (factionDeclaresPeace.IsEliminated || factionDeclaredPeace.IsEliminated)
            {
                __result = false;
                return false;
            }

            float declaringPeaceScore = Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringPeace(
                factionDeclaresPeace,
                factionDeclaredPeace);
            float opposingPeaceScore = Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringPeace(
                factionDeclaredPeace,
                factionDeclaresPeace);
            float declaringFactionSettlementValue = Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(
                factionDeclaresPeace);
            float peacePressure = opposingPeaceScore > 0f
                ? opposingPeaceScore - declaringPeaceScore
                : Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(factionDeclaredPeace)
                    - opposingPeaceScore;
            float nativeEquivalentWarDays = DiplomacyBalanceMath.GetNativeEquivalentDays(
                factionDeclaresPeace.GetStanceWith(factionDeclaredPeace).WarStartDate);

            __result = !(peacePressure > declaringFactionSettlementValue && nativeEquivalentWarDays < 150f);
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultDiplomacyModel), "GetDailyTributeToPay")]
    internal static class TributeDurationDiplomacyBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int tributeDurationInDays)
        {
            if (DiplomacyBalanceMath.IsEnabled && tributeDurationInDays > 0)
            {
                tributeDurationInDays = DailyRateBalance.ToGregorianCalendarDays(tributeDurationInDays);
            }
        }
    }

    // Clan-finance balance already reduces the actual daily tribute payment.
    // Make war scoring use that same effective daily amount so a longer treaty
    // neither exaggerates nor understates the pressure to resume a war.
    [HarmonyPatch]
    internal static class TributeWarScoreDiplomacyBalancePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DefaultDiplomacyModel), "ApplyTributeEffectToBenefitScoreForWar");
        }

        [HarmonyPrefix]
        private static bool Prefix(
            IFaction factionDeclaresWar,
            IFaction factionDeclaredWar,
            IFaction evaluatingFaction,
            ref float benefitScore)
        {
            if (!DiplomacyBalanceMath.IsEnabled)
            {
                return true;
            }

            StanceLink stance = factionDeclaresWar.GetStanceWith(factionDeclaredWar);
            if (stance.GetRemainingTributePaymentCount() == 0)
            {
                return false;
            }

            float declaringFactionTribute = stance.GetDailyTributeToPay(factionDeclaresWar)
                * DailyRateBalance.Factor;
            float declaredFactionTribute = stance.GetDailyTributeToPay(factionDeclaredWar)
                * DailyRateBalance.Factor;
            if (declaringFactionTribute == 0f && declaredFactionTribute == 0f)
            {
                return false;
            }

            bool evaluatingFactionPays = stance.GetDailyTributeToPay(evaluatingFaction.MapFaction) > 0
                && evaluatingFaction.MapFaction == factionDeclaresWar;
            if (declaringFactionTribute > 0f)
            {
                float prosperity = factionDeclaresWar.Fiefs.Sum(town => town.Prosperity) + 1f;
                float multiplier = 1f + declaringFactionTribute / prosperity;
                benefitScore = evaluatingFactionPays
                    ? benefitScore * multiplier
                    : benefitScore / multiplier;
            }
            else if (declaredFactionTribute > 0f)
            {
                float prosperity = factionDeclaredWar.Fiefs.Sum(town => town.Prosperity) + 1f;
                float multiplier = 1f + declaredFactionTribute / prosperity;
                benefitScore = evaluatingFactionPays
                    ? benefitScore * multiplier
                    : benefitScore / multiplier;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultAllianceModel), "get_MaxDurationOfAlliance")]
    internal static class AllianceDurationDiplomacyBalancePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref CampaignTime __result)
        {
            if (!DiplomacyBalanceMath.IsEnabled)
            {
                return true;
            }

            __result = CampaignTime.Years(1f);
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultAllianceModel), "get_MaxDurationOfWarParticipation")]
    internal static class AllianceWarParticipationDurationBalancePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref CampaignTime __result)
        {
            if (!DiplomacyBalanceMath.IsEnabled)
            {
                return true;
            }

            __result = CampaignTime.Years(0.5f);
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultAllianceModel), "GetCallToWarCost")]
    internal static class AllianceCallToWarCostBalancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            __result = DailyRateBalance.ScaleDurationBasedLumpSum(__result);
        }
    }

    [HarmonyPatch(typeof(AllianceCampaignBehavior), "DailyTickClan")]
    internal static class AllianceAggressivenessBalancePatch
    {
        private static readonly FieldInfo AggressivenessField = AccessTools.Field(
            typeof(Clan),
            "_aggressiveness");
        private static bool _canWriteAggressiveness = true;

        [HarmonyPrefix]
        private static void Prefix(Clan clan, out float __state)
        {
            __state = clan == null ? 0f : clan.Aggressiveness;
        }

        [HarmonyPostfix]
        private static void Postfix(Clan clan, float __state)
        {
            if (!DiplomacyBalanceMath.IsEnabled || clan == null)
            {
                return;
            }

            float dailyChange = clan.Aggressiveness - __state;
            if (dailyChange < 0f && _canWriteAggressiveness && AggressivenessField != null)
            {
                try
                {
                    float scaledValue = __state + dailyChange * DailyRateBalance.Factor;
                    AggressivenessField.SetValue(clan, MathF.Clamp(scaledValue, 0f, 100f));
                }
                catch (Exception exception)
                {
                    _canWriteAggressiveness = false;
                    Diagnostics.Error("Alliance aggressiveness balance was disabled because Bannerlord did not expose its backing field.", exception);
                }
            }
        }
    }
}
