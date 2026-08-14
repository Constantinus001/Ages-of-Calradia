using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TwelveMonthCalendar
{
    internal sealed class CampaignPoliticalOverlayView : MapView
    {
        internal const float FadeStartAltitude = 580f;
        internal const float FadeEndAltitude = 740f;
        private const float LabelStartAltitude = 650f;
        private CampaignKingdomBorderBehavior _behavior;
        private readonly List<PoliticalKingdomLabel> _labels = new List<PoliticalKingdomLabel>();
        private GauntletLayer _layer;

        public CampaignPoliticalOverlayView() { }

        internal bool IsMapReady
        {
            get { return MapScreen?.MapCameraView?.Camera != null; }
        }

        internal void AttachBehavior(CampaignKingdomBorderBehavior behavior)
        {
            _behavior = behavior;
            RebuildLabels();
        }

        protected override void CreateLayout()
        {
            base.CreateLayout();
            _layer = new GauntletLayer("AgesOfCalradiaPoliticalOverlay", 190, false);
            MapScreen.AddLayer(_layer);
            RebuildLabels();
        }

        protected override void OnFinalize()
        {
            _behavior?.SetPoliticalOverlayAlpha(0f);
            ClearLabels();
            if (_layer != null) MapScreen.RemoveLayer(_layer);
            _layer = null;
            base.OnFinalize();
        }

        protected override void OnMapScreenUpdate(float dt)
        {
            base.OnMapScreenUpdate(dt);
            Camera camera = MapScreen?.MapCameraView?.Camera;
            if (camera == null) return;
            _behavior?.OnMapFrame(camera);
            float altitude = camera.Frame.origin.z;
            float alpha = Math.Max(0f, Math.Min(1f, (altitude - FadeStartAltitude) / (FadeEndAltitude - FadeStartAltitude)));
            _behavior?.SetPoliticalOverlayAlpha(alpha);
            ProjectLabels(camera, altitude >= LabelStartAltitude && (_behavior?.HasVisiblePoliticalFill ?? false));
        }

        internal void RebuildLabels()
        {
            if (_layer == null || Campaign.Current == null) return;
            ClearLabels();
            foreach (Kingdom kingdom in Kingdom.All.Where(candidate => candidate != null && !candidate.IsEliminated))
            {
                List<Settlement> holdings = Settlement.All.Where(settlement => settlement != null
                    && (settlement.IsTown || settlement.IsCastle)
                    && settlement.OwnerClan != null
                    && ReferenceEquals(settlement.OwnerClan.Kingdom, kingdom)).ToList();
                if (holdings.Count == 0) continue;
                float x = holdings.Average(settlement => settlement.Position.X);
                float y = holdings.Average(settlement => settlement.Position.Y);
                PoliticalKingdomLabelVM vm = new PoliticalKingdomLabelVM(
                    kingdom.Name.ToString().ToUpperInvariant(),
                    ColorToString(kingdom.PrimaryBannerColor));
                GauntletMovieIdentifier movie = _layer.LoadMovie("PoliticalKingdomLabel", vm);
                _labels.Add(new PoliticalKingdomLabel(vm, movie, new Vec2(x, y), holdings.Count));
            }
        }

        private void ProjectLabels(Camera camera, bool zoomedOut)
        {
            if (Campaign.Current == null || !CampaignMapTerrainGridCache.IsReady) return;
            List<Vec2> occupied = new List<Vec2>();
            foreach (PoliticalKingdomLabel label in _labels.OrderByDescending(item => item.Weight))
            {
                float terrainHeight;
                if (!CampaignMapTerrainGridCache.TrySampleHeight(label.WorldPosition, out terrainHeight))
                {
                    label.ViewModel.SetScreenState(0f, 0f, false);
                    continue;
                }
                float screenX = 0f, screenY = 0f, depth = 0f;
                MBWindowManager.WorldToScreenInsideUsableArea(camera,
                    new Vec3(label.WorldPosition.x, label.WorldPosition.y, terrainHeight + 8f),
                    ref screenX, ref screenY, ref depth);
                Vec2 screen = new Vec2(screenX, screenY);
                bool collision = occupied.Any(point => Math.Abs(point.x - screenX) < 170f && Math.Abs(point.y - screenY) < 38f);
                bool visible = zoomedOut && depth > 0f && !collision;
                label.ViewModel.SetScreenState(screenX - 180f, screenY - 22f, visible);
                if (visible) occupied.Add(screen);
            }
        }

        private void ClearLabels()
        {
            if (_layer != null)
            {
                foreach (PoliticalKingdomLabel label in _labels) _layer.ReleaseMovie(label.Movie);
            }
            foreach (PoliticalKingdomLabel label in _labels) label.ViewModel.OnFinalize();
            _labels.Clear();
        }

        private static string ColorToString(uint color) { return "#" + (color & 0x00FFFFFFu).ToString("X6") + "FF"; }

        private sealed class PoliticalKingdomLabel
        {
            internal PoliticalKingdomLabel(PoliticalKingdomLabelVM vm, GauntletMovieIdentifier movie, Vec2 position, int weight)
            { ViewModel = vm; Movie = movie; WorldPosition = position; Weight = weight; }
            internal PoliticalKingdomLabelVM ViewModel { get; private set; }
            internal GauntletMovieIdentifier Movie { get; private set; }
            internal Vec2 WorldPosition { get; private set; }
            internal int Weight { get; private set; }
        }
    }

    /// <summary>
    /// Suppresses only settlement nameplates in the distant political view.
    /// Party nameplates use a separate VM and remain available. The native
    /// worker recomputes this pending flag every frame, so zooming in restores
    /// normal labels without retaining or mutating UI collections.
    /// </summary>
    [HarmonyPatch]
    internal static class CampaignPoliticalSettlementNameplatePatch
    {
        private const float HideAtAltitude = 650f;

        private static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName(
                "SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM");
            return type == null ? null : AccessTools.Method(type, "UpdateNameplateMT");
        }

        private static void Postfix(ref bool ____bindIsVisibleOnMap, Vec3 cameraPosition)
        {
            if (cameraPosition.z >= HideAtAltitude) ____bindIsVisibleOnMap = false;
        }
    }

    internal sealed class PoliticalKingdomLabelVM : ViewModel
    {
        private float _screenX;
        private float _screenY;
        private bool _isVisible;
        internal PoliticalKingdomLabelVM(string name, string color) { Name = name; Color = color; }
        [DataSourceProperty] public string Name { get; private set; }
        [DataSourceProperty] public string Color { get; private set; }
        [DataSourceProperty] public float ScreenX { get { return _screenX; } private set { if (value != _screenX) { _screenX = value; OnPropertyChangedWithValue(value, nameof(ScreenX)); } } }
        [DataSourceProperty] public float ScreenY { get { return _screenY; } private set { if (value != _screenY) { _screenY = value; OnPropertyChangedWithValue(value, nameof(ScreenY)); } } }
        [DataSourceProperty] public bool IsVisible { get { return _isVisible; } private set { if (value != _isVisible) { _isVisible = value; OnPropertyChangedWithValue(value, nameof(IsVisible)); } } }
        internal void SetScreenState(float x, float y, bool visible) { ScreenX = x; ScreenY = y; IsVisible = visible; }
    }
}
