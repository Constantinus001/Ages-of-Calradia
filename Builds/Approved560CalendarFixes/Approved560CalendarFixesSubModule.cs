using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace AgesOfCalradia.Approved560CalendarFixes
{
    public sealed class Approved560CalendarFixesSubModule : MBSubModuleBase
    {
        internal const string HarmonyId = "AgesOfCalradia.Approved560CalendarFixes.560F1B51";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                ApprovedCalendarBridge.Validate();
                CalendarFixTargets.Validate();

                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(Approved560CalendarFixesSubModule).Assembly);

                // The approved DLL remains byte-for-byte untouched. Remove only
                // the superseded calendar patches whose replacements were
                // successfully installed above. Political renderer patches are
                // deliberately outside this list.
                LegacyPatchControl.UnpatchDeclaringTypes(
                    "TwelveMonthCalendar.MapTimeTrackerPatch",
                    "TwelveMonthCalendar.WorkshopProductionBalancePatch",
                    "TwelveMonthCalendar.WorkshopFoodContextPatch",
                    "TwelveMonthCalendar.VillageFoodProductionBalancePatch",
                    "TwelveMonthCalendar.VillageProductionBalancePatch",
                    "TwelveMonthCalendar.SettlementDemandBalancePatch",
                    "TwelveMonthCalendar.SettlementBudgetBalancePatch",
                    "TwelveMonthCalendar.SettlementMarketSmoothingBalancePatch",
                    "TwelveMonthCalendar.KingdomWarCooldownPatch");
            }
            catch (Exception exception)
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchAll(HarmonyId);
                    _harmony = null;
                }
                System.Diagnostics.Trace.WriteLine(
                    "AgesOfCalradia approved-build calendar fixes disabled safely: " + exception);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }
            base.OnSubModuleUnloaded();
        }
    }

    [HarmonyPatch]
    internal static class MapClockMeridiemLayoutPatch
    {
        private static PropertyInfo _timeOfDayProperty;

        private static MethodBase TargetMethod()
        {
            Assembly approved = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(
                    a.GetName().Name,
                    "AgesOfCalradia",
                    StringComparison.Ordinal));
            Type clockType = approved == null
                ? null
                : approved.GetType("TwelveMonthCalendar.CalendarMapTimeControlVM", false);
            if (clockType == null)
                throw new TypeLoadException("TwelveMonthCalendar.CalendarMapTimeControlVM");

            _timeOfDayProperty = clockType.GetProperty(
                "TimeOfDay",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_timeOfDayProperty == null || !_timeOfDayProperty.CanRead || !_timeOfDayProperty.CanWrite)
                throw new MissingMemberException(clockType.FullName, "TimeOfDay");

            MethodInfo refreshClock = clockType.GetMethod(
                "RefreshClock",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (refreshClock == null)
                throw new MissingMethodException(clockType.FullName, "RefreshClock");
            return refreshClock;
        }

        private static void Postfix(object __instance)
        {
            if (__instance == null || Campaign.Current == null || _timeOfDayProperty == null)
                return;

            string current = _timeOfDayProperty.GetValue(__instance, null) as string;
            if (string.IsNullOrWhiteSpace(current))
                return;

            double hourInDay = CampaignTime.Now.ToHours % CampaignTime.HoursInDay;
            if (hourInDay < 0d)
                hourInDay += CampaignTime.HoursInDay;
            string formatted = FormatForVerification(current, (int)Math.Floor(hourInDay));
            _timeOfDayProperty.SetValue(__instance, formatted, null);
        }

        internal static string FormatForVerification(string current, int hour)
        {
            string clock = (current ?? string.Empty).Replace("\r", string.Empty);
            int newline = clock.IndexOf('\n');
            if (newline >= 0)
                clock = clock.Substring(0, newline);
            clock = clock.Trim();
            if (clock.EndsWith(" AM", StringComparison.OrdinalIgnoreCase)
                || clock.EndsWith(" PM", StringComparison.OrdinalIgnoreCase))
            {
                clock = clock.Substring(0, clock.Length - 3).TrimEnd();
            }

            int normalizedHour = ((hour % 24) + 24) % 24;
            return clock + "\n" + (normalizedHour < 12 ? "AM" : "PM");
        }
    }

    internal static class ApprovedCalendarBridge
    {
        private const string ApprovedMainSha256 =
            "560F1B5181F8CC2EFE51564D8675FD3089E722606FA55B0B166D36ECD9868D8E";
        private static Type _settingsType;
        private static Type _dailyRateType;
        private static Type _timeMathType;
        private static Type _formatterType;
        private static Type _financeModelType;
        private static Type _settlementFoodModelType;
        private static Type _strategicMarkerType;
        private static PropertyInfo _extendedEnabled;
        private static PropertyInfo _annualEnabled;
        private static PropertyInfo _factor;
        private static PropertyInfo _campaignMultiplier;
        private static MethodInfo _format;
        private static FieldInfo _nativeFinance;
        private static FieldInfo _nativeSettlementFood;

        internal static Type FinanceModelType { get { return _financeModelType; } }
        internal static Type SettlementFoodModelType { get { return _settlementFoodModelType; } }
        internal static Type StrategicMarkerType { get { return _strategicMarkerType; } }

        internal static void Validate()
        {
            Assembly approved = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "AgesOfCalradia", StringComparison.Ordinal));
            if (approved == null)
                throw new InvalidOperationException("The approved AgesOfCalradia main DLL is not loaded.");
            using (SHA256 sha = SHA256.Create())
            using (System.IO.FileStream stream = System.IO.File.OpenRead(approved.Location))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                if (!string.Equals(actual, ApprovedMainSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Calendar fixes require approved main DLL " + ApprovedMainSha256
                        + "; loaded " + actual + ".");
            }

            _settingsType = RequireType(approved, "TwelveMonthCalendar.CalendarSettingsState");
            _dailyRateType = RequireType(approved, "TwelveMonthCalendar.DailyRateBalance");
            _timeMathType = RequireType(approved, "TwelveMonthCalendar.CalendarTimeMath");
            _formatterType = RequireType(approved, "TwelveMonthCalendar.CalendarFormatter");
            _financeModelType = RequireType(approved, "TwelveMonthCalendar.CalendarClanFinanceModel");
            _settlementFoodModelType = RequireType(approved, "TwelveMonthCalendar.CalendarSettlementFoodModel");
            _strategicMarkerType = RequireType(approved, "TwelveMonthCalendar.CalendarWorldStrategicMarkerVM");

            _extendedEnabled = RequireProperty(_settingsType, "ExtendedCalendarEnabled");
            _annualEnabled = RequireProperty(_settingsType, "AnnualRateBalanceEnabled");
            _factor = RequireProperty(_dailyRateType, "Factor");
            _campaignMultiplier = RequireProperty(_timeMathType, "CampaignTimeMultiplier");
            _format = _formatterType.GetMethod(
                "Format",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(CampaignTime) },
                null);
            if (_format == null)
                throw new MissingMethodException(_formatterType.FullName, "Format(CampaignTime)");
            _nativeFinance = _financeModelType.GetField(
                "_native",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_nativeFinance == null)
                throw new MissingFieldException(_financeModelType.FullName, "_native");
            _nativeSettlementFood = _settlementFoodModelType.GetField(
                "_native",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_nativeSettlementFood == null)
                throw new MissingFieldException(_settlementFoodModelType.FullName, "_native");
        }

        internal static bool ExtendedEnabled
        {
            get { return _extendedEnabled != null && (bool)_extendedEnabled.GetValue(null, null); }
        }

        internal static bool AnnualEnabled
        {
            get { return _annualEnabled != null && (bool)_annualEnabled.GetValue(null, null); }
        }

        internal static float Factor
        {
            get { return _factor == null ? 1f : (float)_factor.GetValue(null, null); }
        }

        internal static float CampaignMultiplier
        {
            get { return _campaignMultiplier == null ? 1f : (float)_campaignMultiplier.GetValue(null, null); }
        }

        internal static string Format(CampaignTime time)
        {
            return _format == null ? null : _format.Invoke(null, new object[] { time }) as string;
        }

        internal static bool WrapsNativeFinance(object instance)
        {
            return instance != null
                && _nativeFinance != null
                && _nativeFinance.GetValue(instance) is DefaultClanFinanceModel;
        }

        internal static SettlementFoodModel GetNativeSettlementFood(object instance)
        {
            return instance == null || _nativeSettlementFood == null
                ? null
                : _nativeSettlementFood.GetValue(instance) as SettlementFoodModel;
        }

        internal static float ScaleDailyProbability(float probability)
        {
            if (!ExtendedEnabled) return probability;
            probability = Math.Max(0f, Math.Min(1f, probability));
            return 1f - (float)Math.Pow(1d - probability, Factor);
        }

        private static Type RequireType(Assembly assembly, string name)
        {
            Type type = assembly.GetType(name, false);
            if (type == null) throw new TypeLoadException(name);
            return type;
        }

        private static PropertyInfo RequireProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) throw new MissingMemberException(type.FullName, name);
            return property;
        }
    }

    internal static class CalendarFixTargets
    {
        internal static MethodInfo CampaignTickMapTime;
        internal static MethodInfo WorkshopConversionSpeed;
        internal static MethodInfo TownFoodStocksChange;
        internal static MethodInfo VillageProduction;
        internal static MethodInfo SettlementDemand;
        internal static MethodInfo SettlementSupplyDemand;
        internal static MethodInfo RandomWarDecision;
        internal static ConstructorInfo StrategicMarkerConstructor;

        internal static void Validate()
        {
            CampaignTickMapTime = AccessTools.Method(typeof(Campaign), "TickMapTime", new[] { typeof(float) });
            WorkshopConversionSpeed = AccessTools.Method(
                typeof(DefaultWorkshopModel),
                "GetEffectiveConversionSpeedOfProduction");
            TownFoodStocksChange = AccessTools.Method(
                ApprovedCalendarBridge.SettlementFoodModelType,
                "CalculateTownFoodStocksChange");
            VillageProduction = AccessTools.Method(
                typeof(DefaultVillageProductionCalculatorModel),
                "CalculateDailyProductionAmount",
                new[] { typeof(Village), typeof(ItemObject) });
            SettlementDemand = AccessTools.Method(
                typeof(DefaultSettlementEconomyModel),
                "GetDailyDemandForCategory",
                new[] { typeof(Town), typeof(ItemCategory), typeof(int) });
            SettlementSupplyDemand = AccessTools.Method(
                typeof(DefaultSettlementEconomyModel),
                "GetSupplyDemandForCategory",
                new[]
                {
                    typeof(Town), typeof(ItemCategory), typeof(float),
                    typeof(float), typeof(float), typeof(float)
                });
            RandomWarDecision = AccessTools.Method(
                typeof(KingdomDecisionProposalBehavior),
                "GetRandomWarDecision");
            StrategicMarkerConstructor = ApprovedCalendarBridge.StrategicMarkerType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault();

            if (CampaignTickMapTime == null) throw new MissingMethodException("Campaign.TickMapTime(float)");
            if (WorkshopConversionSpeed == null)
                throw new MissingMethodException("DefaultWorkshopModel.GetEffectiveConversionSpeedOfProduction");
            if (TownFoodStocksChange == null)
                throw new MissingMethodException(
                    ApprovedCalendarBridge.SettlementFoodModelType.FullName,
                    "CalculateTownFoodStocksChange");
            if (VillageProduction == null)
                throw new MissingMethodException(
                    "DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount(Village, ItemObject)");
            if (SettlementDemand == null)
                throw new MissingMethodException(
                    "DefaultSettlementEconomyModel.GetDailyDemandForCategory(Town, ItemCategory, int)");
            if (SettlementSupplyDemand == null)
                throw new MissingMethodException(
                    "DefaultSettlementEconomyModel.GetSupplyDemandForCategory(Town, ItemCategory, float, float, float, float)");
            if (RandomWarDecision == null)
                throw new MissingMethodException("KingdomDecisionProposalBehavior.GetRandomWarDecision");
            if (StrategicMarkerConstructor == null)
                throw new MissingMethodException("CalendarWorldStrategicMarkerVM constructor");
            ParameterInfo[] markerParameters = StrategicMarkerConstructor.GetParameters();
            if (markerParameters.Length != 11
                || markerParameters[4].ParameterType != typeof(bool)
                || markerParameters[7].ParameterType != typeof(bool))
            {
                throw new InvalidOperationException("Approved strategic marker constructor no longer matches the verified contract.");
            }

            foreach (string method in new[] { "CalculateClanGoldChange", "CalculateClanIncome", "CalculateClanExpenses" })
            {
                if (AccessTools.Method(ApprovedCalendarBridge.FinanceModelType, method) == null)
                    throw new MissingMethodException(ApprovedCalendarBridge.FinanceModelType.FullName, method);
            }
        }
    }

    internal static class LegacyPatchControl
    {
        internal static void UnpatchDeclaringTypes(params string[] declaringTypeNames)
        {
            HashSet<string> targets = new HashSet<string>(declaringTypeNames, StringComparer.Ordinal);
            foreach (MethodBase original in Harmony.GetAllPatchedMethods().ToArray())
            {
                Patches info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                foreach (Patch patch in info.Prefixes
                    .Concat(info.Postfixes)
                    .Concat(info.Transpilers)
                    .Concat(info.Finalizers)
                    .ToArray())
                {
                    MethodInfo patchMethod = patch.PatchMethod;
                    if (patchMethod != null
                        && patchMethod.DeclaringType != null
                        && targets.Contains(patchMethod.DeclaringType.FullName))
                    {
                        new Harmony(Approved560CalendarFixesSubModule.HarmonyId)
                            .Unpatch(original, patchMethod);
                    }
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class CampaignSimulationTimeFix
    {
        private static MethodBase TargetMethod() { return CalendarFixTargets.CampaignTickMapTime; }

        private static void Prefix(ref float realDt)
        {
            if (ApprovedCalendarBridge.ExtendedEnabled)
                realDt *= ApprovedCalendarBridge.CampaignMultiplier;
        }
    }

    [HarmonyPatch]
    internal static class WorkshopProductionFix
    {
        private static MethodBase TargetMethod() { return CalendarFixTargets.WorkshopConversionSpeed; }

        private static void Prefix(Workshop workshop, ref float speed)
        {
            // Food workshops remain on Bannerlord's native daily cadence in
            // the vanilla-food test mode. Other workshops retain the annual
            // conversion installed by v1.5.12.
            if (ApprovedCalendarBridge.AnnualEnabled
                && !VanillaFoodCadence.ProducesFood(workshop))
                speed *= ApprovedCalendarBridge.Factor;
        }
    }

    internal static class VanillaFoodCadence
    {
        internal static bool IsFood(ItemCategory category)
        {
            return category != null
                && category.Properties == ItemCategory.Property.BonusToFoodStores;
        }

        internal static bool ProducesFood(Workshop workshop)
        {
            if (workshop == null || workshop.WorkshopType == null)
                return false;

            foreach (WorkshopType.Production production in workshop.WorkshopType.Productions)
            {
                foreach (var output in production.Outputs)
                {
                    if (IsFood(output.Item1))
                        return true;
                }
            }
            return false;
        }

        internal static float ScaleDemandForVerification(float nativeDemand, bool isFood, float factor)
        {
            return isFood ? nativeDemand : nativeDemand * factor;
        }

        internal static float ScaleFinalForVerification(float nativeResult, float factor)
        {
            return nativeResult * factor;
        }

        internal static void ScaleFinal(ref ExplainedNumber value)
        {
            float factor = ApprovedCalendarBridge.Factor;
            float scaledResult = value.ResultNumber * factor;
            if (!value.IncludeDescriptions)
            {
                value = new ExplainedNumber(scaledResult, false, null);
                return;
            }

            ExplainedNumber scaled = new ExplainedNumber(0f, true, null);
            foreach (var line in value.GetLines())
                scaled.Add(line.number * factor, new TextObject("{=!}" + line.name));
            if (scaled.GetLines().Count == 0 && Math.Abs(scaledResult) > 0.0001f)
                scaled.Add(scaledResult, new TextObject("{=AoCCalendarCadence}Calendar cadence"));
            value = scaled;
        }
    }

    // Native target: DefaultVillageProductionCalculatorModel's discrete food
    // production method. Its legacy annual postfix is retired at startup, so
    // this method intentionally has no replacement: village food goods remain
    // on the native daily cadence. The category-aware production patch below
    // preserves annual conversion for non-food village outputs.
    [HarmonyPatch(typeof(DefaultVillageProductionCalculatorModel), "CalculateDailyProductionAmount")]
    internal static class FoodAwareVillageProductionFix
    {
        private static void Postfix(ItemObject item, ref ExplainedNumber __result)
        {
            if (!ApprovedCalendarBridge.AnnualEnabled
                || item == null
                || VanillaFoodCadence.IsFood(item.ItemCategory))
                return;

            VanillaFoodCadence.ScaleFinal(ref __result);
        }
    }

    // Native target: DefaultSettlementEconomyModel.GetDailyDemandForCategory.
    // Food demand stays vanilla; non-food demand keeps one annual conversion.
    // The legacy budget postfix is retired because Bannerlord derives budget
    // directly from this demand and scaling the budget again was factor^2.
    [HarmonyPatch(typeof(DefaultSettlementEconomyModel), "GetDailyDemandForCategory")]
    internal static class FoodAwareSettlementDemandFix
    {
        private static void Postfix(ItemCategory category, ref float __result)
        {
            if (ApprovedCalendarBridge.AnnualEnabled && !VanillaFoodCadence.IsFood(category))
                __result *= ApprovedCalendarBridge.Factor;
        }
    }

    // Native target: DefaultSettlementEconomyModel.GetSupplyDemandForCategory.
    // Food prices retain native smoothing because their production and demand
    // both run at native cadence. Non-food categories retain Gregorian annual
    // smoothing. If the signature changes, startup validation disables the
    // whole sidecar before any legacy patch is removed.
    [HarmonyPatch(typeof(DefaultSettlementEconomyModel), "GetSupplyDemandForCategory")]
    internal static class FoodAwareMarketSmoothingFix
    {
        private static void Postfix(
            ItemCategory category,
            float dailySupply,
            float dailyDemand,
            float oldSupply,
            float oldDemand,
            ref ValueTuple<float, float> __result)
        {
            if (!ApprovedCalendarBridge.AnnualEnabled || VanillaFoodCadence.IsFood(category))
                return;

            const float nativeDailySmoothing = 0.15f;
            float factor = ApprovedCalendarBridge.Factor;
            float smoothing = 1f - (float)Math.Pow(1f - nativeDailySmoothing, factor);
            float supply = Math.Max(0.1f, oldSupply * (1f - smoothing) + dailySupply * smoothing);
            float demand = oldDemand * (1f - smoothing) + dailyDemand * smoothing;
            __result = new ValueTuple<float, float>(supply, demand);
        }
    }

    // Native target: the approved main DLL's
    // CalendarSettlementFoodModel.CalculateTownFoodStocksChange wrapper.
    // Purpose: run the complete food calculation with native daily values,
    // then annualize only its final surplus or deficit. The prefix
    // is hash-locked to 560F1B51 and falls back to the approved implementation
    // when its native model cannot be resolved. Verify-Approved560CalendarFixes
    // checks target registration and the final-result scaling contract.
    [HarmonyPatch]
    internal static class TownMarketFoodAccountingFix
    {
        private static MethodBase TargetMethod()
        {
            return CalendarFixTargets.TownFoodStocksChange;
        }

        private static bool Prefix(
            object __instance,
            Town town,
            bool includeMarketStocks,
            bool includeDescriptions,
            ref ExplainedNumber __result)
        {
            if (!ApprovedCalendarBridge.AnnualEnabled)
            {
                return true;
            }

            SettlementFoodModel native = ApprovedCalendarBridge.GetNativeSettlementFood(__instance);
            if (native == null || town == null)
            {
                return true;
            }

            ExplainedNumber nativeResult = native.CalculateTownFoodStocksChange(
                town,
                includeMarketStocks: includeMarketStocks,
                includeDescriptions: includeDescriptions);
            VanillaFoodCadence.ScaleFinal(ref nativeResult);
            __result = nativeResult;
            return false;
        }

        internal static float CombineForVerification(
            float nativeDirect,
            float nativeWithMarket,
            bool includeMarketStocks,
            float factor)
        {
            float selected = includeMarketStocks ? nativeWithMarket : nativeDirect;
            return VanillaFoodCadence.ScaleFinalForVerification(selected, factor);
        }
    }

    [HarmonyPatch(typeof(DefaultTournamentModel), "GetTournamentStartChance")]
    internal static class TournamentStartFix
    {
        private static readonly MethodInfo WeekGetter = AccessTools.PropertyGetter(
            typeof(CampaignTime), "GetWeekOfSeason");
        private static readonly MethodInfo Normalize = AccessTools.Method(
            typeof(TournamentStartFix), nameof(NormalizeWeekSlot));

        private static int NormalizeWeekSlot(int week)
        {
            int slot = week % 3;
            return slot < 0 ? slot + 3 : slot;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (!replaced && WeekGetter != null && instruction.Calls(WeekGetter))
                {
                    yield return new CodeInstruction(OpCodes.Call, Normalize);
                    replaced = true;
                }
            }
            if (!replaced)
                throw new InvalidOperationException("Tournament week gate was not found.");
        }

        private static void Postfix(ref float __result)
        {
            if (ApprovedCalendarBridge.ExtendedEnabled && __result > 0f)
                __result = ApprovedCalendarBridge.ScaleDailyProbability(__result);
        }
    }

    [HarmonyPatch(typeof(DefaultTournamentModel), "GetTournamentEndChance")]
    internal static class TournamentEndFix
    {
        private static bool Prefix(TournamentGame tournament, ref float __result)
        {
            if (!ApprovedCalendarBridge.ExtendedEnabled) return true;
            if (tournament == null)
            {
                __result = 0f;
                return false;
            }
            float nativeElapsed = tournament.CreationTime.ElapsedDaysUntilNow * ApprovedCalendarBridge.Factor;
            float nativeChance = Math.Max(0f, (nativeElapsed - 10f) * 0.05f);
            __result = ApprovedCalendarBridge.ScaleDailyProbability(Math.Min(1f, nativeChance));
            return false;
        }
    }

    [HarmonyPatch(typeof(CampaignSceneNotificationHelper), "GetFormalDayAndSeasonText")]
    internal static class SceneNotificationDateFix
    {
        private static bool Prefix(CampaignTime time, ref TextObject __result)
        {
            if (!ApprovedCalendarBridge.ExtendedEnabled) return true;
            string date = ApprovedCalendarBridge.Format(time);
            if (string.IsNullOrWhiteSpace(date)) return true;
            TextObject result = new TextObject("{=AoCFormalCalendarDate}{DATE}");
            result.SetTextVariable("DATE", date);
            __result = result;
            return false;
        }
    }

    internal static class WageText
    {
        internal static string Format(int nativeDailyWage)
        {
            if (!ApprovedCalendarBridge.AnnualEnabled)
                return nativeDailyWage.ToString(CultureInfo.CurrentCulture);
            return (nativeDailyWage * ApprovedCalendarBridge.Factor)
                .ToString("0.##", CultureInfo.CurrentCulture) + "/day";
        }
    }

    [HarmonyPatch(typeof(PartyVM), "RefreshTopInformation")]
    internal static class PartyTotalWageFix
    {
        private static void Postfix(PartyVM __instance)
        {
            if (__instance != null && MobileParty.MainParty != null)
                __instance.MainPartyTotalWeeklyCostLbl = WageText.Format(MobileParty.MainParty.TotalWage);
        }
    }

    [HarmonyPatch(typeof(PartyVM), "RefreshCurrentCharacterInformation")]
    internal static class PartyCharacterWageFix
    {
        private static void Postfix(PartyVM __instance)
        {
            if (__instance != null
                && __instance.CurrentCharacter != null
                && __instance.CurrentCharacter.Character != null
                && __instance.IsCurrentCharacterWageEnabled)
            {
                __instance.CurrentCharacterWageLbl = WageText.Format(
                    __instance.CurrentCharacter.Character.TroopWage);
            }
        }
    }

    [HarmonyPatch(typeof(PartyVM), nameof(PartyVM.RefreshValues))]
    internal static class PartyWageHintFix
    {
        private static void Postfix(PartyVM __instance)
        {
            if (__instance == null || !ApprovedCalendarBridge.AnnualEnabled) return;
            TextObject hint = new TextObject(
                "{=AoCEffectiveCalendarWage}Effective wage per Gregorian calendar day. Native troop rates and all native wage modifiers are annualized once when clan finance is applied.");
            if (__instance.TotalWageHint != null) __instance.TotalWageHint.HintText = hint;
            if (__instance.WageHint != null) __instance.WageHint.HintText = hint;
        }
    }

    [HarmonyPatch]
    internal static class FinanceDoubleScaleFix
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string method in new[] { "CalculateClanGoldChange", "CalculateClanIncome", "CalculateClanExpenses" })
                yield return AccessTools.Method(ApprovedCalendarBridge.FinanceModelType, method);
        }

        private static void Postfix(object __instance, ref ExplainedNumber __result)
        {
            if (!ApprovedCalendarBridge.AnnualEnabled
                || !ApprovedCalendarBridge.WrapsNativeFinance(__instance)) return;
            float factor = ApprovedCalendarBridge.Factor;
            if (factor <= 0f || Math.Abs(factor - 1f) < 0.000001f) return;

            float correctedResult = __result.ResultNumber / factor;
            if (!__result.IncludeDescriptions)
            {
                __result = new ExplainedNumber(correctedResult, false, null);
                return;
            }

            ExplainedNumber corrected = new ExplainedNumber(0f, true, null);
            foreach (var line in __result.GetLines())
                corrected.Add(line.number / factor, new TextObject("{=!}" + line.name));
            if (corrected.GetLines().Count == 0 && Math.Abs(correctedResult) > 0.0001f)
                corrected.Add(correctedResult, new TextObject("{=AoCCalendarCadence}Calendar cadence"));
            __result = corrected;
        }
    }

    [HarmonyPatch]
    internal static class WarCooldownFix
    {
        private const float GregorianTruceDays = 87f;
        private static MethodBase TargetMethod() { return CalendarFixTargets.RandomWarDecision; }

        private static void Postfix(Clan clan, ref KingdomDecision __result)
        {
            if (!ApprovedCalendarBridge.ExtendedEnabled || __result == null || clan == null || clan.Kingdom == null)
                return;
            DeclareWarDecision war = __result as DeclareWarDecision;
            if (war == null || war.FactionToDeclareWarOn == null) return;
            StanceLink stance = clan.Kingdom.GetStanceWith(war.FactionToDeclareWarOn);
            if (stance.PeaceDeclarationDate.ElapsedDaysUntilNow <= GregorianTruceDays)
                __result = null;
        }
    }

    [HarmonyPatch]
    internal static class StrategicUiCityLabelFix
    {
        private static MethodBase TargetMethod()
        {
            return CalendarFixTargets.StrategicMarkerConstructor;
        }

        private static void Prefix(object[] __args)
        {
            // Constructor arguments 4 and 7 are isTown and showLabel in the
            // approved 560F1B51 build. Keep town labels enabled at every zoom;
            // the constructor still suppresses labels for castles.
            if (__args != null
                && __args.Length == 11
                && __args[4] is bool
                && (bool)__args[4])
            {
                __args[7] = true;
            }
        }
    }

    /// <summary>
    /// Runtime widget required by the reviewed UI REDESIGN prefab. The test
    /// build originally supplied it from the alignment-diagnostics assembly;
    /// keeping the focused widget here makes release scrolling functional
    /// without shipping diagnostics or changing the approved main DLL.
    /// </summary>
    public sealed class WorldEventsRowSnapScrollablePanel : ScrollablePanel
    {
        private float _rowStride = 1f;
        private float _wheelTarget;
        private bool _hasWheelTarget;
        private bool _resetOnShow;
        private bool _wasVisible;
        private float _previousInnerHeight;

        public WorldEventsRowSnapScrollablePanel(UIContext context) : base(context) { }

        [Editor(false)]
        public float RowStride
        {
            get { return _rowStride; }
            set { _rowStride = Math.Max(1f, value); }
        }

        [Editor(false)]
        public bool ResetOnShow
        {
            get { return _resetOnShow; }
            set { _resetOnShow = value; }
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);

            float innerHeight = InnerPanel == null ? 0f : InnerPanel.Size.Y;
            bool contentBecameReady = _previousInnerHeight <= 0.5f && innerHeight > 0.5f;
            if (_resetOnShow && IsVisible && (!_wasVisible || contentBecameReady))
            {
                float minimum = VerticalScrollbar == null ? 0f : VerticalScrollbar.MinValue;
                if (VerticalScrollbar != null) VerticalScrollbar.SetValueForced(minimum);
                if (InnerPanel != null) InnerPanel.ScaledPositionYOffset = -minimum;
                _wheelTarget = minimum;
                _hasWheelTarget = true;
            }

            _wasVisible = IsVisible;
            _previousInnerHeight = innerHeight;
        }

        protected override bool OnPreviewMouseScroll()
        {
            return true;
        }

        protected override void OnMouseScroll()
        {
            if (VerticalScrollbar == null || EventManager.DeltaMouseScroll == 0f)
                return;

            float current = VerticalScrollbar.ValueFloat;
            if (!_hasWheelTarget || Math.Abs(current - _wheelTarget) > _rowStride)
                _wheelTarget = (float)Math.Round(current / _rowStride) * _rowStride;

            float direction = EventManager.DeltaMouseScroll < 0f ? 1f : -1f;
            _wheelTarget = Math.Max(
                VerticalScrollbar.MinValue,
                Math.Min(VerticalScrollbar.MaxValue, _wheelTarget + direction * _rowStride));
            _hasWheelTarget = true;
            SetVerticalScrollTarget(_wheelTarget, 0.10f);
        }
    }
}
