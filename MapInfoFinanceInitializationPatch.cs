using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

namespace TwelveMonthCalendar
{
    // The crash originates in MapInfoVM.UpdatePlayerInfo, before NavalDLC
    // delegates to DefaultClanFinanceModel. Target that proven base method
    // instead of a NavalDLC wrapper that can be bypassed by its own override.
    [HarmonyPatch(typeof(MapInfoVM), "UpdatePlayerInfo")]
    internal static class MapInfoFinanceInitializationPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo nativeCall = AccessTools.Method(
                typeof(ClanFinanceModel),
                "CalculateClanGoldChange",
                new[] { typeof(Clan), typeof(bool), typeof(bool), typeof(bool) });
            MethodInfo guardedCall = AccessTools.Method(
                typeof(MapInfoFinanceInitializationPatch),
                "CalculateClanGoldChangeWithCurrentGame");

            int replacements = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (nativeCall != null
                    && guardedCall != null
                    && Equals(instruction.operand, nativeCall))
                {
                    CodeInstruction replacement = new CodeInstruction(OpCodes.Call, guardedCall);
                    replacement.labels.AddRange(instruction.labels);
                    yield return replacement;
                    replacements++;
                    continue;
                }

                yield return instruction;
            }

            Diagnostics.Info(
                "MapInfoVM clan-finance call sites redirected through the immediate campaign-context guard: "
                + replacements + ".");
        }

        private static ExplainedNumber CalculateClanGoldChangeWithCurrentGame(
            ClanFinanceModel financeModel,
            Clan clan,
            bool includeDescriptions,
            bool applyWithdrawals,
            bool includeDetails)
        {
            if (!DefaultClanFinanceInitialization.IsNativeFinanceContextReady())
            {
                // A safe zero value is preferable to permanently poisoning the
                // native finance model while Bannerlord's own context is null.
                DefaultClanFinanceInitialization.LogDeferredMapFinance();
                return new ExplainedNumber(0f, includeDescriptions);
            }

            return financeModel.CalculateClanGoldChange(
                clan,
                includeDescriptions,
                applyWithdrawals,
                includeDetails);
        }
    }
}
