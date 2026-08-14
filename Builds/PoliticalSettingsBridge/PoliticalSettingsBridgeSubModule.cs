using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace AgesOfCalradia.PoliticalSettingsBridge
{
    /// <summary>
    /// Adds configurable presentation to the approved 19:55 political map.
    /// It never changes terrain classification, ownership, frontiers, or the
    /// exact island-exclusion mask.
    /// </summary>
    public sealed class PoliticalSettingsBridgeSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "AgesOfCalradia.PoliticalSettingsBridge.20260811";
        private static readonly HashSet<GameEntity> WaterEntities = new HashSet<GameEntity>();
        private static MethodInfo _diagnosticsInfo;
        private static FieldInfo _territoryFillEntitiesField;
        private static FieldInfo _politicalLayerAlphaField;
        private const int FixedOpacityPercent = 100;
        private const int FixedBrightnessPercent = 25;
        private const bool FixedSolidWater = true;
        private static bool _loggedColor;
        private static bool _loggedMaterial;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Type diagnosticsType = AccessTools.TypeByName("TwelveMonthCalendar.Diagnostics");
            _diagnosticsInfo = diagnosticsType == null
                ? null
                : AccessTools.Method(diagnosticsType, "Info", new[] { typeof(string) });

            Type fillType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CampaignPoliticalTerritoryFill");
            Type builderType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CampaignPoliticalTerritoryFill+Builder");
            Type behaviorType = AccessTools.TypeByName(
                "TwelveMonthCalendar.CampaignKingdomBorderBehavior");

            MethodInfo scaleColor = AccessTools.Method(
                fillType,
                "ScaleOpaqueColor",
                new[] { typeof(uint), typeof(uint) });
            MethodInfo createRowMesh = AccessTools.Method(
                builderType,
                "CreateRowMesh",
                new[] { typeof(int) });
            MethodInfo addRowEntity = AccessTools.Method(builderType, "AddRowEntity");
            MethodInfo applyVisibility = AccessTools.Method(
                behaviorType,
                "ApplyPoliticalEntityVisibility",
                new[] { typeof(bool) });

            _territoryFillEntitiesField = AccessTools.Field(
                behaviorType,
                "_territoryFillEntities");
            _politicalLayerAlphaField = AccessTools.Field(
                behaviorType,
                "_politicalLayerAlpha");

            Harmony harmony = new Harmony(HarmonyId);
            PatchPostfix(harmony, scaleColor, nameof(ApplyConfiguredColor));
            PatchPostfix(harmony, createRowMesh, nameof(ApplyConfiguredMaterial));
            PatchPostfix(harmony, addRowEntity, nameof(TrackWaterEntity));
            PatchPostfix(harmony, applyVisibility, nameof(ApplyConfiguredAlpha));

            WriteLog(
                "Fixed political presentation installed around the approved 19:55 renderer "
                + "(opacity=100, brightness=25, solidWater=true); "
                + "terrain, borders, and island exclusions remain untouched.");
        }

        private static void PatchPostfix(Harmony harmony, MethodInfo target, string patchName)
        {
            MethodInfo patch = AccessTools.Method(
                typeof(PoliticalSettingsBridgeSubModule),
                patchName);
            if (target != null && patch != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(patch));
            }
            else
            {
                WriteLog("Optional presentation hook unavailable: " + patchName + ".");
            }
        }

        private static void ApplyConfiguredColor(
            uint color,
            uint brightnessPercent,
            ref uint __result)
        {
            if (brightnessPercent != 50u) return;
            uint scale = FixedBrightnessPercent;
            uint red = Math.Min(255u, ((color >> 16) & 0xFFu) * scale / 100u);
            uint green = Math.Min(255u, ((color >> 8) & 0xFFu) * scale / 100u);
            uint blue = Math.Min(255u, (color & 0xFFu) * scale / 100u);
            __result = 0xFF000000u | (red << 16) | (green << 8) | blue;
            if (!_loggedColor)
            {
                _loggedColor = true;
                WriteLog("Control-color brightness applied: " + FixedBrightnessPercent + "%.");
            }
        }

        private static void ApplyConfiguredMaterial(int renderOrder, ref Mesh __result)
        {
            if (renderOrder != 100 || __result == null) return;
            string material = FixedOpacityPercent < 100
                ? "vertex_color_blend_mat"
                : "vertex_color_mat";
            __result.SetMaterial(material);
            if (!_loggedMaterial)
            {
                _loggedMaterial = true;
                WriteLog(
                    "Control-fill material applied: " + material
                    + "; opacity=" + FixedOpacityPercent
                    + "; solidWater=" + FixedSolidWater + ".");
            }
        }

        private static void TrackWaterEntity(object __instance, bool riverCap)
        {
            if (!riverCap || __instance == null) return;
            try
            {
                FieldInfo entitiesField = AccessTools.Field(__instance.GetType(), "_entities");
                List<GameEntity> entities = entitiesField == null
                    ? null
                    : entitiesField.GetValue(__instance) as List<GameEntity>;
                if (entities == null || entities.Count == 0) return;
                GameEntity entity = entities[entities.Count - 1];
                if (entity == null) return;
                lock (WaterEntities) WaterEntities.Add(entity);
            }
            catch
            {
            }
        }

        private static void ApplyConfiguredAlpha(object __instance)
        {
            if (__instance == null
                || _territoryFillEntitiesField == null
                || _politicalLayerAlphaField == null)
            {
                return;
            }

            try
            {
                List<GameEntity> entities = _territoryFillEntitiesField.GetValue(__instance)
                    as List<GameEntity>;
                if (entities == null) return;
                float layerAlpha = (float)_politicalLayerAlphaField.GetValue(__instance);
                float configuredAlpha = FixedOpacityPercent / 100f;
                foreach (GameEntity entity in entities)
                {
                    if (entity == null) continue;
                    bool isWater;
                    lock (WaterEntities) isWater = WaterEntities.Contains(entity);
                    entity.SetAlpha(layerAlpha * (FixedSolidWater && isWater ? 1f : configuredAlpha));
                }
            }
            catch
            {
            }
        }

        internal static void WriteLog(string message)
        {
            string fullMessage = "Ages of Calradia political settings: " + message;
            try
            {
                if (_diagnosticsInfo != null)
                {
                    _diagnosticsInfo.Invoke(null, new object[] { fullMessage });
                    return;
                }
            }
            catch
            {
            }
            Debug.Print(fullMessage);
        }
    }
}
