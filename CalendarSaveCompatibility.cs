using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace TwelveMonthCalendar
{
    #pragma warning disable CS0618 // Intentional one-time read of obsolete v1.3 save data.

    /// <summary>
    /// Decode-only definition for saves created before the soft-profile update.
    /// New saves never write this type, so they do not require the module just
    /// to deserialize. Loading a legacy save once with this version and saving
    /// it again removes the legacy module-owned data.
    /// </summary>
    [Obsolete("Legacy save migration type. New saves do not write it.")]
    public sealed class CalendarSaveCompatibilityMarker
    {
        [SaveableField(1)]
        public string ModuleId = "_TwelveMonthCalendar";

        [SaveableField(2)]
        public int SchemaVersion = 2;
    }

    /// <summary>
    /// Snapshot of the settings that alter campaign simulation. Display-only
    /// choices deliberately do not live here, so players may still change date
    /// formats and names while a campaign is active without changing time.
    ///
    /// The SaveableField attributes remain only so this type can decode v1.3
    /// saves during one-time migration. New saves store its serialized string,
    /// not the type itself.
    /// </summary>
    public sealed class CalendarCampaignProfile
    {
        // v3 stores Bannerlord's direct Campaign.SpeedUpMultiplier value. v1/v2
        // stored an additional TickMapTime multiplier and are converted once on
        // load so their old default of 1.00 becomes Bannerlord's native 4x
        // fast-forward speed rather than an unexpectedly slow 1x speed.
        public const int CurrentSchemaVersion = 3;
        private const string SerializedRootName = "CalendarCampaignProfile";
        private const int MaximumSerializedLength = 32768;

        [SaveableField(1)]
        public int SchemaVersion = CurrentSchemaVersion;

        [SaveableField(2)]
        public string CalendarSystem = "Gregorian12Month";

        [SaveableField(3)]
        public bool UseLeapYears;

        [SaveableField(4)]
        public string MonthLengthSignature = string.Empty;

        [SaveableField(5)]
        public bool AutoCampaignTimeScale;

        [SaveableField(6)]
        public float CampaignTimeScale;

        [SaveableField(7)]
        public float NormalPlayTimeMultiplier;

        [SaveableField(8)]
        public float FastForwardTimeMultiplier;

        [SaveableField(9)]
        public bool UseCalendarMonthPregnancy;

        [SaveableField(10)]
        public int PregnancyDurationMonths;

        [SaveableField(11)]
        public float PregnancyDurationInDays;

        [SaveableField(12)]
        public float RenownGainMultiplier;

        [SaveableField(13)]
        public bool BalancePartyImpairment;

        [SaveableField(14)]
        public bool BalancePrisonerRecruitment;

        [SaveableField(15)]
        public bool BalanceNpcMarriage;

        [SaveableField(16)]
        public bool BalanceMapTracks;

        [SaveableField(17)]
        public bool BalanceQuestDeadlines;

        [SaveableField(18)]
        public string Fingerprint = string.Empty;

        [SaveableField(19)]
        public float LordDeathRateMultiplier = CalendarSettingsState.DefaultLordDeathRateMultiplier;

        public static CalendarCampaignProfile Capture()
        {
            CalendarCampaignProfile profile = new CalendarCampaignProfile
            {
                CalendarSystem = CalendarSettingsState.CalendarSystem,
                UseLeapYears = CalendarSettingsState.UseLeapYears,
                MonthLengthSignature = BuildMonthLengthSignature(
                    CalendarSettingsState.MonthLengthsSnapshot()),
                AutoCampaignTimeScale = CalendarSettingsState.AutoCampaignTimeScale,
                CampaignTimeScale = CalendarSettingsState.CampaignTimeScale,
                NormalPlayTimeMultiplier = CalendarSettingsState.NormalPlayTimeMultiplier,
                FastForwardTimeMultiplier = CalendarSettingsState.FastForwardTimeMultiplier,
                UseCalendarMonthPregnancy = CalendarSettingsState.UseCalendarMonthPregnancy,
                PregnancyDurationMonths = CalendarSettingsState.PregnancyDurationMonths,
                PregnancyDurationInDays = CalendarSettingsState.PregnancyDurationInDays,
                RenownGainMultiplier = CalendarSettingsState.RenownGainMultiplier,
                LordDeathRateMultiplier = CalendarSettingsState.LordDeathRateMultiplier,
                BalancePartyImpairment = CalendarSettingsState.BalancePartyImpairment,
                BalancePrisonerRecruitment = CalendarSettingsState.BalancePrisonerRecruitment,
                BalanceNpcMarriage = CalendarSettingsState.BalanceNpcMarriage,
                BalanceMapTracks = CalendarSettingsState.BalanceMapTracks,
                BalanceQuestDeadlines = CalendarSettingsState.BalanceQuestDeadlines
            };
            profile.RefreshFingerprint();
            return profile;
        }

        /// <summary>
        /// Upgrades profiles created before the direct fast-forward-speed
        /// setting. v1 did not store the lord-death multiplier; v1 and v2 both
        /// stored an additional TickMapTime multiplier rather than Bannerlord's
        /// direct Campaign.SpeedUpMultiplier value.
        /// </summary>
        public bool TryUpgradeLegacyProfile()
        {
            if (SchemaVersion == CurrentSchemaVersion)
            {
                return true;
            }

            if (SchemaVersion != 1 && SchemaVersion != 2)
            {
                return false;
            }

            int legacySchema = SchemaVersion;
            NormalPlayTimeMultiplier = CalendarSettingsState.DefaultNormalPlayTimeMultiplier;
            FastForwardTimeMultiplier = CalendarSettingsState.ConvertLegacyFastForwardPacingToSpeed(
                FastForwardTimeMultiplier);
            SchemaVersion = CurrentSchemaVersion;
            if (legacySchema == 1)
            {
                LordDeathRateMultiplier = CalendarSettingsState.DefaultLordDeathRateMultiplier;
            }

            RefreshFingerprint();
            return true;
        }

        public bool TryValidate(out string failure)
        {
            if (SchemaVersion != CurrentSchemaVersion)
            {
                failure = "unsupported profile schema " + SchemaVersion + ".";
                return false;
            }

            if (!string.Equals(
                    CalendarSystem,
                    CalendarSettingsState.CalendarSystem,
                    StringComparison.OrdinalIgnoreCase))
            {
                failure = "calendar system '" + CalendarSystem + "' is not supported.";
                return false;
            }

            int[] monthLengths;
            if (!TryGetMonthLengths(out monthLengths))
            {
                failure = "month lengths are invalid.";
                return false;
            }

            if (!IsFinite(CampaignTimeScale) || CampaignTimeScale < 0.01f || CampaignTimeScale > 1f
                || !IsFinite(NormalPlayTimeMultiplier)
                || !NearlyEqual(NormalPlayTimeMultiplier, CalendarSettingsState.DefaultNormalPlayTimeMultiplier)
                || !IsFinite(FastForwardTimeMultiplier)
                || FastForwardTimeMultiplier < CalendarSettingsState.MinimumPacingMultiplier
                || FastForwardTimeMultiplier > CalendarSettingsState.MaximumPacingMultiplier
                || PregnancyDurationMonths < 1
                || !IsFinite(PregnancyDurationInDays) || PregnancyDurationInDays < 0.1f
                || !IsFinite(RenownGainMultiplier) || RenownGainMultiplier < 0f || RenownGainMultiplier > 1f
                || !IsFinite(LordDeathRateMultiplier) || LordDeathRateMultiplier < 0f || LordDeathRateMultiplier > 1f)
            {
                failure = "one or more numeric values are outside their safe range.";
                return false;
            }

            string expectedFingerprint = BuildFingerprint();
            if (!string.IsNullOrWhiteSpace(Fingerprint)
                && !string.Equals(Fingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                failure = "the profile fingerprint does not match its saved values.";
                return false;
            }

            failure = null;
            return true;
        }

        public bool TryGetMonthLengths(out int[] monthLengths)
        {
            monthLengths = null;
            if (string.IsNullOrWhiteSpace(MonthLengthSignature))
            {
                return false;
            }

            string[] parts = MonthLengthSignature.Split(',');
            if (parts.Length != 12)
            {
                return false;
            }

            int[] parsed = new int[parts.Length];
            int total = 0;
            for (int index = 0; index < parts.Length; index++)
            {
                int value;
                if (!int.TryParse(
                        parts[index],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out value)
                    || value < 1
                    || value > 1000)
                {
                    return false;
                }

                parsed[index] = value;
                total += value;
            }

            if (total != 365)
            {
                return false;
            }

            monthLengths = parsed;
            return true;
        }

        public string DescribeDifferencesFromCurrentSettings()
        {
            List<string> differences = new List<string>();
            if (UseLeapYears != CalendarSettingsState.UseLeapYears) differences.Add("UseLeapYears");
            if (!string.Equals(
                    MonthLengthSignature,
                    BuildMonthLengthSignature(CalendarSettingsState.MonthLengthsSnapshot()),
                    StringComparison.Ordinal)) differences.Add("MonthLengths");
            if (AutoCampaignTimeScale != CalendarSettingsState.AutoCampaignTimeScale) differences.Add("AutoCampaignTimeScale");
            if (!NearlyEqual(CampaignTimeScale, CalendarSettingsState.CampaignTimeScale)) differences.Add("CampaignTimeScale");
            if (!NearlyEqual(NormalPlayTimeMultiplier, CalendarSettingsState.NormalPlayTimeMultiplier)) differences.Add("NormalPlayTimeMultiplier");
            if (!NearlyEqual(FastForwardTimeMultiplier, CalendarSettingsState.FastForwardTimeMultiplier)) differences.Add("FastForwardTimeMultiplier");
            if (UseCalendarMonthPregnancy != CalendarSettingsState.UseCalendarMonthPregnancy) differences.Add("UseCalendarMonthPregnancy");
            if (PregnancyDurationMonths != CalendarSettingsState.PregnancyDurationMonths) differences.Add("PregnancyDurationMonths");
            if (!NearlyEqual(PregnancyDurationInDays, CalendarSettingsState.PregnancyDurationInDays)) differences.Add("PregnancyDurationInDays");
            if (!NearlyEqual(RenownGainMultiplier, CalendarSettingsState.RenownGainMultiplier)) differences.Add("RenownGainMultiplier");
            if (!NearlyEqual(LordDeathRateMultiplier, CalendarSettingsState.LordDeathRateMultiplier)) differences.Add("LordDeathRateMultiplier");
            if (BalancePartyImpairment != CalendarSettingsState.BalancePartyImpairment) differences.Add("BalancePartyImpairment");
            if (BalancePrisonerRecruitment != CalendarSettingsState.BalancePrisonerRecruitment) differences.Add("BalancePrisonerRecruitment");
            if (BalanceNpcMarriage != CalendarSettingsState.BalanceNpcMarriage) differences.Add("BalanceNpcMarriage");
            if (BalanceMapTracks != CalendarSettingsState.BalanceMapTracks) differences.Add("BalanceMapTracks");
            if (BalanceQuestDeadlines != CalendarSettingsState.BalanceQuestDeadlines) differences.Add("BalanceQuestDeadlines");
            return differences.Count == 0 ? string.Empty : string.Join(",", differences);
        }

        public void RefreshFingerprint()
        {
            Fingerprint = BuildFingerprint();
        }

        /// <summary>
        /// Serialize only primitive string data for the campaign behavior. This
        /// avoids embedding a module-owned save type in newly written saves.
        /// </summary>
        public string Serialize()
        {
            RefreshFingerprint();
            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            XmlElement root = document.CreateElement(SerializedRootName);
            document.AppendChild(root);
            root.SetAttribute("SchemaVersion", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            root.SetAttribute("CalendarSystem", CalendarSystem ?? string.Empty);
            root.SetAttribute("UseLeapYears", UseLeapYears.ToString());
            root.SetAttribute("MonthLengthSignature", MonthLengthSignature ?? string.Empty);
            root.SetAttribute("AutoCampaignTimeScale", AutoCampaignTimeScale.ToString());
            root.SetAttribute("CampaignTimeScale", CampaignTimeScale.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("NormalPlayTimeMultiplier", NormalPlayTimeMultiplier.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("FastForwardSpeedMultiplier", FastForwardTimeMultiplier.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("UseCalendarMonthPregnancy", UseCalendarMonthPregnancy.ToString());
            root.SetAttribute("PregnancyDurationMonths", PregnancyDurationMonths.ToString(CultureInfo.InvariantCulture));
            root.SetAttribute("PregnancyDurationInDays", PregnancyDurationInDays.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("RenownGainMultiplier", RenownGainMultiplier.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("LordDeathRateMultiplier", LordDeathRateMultiplier.ToString("R", CultureInfo.InvariantCulture));
            root.SetAttribute("BalancePartyImpairment", BalancePartyImpairment.ToString());
            root.SetAttribute("BalancePrisonerRecruitment", BalancePrisonerRecruitment.ToString());
            root.SetAttribute("BalanceNpcMarriage", BalanceNpcMarriage.ToString());
            root.SetAttribute("BalanceMapTracks", BalanceMapTracks.ToString());
            root.SetAttribute("BalanceQuestDeadlines", BalanceQuestDeadlines.ToString());
            root.SetAttribute("Fingerprint", Fingerprint ?? string.Empty);
            return document.OuterXml;
        }

        public static bool TryDeserialize(
            string serializedProfile,
            out CalendarCampaignProfile profile,
            out string failure)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(serializedProfile)
                || serializedProfile.Length > MaximumSerializedLength)
            {
                failure = "the serialized profile is empty or too large.";
                return false;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                document.LoadXml(serializedProfile);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != SerializedRootName)
                {
                    failure = "the serialized profile root is invalid.";
                    return false;
                }

                CalendarCampaignProfile candidate = new CalendarCampaignProfile();
                if (!TryReadInt(root, "SchemaVersion", out candidate.SchemaVersion)
                    || !TryReadString(root, "CalendarSystem", out candidate.CalendarSystem)
                    || !TryReadBoolean(root, "UseLeapYears", out candidate.UseLeapYears)
                    || !TryReadString(root, "MonthLengthSignature", out candidate.MonthLengthSignature)
                    || !TryReadBoolean(root, "AutoCampaignTimeScale", out candidate.AutoCampaignTimeScale)
                    || !TryReadFloat(root, "CampaignTimeScale", out candidate.CampaignTimeScale)
                    || !TryReadFloat(root, "NormalPlayTimeMultiplier", out candidate.NormalPlayTimeMultiplier)
                    || !TryReadBoolean(root, "UseCalendarMonthPregnancy", out candidate.UseCalendarMonthPregnancy)
                    || !TryReadInt(root, "PregnancyDurationMonths", out candidate.PregnancyDurationMonths)
                    || !TryReadFloat(root, "PregnancyDurationInDays", out candidate.PregnancyDurationInDays)
                    || !TryReadFloat(root, "RenownGainMultiplier", out candidate.RenownGainMultiplier)
                    || !TryReadBoolean(root, "BalancePartyImpairment", out candidate.BalancePartyImpairment)
                    || !TryReadBoolean(root, "BalancePrisonerRecruitment", out candidate.BalancePrisonerRecruitment)
                    || !TryReadBoolean(root, "BalanceNpcMarriage", out candidate.BalanceNpcMarriage)
                    || !TryReadBoolean(root, "BalanceMapTracks", out candidate.BalanceMapTracks)
                    || !TryReadBoolean(root, "BalanceQuestDeadlines", out candidate.BalanceQuestDeadlines)
                    || !TryReadString(root, "Fingerprint", out candidate.Fingerprint))
                {
                    failure = "the serialized profile has missing or invalid values.";
                    return false;
                }

                string fastForwardAttribute = candidate.SchemaVersion >= 3
                    ? "FastForwardSpeedMultiplier"
                    : "FastForwardTimeMultiplier";
                if (!TryReadFloat(root, fastForwardAttribute, out candidate.FastForwardTimeMultiplier))
                {
                    failure = "the serialized profile has a missing or invalid fast-forward speed.";
                    return false;
                }

                if (candidate.SchemaVersion == 1)
                {
                    candidate.LordDeathRateMultiplier = CalendarSettingsState.DefaultLordDeathRateMultiplier;
                }
                else if (!TryReadFloat(root, "LordDeathRateMultiplier", out candidate.LordDeathRateMultiplier))
                {
                    failure = "the serialized profile has a missing or invalid lord-death rate.";
                    return false;
                }

                if (!candidate.TryUpgradeLegacyProfile())
                {
                    failure = "unsupported profile schema " + candidate.SchemaVersion + ".";
                    return false;
                }

                if (!candidate.TryValidate(out failure))
                {
                    return false;
                }

                profile = candidate;
                return true;
            }
            catch (Exception exception)
            {
                failure = "the serialized profile could not be read: " + exception.GetType().Name + ".";
                return false;
            }
        }

        private string BuildFingerprint()
        {
            string payload = string.Join(
                "|",
                new[]
                {
                    SchemaVersion.ToString(CultureInfo.InvariantCulture),
                    CalendarSystem ?? string.Empty,
                    UseLeapYears.ToString(),
                    MonthLengthSignature ?? string.Empty,
                    AutoCampaignTimeScale.ToString(),
                    CampaignTimeScale.ToString("R", CultureInfo.InvariantCulture),
                    NormalPlayTimeMultiplier.ToString("R", CultureInfo.InvariantCulture),
                    FastForwardTimeMultiplier.ToString("R", CultureInfo.InvariantCulture),
                    UseCalendarMonthPregnancy.ToString(),
                    PregnancyDurationMonths.ToString(CultureInfo.InvariantCulture),
                    PregnancyDurationInDays.ToString("R", CultureInfo.InvariantCulture),
                    RenownGainMultiplier.ToString("R", CultureInfo.InvariantCulture),
                    LordDeathRateMultiplier.ToString("R", CultureInfo.InvariantCulture),
                    BalancePartyImpairment.ToString(),
                    BalancePrisonerRecruitment.ToString(),
                    BalanceNpcMarriage.ToString(),
                    BalanceMapTracks.ToString(),
                    BalanceQuestDeadlines.ToString()
                });

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return string.Concat(hash.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static bool TryReadString(XmlElement root, string name, out string value)
        {
            value = root.GetAttribute(name);
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryReadBoolean(XmlElement root, string name, out bool value)
        {
            return bool.TryParse(root.GetAttribute(name), out value);
        }

        private static bool TryReadFloat(XmlElement root, string name, out float value)
        {
            return float.TryParse(
                    root.GetAttribute(name),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value)
                && IsFinite(value);
        }

        private static bool TryReadInt(XmlElement root, string name, out int value)
        {
            return int.TryParse(
                root.GetAttribute(name),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static string BuildMonthLengthSignature(int[] values)
        {
            return values == null
                ? string.Empty
                : string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        }

        private static bool NearlyEqual(float first, float second)
        {
            return Math.Abs(first - second) < 0.0001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

    }

    /// <summary>
    /// Retained only to decode v1.3 saves which contain module-owned values.
    /// The current campaign behavior saves a primitive string profile instead,
    /// so merely enabling this registry does not lock new saves to the module.
    /// </summary>
    public sealed class TwelveMonthCalendarSaveDefiner : SaveableTypeDefiner
    {
        public TwelveMonthCalendarSaveDefiner()
            : base(485013)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CalendarSaveCompatibilityMarker), 1);
            AddClassDefinition(typeof(CalendarCampaignProfile), 2);
        }
    }

    /// <summary>
    /// Saves the simulation profile as a primitive string. This keeps campaigns
    /// internally consistent while the module is enabled, without embedding a
    /// module-owned type that blocks loading the save without the module.
    /// </summary>
    internal sealed class CalendarCampaignProfileBehavior : CampaignBehaviorBase
    {
        private const string SerializedProfileKey = "RealisticCalendarTweaks.CampaignProfileV3";
        private const string LegacySerializedProfileKey = "TwelveMonthCalendar.CampaignProfileV2";
        private const string LegacyMarkerKey = "TwelveMonthCalendar.SaveCompatibilityMarker";
        private const string LegacyProfileKey = "TwelveMonthCalendar.CampaignProfile";

        private string _serializedCampaignProfile = string.Empty;

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                RestoreLoadedProfile(dataStore);
                return;
            }

            CalendarCampaignProfile profile = CalendarCampaignProfile.Capture();
            _serializedCampaignProfile = profile.Serialize();
            dataStore.SyncData(SerializedProfileKey, ref _serializedCampaignProfile);
            CrashFlightRecorder.Record(
                "CampaignProfile",
                "Saved soft profile fingerprint=" + profile.Fingerprint
                + "; primitive payload only; no module-load marker written.");
        }

        private void RestoreLoadedProfile(IDataStore dataStore)
        {
            bool serializedProfileWasPresent = dataStore.SyncData(
                SerializedProfileKey,
                ref _serializedCampaignProfile);
            if (!serializedProfileWasPresent)
            {
                serializedProfileWasPresent = dataStore.SyncData(
                    LegacySerializedProfileKey,
                    ref _serializedCampaignProfile);
            }

            // Read legacy keys only while loading. They are deliberately not
            // synchronized on save, so a save made with this version drops the
            // old module-owned marker and profile types.
            CalendarSaveCompatibilityMarker legacyMarker = null;
            CalendarCampaignProfile legacyProfile = null;
            bool legacyMarkerWasPresent = dataStore.SyncData(LegacyMarkerKey, ref legacyMarker);
            bool legacyProfileWasPresent = dataStore.SyncData(LegacyProfileKey, ref legacyProfile);

            CalendarCampaignProfile profile;
            string source;
            string failure = null;
            if (serializedProfileWasPresent
                && CalendarCampaignProfile.TryDeserialize(
                    _serializedCampaignProfile,
                    out profile,
                    out failure))
            {
                source = "soft saved profile";
            }
            else if (legacyProfileWasPresent
                && legacyProfile != null
                && legacyProfile.TryUpgradeLegacyProfile()
                && legacyProfile.TryValidate(out failure))
            {
                profile = legacyProfile;
                source = "legacy profile migrated to the soft profile format";
            }
            else
            {
                profile = CalendarCampaignProfile.Capture();
                source = "active local settings captured for a legacy/no-profile save";
                if (serializedProfileWasPresent)
                {
                    Diagnostics.Info(
                        "Saved calendar profile was not applied because " + failure
                        + " Current local settings remain active; the next save will replace it with a valid soft profile.");
                    CrashFlightRecorder.Record("CampaignProfile", "Soft profile validation failed: " + failure);
                }
            }

            string differences = profile.DescribeDifferencesFromCurrentSettings();
            CalendarSettingsState.ApplyPersistedCampaignProfile(profile);
            CalendarSettingsState.Save();
            Diagnostics.Info(
                "Calendar " + source + "."
                + (legacyMarkerWasPresent
                    ? " Legacy module marker will be removed on the next save."
                    : string.Empty)
                + (string.IsNullOrWhiteSpace(differences)
                    ? string.Empty
                    : " Differences=" + differences + "."));
            CrashFlightRecorder.Record(
                "CampaignProfile",
                "Restored " + source + "; fingerprint=" + profile.Fingerprint
                + (legacyMarkerWasPresent ? "; LegacyMarker=true" : string.Empty)
                + (string.IsNullOrWhiteSpace(differences) ? string.Empty : "; Differences=" + differences));
        }
    }

    #pragma warning restore CS0618
}
