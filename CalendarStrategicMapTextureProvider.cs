using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;
using EngineTexture = TaleWorlds.Engine.Texture;
using EngineTextureWrapper = TaleWorlds.Engine.GauntletUI.EngineTexture;
using TwoDimensionTexture = TaleWorlds.TwoDimension.Texture;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Renders the Strategic Map as one composed texture instead of relying on
    /// hundreds of independently bound Gauntlet sprite tints. The static map
    /// artwork supplies water and opaque black province borders; the original
    /// drawn-province index identifies only the transparent interior pixels
    /// that need the current owner's faction colour.
    /// </summary>
    public sealed class CalendarStrategicMapTextureProvider : TextureProvider
    {
        private const int TerritoryCount = 133;
        private const byte SyntheticTerritoryBorder = 255;
        private const uint UnmappedTerritoryColor = 0xFF4A525A;
        private static readonly object OwnerColorSync = new object();
        private static readonly object AssetSync = new object();
        private static readonly Dictionary<string, uint> OwnerColorsBySettlementId =
            new Dictionary<string, uint>(StringComparer.Ordinal);
        private static readonly List<StrategicMarkerSnapshot> SettlementMarkers =
            new List<StrategicMarkerSnapshot>();
        private static byte[] _baseBgra;
        private static byte[] _territoryIndices;
        private static PointF[] _territoryCenters;
        private static string[] _settlementIds;
        private static int _mapWidth;
        private static int _mapHeight;
        private static int _ownerColorRevision;
        private static bool _assetsLoaded;
        private static bool _assetLoadAttempted;
        private static bool _assetFailureLogged;

        private int _renderedRevision = -1;
        private EngineTexture _engineTexture;
        private TwoDimensionTexture _renderTexture;

        internal static void UpdateMapState(
            IDictionary<string, uint> ownerColorsBySettlementId,
            IEnumerable<StrategicSettlementPoint> markerPoints)
        {
            if (ownerColorsBySettlementId == null)
            {
                return;
            }

            List<StrategicMarkerSnapshot> incomingMarkers = CreateMarkerSnapshot(markerPoints);
            lock (OwnerColorSync)
            {
                bool changed = !HasSameOwnerColors(ownerColorsBySettlementId)
                    || !HaveSameMarkers(SettlementMarkers, incomingMarkers);
                if (!changed) return;

                OwnerColorsBySettlementId.Clear();
                foreach (KeyValuePair<string, uint> entry in ownerColorsBySettlementId)
                {
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    OwnerColorsBySettlementId[entry.Key] = NormalizeColor(entry.Value);
                }
                SettlementMarkers.Clear();
                SettlementMarkers.AddRange(incomingMarkers);
                _ownerColorRevision++;
            }
        }

        protected override TwoDimensionTexture OnGetTextureForRender(TwoDimensionContext context, string name)
        {
            if (!TryEnsureAssets())
            {
                return _renderTexture;
            }

            int revision;
            uint[] ownerColorSnapshot;
            List<StrategicMarkerSnapshot> markerSnapshot;
            lock (OwnerColorSync)
            {
                revision = _ownerColorRevision;
                ownerColorSnapshot = new uint[TerritoryCount];
                for (int index = 0; index < TerritoryCount; index++)
                {
                    uint color;
                    if (OwnerColorsBySettlementId.TryGetValue(_settlementIds[index], out color))
                    {
                        ownerColorSnapshot[index] = color;
                    }
                }
                markerSnapshot = new List<StrategicMarkerSnapshot>(SettlementMarkers);
            }

            if (_renderTexture != null && _renderTexture.IsValid && revision == _renderedRevision)
            {
                return _renderTexture;
            }

            try
            {
                byte[] encodedMap = BuildMapPng(ownerColorSnapshot, markerSnapshot);
                EngineTexture replacement = EngineTexture.CreateFromMemory(encodedMap);
                if (replacement == null || replacement.IsReleased)
                {
                    return _renderTexture;
                }

                EngineTexture previous = _engineTexture;
                _engineTexture = replacement;
                _renderTexture = new TwoDimensionTexture(new EngineTextureWrapper(replacement));
                _renderedRevision = revision;
                ReleaseTextureAfterFrameBudget(previous, 3);
                return _renderTexture;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Strategic map texture composition failed; the previous map texture remains active.", exception);
                return _renderTexture;
            }
        }

        public override void Clear(bool clearNextFrame)
        {
            base.Clear(clearNextFrame);
            _renderTexture = null;
            _renderedRevision = -1;
            ReleaseTextureAfterFrameBudget(_engineTexture, clearNextFrame ? 1 : 0);
            _engineTexture = null;
        }

        private static uint NormalizeColor(uint color)
        {
            return color == 0 || (color & 0xFF000000u) == 0
                ? color == 0 ? 0u : color | 0xFF000000u
                : color;
        }

        private static bool HasSameOwnerColors(IDictionary<string, uint> incoming)
        {
            if (OwnerColorsBySettlementId.Count != incoming.Count) return false;
            foreach (KeyValuePair<string, uint> entry in incoming)
            {
                uint previous;
                if (!OwnerColorsBySettlementId.TryGetValue(entry.Key, out previous)
                    || previous != NormalizeColor(entry.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<StrategicMarkerSnapshot> CreateMarkerSnapshot(IEnumerable<StrategicSettlementPoint> points)
        {
            List<StrategicMarkerSnapshot> markers = new List<StrategicMarkerSnapshot>();
            if (points == null) return markers;

            foreach (StrategicSettlementPoint point in points)
            {
                if (point == null || point.Settlement == null || string.IsNullOrEmpty(point.Settlement.StringId)) continue;
                markers.Add(new StrategicMarkerSnapshot(
                    point.Settlement.StringId,
                    point.DisplayX,
                    point.DisplayY,
                    point.SourceX,
                    point.SourceY,
                    point.Settlement.IsTown,
                    point.IsUnderSiege));
            }

            markers.Sort(delegate(StrategicMarkerSnapshot left, StrategicMarkerSnapshot right)
            {
                return string.Compare(left.SettlementId, right.SettlementId, StringComparison.Ordinal);
            });
            return markers;
        }

        private static bool HaveSameMarkers(IList<StrategicMarkerSnapshot> left, IList<StrategicMarkerSnapshot> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index])) return false;
            }

            return true;
        }

        private static bool TryEnsureAssets()
        {
            lock (AssetSync)
            {
                if (_assetsLoaded) return true;
                if (_assetLoadAttempted) return false;
                _assetLoadAttempted = true;

                try
                {
                    string moduleRoot = GetModuleRoot();
                    string mapDirectory = Path.Combine(moduleRoot, "GUI", "SpriteParts", "ui_world_calendar");
                    string baseMapPath = Path.Combine(mapDirectory, "strategic_map.png");
                    // Use the source artwork's original 133-province
                    // registration. The later settlement-split index divided
                    // established provinces between neighbouring fiefs and
                    // is the cause of the incorrect ownership areas.
                    string territoryIndexPath = Path.Combine(mapDirectory, "strategic_province_index.png");

                    int baseWidth;
                    int baseHeight;
                    _baseBgra = ReadBgra(baseMapPath, out baseWidth, out baseHeight);
                    int indexWidth;
                    int indexHeight;
                    byte[] indexBgra = ReadBgra(territoryIndexPath, out indexWidth, out indexHeight);
                    if (baseWidth != indexWidth || baseHeight != indexHeight)
                    {
                        throw new InvalidOperationException(
                            "Strategic-map base and original province-index texture dimensions differ: "
                            + baseWidth + "x" + baseHeight + " versus " + indexWidth + "x" + indexHeight + ".");
                    }

                    _mapWidth = baseWidth;
                    _mapHeight = baseHeight;
                    _settlementIds = BuildSettlementIdsFromProvinceLayout();
                    _territoryIndices = new byte[checked(_mapWidth * _mapHeight)];
                    for (int index = 0, offset = 0; index < _territoryIndices.Length; index++, offset += 4)
                    {
                        byte territory = indexBgra[offset + 2];
                        _territoryIndices[index] = territory;

                        if (_baseBgra[offset + 3] == 0)
                        {
                            if (territory == 0 || territory > TerritoryCount)
                            {
                                throw new InvalidOperationException("Strategic-map original province index contains an invalid land id.");
                            }
                        }
                        else if (indexBgra[offset + 3] != 0)
                        {
                            throw new InvalidOperationException("Strategic-map settlement-territory index overwrites an opaque base-map border or water pixel.");
                        }
                    }

                    _territoryCenters = CalculateTerritoryCenters(_territoryIndices, _mapWidth, _mapHeight);

                    _assetsLoaded = true;
                    Diagnostics.Info(
                        "Strategic map composer loaded a " + _mapWidth + "x" + _mapHeight
                        + " original-province index. Owner colours are now rendered as a single texture.");
                    return true;
                }
                catch (Exception exception)
                {
                    if (!_assetFailureLogged)
                    {
                        _assetFailureLogged = true;
                        Diagnostics.Error("Strategic map composer could not load its local map assets.", exception);
                    }
                    return false;
                }
            }
        }

        private static string[] BuildSettlementIdsFromProvinceLayout()
        {
            string[] settlementIds = new string[TerritoryCount];
            for (int ordinal = 1; ordinal <= TerritoryCount; ordinal++)
            {
                string spriteName = "strategic_province_" + ordinal.ToString("D3");
                string settlementId;
                if (!CalendarStrategicMapLayout.TryGetSettlementId(spriteName, out settlementId)
                    || string.IsNullOrEmpty(settlementId))
                {
                    throw new InvalidOperationException(
                        "Strategic-map original province layout has an invalid settlement binding: " + spriteName + ".");
                }

                // The reference artwork has several shared visual regions.
                // Those retain their established neighbouring-fief binding;
                // every real settlement remains visible and selectable through
                // the live marker layer above the province colour.
                settlementIds[ordinal - 1] = settlementId;
            }

            return settlementIds;
        }

        private static string GetModuleRoot()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CalendarStrategicMapTextureProvider).Assembly.Location);
            if (string.IsNullOrEmpty(assemblyDirectory))
            {
                throw new InvalidOperationException("The Strategic Map assembly location is unavailable.");
            }

            DirectoryInfo binaryDirectory = Directory.GetParent(assemblyDirectory);
            DirectoryInfo moduleDirectory = binaryDirectory == null ? null : binaryDirectory.Parent;
            if (moduleDirectory == null)
            {
                throw new InvalidOperationException("The Strategic Map module directory could not be resolved.");
            }

            return moduleDirectory.FullName;
        }

        private static PointF[] CalculateTerritoryCenters(byte[] territoryIndices, int width, int height)
        {
            long[] totalX = new long[TerritoryCount];
            long[] totalY = new long[TerritoryCount];
            int[] pixelCounts = new int[TerritoryCount];
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    byte territory = territoryIndices[rowOffset + x];
                    if (territory < 1 || territory > TerritoryCount) continue;
                    int index = territory - 1;
                    totalX[index] += x;
                    totalY[index] += y;
                    pixelCounts[index]++;
                }
            }

            PointF[] centers = new PointF[TerritoryCount];
            for (int index = 0; index < TerritoryCount; index++)
            {
                if (pixelCounts[index] == 0)
                {
                    throw new InvalidOperationException("Strategic-map settlement-territory index contains an empty province.");
                }
                centers[index] = new PointF(
                    (float)totalX[index] / pixelCounts[index],
                    (float)totalY[index] / pixelCounts[index]);
            }

            return centers;
        }

        private static byte[] BuildMapPng(uint[] ownerColors, IList<StrategicMarkerSnapshot> markers)
        {
            byte[] composedBgra = new byte[_baseBgra.Length];
            Buffer.BlockCopy(_baseBgra, 0, composedBgra, 0, _baseBgra.Length);
            uint[] resolvedProvinceColors = ResolveProvinceOwnerColors(ownerColors, markers);

            for (int pixel = 0, offset = 0; pixel < _territoryIndices.Length; pixel++, offset += 4)
            {
                byte territory = _territoryIndices[pixel];
                if (territory == 0) continue;

                // The generated index uses 255 for narrow dividers inside a
                // shared source province. Keep those dividers visible rather
                // than treating them as a settlement index.
                if (territory == SyntheticTerritoryBorder)
                {
                    composedBgra[offset] = 9;
                    composedBgra[offset + 1] = 12;
                    composedBgra[offset + 2] = 16;
                    composedBgra[offset + 3] = 255;
                    continue;
                }

                uint ownerColor = resolvedProvinceColors[territory - 1];
                if (ownerColor == 0) ownerColor = UnmappedTerritoryColor;
                composedBgra[offset] = (byte)(ownerColor & 0xFF);
                composedBgra[offset + 1] = (byte)((ownerColor >> 8) & 0xFF);
                composedBgra[offset + 2] = (byte)((ownerColor >> 16) & 0xFF);
                composedBgra[offset + 3] = (byte)((ownerColor >> 24) & 0xFF);
            }

            using (Bitmap composedMap = new Bitmap(_mapWidth, _mapHeight, PixelFormat.Format32bppArgb))
            {
                CopyBgraToBitmap(composedBgra, composedMap);
                DrawSettlementMarkers(composedMap, markers);
                using (MemoryStream stream = new MemoryStream())
                {
                    composedMap.Save(stream, ImageFormat.Png);
                    return stream.ToArray();
                }
            }
        }

        // The original index manifest is useful for validating coverage, but
        // cannot be the authority for live politics: art packs can reorder
        // regions while retaining the same IDs. Match each drawn province to
        // its nearest real town/castle anchor instead. This keeps the black
        // province borders from the map art and makes the fill follow the
        // settlement that actually occupies that province.
        private static uint[] ResolveProvinceOwnerColors(
            uint[] ownerColors,
            IList<StrategicMarkerSnapshot> markers)
        {
            uint[] resolved = new uint[TerritoryCount];
            if (markers == null || markers.Count == 0 || _territoryCenters == null)
            {
                if (ownerColors != null) Array.Copy(ownerColors, resolved, Math.Min(ownerColors.Length, resolved.Length));
                return resolved;
            }

            Dictionary<string, uint> colorsBySettlementId = new Dictionary<string, uint>(StringComparer.Ordinal);
            for (int index = 0; index < _settlementIds.Length && index < ownerColors.Length; index++)
            {
                colorsBySettlementId[_settlementIds[index]] = ownerColors[index];
            }

            for (int province = 0; province < TerritoryCount; province++)
            {
                StrategicMarkerSnapshot closestMarker = null;
                float closestDistance = float.MaxValue;
                PointF center = _territoryCenters[province];
                for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
                {
                    StrategicMarkerSnapshot marker = markers[markerIndex];
                    float deltaX = center.X - marker.AnchorX;
                    float deltaY = center.Y - marker.AnchorY;
                    float distance = (deltaX * deltaX) + (deltaY * deltaY);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestMarker = marker;
                    }
                }

                uint color;
                resolved[province] = closestMarker != null
                    && colorsBySettlementId.TryGetValue(closestMarker.SettlementId, out color)
                    ? color
                    : 0;
            }

            return resolved;
        }

        // These map symbols are drawn into the same runtime texture as the
        // province fills.  They deliberately use a neutral gray fill and a
        // dark outline: faction ownership remains the only map colour signal,
        // while towns and castles retain the richer, distinct silhouettes the
        // first Strategic Map iteration used.  Drawing them here also avoids
        // the fragile UI-atlas path that produced clipped marker fragments.
        // Buttons still sit above them in Gauntlet, so map interaction is kept.
        private static void DrawSettlementMarkers(Bitmap map, IList<StrategicMarkerSnapshot> markers)
        {
            if (map == null || markers == null || markers.Count == 0) return;

            using (Graphics graphics = Graphics.FromImage(map))
            using (SolidBrush markerFill = new SolidBrush(Color.FromArgb(255, 170, 176, 182)))
            using (SolidBrush markerHighlight = new SolidBrush(Color.FromArgb(255, 214, 218, 221)))
            using (SolidBrush markerDetail = new SolidBrush(Color.FromArgb(255, 24, 27, 31)))
            using (Pen markerOutline = new Pen(Color.FromArgb(255, 16, 19, 22), 2.5f))
            using (SolidBrush siegeFill = new SolidBrush(Color.FromArgb(255, 185, 54, 43)))
            using (Pen siegeOutline = new Pen(Color.FromArgb(255, 58, 15, 12), 1.8f))
            using (Pen siegeGlyph = new Pen(Color.FromArgb(255, 247, 221, 170), 1.8f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                foreach (StrategicMarkerSnapshot marker in markers)
                {
                    int centerX = (int)Math.Round(marker.X);
                    int centerY = (int)Math.Round(marker.Y);
                    if (marker.IsTown)
                    {
                        DrawTownMarker(
                            graphics,
                            centerX,
                            centerY,
                            markerFill,
                            markerHighlight,
                            markerDetail,
                            markerOutline);
                    }
                    else
                    {
                        DrawCastleMarker(
                            graphics,
                            centerX,
                            centerY,
                            markerFill,
                            markerHighlight,
                            markerDetail,
                            markerOutline);
                    }

                    if (marker.IsUnderSiege)
                    {
                        DrawSiegeBadge(graphics, centerX, centerY, siegeFill, siegeOutline, siegeGlyph);
                    }
                }
            }
        }

        // A compact red crossed-swords badge is baked into the same composed
        // texture as the marker. This is reliable at every zoom level and does
        // not rely on a separate Gauntlet sprite or leave a dark backplate.
        private static void DrawSiegeBadge(
            Graphics graphics,
            int centerX,
            int centerY,
            System.Drawing.Brush fill,
            Pen outline,
            Pen glyph)
        {
            Point[] badge =
            {
                new Point(centerX + 12, centerY - 20),
                new Point(centerX + 20, centerY - 12),
                new Point(centerX + 12, centerY - 4),
                new Point(centerX + 4, centerY - 12)
            };
            graphics.FillPolygon(fill, badge);
            graphics.DrawPolygon(outline, badge);
            graphics.DrawLine(glyph, centerX + 8, centerY - 16, centerX + 16, centerY - 8);
            graphics.DrawLine(glyph, centerX + 16, centerY - 16, centerX + 8, centerY - 8);
        }

        // A tall hall with a second lower hall gives towns the same readable
        // profile as the original town marker, but with a filled interior that
        // remains legible against every faction colour.
        private static void DrawTownMarker(
            Graphics graphics,
            int centerX,
            int centerY,
            System.Drawing.Brush fill,
            System.Drawing.Brush highlight,
            System.Drawing.Brush detail,
            Pen outline)
        {
            Rectangle tallBody = new Rectangle(centerX - 15, centerY - 2, 15, 18);
            Point[] tallRoof =
            {
                new Point(centerX - 17, centerY - 2),
                new Point(centerX - 8, centerY - 19),
                new Point(centerX + 2, centerY - 2)
            };
            graphics.FillRectangle(fill, tallBody);
            graphics.FillPolygon(fill, tallRoof);
            graphics.DrawRectangle(outline, tallBody);
            graphics.DrawPolygon(outline, tallRoof);
            graphics.FillRectangle(highlight, centerX - 13, centerY, 3, 13);
            graphics.FillRectangle(detail, centerX - 10, centerY + 8, 4, 8);

            Rectangle smallBody = new Rectangle(centerX + 1, centerY + 3, 15, 13);
            Point[] smallRoof =
            {
                new Point(centerX - 1, centerY + 3),
                new Point(centerX + 8, centerY - 11),
                new Point(centerX + 18, centerY + 3)
            };
            graphics.FillRectangle(fill, smallBody);
            graphics.FillPolygon(fill, smallRoof);
            graphics.DrawRectangle(outline, smallBody);
            graphics.DrawPolygon(outline, smallRoof);
            graphics.FillRectangle(highlight, centerX + 3, centerY + 5, 3, 9);
            graphics.FillRectangle(detail, centerX + 9, centerY + 9, 4, 7);
        }

        // A filled crenellated wall with a dark arched gate keeps castles
        // visually distinct from towns without depending on an external atlas.
        private static void DrawCastleMarker(
            Graphics graphics,
            int centerX,
            int centerY,
            System.Drawing.Brush fill,
            System.Drawing.Brush highlight,
            System.Drawing.Brush detail,
            Pen outline)
        {
            Point[] wall =
            {
                new Point(centerX - 16, centerY - 14),
                new Point(centerX - 10, centerY - 14),
                new Point(centerX - 10, centerY - 8),
                new Point(centerX - 4, centerY - 8),
                new Point(centerX - 4, centerY - 14),
                new Point(centerX + 2, centerY - 14),
                new Point(centerX + 2, centerY - 8),
                new Point(centerX + 8, centerY - 8),
                new Point(centerX + 8, centerY - 14),
                new Point(centerX + 15, centerY - 14),
                new Point(centerX + 15, centerY + 16),
                new Point(centerX - 16, centerY + 16)
            };
            graphics.FillPolygon(fill, wall);
            graphics.DrawPolygon(outline, wall);
            graphics.FillRectangle(highlight, centerX - 13, centerY - 5, 4, 17);
            graphics.FillEllipse(detail, centerX - 5, centerY + 3, 10, 10);
            graphics.FillRectangle(detail, centerX - 5, centerY + 8, 10, 8);
        }

        // The legend uses the exact same renderer as the map marker.  It is
        // supplied as a memory texture rather than a sprite-atlas entry so it
        // cannot degrade into clipped white fragments after a UI reload.
        internal static byte[] BuildLegendMarkerPng(bool isTown)
        {
            using (Bitmap marker = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(marker))
            using (SolidBrush markerFill = new SolidBrush(Color.FromArgb(255, 170, 176, 182)))
            using (SolidBrush markerHighlight = new SolidBrush(Color.FromArgb(255, 214, 218, 221)))
            using (SolidBrush markerDetail = new SolidBrush(Color.FromArgb(255, 24, 27, 31)))
            using (Pen markerOutline = new Pen(Color.FromArgb(255, 16, 19, 22), 2.5f))
            using (MemoryStream stream = new MemoryStream())
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                if (isTown)
                {
                    DrawTownMarker(graphics, 30, 35, markerFill, markerHighlight, markerDetail, markerOutline);
                }
                else
                {
                    DrawCastleMarker(graphics, 31, 32, markerFill, markerHighlight, markerDetail, markerOutline);
                }

                marker.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private sealed class StrategicMarkerSnapshot : IEquatable<StrategicMarkerSnapshot>
        {
            internal StrategicMarkerSnapshot(
                string settlementId,
                float x,
                float y,
                float anchorX,
                float anchorY,
                bool isTown,
                bool isUnderSiege)
            {
                SettlementId = settlementId ?? string.Empty;
                X = x;
                Y = y;
                AnchorX = anchorX;
                AnchorY = anchorY;
                IsTown = isTown;
                IsUnderSiege = isUnderSiege;
            }

            internal string SettlementId { get; private set; }
            internal float X { get; private set; }
            internal float Y { get; private set; }
            internal float AnchorX { get; private set; }
            internal float AnchorY { get; private set; }
            internal bool IsTown { get; private set; }
            internal bool IsUnderSiege { get; private set; }

            public bool Equals(StrategicMarkerSnapshot other)
            {
                return other != null
                    && string.Equals(SettlementId, other.SettlementId, StringComparison.Ordinal)
                    && Math.Abs(X - other.X) < 0.001f
                    && Math.Abs(Y - other.Y) < 0.001f
                    && Math.Abs(AnchorX - other.AnchorX) < 0.001f
                    && Math.Abs(AnchorY - other.AnchorY) < 0.001f
                    && IsTown == other.IsTown
                    && IsUnderSiege == other.IsUnderSiege;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as StrategicMarkerSnapshot);
            }

            public override int GetHashCode()
            {
                return SettlementId.GetHashCode();
            }
        }

        private static byte[] ReadBgra(string imagePath, out int width, out int height)
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Strategic map texture is missing.", imagePath);
            }

            using (Bitmap source = new Bitmap(imagePath))
            using (Bitmap normalized = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(normalized))
                {
                    graphics.DrawImageUnscaled(source, 0, 0);
                }

                width = normalized.Width;
                height = normalized.Height;
                Rectangle rectangle = new Rectangle(0, 0, width, height);
                BitmapData bitmapData = normalized.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int rowBytes = checked(width * 4);
                    byte[] pixels = new byte[checked(rowBytes * height)];
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride), pixels, y * rowBytes, rowBytes);
                    }
                    return pixels;
                }
                finally
                {
                    normalized.UnlockBits(bitmapData);
                }
            }
        }

        private static void CopyBgraToBitmap(byte[] pixels, Bitmap bitmap)
        {
            Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int rowBytes = checked(bitmap.Width * 4);
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(pixels, y * rowBytes, IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride), rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private static void ReleaseTextureAfterFrameBudget(EngineTexture texture, int frameBudget)
        {
            if (texture == null || texture.IsReleased) return;
            try
            {
                if (frameBudget <= 0)
                {
                    texture.Release();
                }
                else
                {
                    texture.ReleaseAfterNumberOfFrames(frameBudget);
                }
            }
            catch
            {
                // Releasing an already-disposed native texture must never take
                // down the map UI. Bannerlord will clean up the remaining handle.
            }
        }
    }

    // Small direct-memory textures for the legend. Unlike ImageWidget sprites,
    // these do not rely on the module's sprite atlas being loaded correctly.
    public abstract class CalendarStrategicLegendMarkerTextureProvider : TextureProvider
    {
        private EngineTexture _engineTexture;
        private TwoDimensionTexture _renderTexture;

        protected abstract bool IsTown { get; }

        protected override TwoDimensionTexture OnGetTextureForRender(TwoDimensionContext context, string name)
        {
            if (_renderTexture != null && _renderTexture.IsValid)
            {
                return _renderTexture;
            }

            try
            {
                EngineTexture texture = EngineTexture.CreateFromMemory(
                    CalendarStrategicMapTextureProvider.BuildLegendMarkerPng(IsTown));
                if (texture == null || texture.IsReleased)
                {
                    return null;
                }

                _engineTexture = texture;
                _renderTexture = new TwoDimensionTexture(new EngineTextureWrapper(texture));
                return _renderTexture;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Strategic Map legend marker could not be rendered.", exception);
                return _renderTexture;
            }
        }

        public override void Clear(bool clearNextFrame)
        {
            base.Clear(clearNextFrame);
            _renderTexture = null;
            if (_engineTexture == null || _engineTexture.IsReleased)
            {
                _engineTexture = null;
                return;
            }

            try
            {
                if (clearNextFrame)
                {
                    _engineTexture.ReleaseAfterNumberOfFrames(1);
                }
                else
                {
                    _engineTexture.Release();
                }
            }
            catch
            {
                // Native cleanup is best-effort while the containing screen
                // is being disposed.
            }
            finally
            {
                _engineTexture = null;
            }
        }
    }

    public sealed class CalendarStrategicTownLegendTextureProvider : CalendarStrategicLegendMarkerTextureProvider
    {
        protected override bool IsTown { get { return true; } }
    }

    public sealed class CalendarStrategicCastleLegendTextureProvider : CalendarStrategicLegendMarkerTextureProvider
    {
        protected override bool IsTown { get { return false; } }
    }
}
