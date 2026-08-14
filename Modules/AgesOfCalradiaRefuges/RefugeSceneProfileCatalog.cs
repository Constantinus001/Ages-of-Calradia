using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// The fixed-refuge scene contract. A profile is editor-authored terrain,
    /// a linked fort root, markers, collision, and navmesh; it is never a
    /// list of runtime-spawned wall or tower children.
    /// </summary>
    internal struct RefugeSceneProfile
    {
        internal readonly string SceneId;
        internal readonly RefugeSceneClimate Climate;
        internal readonly RefugeWaterAccessType WaterAccess;
        internal readonly string FortPrefabId;

        internal RefugeSceneProfile(string sceneId, RefugeSceneClimate climate, RefugeWaterAccessType waterAccess, string fortPrefabId)
        {
            SceneId = sceneId;
            Climate = climate;
            WaterAccess = waterAccess;
            FortPrefabId = fortPrefabId;
        }
    }

    internal static class RefugeSceneProfileCatalog
    {
        private static readonly RefugeSceneProfile[] Profiles = BuildProfiles();

        private static readonly string[] RequiredSpawnMarkers =
        {
            "spawnpoint_player",
            "rct_refuge_steward_spawn",
            "rct_refuge_cook_spawn",
            "rct_refuge_guard_captain_spawn",
            "rct_refuge_healer_spawn"
        };

        private const string AnchorTag = "rct_refuge_anchor";
        private const string LayoutTag = "rct_refuge_layout";
        private const float MaximumAnchorDistance = 0.5f;
        private const float TransformTolerance = 0.01f;

        internal static bool TryGetProfile(string sceneId, out RefugeSceneProfile profile)
        {
            for (int index = 0; index < Profiles.Length; index++)
            {
                if (string.Equals(Profiles[index].SceneId, sceneId, StringComparison.Ordinal))
                {
                    profile = Profiles[index];
                    return true;
                }
            }

            profile = default(RefugeSceneProfile);
            return false;
        }

        internal static bool TryGetReadySceneId(
            RefugeSceneClimate climate,
            RefugeWaterAccessType waterAccess,
            string fortPrefabId,
            out string sceneId)
        {
            for (int index = 0; index < Profiles.Length; index++)
            {
                RefugeSceneProfile profile = Profiles[index];
                if (profile.Climate == climate
                    && profile.WaterAccess == waterAccess
                    && string.Equals(profile.FortPrefabId, fortPrefabId, StringComparison.Ordinal)
                    && IsReady(profile.SceneId, out _))
                {
                    sceneId = profile.SceneId;
                    return true;
                }
            }

            sceneId = string.Empty;
            string diagnostic = DescribeProfileReadiness(climate, waterAccess, fortPrefabId);
            Diagnostics.Info("Refuge scene-profile selection failed. Climate=" + climate
                + "; Access=" + waterAccess
                + "; Fort=" + fortPrefabId
                + "; Profiles=" + diagnostic + ".");
            return false;
        }

        /// <summary>
        /// Produces a bounded, single-line readiness report for diagnostics.
        /// It is used only at user-triggered profile selection boundaries.
        /// </summary>
        internal static string DescribeProfileReadiness(
            RefugeSceneClimate climate,
            RefugeWaterAccessType waterAccess,
            string fortPrefabId)
        {
            for (int index = 0; index < Profiles.Length; index++)
            {
                RefugeSceneProfile profile = Profiles[index];
                if (profile.Climate == climate
                    && profile.WaterAccess == waterAccess
                    && string.Equals(profile.FortPrefabId, fortPrefabId, StringComparison.Ordinal))
                {
                    string reason;
                    return IsReady(profile.SceneId, out reason)
                        ? profile.SceneId + "=ready"
                        : profile.SceneId + "=" + reason;
                }
            }

            return "no registered profile";
        }

        private static RefugeSceneProfile[] BuildProfiles()
        {
            SceneProfileTemplate[] templates = LoadProfileTemplates();
            List<RefugeSceneProfile> profiles = new List<RefugeSceneProfile>();
            foreach (RefugeFortPrefabDefinition fort in RefugeFortPrefabCatalog.All)
            {
                foreach (SceneProfileTemplate template in templates)
                {
                    string sceneId = template.BaseSceneId;
                    if (!string.Equals(fort.PrefabId, RefugeFortPrefabCatalog.DefaultFortPrefabId, StringComparison.Ordinal))
                    {
                        sceneId += "_" + fort.SceneSuffix;
                    }
                    profiles.Add(new RefugeSceneProfile(sceneId, template.Climate, template.WaterAccess, fort.PrefabId));
                }
            }
            return profiles.ToArray();
        }

        private static SceneProfileTemplate[] LoadProfileTemplates()
        {
            SceneProfileTemplate[] defaults = CreateDefaultProfileTemplates();
            try
            {
                string path = Path.Combine(CalendarRefugeMission.GetModuleDirectoryPath(), "ModuleData", "RefugeSceneProfiles.xml");
                if (!File.Exists(path))
                {
                    return defaults;
                }

                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlNodeList nodes = document.SelectNodes("/refuge_scene_profiles/profile");
                if (nodes == null)
                {
                    return defaults;
                }

                Dictionary<string, SceneProfileTemplate> configured = new Dictionary<string, SceneProfileTemplate>(StringComparer.Ordinal);
                foreach (XmlNode node in nodes)
                {
                    XmlElement element = node as XmlElement;
                    SceneProfileTemplate template;
                    if (element != null && TryReadProfileTemplate(element, out template))
                    {
                        configured[GetTemplateKey(template.Climate, template.WaterAccess)] = template;
                    }
                }

                if (configured.Count != defaults.Length)
                {
                    Diagnostics.Info("Refuge scene-profile manifest must define every temperate, desert, and Sturgian land/river/coast profile; using safe defaults.");
                    return defaults;
                }

                SceneProfileTemplate[] result = new SceneProfileTemplate[defaults.Length];
                for (int index = 0; index < defaults.Length; index++)
                {
                    SceneProfileTemplate configuredTemplate;
                    if (!configured.TryGetValue(GetTemplateKey(defaults[index].Climate, defaults[index].WaterAccess), out configuredTemplate))
                    {
                        Diagnostics.Info("Refuge scene-profile manifest is missing a required climate/access profile; using safe defaults.");
                        return defaults;
                    }
                    result[index] = configuredTemplate;
                }
                return result;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge scene-profile manifest could not be loaded; using safe defaults.", exception);
                return defaults;
            }
        }

        private static SceneProfileTemplate[] CreateDefaultProfileTemplates()
        {
            return new[]
            {
                new SceneProfileTemplate(RefugeSceneClimate.Temperate, RefugeWaterAccessType.Land, "rct_refuge_temperate_land"),
                new SceneProfileTemplate(RefugeSceneClimate.Temperate, RefugeWaterAccessType.River, "rct_refuge_temperate_river"),
                new SceneProfileTemplate(RefugeSceneClimate.Temperate, RefugeWaterAccessType.Coast, "rct_refuge_temperate_coast"),
                new SceneProfileTemplate(RefugeSceneClimate.Desert, RefugeWaterAccessType.Land, "rct_refuge_desert_land"),
                new SceneProfileTemplate(RefugeSceneClimate.Desert, RefugeWaterAccessType.River, "rct_refuge_desert_river"),
                new SceneProfileTemplate(RefugeSceneClimate.Desert, RefugeWaterAccessType.Coast, "rct_refuge_desert_coast"),
                new SceneProfileTemplate(RefugeSceneClimate.Snow, RefugeWaterAccessType.Land, "rct_refuge_snow_land"),
                new SceneProfileTemplate(RefugeSceneClimate.Snow, RefugeWaterAccessType.River, "rct_refuge_snow_river"),
                new SceneProfileTemplate(RefugeSceneClimate.Snow, RefugeWaterAccessType.Coast, "rct_refuge_snow_coast")
            };
        }

        private static bool TryReadProfileTemplate(XmlElement element, out SceneProfileTemplate template)
        {
            template = default(SceneProfileTemplate);
            RefugeSceneClimate climate;
            RefugeWaterAccessType waterAccess;
            string sceneId = element.GetAttribute("scene_id");
            if (!TryParseClimate(element.GetAttribute("climate"), out climate)
                || !TryParseWaterAccess(element.GetAttribute("water"), out waterAccess)
                || !IsSafeSceneId(sceneId))
            {
                return false;
            }
            template = new SceneProfileTemplate(climate, waterAccess, sceneId);
            return true;
        }

        private static bool TryParseClimate(string value, out RefugeSceneClimate climate)
        {
            climate = RefugeSceneClimate.Temperate;
            if (string.Equals(value, "temperate", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "desert", StringComparison.OrdinalIgnoreCase)) { climate = RefugeSceneClimate.Desert; return true; }
            if (string.Equals(value, "sturgian", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "snow", StringComparison.OrdinalIgnoreCase)) { climate = RefugeSceneClimate.Snow; return true; }
            return false;
        }

        private static bool TryParseWaterAccess(string value, out RefugeWaterAccessType waterAccess)
        {
            waterAccess = RefugeWaterAccessType.Land;
            if (string.Equals(value, "land", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "plain", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "river", StringComparison.OrdinalIgnoreCase)) { waterAccess = RefugeWaterAccessType.River; return true; }
            if (string.Equals(value, "coast", StringComparison.OrdinalIgnoreCase)) { waterAccess = RefugeWaterAccessType.Coast; return true; }
            return false;
        }

        private static string GetTemplateKey(RefugeSceneClimate climate, RefugeWaterAccessType waterAccess)
        {
            return ((int)climate).ToString(CultureInfo.InvariantCulture) + ":" + ((int)waterAccess).ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsSafeSceneId(string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId) || !sceneId.StartsWith("rct_refuge_", StringComparison.Ordinal)) return false;
            for (int index = 0; index < sceneId.Length; index++)
            {
                char value = sceneId[index];
                if (!(char.IsLetterOrDigit(value) || value == '_')) return false;
            }
            return true;
        }

        private static string GetClimateToken(RefugeSceneClimate climate)
        {
            switch (climate)
            {
                case RefugeSceneClimate.Desert: return "desert";
                case RefugeSceneClimate.Snow: return "snow";
                default: return "temperate";
            }
        }

        private static string GetWaterToken(RefugeWaterAccessType waterAccess)
        {
            switch (waterAccess)
            {
                case RefugeWaterAccessType.River: return "river";
                case RefugeWaterAccessType.Coast: return "coast";
                default: return "land";
            }
        }

        internal static bool IsReady(string sceneId, out string reason)
        {
            RefugeSceneProfile profile;
            if (!TryGetProfile(sceneId, out profile))
            {
                reason = "not a registered refuge scene profile";
                return false;
            }

            try
            {
                if (!RefugeFortPrefabCatalog.IsAssetReady(profile.FortPrefabId, out reason))
                {
                    return false;
                }

                string sceneDirectory = Path.Combine(CalendarRefugeMission.GetModuleDirectoryPath(), "SceneObj", sceneId);
                string scenePath = Path.Combine(sceneDirectory, "scene.xscene");
                if (!File.Exists(scenePath)) { reason = "missing scene.xscene"; return false; }
                if (!File.Exists(Path.Combine(sceneDirectory, "terrain.bin"))) { reason = "missing terrain.bin"; return false; }
                if (!File.Exists(Path.Combine(sceneDirectory, "navmesh.bin"))) { reason = "missing navmesh.bin"; return false; }

                XmlDocument document = new XmlDocument();
                document.Load(scenePath);
                XmlElement root = document.DocumentElement;
                if (root == null || !string.Equals(root.GetAttribute("name"), sceneId, StringComparison.Ordinal))
                {
                    reason = "scene internal name does not match profile ID";
                    return false;
                }

                XmlElement anchor = FindSingleTaggedEntity(document, AnchorTag, out reason);
                if (anchor == null)
                {
                    return false;
                }

                XmlElement layout = FindSingleTaggedEntity(document, LayoutTag, out reason);
                if (layout == null)
                {
                    return false;
                }

                if (object.ReferenceEquals(anchor, layout))
                {
                    reason = "anchor and linked layout must be separate entities";
                    return false;
                }

                if (!IsFortLayoutRoot(layout, RefugeFortPrefabCatalog.GetLinkedPrefabRootId(profile.FortPrefabId)))
                {
                    reason = "layout marker is not attached to the registered fort prefab root";
                    return false;
                }

                SceneTransform anchorTransform;
                SceneTransform layoutTransform;
                if (!TryReadTransform(anchor, out anchorTransform)
                    || !TryReadTransform(layout, out layoutTransform))
                {
                    reason = "anchor or linked layout is missing a valid transform";
                    return false;
                }

                if (Distance(anchorTransform, layoutTransform) > MaximumAnchorDistance)
                {
                    reason = "anchor is more than 0.5m from the linked layout root";
                    return false;
                }

                if (!ApproximatelyEqual(anchorTransform.RotationX, layoutTransform.RotationX)
                    || !ApproximatelyEqual(anchorTransform.RotationY, layoutTransform.RotationY)
                    || !ApproximatelyEqual(anchorTransform.RotationZ, layoutTransform.RotationZ))
                {
                    reason = "anchor rotation does not match the linked layout root";
                    return false;
                }

                if (!ApproximatelyEqual(layoutTransform.ScaleX, 1f)
                    || !ApproximatelyEqual(layoutTransform.ScaleY, 1f)
                    || !ApproximatelyEqual(layoutTransform.ScaleZ, 1f))
                {
                    reason = "linked layout root scale must be 1,1,1";
                    return false;
                }

                for (int index = 0; index < RequiredSpawnMarkers.Length; index++)
                {
                    if (!HasNamedOrTaggedEntity(document, RequiredSpawnMarkers[index]))
                    {
                        reason = "missing required spawn marker " + RequiredSpawnMarkers[index];
                        return false;
                    }
                }

                string xml = document.OuterXml;
                if (xml.IndexOf("editor_plane_low", StringComparison.OrdinalIgnoreCase) >= 0
                    || xml.IndexOf("bo_editor_plane", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reason = "contains an editor ground plane";
                    return false;
                }

                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "scene probe failed: " + exception.GetType().Name;
                return false;
            }
        }

        private static XmlElement FindSingleTaggedEntity(XmlDocument document, string tag, out string reason)
        {
            XmlNodeList nodes = document.SelectNodes("//game_entity[tags/tag[@name='" + tag + "']]");
            if (nodes == null || nodes.Count == 0)
            {
                reason = "missing required marker " + tag;
                return null;
            }

            if (nodes.Count != 1)
            {
                reason = "marker " + tag + " is attached to " + nodes.Count + " entities; exactly one is required";
                return null;
            }

            reason = string.Empty;
            return nodes[0] as XmlElement;
        }

        private static bool HasNamedOrTaggedEntity(XmlDocument document, string marker)
        {
            XmlNode node = document.SelectSingleNode("//game_entity[@name='" + marker
                + "' or @prefab='" + marker + "' or tags/tag[@name='" + marker + "']]");
            return node != null;
        }

        private static bool IsFortLayoutRoot(XmlElement entity, string fortPrefabId)
        {
            return string.Equals(entity.GetAttribute("name"), fortPrefabId, StringComparison.Ordinal)
                || string.Equals(entity.GetAttribute("prefab"), fortPrefabId, StringComparison.Ordinal)
                || string.Equals(entity.GetAttribute("old_prefab_name"), fortPrefabId, StringComparison.Ordinal);
        }

        private static bool TryReadTransform(XmlElement entity, out SceneTransform transform)
        {
            transform = default(SceneTransform);
            XmlElement node = entity.SelectSingleNode("transform") as XmlElement;
            if (node == null)
            {
                return false;
            }

            return TryParseVector(node.GetAttribute("position"), out transform.PositionX, out transform.PositionY, out transform.PositionZ)
                && TryParseVectorOrDefault(node.GetAttribute("rotation_euler"), 0f, out transform.RotationX, out transform.RotationY, out transform.RotationZ)
                && TryParseVectorOrDefault(node.GetAttribute("scale"), 1f, out transform.ScaleX, out transform.ScaleY, out transform.ScaleZ);
        }

        private static bool TryParseVectorOrDefault(string value, float defaultValue, out float x, out float y, out float z)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                x = defaultValue;
                y = defaultValue;
                z = defaultValue;
                return true;
            }

            return TryParseVector(value, out x, out y, out z);
        }

        private static bool TryParseVector(string value, out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            string[] values = (value ?? string.Empty).Split(',');
            return values.Length == 3
                && float.TryParse(values[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                && float.TryParse(values[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && float.TryParse(values[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z);
        }

        private static float Distance(SceneTransform left, SceneTransform right)
        {
            float x = left.PositionX - right.PositionX;
            float y = left.PositionY - right.PositionY;
            float z = left.PositionZ - right.PositionZ;
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }

        private static bool ApproximatelyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= TransformTolerance;
        }

        private struct SceneTransform
        {
            internal float PositionX;
            internal float PositionY;
            internal float PositionZ;
            internal float RotationX;
            internal float RotationY;
            internal float RotationZ;
            internal float ScaleX;
            internal float ScaleY;
            internal float ScaleZ;
        }

        private struct SceneProfileTemplate
        {
            internal readonly RefugeSceneClimate Climate;
            internal readonly RefugeWaterAccessType WaterAccess;
            internal readonly string BaseSceneId;

            internal SceneProfileTemplate(RefugeSceneClimate climate, RefugeWaterAccessType waterAccess, string baseSceneId)
            {
                Climate = climate;
                WaterAccess = waterAccess;
                BaseSceneId = baseSceneId;
            }
        }
    }
}
