using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;

namespace AgesOfCalradiaReligions
{
    internal enum StrategicMapMode
    {
        Political,
        Religion,
        Population,
        Culture
    }

    /// <summary>
    /// Optional World Events integration. It adds a second transparent movie
    /// to the existing Gauntlet layer and patches only the strategic atlas's
    /// colour input. The protected core DLL and WorldCalendar.xml are never
    /// replaced or modified.
    /// </summary>
    internal static class StrategicMapModeIntegration
    {
        private const string WorldScreenTypeName = "TwelveMonthCalendar.WorldCalendarScreen";
        private const string LedgerViewModelTypeName = "TwelveMonthCalendar.CalendarWorldLedgerVM";
        private const string AtlasProviderTypeName = "TwelveMonthCalendar.CalendarStrategicCampaignAtlasTextureProvider";
        private const uint NeutralProvinceColour = 0xFF5B5147u;
        private static readonly string[] LeaveStrategicCommands =
        {
            "ExecuteShowCalendarPage",
            "ExecuteShowSavedSummaries",
            "ExecuteShowCharacterStory",
            "ExecuteShowCompanionsPage",
            "ExecuteShowDiplomacyRelations",
            "ExecuteShowKingdomFinances",
            "ExecuteShowMarriagesPage",
            "ExecuteShowStrategicWarStatistics"
        };

        private static StrategicMapModeViewModel _overlayViewModel;
        private static object _ledgerViewModel;
        private static MethodInfo _buildStrategicMapLayers;
        private static bool _installed;

        internal static StrategicMapMode CurrentMode { get; private set; }

