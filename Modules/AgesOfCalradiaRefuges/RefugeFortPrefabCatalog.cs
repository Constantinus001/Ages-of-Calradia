using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Describes the complete, authored refuge compound used by every profile.
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
    /// Single source of truth for the one release refuge compound: Palisade
    /// Ring. Existing saves naming a retired style fall back to this entry.
    /// </summary>
    internal static class RefugeFortPrefabCatalog
    {
        internal const string DefaultFortPrefabId = "rct_refuge_fort_layout";

        private static readonly RefugeFortPrefabDefinition[] Definitions =
        {
            // This large, flattened prefab must be embedded in an authored
            // SceneObj so the editor owns terrain, collision, and navmesh.
            new RefugeFortPrefabDefinition(
                DefaultFortPrefabId,
                "rct_refuge_fort_layout.xml",
                "rct_refuge_fort_runtime_layout.xml",
                requiresSceneLink: true,
                displayName: "Palisade Ring",
                description: "A round timber palisade with inward-facing wall platforms.",
                sceneSuffix: "palisade",
                allowsNativeTestFallback: true)
        };

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
    }
}
