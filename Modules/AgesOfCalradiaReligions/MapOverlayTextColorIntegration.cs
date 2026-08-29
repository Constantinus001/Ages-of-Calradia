using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace AgesOfCalradiaReligions
{
    /// <summary>Runtime-only black text treatment for map-canvas labels.</summary>
    internal static class MapOverlayTextColorIntegration
    {
        private const string LedgerTypeName = "TwelveMonthCalendar.CalendarWorldLedgerVM";
        private const string KingdomLabelTypeName = "TwelveMonthCalendar.CalendarStrategicKingdomLabelVM";
        private const string FriendlyArmyTypeName = "TwelveMonthCalendar.CalendarStrategicFriendlyArmyVM";
        private const string PoliticalViewTypeName = "TwelveMonthCalendar.CampaignPoliticalOverlayView";
        private const string StrategicCanvasId = "StrategicMapCanvas";
        private const int RetryFrameCount = 30;
        private const float PoliticalOutlineAmount = 0.10f;
        private const float PoliticalOutlineDarkenFactor = 0.55f;
        private static readonly string[] WorldScreenTypeNames =
        {
            "TwelveMonthCalendar.WorldCalendarScreen",
            "TwelveMonthCalendar.StandaloneWorldEventsScreen"
        };

        private static bool _installed;
        private static int _strategicRetryFrames;
        private static int _politicalRetryFrames;
        private static int _lastStrategicTextCount = -1;
        private static int _lastPoliticalTextCount = -1;

        internal static void Install(Harmony harmony)
        {
            if (_installed || harmony == null) return;
            Type ledgerType = AccessTools.TypeByName(LedgerTypeName);
            Type kingdomLabelType = AccessTools.TypeByName(KingdomLabelTypeName);
            Type friendlyArmyType = AccessTools.TypeByName(FriendlyArmyTypeName);
            Type politicalViewType = AccessTools.TypeByName(PoliticalViewTypeName);
            if (ledgerType == null || kingdomLabelType == null || friendlyArmyType == null || politicalViewType == null)
                throw new InvalidOperationException("One or more approved map-label integration types were not found.");

            MethodInfo buildMap = AccessTools.Method(ledgerType, "BuildStrategicMapLayers");
            MethodInfo kingdomColor = AccessTools.PropertyGetter(kingdomLabelType, "GoldColor");
            MethodInfo armyColor = AccessTools.PropertyGetter(friendlyArmyType, "GoldColor");
            MethodInfo rebuildPoliticalLabels = AccessTools.Method(politicalViewType, "RebuildLabels");
            MethodInfo politicalFrame = AccessTools.Method(politicalViewType, "OnMapScreenUpdate");
            if (buildMap == null || kingdomColor == null || armyColor == null
                || rebuildPoliticalLabels == null || politicalFrame == null)
                throw new MissingMethodException("The approved map-label runtime contract changed.");

            HarmonyMethod blackResult = new HarmonyMethod(typeof(MapOverlayTextColorIntegration), nameof(ReturnBlack));
            harmony.Patch(kingdomColor, postfix: blackResult);
            harmony.Patch(armyColor, postfix: blackResult);
            harmony.Patch(buildMap, postfix: new HarmonyMethod(typeof(MapOverlayTextColorIntegration), nameof(AfterStrategicMapBuilt)));
            harmony.Patch(rebuildPoliticalLabels, postfix: new HarmonyMethod(typeof(MapOverlayTextColorIntegration), nameof(AfterPoliticalLabelsRebuilt)));
            harmony.Patch(politicalFrame, postfix: new HarmonyMethod(typeof(MapOverlayTextColorIntegration), nameof(AfterPoliticalFrame)));

            int screenTickPatches = 0;
            foreach (string screenTypeName in WorldScreenTypeNames)
            {
                Type screenType = AccessTools.TypeByName(screenTypeName);
                MethodInfo tick = screenType == null ? null : AccessTools.Method(screenType, "OnTick");
                if (tick == null) continue;
                harmony.Patch(tick, postfix: new HarmonyMethod(typeof(MapOverlayTextColorIntegration), nameof(AfterWorldScreenTick)));
                screenTickPatches++;
            }
            if (screenTickPatches == 0) throw new MissingMethodException("No approved World Events screen tick was found.");

            _installed = true;
            ReligionDiagnostics.Info("[MAPTEXT] Black map-label integration registered: strategicScreens="
                + screenTickPatches + "; getterOverrides=2; politicalRuntimeOverride=1; protectedFilesChanged=0.");
        }

        internal static void Reset()
        {
            _installed = false;
            _strategicRetryFrames = 0;
            _politicalRetryFrames = 0;
            _lastStrategicTextCount = -1;
            _lastPoliticalTextCount = -1;
        }

        private static void ReturnBlack(ref string __result) { __result = "#000000FF"; }
        private static void AfterStrategicMapBuilt() { _strategicRetryFrames = RetryFrameCount; }
        private static void AfterPoliticalLabelsRebuilt(object __instance)
        {
            _politicalRetryFrames = RetryFrameCount;
            CorrectCoastalPoliticalLabelAnchors(__instance);
        }

        private static void AfterWorldScreenTick(object __instance)
        {
            if (_strategicRetryFrames <= 0 || __instance == null) return;
            _strategicRetryFrames--;
            int inspected;
            int widgetVerified;
            int brushVerified;
            int clonedBrushes;
            int missingBrushes;
            string failure;
            if (TryApplyLayerText(__instance, StrategicCanvasId, Color.Black, out inspected, out widgetVerified,
                out brushVerified, out clonedBrushes, out missingBrushes, out failure))
            {
                _strategicRetryFrames = 0;
                if (_lastStrategicTextCount != inspected)
                {
                    _lastStrategicTextCount = inspected;
                    ReligionDiagnostics.Info("[MAPTEXT] Strategic rendered text verified black: inspected="
                        + inspected + "; widgetBlack=" + widgetVerified + "; renderedBrushBlack=" + brushVerified
                        + "; clonedBrushes=" + clonedBrushes + "; missingBrushes=" + missingBrushes
                        + "; canvas=" + StrategicCanvasId + ".");
                }
            }
            else if (_strategicRetryFrames == 0)
            {
                ReligionDiagnostics.Info("[MAPTEXT] DIAGNOSTIC FAILURE: strategic map text did not appear black after "
                    + RetryFrameCount + " frames; inspected=" + inspected + "; widgetBlack=" + widgetVerified
                    + "; renderedBrushBlack=" + brushVerified + "; clonedBrushes=" + clonedBrushes
                    + "; missingBrushes=" + missingBrushes + "; reason=" + failure + ".");
            }
        }

        private static void AfterPoliticalFrame(object __instance)
        {
            if (_politicalRetryFrames <= 0 || __instance == null) return;
            _politicalRetryFrames--;
            int inspected;
            int widgetVerified;
            int brushVerified;
            int outlineVerified;
            int clonedBrushes;
            int missingBrushes;
            string failure;
            if (TryApplyPoliticalLabelText(__instance, out inspected, out widgetVerified,
                out brushVerified, out outlineVerified, out clonedBrushes, out missingBrushes, out failure))
            {
                _politicalRetryFrames = 0;
                if (_lastPoliticalTextCount != inspected)
                {
                    _lastPoliticalTextCount = inspected;
                    ReligionDiagnostics.Info("[MAPTEXT] Campaign political black text with kingdom-color outlines verified: inspected="
                        + inspected + "; widgetBlack=" + widgetVerified + "; renderedBrushBlack=" + brushVerified
                        + "; outlineVerified=" + outlineVerified + "; clonedBrushes=" + clonedBrushes
                        + "; missingBrushes=" + missingBrushes + ".");
                }
            }
            else if (_politicalRetryFrames == 0)
            {
                ReligionDiagnostics.Info("[MAPTEXT] DIAGNOSTIC FAILURE: campaign political text did not appear black after "
                    + RetryFrameCount + " frames; inspected=" + inspected + "; widgetBlack=" + widgetVerified
                    + "; renderedBrushBlack=" + brushVerified + "; outlineVerified=" + outlineVerified
                    + "; clonedBrushes=" + clonedBrushes
                    + "; missingBrushes=" + missingBrushes + "; reason=" + failure + ".");
            }
        }

        private static bool TryApplyLayerText(object host, string requiredRootId, Color targetColor, out int inspected,
            out int widgetVerified, out int brushVerified, out int clonedBrushes, out int missingBrushes,
            out string failure)
        {
            inspected = 0;
            widgetVerified = 0;
            brushVerified = 0;
            clonedBrushes = 0;
            missingBrushes = 0;
            failure = string.Empty;
            try
            {
                FieldInfo layerField = AccessTools.Field(host.GetType(), "_layer");
                GauntletLayer layer = layerField == null ? null : layerField.GetValue(host) as GauntletLayer;
                Widget root = layer == null || layer.UIContext == null ? null : layer.UIContext.Root;
                if (root == null)
                {
                    failure = "Gauntlet layer/root unavailable";
                    return false;
                }

                Widget scope = string.IsNullOrEmpty(requiredRootId) ? root : root.FindChild(requiredRootId, true);
                if (scope == null)
                {
                    failure = "required widget not found: " + requiredRootId;
                    return false;
                }

                List<TextWidget> textWidgets = scope.GetAllChildrenOfTypeRecursive<TextWidget>(widget => widget != null);
                if (textWidgets == null || textWidgets.Count == 0)
                {
                    failure = "no instantiated text widgets found";
                    return false;
                }

                uint target = targetColor.ToUnsignedInteger();
                foreach (TextWidget textWidget in textWidgets)
                {
                    inspected++;
                    textWidget.Color = targetColor;
                    if (textWidget.Color.ToUnsignedInteger() == target) widgetVerified++;

                    // TextWidget.Color is only the widget tint. Bannerlord draws the glyphs
                    // with Brush.FontColor, which can remain cream even when Color is black.
                    // Clone before changing it because named Gauntlet brushes are shared by
                    // unrelated UI widgets.
                    Brush brush = textWidget.Brush;
                    if (brush == null)
                    {
                        missingBrushes++;
                        continue;
                    }
                    if (brush.FontColor.ToUnsignedInteger() != target)
                    {
                        Brush privateBrush = brush.Clone();
                        privateBrush.FontColor = targetColor;
                        textWidget.Brush = privateBrush;
                        brush = privateBrush;
                        clonedBrushes++;
                    }
                    if (brush.FontColor.ToUnsignedInteger() == target) brushVerified++;
                }
                if (widgetVerified != inspected || brushVerified != inspected)
                {
                    failure = "one or more rendered TextWidget colors were not black";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static bool TryApplyPoliticalLabelText(object host, out int inspected,
            out int widgetVerified, out int brushVerified, out int outlineVerified,
            out int clonedBrushes, out int missingBrushes, out string failure)
        {
            inspected = 0;
            widgetVerified = 0;
            brushVerified = 0;
            outlineVerified = 0;
            clonedBrushes = 0;
            missingBrushes = 0;
            failure = string.Empty;
            try
            {
                FieldInfo labelsField = host == null ? null : AccessTools.Field(host.GetType(), "_labels");
                System.Collections.IEnumerable labels = labelsField == null
                    ? null
                    : labelsField.GetValue(host) as System.Collections.IEnumerable;
                if (labels == null)
                {
                    failure = "political label collection unavailable";
                    return false;
                }

                uint black = Color.Black.ToUnsignedInteger();
                foreach (object label in labels)
                {
                    if (label == null) continue;
                    string labelName = GetPoliticalLabelName(label);
                    Kingdom kingdom = FindKingdomByPoliticalLabel(labelName);
                    if (kingdom == null) continue;

                    PropertyInfo movieIdentifierProperty = AccessTools.Property(label.GetType(), "Movie");
                    object movieIdentifier = movieIdentifierProperty == null
                        ? null
                        : movieIdentifierProperty.GetValue(label, null);
                    PropertyInfo movieProperty = movieIdentifier == null
                        ? null
                        : AccessTools.Property(movieIdentifier.GetType(), "Movie");
                    object movie = movieProperty == null ? null : movieProperty.GetValue(movieIdentifier, null);
                    PropertyInfo rootProperty = movie == null ? null : AccessTools.Property(movie.GetType(), "RootWidget");
                    Widget root = rootProperty == null ? null : rootProperty.GetValue(movie, null) as Widget;
                    if (root == null) continue;

                    Color outlineColor = DarkenKingdomColor(kingdom.PrimaryBannerColor);
                    uint outline = outlineColor.ToUnsignedInteger();
                    List<TextWidget> widgets = root.GetAllChildrenOfTypeRecursive<TextWidget>(widget => widget != null);
                    foreach (TextWidget textWidget in widgets)
                    {
                        inspected++;
                        textWidget.Color = Color.Black;
                        if (textWidget.Color.ToUnsignedInteger() == black) widgetVerified++;

                        Brush brush = textWidget.Brush;
                        if (brush == null)
                        {
                            missingBrushes++;
                            continue;
                        }
                        bool alreadyStyled = brush.FontColor.ToUnsignedInteger() == black
                            && brush.DefaultStyle != null
                            && brush.DefaultStyle.TextOutlineColor.ToUnsignedInteger() == outline
                            && Math.Abs(brush.DefaultStyle.TextOutlineAmount - PoliticalOutlineAmount) < 0.001f;
                        if (!alreadyStyled)
                        {
                            Brush privateBrush = brush.Clone();
                            privateBrush.FontColor = Color.Black;
                            if (privateBrush.DefaultStyle != null)
                            {
                                privateBrush.DefaultStyle.FontColor = Color.Black;
                                privateBrush.DefaultStyle.TextOutlineColor = outlineColor;
                                privateBrush.DefaultStyle.TextOutlineAmount = PoliticalOutlineAmount;
                                privateBrush.DefaultStyle.TextGlowRadius = 0f;
                            }
                            textWidget.Brush = privateBrush;
                            brush = privateBrush;
                            clonedBrushes++;
                        }
                        if (brush.FontColor.ToUnsignedInteger() == black) brushVerified++;
                        if (brush.DefaultStyle != null
                            && brush.DefaultStyle.TextOutlineColor.ToUnsignedInteger() == outline
                            && Math.Abs(brush.DefaultStyle.TextOutlineAmount - PoliticalOutlineAmount) < 0.001f)
                            outlineVerified++;
                    }
                }

                if (inspected == 0)
                {
                    failure = "no instantiated political kingdom text widgets found";
                    return false;
                }
                if (widgetVerified != inspected || brushVerified != inspected || outlineVerified != inspected)
                {
                    failure = "one or more political labels rejected black text or kingdom outline styling";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static string GetPoliticalLabelName(object label)
        {
            PropertyInfo vmProperty = label == null ? null : AccessTools.Property(label.GetType(), "ViewModel");
            object vm = vmProperty == null ? null : vmProperty.GetValue(label, null);
            PropertyInfo nameProperty = vm == null ? null : AccessTools.Property(vm.GetType(), "Name");
            return nameProperty == null ? null : nameProperty.GetValue(vm, null) as string;
        }

        private static Kingdom FindKingdomByPoliticalLabel(string labelName)
        {
            if (string.IsNullOrEmpty(labelName)) return null;
            return Kingdom.All.FirstOrDefault(candidate => candidate != null
                && !candidate.IsEliminated
                && string.Equals(candidate.Name.ToString().ToUpperInvariant(), labelName, StringComparison.Ordinal));
        }

        private static Color DarkenKingdomColor(uint bannerColor)
        {
            Color source = Color.FromUint(bannerColor);
            return new Color(source.Red * PoliticalOutlineDarkenFactor,
                source.Green * PoliticalOutlineDarkenFactor,
                source.Blue * PoliticalOutlineDarkenFactor,
                1f);
        }

        private static void CorrectCoastalPoliticalLabelAnchors(object host)
        {
            if (host == null || Campaign.Current == null) return;
            try
            {
                FieldInfo labelsField = AccessTools.Field(host.GetType(), "_labels");
                System.Collections.IEnumerable labels = labelsField == null
                    ? null
                    : labelsField.GetValue(host) as System.Collections.IEnumerable;
                if (labels == null) return;

                foreach (object label in labels)
                {
                    if (label == null) continue;
                    string labelName = GetPoliticalLabelName(label);
                    if (string.IsNullOrEmpty(labelName)) continue;

                    Kingdom kingdom = FindKingdomByPoliticalLabel(labelName);
                    if (kingdom == null || !NeedsLandAnchor(kingdom)) continue;

                    List<Settlement> holdings = Settlement.All.Where(settlement => settlement != null
                        && (settlement.IsTown || settlement.IsCastle)
                        && settlement.OwnerClan != null
                        && ReferenceEquals(settlement.OwnerClan.Kingdom, kingdom)).ToList();
                    if (holdings.Count == 0) continue;

                    float centerX = holdings.Average(settlement => settlement.Position.X);
                    float centerY = holdings.Average(settlement => settlement.Position.Y);
                    Settlement anchor = holdings.OrderBy(settlement =>
                    {
                        float dx = settlement.Position.X - centerX;
                        float dy = settlement.Position.Y - centerY;
                        return (dx * dx) + (dy * dy);
                    }).ThenBy(settlement => settlement.StringId, StringComparer.Ordinal).First();

                    PropertyInfo positionProperty = AccessTools.Property(label.GetType(), "WorldPosition");
                    MethodInfo setter = positionProperty == null ? null : positionProperty.GetSetMethod(true);
                    if (setter == null) continue;
                    setter.Invoke(label, new object[] { new Vec2(anchor.Position.X, anchor.Position.Y) });
                    ReligionDiagnostics.Info("[MAPTEXT] Political label land anchor corrected: kingdom="
                        + kingdom.Name + "; anchorSettlement=" + anchor.Name + "; holdings=" + holdings.Count + ".");
                }
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Info("[MAPTEXT] DIAGNOSTIC FAILURE: political label land-anchor correction failed: "
                    + exception.GetType().Name + ": " + exception.Message + ".");
            }
        }

        private static bool NeedsLandAnchor(Kingdom kingdom)
        {
            string identity = ((kingdom.StringId ?? string.Empty) + " "
                + (kingdom.Culture == null ? string.Empty : kingdom.Culture.StringId) + " "
                + kingdom.Name).ToLowerInvariant();
            return identity.Contains("aserai") || identity.Contains("nord");
        }
    }
}
