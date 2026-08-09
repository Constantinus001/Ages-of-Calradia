using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Scripts;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Converts the calendar's visual daylight window to the fixed daylight
    /// window baked into Bannerlord's map scene (02:00--22:00).  This is used
    /// only at the renderer boundary; campaign time and gameplay sunrise stay
    /// native and are never mutated.
    /// </summary>
    internal static class VisualDaylightClock
    {
        internal const float NativeSceneSunrise = 2f;
        internal const float NativeScenePeakBrightness = 12f;
        internal const float NativeSceneSunset = 22f;

        internal static bool IsEnabled
        {
            get
            {
                return CalendarSettingsState.ClockSynchronizedLighting
                    && Campaign.Current != null;
            }
        }

        // Discrete skybox/color-grade replacement was useful diagnostically,
        // but pinning either resource to its daytime variant lifts the night.
        // The calibrated native atmosphere is now authoritative.
        internal static bool StabilizeDiscreteResources { get { return false; } }

        internal static float GetCalibratedSceneHour()
        {
            return MapCampaignHourToSceneHour(CampaignTime.Now.CurrentHourInDay);
        }

        internal static float MapCampaignHourToSceneHour(float campaignHour)
        {
            if (!IsEnabled)
            {
                return NormalizeHour(campaignHour);
            }

            float sunrise = NormalizeHour(CalendarSettingsState.VisualSunriseHour);
            float sunset = NormalizeHour(CalendarSettingsState.VisualSunsetHour);
            float visualDayLength = ForwardDistance(sunrise, sunset);
            if (visualDayLength < 0.25f || visualDayLength > 23.75f)
            {
                return NormalizeHour(campaignHour);
            }

            campaignHour = NormalizeHour(campaignHour);
            float sinceSunrise = ForwardDistance(sunrise, campaignHour);
            if (sinceSunrise < visualDayLength)
            {
                // Solar noon is the exact midpoint of configured sunrise and
                // sunset. For 06:15--18:15 this places maximum brightness at
                // 12:15 and maps it to the atmosphere's authored frame 120.
                float peakDistance = visualDayLength * 0.5f;

                if (sinceSunrise <= peakDistance)
                {
                    return NativeSceneSunrise
                        + (NativeScenePeakBrightness - NativeSceneSunrise)
                        * SmoothStep(sinceSunrise / peakDistance);
                }

                return NativeScenePeakBrightness
                    + (NativeSceneSunset - NativeScenePeakBrightness)
                    * SmoothStep(
                        (sinceSunrise - peakDistance)
                        / (visualDayLength - peakDistance));
            }

            float visualNightLength = 24f - visualDayLength;
            float sinceSunset = ForwardDistance(sunset, campaignHour);
            float peakDarknessDistance = visualNightLength * 0.5f;
            if (sinceSunset <= peakDarknessDistance)
            {
                return NormalizeHour(NativeSceneSunset
                    + 2f * SmoothStep(sinceSunset / peakDarknessDistance));
            }

            // Solar midnight is the exact midpoint of sunset and the next
            // sunrise. It maps to authored frame 0; with the default profile
            // that is 00:15. The second half remains in the darkest authored
            // range and rises smoothly toward frame 20 at sunrise.
            return 2f * SmoothStep(
                (sinceSunset - peakDarknessDistance)
                / (visualNightLength - peakDarknessDistance));
        }

        private static float SmoothStep(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - 2f * value);
        }

        private static float Hermite(
            float start,
            float end,
            float startTangent,
            float endTangent,
            float progress)
        {
            float t2 = progress * progress;
            float t3 = t2 * progress;
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + progress;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;
            return h00 * start + h10 * startTangent
                + h01 * end + h11 * endTangent;
        }

        private static float ForwardDistance(float start, float end)
        {
            return NormalizeHour(end - start);
        }

        private static float NormalizeHour(float hour)
        {
            hour %= 24f;
            return hour < 0f ? hour + 24f : hour;
        }
    }

    /// <summary>
    /// MapScreen writes CampaignTime.Now.CurrentHourInDay directly to
    /// Scene.TimeOfDay.  Calibrate the value on the evaluation stack before
    /// that assignment so the scene never alternates between raw and
    /// calibrated hours within the same rendered frame.
    /// </summary>
    [HarmonyPatch]
    internal static class MapSceneVisualClockPatch
    {
        private static MethodBase TargetMethod()
        {
            Type mapScreenType = AccessTools.TypeByName("SandBox.View.Map.MapScreen");
            return mapScreenType == null ? null : AccessTools.Method(mapScreenType, "TickVisuals");
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo hourGetter = AccessTools.PropertyGetter(
                typeof(CampaignTime),
                nameof(CampaignTime.CurrentHourInDay));
            MethodInfo calibrator = AccessTools.Method(
                typeof(VisualDaylightClock),
                nameof(VisualDaylightClock.MapCampaignHourToSceneHour));
            int replacements = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(hourGetter))
                {
                    yield return new CodeInstruction(OpCodes.Call, calibrator);
                    replacements++;
                }
            }

            if (replacements != 1)
            {
                throw new InvalidOperationException(string.Format(
                    "Expected one campaign-map Scene.TimeOfDay source, found {0}.",
                    replacements));
            }

            Diagnostics.Info("Campaign-map raw Scene.TimeOfDay write replaced with calibrated time.");
        }
    }

    /// <summary>
    /// This is the authoritative map-lighting hook.  SandBox calls
    /// MBMapScene.TickVisuals with the raw campaign hour after writing
    /// Scene.TimeOfDay.  Replacing argument 1 means the native renderer,
    /// terrain, sky and map scene all receive the calibrated time in the same
    /// tick; no global CampaignTime or weather result is overridden.
    /// </summary>
    [HarmonyPatch]
    internal static class MapRendererVisualClockPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(MBMapScene), "TickVisuals");
        }

        [HarmonyPrefix]
        private static void Prefix(ref float __1)
        {
            if (VisualDaylightClock.IsEnabled)
            {
                __1 = VisualDaylightClock.GetCalibratedSceneHour();
            }
        }
    }

    /// <summary>
    /// The campaign map's final atmosphere pass is owned by
    /// MapColorGradeManager.  Each frame it reads its TimeOfDay field and
    /// calls MBMapScene.SetFrameForAtmosphere(TimeOfDay * 10).  Calibrating
    /// this field immediately before that call is the final, authoritative
    /// campaign-map visual boundary.
    /// </summary>
    [HarmonyPatch(typeof(MapColorGradeManager), nameof(MapColorGradeManager.ApplyAtmosphere))]
    internal static class CampaignMapAtmosphereClockPatch
    {
        private const string StableSkyboxTextureName = "semi_cloudy_2";
        private const float StableAtmosphereFrame = 140f;
        private static int _lastLoggedCampaignHour = -1;
        private static Scene _lastManagedAtmosphereScene;
        private static Scene _lastSkyboxScene;
        private static string _lastSkyboxSignature = string.Empty;
        private static Texture _stableSkyboxTexture;
        private static bool _skyboxStabilizerLogged;
        private static bool _skyboxDiagnosticErrorLogged;

        [HarmonyPrefix]
        private static bool Prefix(MapColorGradeManager __instance, ref bool forceLoadTextures)
        {
            if (!VisualDaylightClock.IsEnabled || __instance == null)
            {
                return true;
            }

            try
            {
                float campaignHour = CampaignTime.Now.CurrentHourInDay;
                float rendererHour = VisualDaylightClock.MapCampaignHourToSceneHour(campaignHour);
                __instance.TimeOfDay = rendererHour;
                if (IsTextureStreamingTransition(rendererHour))
                {
                    // Fast-forward can cross several authored atmosphere
                    // resources before asynchronous streaming completes.
                    // Force them ready only around dawn/dusk, where a late
                    // resource otherwise appears as a sequence of flashes.
                    forceLoadTextures = true;
                }
                Scene scene = __instance.Scene;
                float cameraElevation = scene == null
                    ? 0f
                    : scene.LastFinalRenderCameraFrame.origin.z;
                CampaignMapLightingDiagnostics.Record(
                    scene,
                    rendererHour * 10f,
                    cameraElevation,
                    forceLoadTextures);
                LogApplicationOnce(campaignHour, rendererHour);
                // Let Bannerlord apply its complete native atmosphere at the
                // calibrated time. The previous fixed-afternoon-frame path
                // left a daytime cubemap/exposure baseline active at night.
                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Campaign-map atmosphere clock calibration failed.", exception);
                return true;
            }
        }

        private static void ApplyContinuousManagedLighting(Scene scene, float rendererHour)
        {
            float dayProgress = (rendererHour - VisualDaylightClock.NativeSceneSunrise)
                / (VisualDaylightClock.NativeSceneSunset - VisualDaylightClock.NativeSceneSunrise);
            bool isDay = dayProgress >= 0f && dayProgress <= 1f;
            float solarHeight = isDay
                ? (float)Math.Sin(Math.PI * dayProgress)
                : 0f;
            solarHeight = Clamp01(solarHeight);

            // Power curves approximate the shipped campaign atmosphere's
            // continuous dawn/noon/dusk values while remaining C1 at both
            // horizons. Unlike the native string texture keys, none of these
            // values can jump when fast-forward crosses an authored frame.
            float skyFactor = (float)Math.Pow(solarHeight, 4.3d);
            float sunFactor = (float)Math.Pow(solarHeight, 4d);
            float exposureFactor = SmoothStep(solarHeight);
            scene.SetSkyBrightness(Lerp(0.5f, 658.962f, skyFactor));
            scene.SetMinExposure(Lerp(-6f, -15.751f, exposureFactor));
            scene.SetMaxExposure(Lerp(-1f, -9.341f, exposureFactor));
            scene.SetTargetExposure(Lerp(-3f, -9.5f, exposureFactor));
            scene.SetMiddleGray(Lerp(0.2f, 0.084f, exposureFactor));
            // The fixed neutral atmosphere carries a daytime environment map.
            // Its multiplier must follow the same solar curve or that ambient
            // cubemap lights the terrain hours before the configured sunrise.
            scene.SetEnvironmentMultiplier(
                useMultiplier: true,
                multiplier: Lerp(0.015f, 1f, exposureFactor));

            float altitude = isDay ? -5f + 60f * solarHeight : -5f;
            float angle = isDay ? 90f + 180f * dayProgress : 90f;
            float warmth = 1f - SmoothStep(Math.Min(1f, solarHeight * 3f));
            Vec3 sunColor = new Vec3(
                1f,
                Lerp(0.62f, 0.97f, 1f - warmth),
                Lerp(0.38f, 0.92f, 1f - warmth));
            scene.SetSun(
                ref sunColor,
                altitude,
                angle,
                Lerp(1f, 8200f, sunFactor));
        }

        private static float SmoothStep(float value)
        {
            value = Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * Clamp01(amount);
        }

        [HarmonyPostfix]
        private static void Postfix(MapColorGradeManager __instance)
        {
            if (!VisualDaylightClock.IsEnabled
                || !VisualDaylightClock.StabilizeDiscreteResources
                || __instance == null)
            {
                return;
            }

            StabilizeSkyboxAndRecordResourceChange(__instance.Scene);
        }

        private static void StabilizeSkyboxAndRecordResourceChange(Scene scene)
        {
            try
            {
                if (scene == null)
                {
                    return;
                }

                Mesh skyboxMesh = scene.GetSkyboxMesh();
                if (skyboxMesh == null || !skyboxMesh.IsValid)
                {
                    return;
                }

                Material material = skyboxMesh.GetMaterial();
                if (material == null || !material.IsValid)
                {
                    return;
                }

                // campaign_map.xml uses discrete string keys for its skybox
                // cubemap. The engine therefore replaces texture slot 0 at
                // frames 30, 40, 50, 60, 69, 80, 100, 110, 130, 140, 160,
                // 171, 180, 190, 200 and 210 instead of interpolating it.
                // Runtime diagnostics confirmed that the visible sunset
                // flashes align exactly with slot 0 changing from
                // semi_cloudy_2 to semi_cloudy_5_am and then sky_night. Keep
                // only that cubemap stable after the native atmosphere pass;
                // all continuously interpolated light, exposure, fog, sun,
                // weather and color-grade values remain native.
                Texture nativeSkyboxTexture = material.GetTextureWithSlot(0);
                StringBuilder signatureBuilder = new StringBuilder(192);
                signatureBuilder.Append("mesh=").Append(skyboxMesh.Name)
                    .Append("; material=").Append(material.Name)
                    .Append("; slots=");
                for (int slot = 0; slot <= 4; slot++)
                {
                    if (slot > 0)
                    {
                        signatureBuilder.Append(',');
                    }

                    Texture texture = material.GetTextureWithSlot(slot);
                    signatureBuilder.Append(slot).Append('=')
                        .Append(texture != null && texture.IsValid ? texture.Name : "<none>");
                }

                string signature = signatureBuilder.ToString();
                bool resourceChanged = !ReferenceEquals(_lastSkyboxScene, scene)
                    || !string.Equals(_lastSkyboxSignature, signature, StringComparison.Ordinal);
                if (resourceChanged)
                {
                    _lastSkyboxScene = scene;
                    _lastSkyboxSignature = signature;
                    Diagnostics.Info(string.Format(
                        CultureInfo.InvariantCulture,
                        "Campaign-map native skybox resource changed: campaign={0:0.0000}; renderer={1:0.0000}; {2}",
                        CampaignTime.Now.CurrentHourInDay,
                        VisualDaylightClock.GetCalibratedSceneHour(),
                        signature));
                }

                if (_stableSkyboxTexture == null || !_stableSkyboxTexture.IsValid)
                {
                    _stableSkyboxTexture = Texture.GetFromResource(StableSkyboxTextureName);
                    if (_stableSkyboxTexture == null || !_stableSkyboxTexture.IsValid)
                    {
                        throw new InvalidOperationException(
                            "Stable campaign-map skybox texture is unavailable: "
                            + StableSkyboxTextureName);
                    }

                    _stableSkyboxTexture.PreloadTexture(blocking: true);
                }

                if (nativeSkyboxTexture == null
                    || !nativeSkyboxTexture.IsValid
                    || !string.Equals(
                        nativeSkyboxTexture.Name,
                        StableSkyboxTextureName,
                        StringComparison.Ordinal))
                {
                    material.SetTextureAtSlot(0, _stableSkyboxTexture);
                }

                if (!_skyboxStabilizerLogged)
                {
                    _skyboxStabilizerLogged = true;
                    Diagnostics.Info(
                        "Campaign-map skybox slot 0 stabilized with preloaded texture '"
                        + StableSkyboxTextureName
                        + "'; native continuous atmosphere channels remain active.");
                }
            }
            catch (Exception exception)
            {
                if (_skyboxDiagnosticErrorLogged)
                {
                    return;
                }

                _skyboxDiagnosticErrorLogged = true;
                Diagnostics.Error("Campaign-map skybox resource diagnostics failed.", exception);
            }
        }

        private static bool IsTextureStreamingTransition(float rendererHour)
        {
            rendererHour %= 24f;
            if (rendererHour < 0f) rendererHour += 24f;
            // The calibrated 05:00 sunrise maps to renderer hour 02:00, but
            // the authored dawn resources continue changing through roughly
            // renderer hour 04:00 (about 06:27 campaign time). Keep one hour
            // of margin so the last resource cannot arrive late near 05:59.
            return rendererHour >= 19f || rendererHour <= 5f;
        }

        private static void LogApplicationOnce(float campaignHour, float rendererHour)
        {
            int wholeHour = (int)Math.Floor(campaignHour);
            if (_lastLoggedCampaignHour == wholeHour)
            {
                return;
            }

            _lastLoggedCampaignHour = wholeHour;
            Diagnostics.Info(string.Format(
                "Campaign-map atmosphere applied: campaign={0:0.00}; renderer={1:0.00}.",
                campaignHour,
                rendererHour));
        }
    }

    /// <summary>
    /// Bannerlord changes to a separate worldmap_colorgrade_night texture when
    /// TimeOfDay crosses 02:00/22:00.  The atmosphere is already changing
    /// continuously, so that second discrete switch is the remaining flash.
    /// ApplyColorGrade uses TimeOfDay only for that day/night override; expose
    /// a neutral hour while it selects a grade, then restore the calibrated
    /// time. Terrain and rain grades remain native.
    /// </summary>
    [HarmonyPatch(typeof(MapColorGradeManager), nameof(MapColorGradeManager.ApplyColorGrade))]
    internal static class CampaignMapColorGradeSmoothingPatch
    {
        private const float ColorGradeBlendSeconds = 5f;
        private static readonly FieldInfo LastColorGradeField =
            AccessTools.Field(typeof(MapColorGradeManager), "lastColorGrade");
        private static readonly FieldInfo ColorGradeMappingField =
            AccessTools.Field(typeof(MapColorGradeManager), "colorGradeGridMapping");
        private static readonly FieldInfo DefaultColorGradeField =
            AccessTools.Field(typeof(MapColorGradeManager), "defaultColorGradeTextureName");
        private static readonly FieldInfo PrimaryTransitionField =
            AccessTools.Field(typeof(MapColorGradeManager), "primaryTransitionRecord");
        private static bool _logged;
        private static bool _stabilizerLogged;
        private static bool _stabilizerErrorLogged;

        [HarmonyPrefix]
        private static void Prefix(MapColorGradeManager __instance, ref float dt, out float __state)
        {
            __state = __instance == null ? 12f : __instance.TimeOfDay;
            if (!VisualDaylightClock.IsEnabled
                || !VisualDaylightClock.StabilizeDiscreteResources
                || __instance == null)
            {
                return;
            }

            CampaignMapLightingDiagnostics.RecordColorGrade(
                string.Empty,
                string.Empty,
                -1f,
                manualOverride: false);

            // ApplyColorGrade's only TimeOfDay read selects the hardcoded
            // night texture. The atmosphere frame still receives the real,
            // calibrated visual hour through ApplyAtmosphere.
            __instance.TimeOfDay = 12f;
            if (dt > 0f)
            {
                dt /= ColorGradeBlendSeconds;
            }
            if (_logged)
            {
                return;
            }

            _logged = true;
            Diagnostics.Info(string.Format(
                "Campaign-map discrete night color grade suppressed; native terrain/rain blends use {0:0.0} seconds.",
                ColorGradeBlendSeconds));
        }

        [HarmonyPostfix]
        private static void Postfix(MapColorGradeManager __instance, float __state)
        {
            if (VisualDaylightClock.IsEnabled
                && VisualDaylightClock.StabilizeDiscreteResources
                && __instance != null)
            {
                __instance.TimeOfDay = __state;
                ReassertManagedColorGrade(__instance);
            }
        }

        private static void ReassertManagedColorGrade(MapColorGradeManager manager)
        {
            try
            {
                Scene scene = manager.Scene;
                if (scene == null
                    || LastColorGradeField == null
                    || ColorGradeMappingField == null
                    || DefaultColorGradeField == null
                    || PrimaryTransitionField == null)
                {
                    return;
                }

                // While Bannerlord is actively blending between terrain/rain
                // grades, ApplyColorGrade has already issued the authoritative
                // SetColorGradeBlend call for this frame. Once that transition
                // finishes it stops writing, so reassert the selected texture
                // every frame to overwrite campaign_map.xml's discrete
                // atmosphere colorgrade keys at native frames 20 and 220.
                if (PrimaryTransitionField.GetValue(manager) != null)
                {
                    return;
                }

                byte selectedGrade = (byte)LastColorGradeField.GetValue(manager);
                Dictionary<byte, string> mapping =
                    ColorGradeMappingField.GetValue(manager) as Dictionary<byte, string>;
                string textureName;
                if (mapping == null
                    || !mapping.TryGetValue(selectedGrade, out textureName)
                    || string.IsNullOrWhiteSpace(textureName))
                {
                    textureName = DefaultColorGradeField.GetValue(manager) as string;
                }
                if (string.IsNullOrWhiteSpace(textureName))
                {
                    return;
                }

                scene.SetSceneColorGrade(textureName);
                CampaignMapLightingDiagnostics.RecordColorGrade(
                    textureName,
                    string.Empty,
                    0f,
                    manualOverride: true);
                if (!_stabilizerLogged)
                {
                    _stabilizerLogged = true;
                    Diagnostics.Info(
                        "Campaign-map atmosphere color-grade keys are overridden by the current managed terrain/rain grade.");
                }
            }
            catch (Exception exception)
            {
                if (_stabilizerErrorLogged)
                {
                    return;
                }
                _stabilizerErrorLogged = true;
                Diagnostics.Error(
                    "Campaign-map color-grade stabilization failed.",
                    exception);
            }
        }
    }

    /// <summary>
    /// Captures the final time value immediately before the campaign map's
    /// native atmosphere call. Samples are limited to dawn/dusk and unexpected
    /// jumps, and buffered so diagnostics do not introduce frame stalls.
    /// </summary>
    internal static class CampaignMapLightingDiagnostics
    {
        private const double SampleIntervalSeconds = 0.1d;
        private const double FlushIntervalSeconds = 1d;
        private const float TransitionWindowHours = 2.5f;
        private const float UnexpectedJumpHours = 0.2f;
        private static readonly object SyncRoot = new object();
        private static readonly StringBuilder Buffer = new StringBuilder(8192);
        private static string _path;
        private static bool _initialized;
        private static bool _hasPrevious;
        private static float _previousAtmosphereHour;
        private static long _previousCallTimestamp;
        private static long _lastSampleTimestamp;
        private static long _lastFlushTimestamp;
        private static string _dayColorGrade = string.Empty;
        private static string _nightColorGrade = string.Empty;
        private static float _nightColorGradeAlpha = -1f;
        private static bool _manualColorGradeOverride;

        internal static void RecordColorGrade(
            string dayTexture,
            string nightTexture,
            float nightAlpha,
            bool manualOverride)
        {
            _dayColorGrade = dayTexture ?? string.Empty;
            _nightColorGrade = nightTexture ?? string.Empty;
            _nightColorGradeAlpha = nightAlpha;
            _manualColorGradeOverride = manualOverride;
        }

        internal static void Record(
            Scene mapScene,
            float atmosphereFrame,
            float cameraElevation,
            bool forceLoadTextures)
        {
            if (!VisualDaylightClock.IsEnabled || mapScene == null)
            {
                FlushIfPending(force: true);
                return;
            }

            try
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                float atmosphereHour = NormalizeHour(atmosphereFrame / 10f);
                float campaignHour = CampaignTime.Now.CurrentHourInDay;
                float targetHour = VisualDaylightClock.MapCampaignHourToSceneHour(campaignHour);
                float sceneHour = NormalizeHour(mapScene.TimeOfDay);
                float delta = _hasPrevious
                    ? SignedCircularDelta(_previousAtmosphereHour, atmosphereHour)
                    : 0f;
                double callDeltaMilliseconds = _hasPrevious
                    ? ElapsedSeconds(_previousCallTimestamp, now) * 1000d
                    : 0d;
                bool unexpectedJump = _hasPrevious && Math.Abs(delta) > UnexpectedJumpHours;
                bool nearTransition = CircularDistance(atmosphereHour, 2f) <= TransitionWindowHours
                    || CircularDistance(atmosphereHour, 22f) <= TransitionWindowHours;
                bool sampleDue = ElapsedSeconds(_lastSampleTimestamp, now) >= SampleIntervalSeconds;

                _hasPrevious = true;
                _previousAtmosphereHour = atmosphereHour;
                _previousCallTimestamp = now;
                if (!unexpectedJump && (!nearTransition || !sampleDue))
                {
                    FlushIfDue(now);
                    return;
                }

                EnsureInitialized(now);
                _lastSampleTimestamp = now;
                string mode = Campaign.Current == null
                    ? "none"
                    : Campaign.Current.TimeControlMode.ToString();
                float speed = Campaign.Current == null
                    ? 0f
                    : Campaign.Current.SpeedUpMultiplier;
                float targetMismatch = SignedCircularDelta(targetHour, atmosphereHour);
                float sceneMismatch = SignedCircularDelta(sceneHour, atmosphereHour);
                Buffer.Append(DateTime.Now.ToString("O", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(unexpectedJump ? "JUMP" : "SAMPLE").Append('\t')
                    .Append(campaignHour.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(targetHour.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(atmosphereHour.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(sceneHour.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(delta.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(callDeltaMilliseconds.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(targetMismatch.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(sceneMismatch.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(mode).Append('\t')
                    .Append(speed.ToString("F3", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(forceLoadTextures ? "1" : "0").Append('\t')
                    .Append(cameraElevation.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(_manualColorGradeOverride ? "1" : "0").Append('\t')
                    .Append(_nightColorGradeAlpha.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(_dayColorGrade).Append('\t')
                    .Append(_nightColorGrade)
                    .AppendLine();
                FlushIfDue(now);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Campaign-map lighting diagnostics failed.", exception);
            }
        }

        private static void EnsureInitialized(long now)
        {
            if (_initialized)
            {
                return;
            }

            string primaryPath = Diagnostics.LogPath;
            string directory = string.IsNullOrWhiteSpace(primaryPath)
                ? null
                : System.IO.Path.GetDirectoryName(primaryPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            _path = System.IO.Path.Combine(directory, "CampaignMapLightingDiagnostics.tsv");
            Buffer.Append("# session\t")
                .Append(DateTime.Now.ToString("O", CultureInfo.InvariantCulture))
                .AppendLine();
            Buffer.AppendLine(
                "Timestamp\tReason\tCampaignHour\tTargetHour\tAtmosphereHour\tSceneHour\tAtmosphereDelta\tCallDeltaMs\tTargetMismatch\tSceneMismatch\tTimeMode\tSpeedMultiplier\tForceLoadTextures\tCameraElevation\tManualColorGrade\tNightAlpha\tDayTexture\tNightTexture");
            _initialized = true;
            _lastFlushTimestamp = now;
            Diagnostics.Info("Campaign-map transition diagnostics active: " + _path);
        }

        private static void FlushIfDue(long now)
        {
            if (Buffer.Length > 0
                && ElapsedSeconds(_lastFlushTimestamp, now) >= FlushIntervalSeconds)
            {
                FlushIfPending(force: true);
                _lastFlushTimestamp = now;
            }
        }

        private static void FlushIfPending(bool force)
        {
            if (!force || string.IsNullOrWhiteSpace(_path) || Buffer.Length == 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                File.AppendAllText(_path, Buffer.ToString(), Encoding.UTF8);
                Buffer.Clear();
            }
        }

        private static double ElapsedSeconds(long start, long end)
        {
            if (start <= 0L || end <= start)
            {
                return double.MaxValue;
            }
            return (double)(end - start) / System.Diagnostics.Stopwatch.Frequency;
        }

        private static float CircularDistance(float first, float second)
        {
            return Math.Abs(SignedCircularDelta(first, second));
        }

        private static float SignedCircularDelta(float from, float to)
        {
            float delta = NormalizeHour(to) - NormalizeHour(from);
            if (delta > 12f) delta -= 24f;
            if (delta < -12f) delta += 24f;
            return delta;
        }

        private static float NormalizeHour(float hour)
        {
            hour %= 24f;
            return hour < 0f ? hour + 24f : hour;
        }
    }

    /// <summary>
    /// Stable lighting path: adjust the weather result without rewriting the
    /// map scene clock, atmosphere frame, skybox, or renderer-owned sun.
    /// This preserves Bannerlord's real night rendering while allowing the
    /// configured campaign daylight window to darken early-morning hours.
    /// </summary>
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultMapWeatherModel), "GetAtmosphereModel")]
    internal static class StableMapWeatherLightingPatch
    {
        private static int _lastLoggedHour = -1;

        [HarmonyPostfix]
        private static void Postfix(ref AtmosphereInfo __result)
        {
            if (!CalendarSettingsState.ClockSynchronizedLighting || Campaign.Current == null)
            {
                return;
            }

            try
            {
                float hour = NormalizeHour(CampaignTime.Now.CurrentHourInDay);
                float customDaylight = GetDaylightFactor(
                    hour,
                    CalendarSettingsState.VisualSunriseHour,
                    CalendarSettingsState.VisualSunsetHour,
                    CalendarSettingsState.VisualLightingTransitionHours);
                float nativeDaylight = GetDaylightFactor(
                    hour,
                    CampaignTime.SunRise,
                    CampaignTime.SunSet,
                    2f);
                float ratio = Clamp(
                    (0.001f + 0.999f * customDaylight) / (0.001f + 0.999f * nativeDaylight),
                    0.001f,
                    3f);

                __result.SunInfo.Brightness = Math.Max(0f, __result.SunInfo.Brightness * ratio);
                __result.SunInfo.MaxBrightness = Math.Max(0f, __result.SunInfo.MaxBrightness * ratio);
                __result.SunInfo.RayStrength = Math.Max(0f, __result.SunInfo.RayStrength * ratio);
                __result.AmbientInfo.EnvironmentMultiplier = Clamp(
                    __result.AmbientInfo.EnvironmentMultiplier * ratio,
                    0.001f,
                    1.5f);
                __result.SkyInfo.Brightness = Math.Max(0f, __result.SkyInfo.Brightness * ratio);
                float rendererHour = VisualDaylightClock.MapCampaignHourToSceneHour(hour);
                __result.TimeInfo.TimeOfDay = rendererHour;
                __result.TimeInfo.NightTimeFactor = 1f - customDaylight;
                // Bannerlord uses -2 at night and -3 by day. The old formula
                // reversed those endpoints and introduced an exposure change
                // at the native 02:00 boundary.
                __result.PostProInfo.MinExposure = Lerp(-2f, -3f, customDaylight);

                int wholeHour = (int)Math.Floor(hour);
                if (wholeHour != _lastLoggedHour)
                {
                    _lastLoggedHour = wholeHour;
                    Diagnostics.Info(string.Format(
                        CultureInfo.InvariantCulture,
                        "Campaign lighting output: campaign={0:0.000}; renderer={1:0.000}; daylight={2:0.0000}; nativeDaylight={3:0.0000}; ratio={4:0.0000}; sun={5:0.0000}; sky={6:0.0000}; environment={7:0.0000}; minExposure={8:0.0000}; night={9:0.0000}.",
                        hour,
                        rendererHour,
                        customDaylight,
                        nativeDaylight,
                        ratio,
                        __result.SunInfo.Brightness,
                        __result.SkyInfo.Brightness,
                        __result.AmbientInfo.EnvironmentMultiplier,
                        __result.PostProInfo.MinExposure,
                        __result.TimeInfo.NightTimeFactor));
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Stable campaign-map lighting adjustment failed; native atmosphere remains active.", exception);
            }
        }

        private static float GetDaylightFactor(float hour, float sunrise, float sunset, float transition)
        {
            float dayLength = NormalizeHour(sunset - sunrise);
            if (dayLength <= 0.25f || dayLength >= 23.75f)
            {
                return 0f;
            }

            float edge = Math.Max(0.25f, Math.Min(transition, 2f));
            float dawnStart = NormalizeHour(sunrise - edge);
            float sinceDawnStart = NormalizeHour(hour - dawnStart);
            if (sinceDawnStart < edge)
            {
                return SmoothStep(sinceDawnStart / edge);
            }

            float sinceSunrise = NormalizeHour(hour - sunrise);
            if (sinceSunrise < dayLength)
            {
                return 1f;
            }

            float sinceSunset = NormalizeHour(hour - sunset);
            return sinceSunset < edge
                ? 1f - SmoothStep(sinceSunset / edge)
                : 0f;
        }

        private static float SmoothStep(float value)
        {
            value = Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float NormalizeHour(float hour)
        {
            hour %= 24f;
            return hour < 0f ? hour + 24f : hour;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static float Lerp(float start, float end, float amount)
        {
            return start + (end - start) * Clamp(amount, 0f, 1f);
        }
    }
}
