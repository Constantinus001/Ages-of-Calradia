using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using TaleWorlds.Library;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Projects the module-owned strategic province index onto campaign space.
    /// Opposing land probes close narrow authored river gaps without dilating
    /// coastlines into open water.
    /// </summary>
    internal sealed class CampaignStrategicLandMask
    {
        private const int NarrowChannelRadiusPixels = 32;
        private const int ElevatedChannelRadiusPixels = 96;
        private const int TopologySeparatorRadiusPixels = 4;
        private const int ProbeStepPixels = 2;
        private const float ElevatedInteriorHeightMargin = 2.5f;
        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _land;
        private readonly byte[] _enclosedWater;
        private readonly double[] _projectionX;
        private readonly double[] _projectionY;

        private CampaignStrategicLandMask(
            int width,
            int height,
            byte[] land,
            byte[] enclosedWater,
            double[] projectionX,
            double[] projectionY)
        {
            _width = width;
            _height = height;
            _land = land;
            _enclosedWater = enclosedWater;
            _projectionX = projectionX;
            _projectionY = projectionY;
        }

        internal static CampaignStrategicLandMask Load()
        {
            double[] projectionX;
            double[] projectionY;
            if (!CalendarWorldLedgerVM.TryGetCampaignToReferenceProjection(out projectionX, out projectionY))
            {
                throw new InvalidOperationException("Campaign-to-strategic projection is unavailable.");
            }

            string path = Path.Combine(
                GetModuleRoot(),
                "GUI",
                "SpriteParts",
                "ui_world_calendar",
                "strategic_province_index.png");
            if (!File.Exists(path)) throw new FileNotFoundException("Strategic province index is missing.", path);

            using (Bitmap source = new Bitmap(path))
            using (Bitmap argb = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics graphics = Graphics.FromImage(argb)) graphics.DrawImageUnscaled(source, 0, 0);
                Rectangle rectangle = new Rectangle(0, 0, argb.Width, argb.Height);
                BitmapData data = argb.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] pixels = new byte[Math.Abs(data.Stride) * data.Height];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    byte[] land = new byte[argb.Width * argb.Height];
                    for (int y = 0; y < argb.Height; y++)
                    {
                        int sourceRow = data.Stride >= 0 ? y * data.Stride : (argb.Height - 1 - y) * -data.Stride;
                        for (int x = 0; x < argb.Width; x++)
                        {
                            int sourcePixel = sourceRow + x * 4;
                            byte red = pixels[sourcePixel + 2];
                            byte alpha = pixels[sourcePixel + 3];
                            if (alpha > 0 && red >= 1 && red <= 133) land[y * argb.Width + x] = 1;
                        }
                    }
                    return new CampaignStrategicLandMask(
                        argb.Width,
                        argb.Height,
                        land,
                        BuildEnclosedWater(argb.Width, argb.Height, land),
                        projectionX,
                        projectionY);
                }
                finally
                {
                    argb.UnlockBits(data);
                }
            }
        }

        internal bool IsPoliticalLand(Vec2 campaignPosition)
        {
            int x;
            int y;
            Project(campaignPosition, out x, out y);
            return IsLandPixel(x, y)
                || IsEnclosedWaterPixel(x, y)
                || IsPoliticalLandPixel(x, y, NarrowChannelRadiusPixels, false);
        }

        internal bool IsAuthoredLand(Vec2 campaignPosition)
        {
            int x;
            int y;
            Project(campaignPosition, out x, out y);
            return IsLandPixel(x, y);
        }

        internal bool IsEnclosedWater(Vec2 campaignPosition)
        {
            int x;
            int y;
            Project(campaignPosition, out x, out y);
            return IsEnclosedWaterPixel(x, y);
        }

        internal bool IsPoliticalLand(Vec2 campaignPosition, float terrainHeight, float openSeaHeightCeiling)
        {
            int x;
            int y;
            Project(campaignPosition, out x, out y);
            if (IsLandPixel(x, y)) return true;
            if (IsEnclosedWaterPixel(x, y)) return true;
            if (terrainHeight <= openSeaHeightCeiling) return false;
            if (IsPoliticalLandPixel(x, y, NarrowChannelRadiusPixels, false)) return true;
            if (terrainHeight >= openSeaHeightCeiling + ElevatedInteriorHeightMargin) return true;
            return IsPoliticalLandPixel(x, y, ElevatedChannelRadiusPixels, true);
        }

        private bool IsPoliticalLandPixel(int x, int y, int radius, bool includeIntermediateAngles)
        {
            if (IsLandPixel(x, y)) return true;
            if (HasOpposingLand(x, y, -1, 0, radius)
                || HasOpposingLand(x, y, 0, -1, radius)
                || HasOpposingLand(x, y, -1, -1, radius)
                || HasOpposingLand(x, y, 1, -1, radius))
            {
                return true;
            }
            return includeIntermediateAngles
                && (HasOpposingLand(x, y, 2, 1, radius)
                    || HasOpposingLand(x, y, 1, 2, radius)
                    || HasOpposingLand(x, y, 2, -1, radius)
                    || HasOpposingLand(x, y, 1, -2, radius));
        }

        private bool HasOpposingLand(int x, int y, int directionX, int directionY, int radius)
        {
            return HasLandAlongRay(x, y, directionX, directionY, radius)
                && HasLandAlongRay(x, y, -directionX, -directionY, radius);
        }

        private bool HasLandAlongRay(int x, int y, int directionX, int directionY, int radius)
        {
            double directionLength = Math.Sqrt(directionX * directionX + directionY * directionY);
            for (int distance = ProbeStepPixels;
                distance <= radius;
                distance += ProbeStepPixels)
            {
                double scale = distance / directionLength;
                int sampleX = x + (int)Math.Round(directionX * scale);
                int sampleY = y + (int)Math.Round(directionY * scale);
                if (IsLandPixel(sampleX, sampleY)) return true;
            }
            return false;
        }

        private void Project(Vec2 campaignPosition, out int x, out int y)
        {
            x = (int)Math.Round(
                campaignPosition.x * _projectionX[0]
                + campaignPosition.y * _projectionX[1]
                + _projectionX[2]
                - CalendarStrategicMapLayout.CropLeft);
            y = (int)Math.Round(
                campaignPosition.x * _projectionY[0]
                + campaignPosition.y * _projectionY[1]
                + _projectionY[2]
                - CalendarStrategicMapLayout.CropTop);
        }

        private bool IsLandPixel(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height && _land[y * _width + x] != 0;
        }

        private bool IsEnclosedWaterPixel(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height && _enclosedWater[y * _width + x] != 0;
        }

        private static byte[] BuildEnclosedWater(int width, int height, byte[] land)
        {
            byte[] exteriorWater = new byte[land.Length];
            Queue<int> pending = new Queue<int>();
            for (int x = 0; x < width; x++)
            {
                AddExteriorWater(x, 0, width, height, land, exteriorWater, pending);
                AddExteriorWater(x, height - 1, width, height, land, exteriorWater, pending);
            }
            for (int y = 1; y < height - 1; y++)
            {
                AddExteriorWater(0, y, width, height, land, exteriorWater, pending);
                AddExteriorWater(width - 1, y, width, height, land, exteriorWater, pending);
            }

            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                int x = index % width;
                int y = index / width;
                AddExteriorWater(x - 1, y, width, height, land, exteriorWater, pending);
                AddExteriorWater(x + 1, y, width, height, land, exteriorWater, pending);
                AddExteriorWater(x, y - 1, width, height, land, exteriorWater, pending);
                AddExteriorWater(x, y + 1, width, height, land, exteriorWater, pending);
            }

            byte[] enclosedWater = new byte[land.Length];
            for (int index = 0; index < land.Length; index++)
            {
                if (land[index] == 0 && exteriorWater[index] == 0) enclosedWater[index] = 1;
            }
            return enclosedWater;
        }

        private static void AddExteriorWater(
            int x,
            int y,
            int width,
            int height,
            byte[] land,
            byte[] exteriorWater,
            Queue<int> pending)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int index = y * width + x;
            if (IsTopologyLandPixel(x, y, width, height, land) || exteriorWater[index] != 0) return;
            exteriorWater[index] = 1;
            pending.Enqueue(index);
        }

        private static bool IsTopologyLandPixel(int x, int y, int width, int height, byte[] land)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            if (land[y * width + x] != 0) return true;
            return HasOpposingTopologyLand(x, y, -1, 0, width, height, land)
                || HasOpposingTopologyLand(x, y, 0, -1, width, height, land)
                || HasOpposingTopologyLand(x, y, -1, -1, width, height, land)
                || HasOpposingTopologyLand(x, y, 1, -1, width, height, land);
        }

        private static bool HasOpposingTopologyLand(
            int x,
            int y,
            int directionX,
            int directionY,
            int width,
            int height,
            byte[] land)
        {
            return HasTopologyLandAlongRay(x, y, directionX, directionY, width, height, land)
                && HasTopologyLandAlongRay(x, y, -directionX, -directionY, width, height, land);
        }

        private static bool HasTopologyLandAlongRay(
            int x,
            int y,
            int directionX,
            int directionY,
            int width,
            int height,
            byte[] land)
        {
            double length = Math.Sqrt(directionX * directionX + directionY * directionY);
            for (int distance = 1; distance <= TopologySeparatorRadiusPixels; distance++)
            {
                int sampleX = x + (int)Math.Round(directionX * distance / length);
                int sampleY = y + (int)Math.Round(directionY * distance / length);
                if (sampleX >= 0 && sampleY >= 0 && sampleX < width && sampleY < height
                    && land[sampleY * width + sampleX] != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetModuleRoot()
        {
            DirectoryInfo directory = new FileInfo(typeof(MySubModule).Assembly.Location).Directory;
            for (int depth = 0; directory != null && depth < 5; depth++, directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SubModule.xml"))) return directory.FullName;
            }
            throw new DirectoryNotFoundException("The Ages of Calradia module root could not be resolved.");
        }
    }
}
