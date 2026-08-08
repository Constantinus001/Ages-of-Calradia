using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Describes a complete refuge compound prefab. Forts are registered as
    /// whole authored roots, never as a collection of independently spawned
    /// wall, tower, or platform children.
    /// </summary>
    internal struct RefugeFortPrefabDefinition
    {
        internal readonly string PrefabId;
        internal readonly string AssetFileName;
        internal readonly string RuntimeLayoutFileName;
        internal readonly bool RequiresSceneLink;
        internal readonly string DisplayName;
        internal readonly string Description;
        internal readonly string SceneSuffix;
        internal readonly string LinkedPrefabRootId;
        internal readonly bool AllowsNativeTestFallback;

        internal RefugeFortPrefabDefinition(
            string prefabId,
            string assetFileName,
            string runtimeLayoutFileName,
            bool requiresSceneLink,
            string displayName,
            string description,
            string sceneSuffix,
            string linkedPrefabRootId = null,
            bool allowsNativeTestFallback = false)
        {
            PrefabId = prefabId;
            AssetFileName = assetFileName;
            RuntimeLayoutFileName = runtimeLayoutFileName;
            RequiresSceneLink = requiresSceneLink;
            DisplayName = displayName;
            Description = description;
            SceneSuffix = sceneSuffix;
            LinkedPrefabRootId = string.IsNullOrWhiteSpace(linkedPrefabRootId) ? prefabId : linkedPrefabRootId;
            AllowsNativeTestFallback = allowsNativeTestFallback;
        }
    }

    /// <summary>
    /// Single source of truth for refuge-fort prefab assets. Adding another
    /// fort is intentionally data-only: register its root ID and files here,
    /// then reference that ID from an authored scene profile.
    /// </summary>
    internal static class RefugeFortPrefabCatalog
    {
        internal const string DefaultFortPrefabId = "rct_refuge_fort_layout";

        private static readonly RefugeFortPrefabDefinition[] BuiltInDefinitions =
        {
            // This large, flattened 192-entity prefab has triggered native
            // access violations when instantiated as a mission-time root.
            // It must therefore be linked into an authored SceneObj, where
            // the editor owns terrain, collision, and navmesh.
            new RefugeFortPrefabDefinition(
                DefaultFortPrefabId,
                "rct_refuge_fort_layout.xml",
                "rct_refuge_fort_runtime_layout.xml",
                requiresSceneLink: true,
                displayName: "Palisade Ring",
                description: "A round timber palisade with inward-facing wall platforms.",
                sceneSuffix: "palisade",
                allowsNativeTestFallback: true),
            new RefugeFortPrefabDefinition(
                "rct_refuge_fort_hill",
                "rct_refuge_fort_layout.xml",
                "rct_refuge_fort_runtime_layout.xml",
                requiresSceneLink: true,
                displayName: "Hill Fort",
                description: "A compact raised fort intended for a clear hilltop pad.",
                sceneSuffix: "hill",
                // Temporary compatibility alias: this test option uses the
                // known Palisade Ring root until its own art is authored.
                linkedPrefabRootId: DefaultFortPrefabId,
                allowsNativeTestFallback: true),
            new RefugeFortPrefabDefinition(
                "rct_refuge_fort_river",
                "rct_refuge_fort_layout.xml",
                "rct_refuge_fort_runtime_layout.xml",
                requiresSceneLink: true,
                displayName: "Riverhold",
                description: "A fortified river-or-coast refuge with a protected waterside approach.",
                sceneSuffix: "riverhold",
                // Temporary compatibility alias; see Hill Fort above.
                linkedPrefabRootId: DefaultFortPrefabId,
                allowsNativeTestFallback: true)
        };

        private static readonly RefugeFortPrefabDefinition[] Definitions = LoadDefinitions();

        internal static IEnumerable<RefugeFortPrefabDefinition> All => Definitions;

        internal static bool TryGet(string prefabId, out RefugeFortPrefabDefinition definition)
        {
            for (int index = 0; index < Definitions.Length; index++)
            {
                if (string.Equals(Definitions[index].PrefabId, prefabId, StringComparison.Ordinal))
                {
                    definition = Definitions[index];
                    return true;
                }
            }

            definition = default(RefugeFortPrefabDefinition);
            return false;
        }

        internal static RefugeFortPrefabDefinition GetDefault()
        {
            return Definitions[0];
        }

        internal static bool IsAssetReady(string prefabId, out string reason)
        {
            RefugeFortPrefabDefinition definition;
            if (!TryGet(prefabId, out definition))
            {
                reason = "fort prefab is not registered";
                return false;
            }

            try
            {
                string path = GetAssetPath(definition);
                if (!File.Exists(path))
                {
                    reason = "missing prefab asset " + definition.AssetFileName;
                    return false;
                }

                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlElement root = document.SelectSingleNode("/prefabs/game_entity") as XmlElement;
                if (root == null || !string.Equals(root.GetAttribute("name"), definition.LinkedPrefabRootId, StringComparison.Ordinal))
                {
                    reason = "prefab root ID does not match its linked prefab root ID";
                    return false;
                }

                if (root.SelectSingleNode("tags/tag[@name='rct_refuge_layout']") == null)
                {
                    reason = "prefab root is missing rct_refuge_layout tag";
                    return false;
                }

                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = "prefab probe failed: " + exception.GetType().Name;
                return false;
            }
        }

        internal static string GetAssetPath(RefugeFortPrefabDefinition definition)
        {
            return Path.Combine(CalendarRefugeMission.GetModuleDirectoryPath(), "Prefabs", definition.AssetFileName);
        }

        internal static string GetRuntimeLayoutPath(RefugeFortPrefabDefinition definition)
        {
            return Path.Combine(CalendarRefugeMission.GetModuleDirectoryPath(), "Prefabs", definition.RuntimeLayoutFileName);
        }

        internal static string GetLinkedPrefabRootId(string fortStyleId)
        {
            RefugeFortPrefabDefinition definition;
            return TryGet(fortStyleId, out definition) ? definition.LinkedPrefabRootId : fortStyleId;
        }

        internal static bool AllowsNativeTestFallback(string fortStyleId)
        {
            RefugeFortPrefabDefinition definition;
            return TryGet(fortStyleId, out definition) && definition.AllowsNativeTestFallback;
        }

        private static RefugeFortPrefabDefinition[] LoadDefinitions()
        {
            List<RefugeFortPrefabDefinition> definitions =
                new List<RefugeFortPrefabDefinition>(BuiltInDefinitions);
            try
            {
                string moduleDirectory = CalendarRefugeMission.GetModuleDirectoryPath();
                string manifestPath = Path.Combine(moduleDirectory, "ModuleData", "RefugeFortStyles.xml");
                if (File.Exists(manifestPath))
                {
                    XmlDocument manifest = new XmlDocument();
                    manifest.Load(manifestPath);
                    LoadManifestDefinitions(manifest, definitions);
                }

                // A style can be distributed as a self-contained folder:
                // ModuleData/RefugeStyles/<StyleName>/style.xml. This keeps
                // drop-in definitions separate without moving live prefab XML
                // into a nested Prefabs directory whose engine load behavior
                // is not guaranteed across Bannerlord versions.
                string stylesDirectory = Path.Combine(moduleDirectory, "ModuleData", "RefugeStyles");
                if (Directory.Exists(stylesDirectory))
                {
                    foreach (string styleDirectory in Directory.GetDirectories(stylesDirectory))
                    {
                        string stylePath = Path.Combine(styleDirectory, "style.xml");
                        if (!File.Exists(stylePath))
                        {
                            continue;
                        }
                        XmlDocument styleManifest = new XmlDocument();
                        styleManifest.Load(stylePath);
                        LoadManifestDefinitions(styleManifest, definitions);
                    }
                }

                // Drop-in convention: a valid rct_refuge_fort_*.xml asset can
                // be copied into Prefabs without a code change. The optional
                // manifest supplies polished display text for custom styles.
                string prefabDirectory = Path.Combine(moduleDirectory, "Prefabs");
                if (Directory.Exists(prefabDirectory))
                {
                    foreach (string assetPath in Directory.GetFiles(prefabDirectory, "rct_refuge_fort_*.xml"))
                    {
                        if (assetPath.EndsWith("_runtime_layout.xml", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        XmlDocument asset = new XmlDocument();
                        asset.Load(assetPath);
                        XmlElement root = asset.SelectSingleNode("/prefabs/game_entity") as XmlElement;
                        string prefabId = root == null ? string.Empty : root.GetAttribute("name");
                        if (!IsSafeDropInId(prefabId)
                            || root.SelectSingleNode("tags/tag[@name='rct_refuge_layout']") == null)
                        {
                            continue;
                        }

                        string stem = Path.GetFileNameWithoutExtension(assetPath);
                        string styleToken = prefabId.Substring("rct_refuge_fort_".Length);
                        AddIfNew(definitions, new RefugeFortPrefabDefinition(
                            prefabId,
                            Path.GetFileName(assetPath),
                            stem + "_runtime_layout.xml",
                            requiresSceneLink: true,
                            displayName: ToDisplayName(styleToken),
                            description: "Drop-in refuge fort style. Author its matching terrain and navmesh scenes before use.",
                            sceneSuffix: styleToken));
                    }
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge fort-style catalog configuration could not be loaded; built-in styles remain available.", exception);
            }

            return definitions.ToArray();
        }

        private static void LoadManifestDefinitions(XmlDocument manifest, List<RefugeFortPrefabDefinition> definitions)
        {
            XmlNodeList nodes = manifest.SelectNodes("/refuge_fort_styles/fort | /refuge_fort_style/fort");
            if (nodes == null)
            {
                return;
            }
            foreach (XmlNode node in nodes)
            {
                XmlElement element = node as XmlElement;
                RefugeFortPrefabDefinition definition;
                if (element != null && TryReadManifestDefinition(element, out definition))
                {
                    AddOrReplace(definitions, definition);
                }
            }
        }

        private static bool TryReadManifestDefinition(XmlElement element, out RefugeFortPrefabDefinition definition)
        {
            string prefabId = element.GetAttribute("id");
            string asset = element.GetAttribute("prefab_file");
            string suffix = element.GetAttribute("scene_suffix");
            if (!IsSafeDropInId(prefabId)
                || !IsSafeFileName(asset)
                || !IsSafeToken(suffix))
            {
                definition = default(RefugeFortPrefabDefinition);
                return false;
            }

            string displayName = element.GetAttribute("display_name");
            string description = element.GetAttribute("description");
            string runtimeLayout = element.GetAttribute("runtime_layout_file");
            string linkedPrefabRootId = element.GetAttribute("linked_prefab_root");
            if (!string.IsNullOrWhiteSpace(runtimeLayout) && !IsSafeFileName(runtimeLayout))
            {
                definition = default(RefugeFortPrefabDefinition);
                return false;
            }
            if (!string.IsNullOrWhiteSpace(linkedPrefabRootId) && !IsSafeDropInId(linkedPrefabRootId))
            {
                definition = default(RefugeFortPrefabDefinition);
                return false;
            }
            bool allowsNativeTestFallback;
            bool.TryParse(element.GetAttribute("allow_native_test_fallback"), out allowsNativeTestFallback);
            definition = new RefugeFortPrefabDefinition(
                prefabId,
                asset,
                string.IsNullOrWhiteSpace(runtimeLayout) ? Path.GetFileNameWithoutExtension(asset) + "_runtime_layout.xml" : runtimeLayout,
                requiresSceneLink: true,
                displayName: string.IsNullOrWhiteSpace(displayName) ? ToDisplayName(suffix) : displayName,
                description: string.IsNullOrWhiteSpace(description) ? "Custom refuge fort style." : description,
                sceneSuffix: suffix,
                linkedPrefabRootId: linkedPrefabRootId,
                allowsNativeTestFallback: allowsNativeTestFallback);
            return true;
        }

        private static void AddIfNew(List<RefugeFortPrefabDefinition> definitions, RefugeFortPrefabDefinition candidate)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                if (string.Equals(definitions[index].PrefabId, candidate.PrefabId, StringComparison.Ordinal))
                {
                    return;
                }
            }
            definitions.Add(candidate);
        }

        private static void AddOrReplace(List<RefugeFortPrefabDefinition> definitions, RefugeFortPrefabDefinition candidate)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                if (string.Equals(definitions[index].PrefabId, candidate.PrefabId, StringComparison.Ordinal))
                {
                    definitions[index] = candidate;
                    return;
                }
            }
            definitions.Add(candidate);
        }

        private static bool IsSafeDropInId(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId) || !prefabId.StartsWith("rct_refuge_fort_", StringComparison.Ordinal))
            {
                return false;
            }
            for (int index = 0; index < prefabId.Length; index++)
            {
                char value = prefabId[index];
                if (!(char.IsLetterOrDigit(value) || value == '_')) return false;
            }
            return true;
        }

        private static bool IsSafeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(char.IsLetterOrDigit(character) || character == '_')) return false;
            }
            return true;
        }

        private static bool IsSafeFileName(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
                && value.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToDisplayName(string value)
        {
            string[] words = (value ?? string.Empty).Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < words.Length; index++)
            {
                if (words[index].Length > 0)
                {
                    words[index] = char.ToUpperInvariant(words[index][0]) + words[index].Substring(1);
                }
            }
            return words.Length == 0 ? "Custom Fort" : string.Join(" ", words);
        }
    }
}
