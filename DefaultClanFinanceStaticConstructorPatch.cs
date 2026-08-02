using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TwelveMonthCalendar
{
    // Bannerlord v1.4.7 initializes DefaultClanFinanceModel by fetching a
    // number of localized strings through Game.Current. NavalDLC reaches that
    // initializer during campaign creation, where that native lookup throws.
    // Replace only those eager lookups with the verified campaign provider;
    // all actual finance calculations remain Bannerlord's native logic.
    [HarmonyPatch]
    internal static class DefaultClanFinanceStaticConstructorPatch
    {
        private static MethodBase TargetMethod()
        {
            ConstructorInfo initializer = typeof(DefaultClanFinanceModel).TypeInitializer;
            Diagnostics.Info(initializer == null
                ? "DefaultClanFinanceModel static initializer was not found."
                : "DefaultClanFinanceModel static initializer compatibility target found.");
            return initializer;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            List<CodeInstruction> instructions = source.ToList();
            MethodInfo getCurrent = AccessTools.PropertyGetter(typeof(Game), "Current");
            MethodInfo getTextManager = AccessTools.PropertyGetter(typeof(Game), "GameTextManager");
            MethodInfo findText = AccessTools.Method(
                typeof(GameTextManager),
                "FindText",
                new[] { typeof(string), typeof(string) });
            MethodInfo provider = AccessTools.Method(
                typeof(DefaultClanFinanceInitialization),
                "FindFinanceText");

            int replacements = 0;
            for (int index = 0; index < instructions.Count; index++)
            {
                if (index + 4 < instructions.Count
                    && Calls(instructions[index], getCurrent)
                    && Calls(instructions[index + 1], getTextManager)
                    && instructions[index + 2].opcode == OpCodes.Ldstr
                    && instructions[index + 3].opcode == OpCodes.Ldnull
                    && Calls(instructions[index + 4], findText))
                {
                    CodeInstruction textId = new CodeInstruction(
                        OpCodes.Ldstr,
                        instructions[index + 2].operand);
                    textId.labels.AddRange(instructions[index].labels);
                    yield return textId;
                    yield return new CodeInstruction(OpCodes.Call, provider);
                    index += 4;
                    replacements++;
                    continue;
                }

                yield return instructions[index];
            }

            Diagnostics.Info(
                "DefaultClanFinanceModel static initializer finance-text lookups replaced: "
                + replacements + ".");
        }

        private static bool Calls(CodeInstruction instruction, MethodInfo method)
        {
            return method != null && Equals(instruction.operand, method);
        }
    }
}
