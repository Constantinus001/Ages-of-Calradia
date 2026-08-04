using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Verifies the exact Bannerlord methods that define calendar semantics
    /// before they are patched. The module is intentionally built for the
    /// supported game version; an unexpected method body is safer to skip than
    /// to patch blindly after an engine update.
    /// </summary>
    internal static class CalendarPatchSafetyAudit
    {
        private const string DefaultCampaignStartTimeHash = "2fc5c37833f31d25189a8575abd0578e0f1d4ba8fa164f97137f0f570b3247ac";
        private const string CampaignTimeDaysInSeasonHash = "fee1b3082d2e1ed072bf42732f90549313be3a70e59b88942b5e6875f24fce74";
        private const string CampaignTimeDaysInYearHash = "64db062705054b0598a6e68ab7bf25ba29d4963c7d9cca6359685de2eb52d37b";
        private const string CampaignTimeElapsedSeasonsHash = "5302269694a7778493f602d4fbc93c477d0de3aa44183b13782c643c08e86a3c";
        private const string CampaignTimeElapsedYearsHash = "4ff8f3f8733a8ee98bfa7d8478baeb5c96426bafa1aa71673c307c153cdb1516";
        private const string CampaignTimeRemainingSeasonsHash = "7ace3b47bc9b694fef8ba3de92ff3a9b627cfb5c6d83fed97b68268c5aebba91";
        private const string CampaignTimeRemainingYearsHash = "7e52b9f58588c3047f28c09892076744be0b8ee60ef25e1d27d87703aa28f601";
        private const string CampaignTimeToSeasonsHash = "0413029867e697303826fda9dfbd4c1a97a2744c198203637e7bc73e14610ff3";
        private const string CampaignTimeToYearsHash = "ec6d20d6159dd257b8263d183d9d24076805d06d1a651f522969c1695e8de55c";
        private const string CampaignTimeDayOfSeasonHash = "31f3618ff774d90149c1103820d66da869bef052a870e6544c27d078b61ed252";
        private const string CampaignTimeDayOfYearHash = "86ef22173aee650eca5c34e8204ea0ce21e25f823163e6866174b99df5a27d95";
        private const string CampaignTimeWeekOfSeasonHash = "b42ab07223aeeea1c8258fe00475d8d53aea7f1616cbb183e81999daea6b6f58";
        private const string CampaignTimeSeasonOfYearHash = "2ad50531d792d0e524272a11df2f2dd8073f42229004251ddc7130525b613717";
        private const string CampaignTimeYearHash = "5055049ec16bfc4cb7752df10533f47e441fd2d996ec7339436c665aacfb73c4";
        private const string CampaignTimeYearsHash = "45dc62d44c7cd77e760fe65660bc226000d518298ed56cd7a1f1bcc7bbb99e73";
        private const string CampaignTimeYearsFromNowHash = "7d86df0621edf3f9c84dc3795331e90f0290c5780027da84a1da0d6fc5e28d92";
        private const string CampaignTimeToStringHash = "b028ffe9357e9aa51e8545f2f1aad544fdacdf685e309b16dd00502c08e2448f";
        private const string MapTimeTrackerTickHash = "876cbc64330b72822fe5ff542afe108d60a32f7d0e825db09979ca2c19a1646e";
        private const string CampaignTickMapTimeHash = "85d84a6d53d8bdf96a11396104b1d22be643a6860cda651029196cb35a22d19a";

        private static readonly object SyncRoot = new object();
        private static readonly List<string> CoreValidationFailures = new List<string>();

        internal static void BeginStartupAudit()
        {
            lock (SyncRoot)
            {
                CoreValidationFailures.Clear();
            }

            Assembly assembly = typeof(Campaign).Assembly;
            Diagnostics.Info(
                "Patch safety audit started. CampaignSystemAssembly="
                + assembly.GetName().Name
                + "; Version=" + assembly.GetName().Version
                + "; Location=" + assembly.Location);
        }

        internal static bool ValidateCampaignTimeCalendarTargets()
        {
            bool valid = true;
            valid &= ValidateRequiredTarget(
                "DefaultCampaignTimeModel.get_CampaignStartTime",
                AccessTools.Method(typeof(DefaultCampaignTimeModel), "get_CampaignStartTime", Type.EmptyTypes),
                typeof(CampaignTime),
                false,
                Type.EmptyTypes,
                DefaultCampaignStartTimeHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_DaysInSeason",
                AccessTools.Method(typeof(CampaignTime), "get_DaysInSeason", Type.EmptyTypes),
                typeof(int),
                true,
                Type.EmptyTypes,
                CampaignTimeDaysInSeasonHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_DaysInYear",
                AccessTools.Method(typeof(CampaignTime), "get_DaysInYear", Type.EmptyTypes),
                typeof(int),
                true,
                Type.EmptyTypes,
                CampaignTimeDaysInYearHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_ElapsedSeasonsUntilNow",
                AccessTools.Method(typeof(CampaignTime), "get_ElapsedSeasonsUntilNow", Type.EmptyTypes),
                typeof(float),
                false,
                Type.EmptyTypes,
                CampaignTimeElapsedSeasonsHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_ElapsedYearsUntilNow",
                AccessTools.Method(typeof(CampaignTime), "get_ElapsedYearsUntilNow", Type.EmptyTypes),
                typeof(float),
                false,
                Type.EmptyTypes,
                CampaignTimeElapsedYearsHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_RemainingSeasonsFromNow",
                AccessTools.Method(typeof(CampaignTime), "get_RemainingSeasonsFromNow", Type.EmptyTypes),
                typeof(float),
                false,
                Type.EmptyTypes,
                CampaignTimeRemainingSeasonsHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_RemainingYearsFromNow",
                AccessTools.Method(typeof(CampaignTime), "get_RemainingYearsFromNow", Type.EmptyTypes),
                typeof(float),
                false,
                Type.EmptyTypes,
                CampaignTimeRemainingYearsHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_ToSeasons",
                AccessTools.Method(typeof(CampaignTime), "get_ToSeasons", Type.EmptyTypes),
                typeof(double),
                false,
                Type.EmptyTypes,
                CampaignTimeToSeasonsHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_ToYears",
                AccessTools.Method(typeof(CampaignTime), "get_ToYears", Type.EmptyTypes),
                typeof(double),
                false,
                Type.EmptyTypes,
                CampaignTimeToYearsHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_GetDayOfSeason",
                AccessTools.Method(typeof(CampaignTime), "get_GetDayOfSeason", Type.EmptyTypes),
                typeof(int),
                false,
                Type.EmptyTypes,
                CampaignTimeDayOfSeasonHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_GetDayOfYear",
                AccessTools.Method(typeof(CampaignTime), "get_GetDayOfYear", Type.EmptyTypes),
                typeof(int),
                false,
                Type.EmptyTypes,
                CampaignTimeDayOfYearHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_GetWeekOfSeason",
                AccessTools.Method(typeof(CampaignTime), "get_GetWeekOfSeason", Type.EmptyTypes),
                typeof(int),
                false,
                Type.EmptyTypes,
                CampaignTimeWeekOfSeasonHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_GetSeasonOfYear",
                AccessTools.Method(typeof(CampaignTime), "get_GetSeasonOfYear", Type.EmptyTypes),
                typeof(CampaignTime.Seasons),
                false,
                Type.EmptyTypes,
                CampaignTimeSeasonOfYearHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.get_GetYear",
                AccessTools.Method(typeof(CampaignTime), "get_GetYear", Type.EmptyTypes),
                typeof(int),
                false,
                Type.EmptyTypes,
                CampaignTimeYearHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.Years(float)",
                AccessTools.Method(typeof(CampaignTime), "Years", new[] { typeof(float) }),
                typeof(CampaignTime),
                true,
                new[] { typeof(float) },
                CampaignTimeYearsHash);
            valid &= ValidateRequiredTarget(
                "CampaignTime.YearsFromNow(float)",
                AccessTools.Method(typeof(CampaignTime), "YearsFromNow", new[] { typeof(float) }),
                typeof(CampaignTime),
                true,
                new[] { typeof(float) },
                CampaignTimeYearsFromNowHash);
            return valid;
        }

        internal static bool ValidateCampaignTimeStringTarget()
        {
            return ValidateRequiredTarget(
                "CampaignTime.ToString()",
                AccessTools.Method(typeof(CampaignTime), nameof(CampaignTime.ToString), Type.EmptyTypes),
                typeof(string),
                false,
                Type.EmptyTypes,
                CampaignTimeToStringHash);
        }

        internal static bool ValidateMapTimeTrackerTarget(MethodBase target)
        {
            return ValidateRequiredTarget(
                "MapTimeTracker.Tick(float)",
                target,
                typeof(void),
                false,
                new[] { typeof(float) },
                MapTimeTrackerTickHash);
        }

        internal static bool ValidateCampaignPacingTarget(MethodBase target)
        {
            return ValidateOptionalTarget(
                "Campaign.TickMapTime(float)",
                target,
                typeof(void),
                false,
                new[] { typeof(float) },
                CampaignTickMapTimeHash);
        }

        internal static void EnsureCoreTargetsValidated()
        {
            string failures;
            lock (SyncRoot)
            {
                failures = string.Join("; ", CoreValidationFailures);
            }

            if (!string.IsNullOrWhiteSpace(failures))
            {
                throw new InvalidOperationException(
                    "Bannerlord patch target validation failed. Core calendar runtime was not enabled. " + failures);
            }
        }

        internal static void WriteHarmonyPatchAudit(string harmonyId)
        {
            try
            {
                MethodBase[] targets = Harmony.GetAllPatchedMethods()
                    .Where(
                        method =>
                        {
                            Patches patches = Harmony.GetPatchInfo(method);
                            return patches != null && patches.Owners.Contains(harmonyId);
                        })
                    .OrderBy(DescribeMethod, StringComparer.Ordinal)
                    .ToArray();

                Diagnostics.Info("Patch safety audit: CalendarTargets=" + targets.Length + ".");
                foreach (MethodBase target in targets)
                {
                    Patches patches = Harmony.GetPatchInfo(target);
                    string[] externalOwners = patches.Owners
                        .Where(owner => !string.Equals(owner, harmonyId, StringComparison.Ordinal))
                        .OrderBy(owner => owner, StringComparer.Ordinal)
                        .ToArray();
                    string hash = GetIlHash(target) ?? "<unavailable>";
                    string message = "Patch audit target=" + DescribeMethod(target)
                        + "; IL_SHA256=" + hash
                        + "; ExternalOwners="
                        + (externalOwners.Length == 0 ? "<none>" : string.Join(",", externalOwners))
                        + ".";
                    Diagnostics.Info(message);
                    if (externalOwners.Length > 0)
                    {
                        CrashFlightRecorder.Record("PatchAudit", message);
                    }
                }
            }
            catch (Exception exception)
            {
                // Diagnostics must never affect startup or patch registration.
                Diagnostics.Error("Patch safety audit could not inspect Harmony owners.", exception);
            }
        }

        private static bool ValidateRequiredTarget(
            string feature,
            MethodBase target,
            Type expectedReturnType,
            bool expectedStatic,
            Type[] expectedParameterTypes,
            string expectedHash)
        {
            string failure;
            bool valid = ValidateTarget(
                feature,
                target,
                expectedReturnType,
                expectedStatic,
                expectedParameterTypes,
                expectedHash,
                out failure);
            if (valid)
            {
                return true;
            }

            lock (SyncRoot)
            {
                if (!CoreValidationFailures.Contains(failure))
                {
                    CoreValidationFailures.Add(failure);
                }
            }

            Diagnostics.Info("Required patch target rejected: " + failure);
            CrashFlightRecorder.Record("PatchAudit", "Required target rejected: " + failure);
            return false;
        }

        private static bool ValidateOptionalTarget(
            string feature,
            MethodBase target,
            Type expectedReturnType,
            bool expectedStatic,
            Type[] expectedParameterTypes,
            string expectedHash)
        {
            string failure;
            bool valid = ValidateTarget(
                feature,
                target,
                expectedReturnType,
                expectedStatic,
                expectedParameterTypes,
                expectedHash,
                out failure);
            if (!valid)
            {
                Diagnostics.Info("Optional patch target rejected; feature remains disabled: " + failure);
                CrashFlightRecorder.Record("PatchAudit", "Optional target rejected: " + failure);
            }

            return valid;
        }

        private static bool ValidateTarget(
            string feature,
            MethodBase target,
            Type expectedReturnType,
            bool expectedStatic,
            Type[] expectedParameterTypes,
            string expectedHash,
            out string failure)
        {
            MethodInfo method = target as MethodInfo;
            if (method == null)
            {
                failure = feature + " was not found.";
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (method.ReturnType != expectedReturnType
                || method.IsStatic != expectedStatic
                || parameters.Length != expectedParameterTypes.Length
                || parameters.Where((parameter, index) => parameter.ParameterType != expectedParameterTypes[index]).Any())
            {
                failure = feature + " has an unexpected signature: " + DescribeMethod(method) + ".";
                return false;
            }

            string actualHash = GetIlHash(method);
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                failure = feature + " has an unknown IL fingerprint. Expected="
                    + expectedHash + "; Actual=" + (actualHash ?? "<unavailable>") + ".";
                return false;
            }

            Diagnostics.Info("Patch target validated: " + feature + "; IL_SHA256=" + actualHash + ".");
            failure = null;
            return true;
        }

        private static string GetIlHash(MethodBase method)
        {
            try
            {
                MethodBody body = method.GetMethodBody();
                if (body == null)
                {
                    return null;
                }

                byte[] bytes = body.GetILAsByteArray();
                if (bytes == null || bytes.Length == 0)
                {
                    return null;
                }

                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(bytes);
                    StringBuilder builder = new StringBuilder(hash.Length * 2);
                    foreach (byte value in hash)
                    {
                        builder.Append(value.ToString("x2"));
                    }

                    return builder.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        private static string DescribeMethod(MethodBase method)
        {
            if (method == null)
            {
                return "<null>";
            }

            MethodInfo info = method as MethodInfo;
            string returnType = info == null ? "<constructor>" : info.ReturnType.Name;
            string parameters = string.Join(
                ",",
                method.GetParameters().Select(parameter => parameter.ParameterType.Name));
            return (method.DeclaringType == null ? "<unknown>" : method.DeclaringType.FullName)
                + "." + method.Name + "(" + parameters + "):" + returnType;
        }
    }
}
