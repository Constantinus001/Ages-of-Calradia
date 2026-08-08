using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Global, mod-owned calibration for native river/coast scenes. This is
    /// intentionally outside campaign save data: a player chooses a dry
    /// anchor for a scene once, and every save reuses it.
    /// </summary>
    internal static class PortableCampAnchorStore
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Vec3> Anchors = new Dictionary<string, Vec3>(StringComparer.Ordinal);
        private static bool _loaded;

        private static string ConfigPath
        {
            get
            {
                return Path.Combine(
                    CalendarRefugeMission.GetModuleDirectoryPath(),
                    "ModuleData",
                    "RefugeAnchors",
                    "portable_camp_anchors.xml");
            }
        }

        internal static bool TryGet(string sceneId, out Vec3 anchor)
        {
            lock (SyncRoot)
            {
                EnsureLoaded();
                Vec3 value;
                if (!string.IsNullOrWhiteSpace(sceneId) && Anchors.TryGetValue(sceneId, out value))
                {
                    anchor = value;
                    return true;
                }
                anchor = Vec3.Invalid;
                return false;
            }
        }

        internal static void Save(string sceneId, Vec3 position)
        {
            if (string.IsNullOrWhiteSpace(sceneId) || !position.IsValid)
            {
                return;
            }
            lock (SyncRoot)
            {
                EnsureLoaded();
                Anchors[sceneId] = position;
                SaveCore();
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(ConfigPath)) return;
                XmlDocument document = new XmlDocument();
                document.Load(ConfigPath);
                XmlNodeList nodes = document.SelectNodes("/portable_camp_anchors/anchor");
                if (nodes == null) return;
                foreach (XmlNode node in nodes)
                {
                    XmlAttribute scene = node.Attributes == null ? null : node.Attributes["scene"];
                    XmlAttribute x = node.Attributes == null ? null : node.Attributes["x"];
                    XmlAttribute y = node.Attributes == null ? null : node.Attributes["y"];
                    XmlAttribute z = node.Attributes == null ? null : node.Attributes["z"];
                    float parsedX;
                    float parsedY;
                    float parsedZ = 0f;
                    if (scene != null && x != null && y != null
                        && float.TryParse(x.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedX)
                        && float.TryParse(y.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedY)
                        && (z == null || float.TryParse(z.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedZ)))
                    {
                        Anchors[scene.Value] = new Vec3(parsedX, parsedY, parsedZ);
                    }
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Portable camp anchor settings could not be loaded.", exception);
            }
        }

        private static void SaveCore()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigPath);
                Directory.CreateDirectory(directory);
                XmlWriterSettings settings = new XmlWriterSettings { Indent = true };
                string temporaryPath = ConfigPath + ".tmp";
                using (XmlWriter writer = XmlWriter.Create(temporaryPath, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("portable_camp_anchors");
                    foreach (KeyValuePair<string, Vec3> anchor in Anchors)
                    {
                        writer.WriteStartElement("anchor");
                        writer.WriteAttributeString("scene", anchor.Key);
                        writer.WriteAttributeString("x", anchor.Value.x.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("y", anchor.Value.y.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("z", anchor.Value.z.ToString("R", CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
                if (File.Exists(ConfigPath)) File.Replace(temporaryPath, ConfigPath, null);
                else File.Move(temporaryPath, ConfigPath);
                Diagnostics.Info("Portable camp anchor saved globally. Path=" + ConfigPath + ".");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Portable camp anchor settings could not be saved.", exception);
            }
        }
    }
}
