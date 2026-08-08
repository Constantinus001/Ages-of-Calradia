using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Creates the module's own settlement reference from Bannerlord's live
    /// campaign data. It deliberately does not import an external map table.
    /// </summary>
    internal static class CalendarStrategicSettlementReference
    {
        private static readonly object Sync = new object();
        private static string _lastSnapshot;

        internal static void CaptureNativeSnapshot()
        {
            lock (Sync)
            {
                try
                {
                    List<string> rows = new List<string>();
                    foreach (Settlement settlement in Settlement.All)
                    {
                        if (settlement == null || string.IsNullOrEmpty(settlement.StringId)) continue;

                        Vec2 native = settlement.GetPosition2D;
                        Vec2 reference = CalendarWorldLedgerVM.ProjectSettlementToReferenceMap(settlement);
                        rows.Add(string.Join(",", new string[]
                        {
                            Csv(settlement.StringId),
                            Csv(GetSettlementKind(settlement)),
                            Csv(settlement.Name == null ? string.Empty : settlement.Name.ToString()),
                            native.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                            native.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                            reference.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                            reference.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                        }));
                    }

                    rows.Sort(StringComparer.Ordinal);
                    StringBuilder snapshot = new StringBuilder();
                    snapshot.AppendLine("SettlementId,Type,Name,NativeX,NativeY,ReferenceX,ReferenceY");
                    foreach (string row in rows) snapshot.AppendLine(row);

                    string content = snapshot.ToString();
                    if (string.Equals(content, _lastSnapshot, StringComparison.Ordinal)) return;

                    string assemblyDirectory = Path.GetDirectoryName(typeof(CalendarStrategicSettlementReference).Assembly.Location);
                    DirectoryInfo binaryDirectory = string.IsNullOrEmpty(assemblyDirectory) ? null : Directory.GetParent(assemblyDirectory);
                    DirectoryInfo moduleDirectory = binaryDirectory == null ? null : binaryDirectory.Parent;
                    if (moduleDirectory == null) throw new InvalidOperationException("The calendar module directory could not be resolved.");

                    string directory = Path.Combine(moduleDirectory.FullName, "ModuleData");
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, "strategic_settlements_native.csv");
                    if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
                    {
                        _lastSnapshot = content;
                        return;
                    }

                    File.WriteAllText(path, content, Encoding.UTF8);
                    _lastSnapshot = content;
                    Diagnostics.Info("Wrote independent native strategic settlement reference: " + rows.Count + " settlements.");
                }
                catch (Exception exception)
                {
                    Diagnostics.Error("Could not write the independent native strategic settlement reference.", exception);
                }
            }
        }

        private static string GetSettlementKind(Settlement settlement)
        {
            if (settlement == null) return "Unknown";
            if (settlement.Village != null) return "Village";
            if (settlement.Town != null) return settlement.IsTown ? "Town" : "Castle";
            return "Other";
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