        internal static void Install(Harmony harmony)
        {
            if (_installed || harmony == null) return;

            Type screenType = AccessTools.TypeByName(WorldScreenTypeName);
            Type ledgerType = AccessTools.TypeByName(LedgerViewModelTypeName);
            Type providerType = AccessTools.TypeByName(AtlasProviderTypeName);
            if (screenType == null || ledgerType == null || providerType == null)
            {
                throw new InvalidOperationException("The approved World Events integration types were not found.");
            }

            ConstructorInfo screenConstructor = AccessTools.Constructor(screenType, Type.EmptyTypes);
            MethodInfo screenClose = AccessTools.Method(screenType, "Close", new[] { typeof(bool), typeof(string) });
            MethodInfo atlasUpdate = AccessTools.Method(providerType, "UpdateMapState");
            MethodInfo resolveProvinceColours = AccessTools.Method(providerType, "ResolveProvinceOwnerColors");
            MethodInfo resolveContestedColours = AccessTools.Method(providerType, "ResolveContestedProvinceColors");
            MethodInfo resolveContestedDefenders = AccessTools.Method(providerType, "ResolveContestedDefenderColors");
            MethodInfo showStrategic = AccessTools.Method(ledgerType, "ExecuteShowStrategicMapPage");
            MethodInfo selectMainTab = AccessTools.Method(ledgerType, "SelectTab");
            _buildStrategicMapLayers = AccessTools.Method(ledgerType, "BuildStrategicMapLayers");
            if (screenConstructor == null || screenClose == null || atlasUpdate == null
                || resolveProvinceColours == null || resolveContestedColours == null || resolveContestedDefenders == null
                || showStrategic == null || selectMainTab == null || _buildStrategicMapLayers == null)
            {
                throw new MissingMethodException("The approved World Events integration contract changed.");
            }

            harmony.Patch(screenConstructor, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterWorldScreenConstructed)));
            harmony.Patch(screenClose, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterWorldScreenClosed)));
            harmony.Patch(
                atlasUpdate,
                prefix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(BeforeAtlasUpdate)),
                postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterAtlasUpdate)));
            harmony.Patch(resolveProvinceColours, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterProvinceColoursResolved)));
            harmony.Patch(resolveContestedColours, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterContestedColoursResolved)));
            harmony.Patch(resolveContestedDefenders, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterContestedColoursResolved)));
            harmony.Patch(_buildStrategicMapLayers, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterStrategicMapLayersBuilt)));
            harmony.Patch(showStrategic, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterShowStrategicMap)));
            harmony.Patch(selectMainTab, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterMainTabSelected)));
            foreach (string command in LeaveStrategicCommands)
            {
                MethodInfo method = AccessTools.Method(ledgerType, command);
                if (method != null)
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(StrategicMapModeIntegration), nameof(AfterLeaveStrategicMap)));
                }
            }

            _installed = true;
            CurrentMode = StrategicMapMode.Political;
            ReligionDiagnostics.Info("Compact World Events map-mode overlay registered without changing protected core artifacts.");
        }

        internal static void Reset()
        {
            _overlayViewModel = null;
            _ledgerViewModel = null;
            _buildStrategicMapLayers = null;
            _installed = false;
            CurrentMode = StrategicMapMode.Political;
        }

        internal static void SelectMode(StrategicMapMode mode)
        {
            MapTrace("button selected; previous=" + CurrentMode + "; requested=" + mode + ".");
            CurrentMode = mode;
            if (_overlayViewModel != null) _overlayViewModel.RefreshSelection();
            RefreshStrategicMap();
            ReligionDiagnostics.Info("Strategic map mode selected: " + mode + ".");
        }

        internal static void RefreshStrategicMap()
        {
            if (_ledgerViewModel == null || _buildStrategicMapLayers == null)
            {
                MapTrace("refresh skipped; ledger=" + (_ledgerViewModel == null ? "null" : "ready")
                    + "; builder=" + (_buildStrategicMapLayers == null ? "null" : "ready") + ".");
                return;
            }
            try
            {
                MapTrace("invoking BuildStrategicMapLayers for mode=" + CurrentMode + ".");
                _buildStrategicMapLayers.Invoke(_ledgerViewModel, null);
                MapTrace("BuildStrategicMapLayers completed for mode=" + CurrentMode + ".");
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("The strategic map could not refresh after a map-mode change.", exception);
            }
        }

        internal static void RefreshForMonthlyUpdate()
        {
            if (_overlayViewModel != null
                && _overlayViewModel.IsVisible
                && CurrentMode != StrategicMapMode.Political)
            {
                RefreshStrategicMap();
            }
            if (_overlayViewModel != null && _overlayViewModel.IsCensusPageVisible)
            {
                _overlayViewModel.RefreshCensus();
            }
        }

        internal static void InvokeRealmCommand(string command)
        {
            if (_ledgerViewModel == null || string.IsNullOrEmpty(command)) return;
            try
            {
                MethodInfo method = AccessTools.Method(_ledgerViewModel.GetType(), command);
                if (method == null) throw new MissingMethodException(command);
                method.Invoke(_ledgerViewModel, null);
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("The Realm Affairs page could not change to " + command + ".", exception);
            }
        }

        internal static void HideNativeRealmPageForCensus()
        {
            if (_ledgerViewModel == null) return;
            try
            {
                SetLedgerBoolean("IsKingdomFinancesPage", false);
                SetLedgerBoolean("IsDiplomacyRelationsPage", false);
                // The protected VM raises its shared Realm-ledger visibility
                // notification from the marriages setter. Pulse that state so
                // the old native page body is removed before Census is drawn.
                SetLedgerBoolean("IsMarriagesPage", true);
                SetLedgerBoolean("IsMarriagesPage", false);
                ReligionDiagnostics.Info("Native Realm Affairs selection and page body cleared while the sidecar Census artwork is open.");
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("The previous Realm Affairs selection could not be cleared for Census.", exception);
            }
        }

        private static void SetLedgerBoolean(string propertyName, bool value)
        {
            PropertyInfo property = AccessTools.Property(_ledgerViewModel.GetType(), propertyName);
            MethodInfo setter = property == null ? null : property.GetSetMethod(true);
            if (setter == null) throw new MissingMethodException(_ledgerViewModel.GetType().FullName, "set_" + propertyName);
            setter.Invoke(_ledgerViewModel, new object[] { value });
        }

        private static void AfterWorldScreenConstructed(object __instance)
        {
            try
            {
                FieldInfo layerField = AccessTools.Field(__instance.GetType(), "_layer");
                FieldInfo dataSourceField = AccessTools.Field(__instance.GetType(), "_dataSource");
                GauntletLayer layer = layerField == null ? null : layerField.GetValue(__instance) as GauntletLayer;
                _ledgerViewModel = dataSourceField == null ? null : dataSourceField.GetValue(__instance);
                if (layer == null || _ledgerViewModel == null) return;

                _overlayViewModel = new StrategicMapModeViewModel();
                layer.LoadMovie("AocStrategicMapModes", _overlayViewModel);
                SynchronizeOverlayVisibility();
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("The compact strategic map buttons could not be attached to World Events.", exception);
                _overlayViewModel = null;
                _ledgerViewModel = null;
            }
        }

        private static void AfterShowStrategicMap(object __instance)
        {
            _ledgerViewModel = __instance;
            if (_overlayViewModel != null) _overlayViewModel.IsVisible = true;
        }

        private static void AfterMainTabSelected(object __instance)
        {
            _ledgerViewModel = __instance;
            SynchronizeOverlayVisibility();
        }

        private static void SynchronizeOverlayVisibility()
        {
            if (_overlayViewModel == null || _ledgerViewModel == null) return;
            PropertyInfo strategicProperty = AccessTools.Property(_ledgerViewModel.GetType(), "IsStrategicMap");
            object value = strategicProperty == null ? null : strategicProperty.GetValue(_ledgerViewModel, null);
            _overlayViewModel.IsVisible = value is bool && (bool)value;
            PropertyInfo realmProperty = AccessTools.Property(_ledgerViewModel.GetType(), "IsDiplomacyVisible");
            object realmValue = realmProperty == null ? null : realmProperty.GetValue(_ledgerViewModel, null);
            bool realmVisible = realmValue is bool && (bool)realmValue;
            _overlayViewModel.IsRealmNavigationVisible = realmVisible;
            if (realmVisible && !_overlayViewModel.IsCensusSelected)
            {
                _overlayViewModel.SynchronizeRealmSelection(
                    ReadLedgerBoolean("IsKingdomFinancesPage") ? "Finance"
                    : ReadLedgerBoolean("IsMarriagesPage") ? "Marriages"
                    : "Diplomacy");
            }
        }

        private static bool ReadLedgerBoolean(string propertyName)
        {
            if (_ledgerViewModel == null) return false;
            PropertyInfo property = AccessTools.Property(_ledgerViewModel.GetType(), propertyName);
            object value = property == null ? null : property.GetValue(_ledgerViewModel, null);
            return value is bool && (bool)value;
        }

        private static void AfterWorldScreenClosed()
        {
            _overlayViewModel = null;
            _ledgerViewModel = null;
        }

        private static void AfterLeaveStrategicMap(MethodBase __originalMethod)
        {
            if (_overlayViewModel != null) _overlayViewModel.IsVisible = false;
            if (_overlayViewModel == null || __originalMethod == null) return;
            if (__originalMethod.Name == "ExecuteShowKingdomFinances") _overlayViewModel.SynchronizeRealmSelection("Finance");
            else if (__originalMethod.Name == "ExecuteShowDiplomacyRelations") _overlayViewModel.SynchronizeRealmSelection("Diplomacy");
            else if (__originalMethod.Name == "ExecuteShowMarriagesPage") _overlayViewModel.SynchronizeRealmSelection("Marriages");
        }

        private static void BeforeAtlasUpdate(object[] __args)
        {
            IDictionary<string, uint> ownerColorsBySettlementId = __args == null || __args.Length == 0
                ? null
                : __args[0] as IDictionary<string, uint>;
            MapTrace("atlas prefix entered; mode=" + CurrentMode
                + "; politicalInput=" + ColourDictionarySummary(ownerColorsBySettlementId)
                + "; markerArg=" + DescribeArgument(__args, 1) + ".");
            if (CurrentMode == StrategicMapMode.Political || ownerColorsBySettlementId == null)
            {
                MapTrace("atlas prefix left political input unchanged.");
                return;
            }

            // Every demographic mode begins from a neutral province canvas.
            // This deliberately removes all live kingdom colours before any
            // culture, faith, or population data is applied.
            Dictionary<string, uint> colours = new Dictionary<string, uint>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, uint> entry in ownerColorsBySettlementId)
            {
                colours[entry.Key] = NeutralProvinceColour;
            }
            if (__args != null && __args.Length > 1)
            {
                __args[1] = BuildDemographicMarkerSequence(__args[1]);
                MapTrace("atlas marker argument replaced; markerArg=" + DescribeArgument(__args, 1) + ".");
            }

            Dictionary<string, StrategicProvinceData> demographics;
            if (!TryReadDemographics(out demographics))
            {
                __args[0] = colours;
                MapTrace("demographic snapshot unavailable; neutral canvas submitted: " + ColourDictionarySummary(colours) + ".");
                return;
            }

            foreach (KeyValuePair<string, StrategicProvinceData> entry in demographics)
            {
                if (!colours.ContainsKey(entry.Key)) continue;
                colours[entry.Key] = ResolveColour(entry.Value);
            }
            __args[0] = colours;
            MapTrace("demographic colours submitted; records=" + demographics.Count
                + "; palette=" + ColourDictionarySummary(colours) + ".");
        }

        private static void AfterAtlasUpdate()
        {
            try
            {
                Type providerType = AccessTools.TypeByName(AtlasProviderTypeName);
                FieldInfo coloursField = providerType == null ? null : AccessTools.Field(providerType, "OwnerColorsBySettlementId");
                FieldInfo revisionField = providerType == null ? null : AccessTools.Field(providerType, "_ownerColorRevision");
                object stored = coloursField == null ? null : coloursField.GetValue(null);
                object revision = revisionField == null ? null : revisionField.GetValue(null);
                MapTrace("atlas provider stored update; mode=" + CurrentMode + "; revision=" + (revision ?? "unknown")
                    + "; palette=" + ColourDictionarySummary(stored as IDictionary<string, uint>) + ".");
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("[MAPTRACE] Atlas provider state could not be inspected.", exception);
            }
        }

        private static object BuildDemographicMarkerSequence(object sourceMarkers)
        {
            if (sourceMarkers == null) return null;
            try
            {
                Type pointType = AccessTools.TypeByName("TwelveMonthCalendar.StrategicSettlementPoint");
                if (pointType == null) return null;

                PropertyInfo settlement = AccessTools.Property(pointType, "Settlement");
                PropertyInfo sourceX = AccessTools.Property(pointType, "SourceX");
                PropertyInfo sourceY = AccessTools.Property(pointType, "SourceY");
                PropertyInfo displayX = AccessTools.Property(pointType, "DisplayX");
                PropertyInfo displayY = AccessTools.Property(pointType, "DisplayY");
                PropertyInfo owner = AccessTools.Property(pointType, "Owner");
                PropertyInfo besieger = AccessTools.Property(pointType, "Besieger");
                if (settlement == null || sourceX == null || sourceY == null || displayX == null || displayY == null || owner == null || besieger == null)
                {
                    return null;
                }

                ConstructorInfo constructor = AccessTools.Constructor(pointType, new[]
                {
                    settlement.PropertyType,
                    typeof(float),
                    typeof(float),
                    owner.PropertyType,
                    besieger.PropertyType
                });
                MethodInfo setDisplayPosition = AccessTools.Method(pointType, "SetDisplayPosition", new[] { typeof(float), typeof(float) });
                if (constructor == null || setDisplayPosition == null) return null;

                IList neutralMarkers = Activator.CreateInstance(typeof(List<>).MakeGenericType(pointType)) as IList;
                IEnumerable enumerable = sourceMarkers as IEnumerable;
                if (neutralMarkers == null || enumerable == null) return null;
                foreach (object marker in enumerable)
                {
                    if (marker == null) continue;
                    object neutralMarker = constructor.Invoke(new[]
                    {
                        settlement.GetValue(marker, null),
                        sourceX.GetValue(marker, null),
                        sourceY.GetValue(marker, null),
                        null,
                        null
                    });
                    setDisplayPosition.Invoke(neutralMarker, new[]
                    {
                        displayX.GetValue(marker, null),
                        displayY.GetValue(marker, null)
                    });
                    neutralMarkers.Add(neutralMarker);
                }
                MapTrace("neutral marker copy built; count=" + neutralMarkers.Count + "; type=" + neutralMarkers.GetType().FullName + ".");
                return neutralMarkers;
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("Demographic map markers could not be detached from political ownership.", exception);
                return null;
            }
        }

        private static void AfterProvinceColoursResolved(object[] __args, ref uint[] __result)
        {
            MapTrace("final province resolver entered; mode=" + CurrentMode + "; before=" + ColourArraySummary(__result) + ".");
            if (CurrentMode == StrategicMapMode.Political || __args == null || __args.Length == 0) return;
            uint[] requestedColours = __args[0] as uint[];
            if (requestedColours == null) return;
            __result = new uint[requestedColours.Length];
            Array.Copy(requestedColours, __result, requestedColours.Length);
            MapTrace("final province resolver forced requested demographic palette; after=" + ColourArraySummary(__result) + ".");
        }

        private static void AfterContestedColoursResolved(ref uint[] __result)
        {
            if (CurrentMode == StrategicMapMode.Political || __result == null) return;
            __result = new uint[__result.Length];
            MapTrace("political contested overlay cleared; entries=" + __result.Length + "; mode=" + CurrentMode + ".");
        }

        private static void AfterStrategicMapLayersBuilt(object __instance)
        {
            if (CurrentMode == StrategicMapMode.Political || __instance == null) return;

            // Kingdom names belong only to the political view. Settlement
            // markers and city names remain available on demographic maps.
            PropertyInfo labelsProperty = AccessTools.Property(__instance.GetType(), "StrategicKingdomLabels");
            object labels = labelsProperty == null ? null : labelsProperty.GetValue(__instance, null);
            MethodInfo clear = labels == null ? null : AccessTools.Method(labels.GetType(), "Clear", Type.EmptyTypes);
            if (clear != null) clear.Invoke(labels, null);
        }

        private static uint ResolveColour(StrategicProvinceData province)
        {
            if (CurrentMode == StrategicMapMode.Religion) return ResolveReligionColour(province.FaithId);
            if (CurrentMode == StrategicMapMode.Population) return ResolvePopulationColour(province.Population);
            return ResolveCultureColour(province.CultureId, province.SettlementId);
        }

        private static bool TryReadDemographics(out Dictionary<string, StrategicProvinceData> provinces)
        {
            provinces = new Dictionary<string, StrategicProvinceData>(StringComparer.Ordinal);
            string payload = PopulationService.GetStrategicMapSnapshotPayload();
            if (string.IsNullOrWhiteSpace(payload))
            {
                MapTrace("population service returned an empty strategic snapshot.");
                return false;
            }
            string[] lines = payload.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0 || !string.Equals(lines[0].Trim(), "AOCMAP1", StringComparison.Ordinal))
            {
                MapTrace("population snapshot header invalid; characters=" + payload.Length + "; lines=" + lines.Length + ".");
                return false;
            }
            for (int index = 1; index < lines.Length; index++)
            {
                string[] fields = lines[index].Split('|');
                long population;
                if (fields.Length != 5 || !long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out population)) continue;
                string settlementId = Uri.UnescapeDataString(fields[0]);
                provinces[settlementId] = new StrategicProvinceData(
                    settlementId,
                    Math.Max(0L, population),
                    Uri.UnescapeDataString(fields[2]),
                    Uri.UnescapeDataString(fields[3]));
            }
            MapTrace("population snapshot parsed; characters=" + payload.Length + "; lines=" + lines.Length
                + "; records=" + provinces.Count + ".");
            return provinces.Count > 0;
        }

        private static void MapTrace(string message)
        {
            ReligionDiagnostics.Info("[MAPTRACE] " + message);
        }

        private static string DescribeArgument(object[] arguments, int index)
        {
            if (arguments == null || index < 0 || index >= arguments.Length) return "missing";
            object value = arguments[index];
            if (value == null) return "null";
            ICollection collection = value as ICollection;
            return value.GetType().FullName + (collection == null ? string.Empty : "[count=" + collection.Count + "]");
        }

        private static string ColourDictionarySummary(IDictionary<string, uint> colours)
        {
            if (colours == null) return "null";
            HashSet<uint> distinct = new HashSet<uint>();
            string sample = string.Empty;
            int sampled = 0;
            foreach (KeyValuePair<string, uint> entry in colours)
            {
                distinct.Add(entry.Value);
                if (sampled >= 6) continue;
                if (sample.Length > 0) sample += ",";
                sample += entry.Key + "=" + entry.Value.ToString("X8", CultureInfo.InvariantCulture);
                sampled++;
            }
            return "count=" + colours.Count + "; distinct=" + distinct.Count + "; sample=[" + sample + "]";
        }

        private static string ColourArraySummary(uint[] colours)
        {
            if (colours == null) return "null";
            HashSet<uint> distinct = new HashSet<uint>();
            string sample = string.Empty;
            for (int index = 0; index < colours.Length; index++)
            {
                distinct.Add(colours[index]);
                if (index >= 8) continue;
                if (sample.Length > 0) sample += ",";
                sample += colours[index].ToString("X8", CultureInfo.InvariantCulture);
            }
            return "count=" + colours.Length + "; distinct=" + distinct.Count + "; sample=[" + sample + "]";
        }

        private static uint ResolveCultureColour(string cultureId, string settlementId)
        {
            string culture = (cultureId ?? string.Empty).ToLowerInvariant();
            string settlement = settlementId ?? string.Empty;
            if (culture.Contains("aserai") || settlement.Contains("_A")) return 0xFFC9A66Bu;
            if (culture.Contains("battania") || settlement.Contains("_B")) return 0xFF557A32u;
            if (culture.Contains("khuzait") || settlement.Contains("_K")) return 0xFF4B9B91u;
            if (culture.Contains("vlandia") || settlement.Contains("_V")) return 0xFF9D443Au;
            if (culture.Contains("nord") || settlement.Contains("_N")) return 0xFF17283Du;
            if (culture.Contains("sturgia") || settlement.Contains("_S")) return 0xFF294F78u;
            if (culture.Contains("empire") || settlement.Contains("_E")) return 0xFF77558Eu;
            return 0xFF6B665Du;
        }

        private static uint ResolveReligionColour(string faithId)
        {
            switch (faithId ?? string.Empty)
            {
                case "asharim": return 0xFF9F3F36u;
                case "valeronism": return 0xFF78518Fu;
                case "mazirism": return 0xFFB77A2Eu;
                case "isharan_way": return 0xFFD3AB3Fu;
                case "kok_orun_way": return 0xFF3E938Bu;
                case "caerwydd": return 0xFF4F7738u;
                case "veyrhold": return 0xFF2D527Du;
                case "calradic_old_faith": return 0xFF80603Du;
                default: return 0xFF69645Cu;
            }
        }

        private static uint ResolvePopulationColour(long population)
        {
            if (population < 150000L) return 0xFF26384Du;
            if (population < 300000L) return 0xFF37627Bu;
            if (population < 450000L) return 0xFF538B88u;
            if (population < 650000L) return 0xFF92A55Fu;
            if (population < 900000L) return 0xFFD1A047u;
            return 0xFFD55A3Fu;
        }
    }

    internal sealed class StrategicMapModeViewModel : ViewModel
    {
        private bool _isVisible;
        private bool _isRealmNavigationVisible;
        private string _realmPage = "Diplomacy";
        private CensusDisplayData _census = CensusDisplayData.Unavailable();

        [DataSourceProperty]
        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChangedWithValue(value, "IsVisible");
            }
        }

        [DataSourceProperty]
        public bool IsRealmNavigationVisible
        {
            get { return _isRealmNavigationVisible; }
            set
            {
                if (_isRealmNavigationVisible == value) return;
                _isRealmNavigationVisible = value;
                OnPropertyChangedWithValue(value, "IsRealmNavigationVisible");
                OnPropertyChangedWithValue(IsCensusPageVisible, "IsCensusPageVisible");
            }
        }

        [DataSourceProperty] public bool IsPolitical { get { return StrategicMapModeIntegration.CurrentMode == StrategicMapMode.Political; } }
        [DataSourceProperty] public bool IsReligion { get { return StrategicMapModeIntegration.CurrentMode == StrategicMapMode.Religion; } }
        [DataSourceProperty] public bool IsPopulation { get { return StrategicMapModeIntegration.CurrentMode == StrategicMapMode.Population; } }
        [DataSourceProperty] public bool IsCulture { get { return StrategicMapModeIntegration.CurrentMode == StrategicMapMode.Culture; } }
        [DataSourceProperty] public bool IsFinanceSelected { get { return string.Equals(_realmPage, "Finance", StringComparison.Ordinal); } }
        [DataSourceProperty] public bool IsDiplomacySelected { get { return string.Equals(_realmPage, "Diplomacy", StringComparison.Ordinal); } }
        [DataSourceProperty] public bool IsMarriagesSelected { get { return string.Equals(_realmPage, "Marriages", StringComparison.Ordinal); } }
        [DataSourceProperty] public bool IsCensusSelected { get { return string.Equals(_realmPage, "Census", StringComparison.Ordinal); } }
        [DataSourceProperty] public bool IsCensusPageVisible { get { return IsRealmNavigationVisible && IsCensusSelected; } }

        [DataSourceProperty] public string CensusRealmName { get { return _census.RealmName; } }
        [DataSourceProperty] public string CensusRealmPopulation { get { return _census.RealmPopulation; } }
        [DataSourceProperty] public string CensusRealmShare { get { return _census.RealmShare; } }
        [DataSourceProperty] public string CensusRealmProvinces { get { return _census.RealmProvinces; } }
        [DataSourceProperty] public string CensusRealmHappiness { get { return _census.RealmHappiness; } }
        [DataSourceProperty] public string CensusRealmProvinceText { get { return "PROVINCES  " + CensusRealmProvinces; } }
        [DataSourceProperty] public string CensusRealmHappinessText { get { return "HAPPINESS  " + CensusRealmHappiness; } }
        [DataSourceProperty] public string CensusRealmCultures { get { return _census.RealmCultures; } }
        [DataSourceProperty] public string CensusRealmReligions { get { return _census.RealmReligions; } }
        [DataSourceProperty] public string CensusCalradiaPopulation { get { return _census.CalradiaPopulation; } }
        [DataSourceProperty] public string CensusCalradiaProvinces { get { return _census.CalradiaProvinces; } }
        [DataSourceProperty] public string CensusCalradiaHappiness { get { return _census.CalradiaHappiness; } }
        [DataSourceProperty] public string CensusCalradiaProvinceText { get { return "PROVINCES  " + CensusCalradiaProvinces; } }
        [DataSourceProperty] public string CensusCalradiaHappinessText { get { return "HAPPINESS  " + CensusCalradiaHappiness; } }
        [DataSourceProperty] public string CensusCalradiaCultures { get { return _census.CalradiaCultures; } }
        [DataSourceProperty] public string CensusCalradiaReligions { get { return _census.CalradiaReligions; } }

        public void ExecutePolitical() { StrategicMapModeIntegration.SelectMode(StrategicMapMode.Political); }
        public void ExecuteReligion() { StrategicMapModeIntegration.SelectMode(StrategicMapMode.Religion); }
        public void ExecutePopulation() { StrategicMapModeIntegration.SelectMode(StrategicMapMode.Population); }
        public void ExecuteCulture() { StrategicMapModeIntegration.SelectMode(StrategicMapMode.Culture); }
        public void ExecuteFinances() { StrategicMapModeIntegration.InvokeRealmCommand("ExecuteShowKingdomFinances"); }
        public void ExecuteDiplomacy() { StrategicMapModeIntegration.InvokeRealmCommand("ExecuteShowDiplomacyRelations"); }
        public void ExecuteMarriages() { StrategicMapModeIntegration.InvokeRealmCommand("ExecuteShowMarriagesPage"); }
        public void ExecuteCensus()
        {
            StrategicMapModeIntegration.HideNativeRealmPageForCensus();
            _realmPage = "Census";
            RefreshRealmSelection();
            RefreshCensus();
        }

        internal void RefreshSelection()
        {
            OnPropertyChangedWithValue(IsPolitical, "IsPolitical");
            OnPropertyChangedWithValue(IsReligion, "IsReligion");
            OnPropertyChangedWithValue(IsPopulation, "IsPopulation");
            OnPropertyChangedWithValue(IsCulture, "IsCulture");
        }

        internal void SynchronizeRealmSelection(string page)
        {
            _realmPage = string.IsNullOrEmpty(page) ? "Diplomacy" : page;
            RefreshRealmSelection();
        }

        internal void RefreshCensus()
        {
            _census = CensusReportBuilder.Build();
            OnPropertyChangedWithValue(CensusRealmName, "CensusRealmName");
            OnPropertyChangedWithValue(CensusRealmPopulation, "CensusRealmPopulation");
            OnPropertyChangedWithValue(CensusRealmShare, "CensusRealmShare");
            OnPropertyChangedWithValue(CensusRealmProvinces, "CensusRealmProvinces");
            OnPropertyChangedWithValue(CensusRealmHappiness, "CensusRealmHappiness");
            OnPropertyChangedWithValue(CensusRealmProvinceText, "CensusRealmProvinceText");
            OnPropertyChangedWithValue(CensusRealmHappinessText, "CensusRealmHappinessText");
            OnPropertyChangedWithValue(CensusRealmCultures, "CensusRealmCultures");
            OnPropertyChangedWithValue(CensusRealmReligions, "CensusRealmReligions");
            OnPropertyChangedWithValue(CensusCalradiaPopulation, "CensusCalradiaPopulation");
            OnPropertyChangedWithValue(CensusCalradiaProvinces, "CensusCalradiaProvinces");
            OnPropertyChangedWithValue(CensusCalradiaHappiness, "CensusCalradiaHappiness");
            OnPropertyChangedWithValue(CensusCalradiaProvinceText, "CensusCalradiaProvinceText");
            OnPropertyChangedWithValue(CensusCalradiaHappinessText, "CensusCalradiaHappinessText");
            OnPropertyChangedWithValue(CensusCalradiaCultures, "CensusCalradiaCultures");
            OnPropertyChangedWithValue(CensusCalradiaReligions, "CensusCalradiaReligions");
        }

        private void RefreshRealmSelection()
        {
            OnPropertyChangedWithValue(IsFinanceSelected, "IsFinanceSelected");
            OnPropertyChangedWithValue(IsDiplomacySelected, "IsDiplomacySelected");
            OnPropertyChangedWithValue(IsMarriagesSelected, "IsMarriagesSelected");
            OnPropertyChangedWithValue(IsCensusSelected, "IsCensusSelected");
            OnPropertyChangedWithValue(IsCensusPageVisible, "IsCensusPageVisible");
        }
    }

    internal sealed class StrategicProvinceData
    {
        internal StrategicProvinceData(string settlementId, long population, string cultureId, string faithId)
        {
            SettlementId = settlementId ?? string.Empty;
            Population = population;
            CultureId = cultureId ?? string.Empty;
            FaithId = faithId ?? string.Empty;
        }

        internal string SettlementId { get; private set; }
        internal long Population { get; private set; }
        internal string CultureId { get; private set; }
        internal string FaithId { get; private set; }
    }
}
