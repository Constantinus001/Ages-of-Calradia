using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;
using EngineTexture = TaleWorlds.Engine.Texture;
using EngineTextureWrapper = TaleWorlds.Engine.GauntletUI.EngineTexture;
using TwoDimensionTexture = TaleWorlds.TwoDimension.Texture;

namespace AgesOfCalradiaReligions
{
    /// <summary>
    /// Loads the religion module's baked Census cabinet without placing the
    /// asset in, or changing, the protected Ages of Calradia core module.
    /// </summary>
    public sealed class AocCensusPageTextureProvider : TextureProvider
    {
        private EngineTexture _engineTexture;
        private TwoDimensionTexture _renderTexture;

        protected override TwoDimensionTexture OnGetTextureForRender(TwoDimensionContext context, string name)
        {
            if (_renderTexture != null && _renderTexture.IsValid) return _renderTexture;

            try
            {
                string assemblyDirectory = Path.GetDirectoryName(typeof(AocCensusPageTextureProvider).Assembly.Location);
                DirectoryInfo binaryDirectory = string.IsNullOrEmpty(assemblyDirectory) ? null : Directory.GetParent(assemblyDirectory);
                DirectoryInfo moduleDirectory = binaryDirectory == null ? null : binaryDirectory.Parent;
                if (moduleDirectory == null) throw new InvalidOperationException("Religion module directory could not be resolved.");

                string path = Path.Combine(moduleDirectory.FullName, "GUI", "CustomUI", "WorldEventsSkin", "page_cabinet_census_v1.png");
                if (!File.Exists(path))
                {
                    ReligionDiagnostics.Info("The baked Census cabinet is unavailable: " + path + ".");
                    return null;
                }

                int width;
                int height;
                byte[] pixels = DecodeRgba(path, out width, out height);
                _engineTexture = EngineTexture.CreateFromByteArray(pixels, width, height);
                if (_engineTexture == null || _engineTexture.IsReleased) return null;
                _engineTexture.Name = "aoc_religions_census_page_v1";
                _renderTexture = new TwoDimensionTexture(new EngineTextureWrapper(_engineTexture));
                ReligionDiagnostics.Info("Baked Census cabinet loaded: " + width + "x" + height + ".");
                return _renderTexture;
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("The baked Census cabinet could not be loaded.", exception);
                return null;
            }
        }

        public override void Clear(bool clearNextFrame)
        {
            base.Clear(clearNextFrame);
            _renderTexture = null;
            if (_engineTexture != null && !_engineTexture.IsReleased) _engineTexture.Release();
            _engineTexture = null;
        }

        private static byte[] DecodeRgba(string path, out int width, out int height)
        {
            using (Bitmap bitmap = new Bitmap(path))
            {
                width = bitmap.Width;
                height = bitmap.Height;
                Rectangle bounds = new Rectangle(0, 0, width, height);
                BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int absoluteStride = Math.Abs(data.Stride);
                    byte[] lockedPixels = new byte[absoluteStride * height];
                    byte[] pixels = new byte[width * height * 4];
                    Marshal.Copy(data.Scan0, lockedPixels, 0, lockedPixels.Length);
                    for (int y = 0; y < height; y++)
                    {
                        int sourceRow = data.Stride >= 0 ? y * absoluteStride : (height - 1 - y) * absoluteStride;
                        int targetRow = y * width * 4;
                        Buffer.BlockCopy(lockedPixels, sourceRow, pixels, targetRow, width * 4);
                        for (int x = 0; x < width; x++)
                        {
                            int offset = targetRow + (x * 4);
                            byte blue = pixels[offset];
                            pixels[offset] = pixels[offset + 2];
                            pixels[offset + 2] = blue;
                        }
                    }
                    return pixels;
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
        }
    }
}
