using System;
using System.IO;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;
using EngineTexture = TaleWorlds.Engine.Texture;
using EngineTextureWrapper = TaleWorlds.Engine.GauntletUI.EngineTexture;
using TwoDimensionTexture = TaleWorlds.TwoDimension.Texture;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Loads one of the lightweight Strategic Map marker images directly from
    /// the module. This avoids relying on a Gauntlet sprite atlas for a pair
    /// of small UI symbols and mirrors the stable live-map texture path.
    /// </summary>
    public abstract class CalendarStrategicMarkerTextureProvider : TextureProvider
    {
        private EngineTexture _engineTexture;
        private TwoDimensionTexture _renderTexture;

        protected abstract string IconFileName { get; }

        protected override TwoDimensionTexture OnGetTextureForRender(TwoDimensionContext context, string name)
        {
            if (_renderTexture != null && _renderTexture.IsValid)
            {
                return _renderTexture;
            }

            try
            {
                string path = Path.Combine(
                    GetModuleRoot(),
                    "GUI",
                    "SpriteParts",
                    "ui_world_calendar",
                    IconFileName);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("Strategic Map marker texture is missing.", path);
                }

                EngineTexture texture = EngineTexture.CreateFromMemory(File.ReadAllBytes(path));
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
                Diagnostics.Error("Strategic Map marker texture could not be loaded: " + IconFileName, exception);
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
                // The engine owns the remaining native cleanup if a screen is
                // already being disposed while this provider is cleared.
            }
            finally
            {
                _engineTexture = null;
            }
        }

        private static string GetModuleRoot()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CalendarStrategicMarkerTextureProvider).Assembly.Location);
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
    }

    public sealed class CalendarStrategicTownMarkerTextureProvider : CalendarStrategicMarkerTextureProvider
    {
        protected override string IconFileName { get { return "strategic_marker_town.png"; } }
    }

    public sealed class CalendarStrategicCastleMarkerTextureProvider : CalendarStrategicMarkerTextureProvider
    {
        protected override string IconFileName { get { return "strategic_marker_castle.png"; } }
    }
}
