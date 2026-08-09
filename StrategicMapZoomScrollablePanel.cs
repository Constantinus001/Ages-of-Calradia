using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

namespace TwelveMonthCalendar
{
    // The map still supports drag panning, but this widget owns the wheel so
    // WorldCalendarScreen can apply it exclusively as zoom input.
    public sealed class StrategicMapZoomScrollablePanel : EncyclopediaTroopScrollablePanel
    {
        private bool _wheelDiagnosticLogged;
        private float _previousCanvasWidth;
        private float _previousCanvasHeight;

        public StrategicMapZoomScrollablePanel(UIContext context) : base(context) { }

        protected override bool OnPreviewMouseScroll()
        {
            // Bannerlord's ScrollablePanel returns true to accept and process
            // wheel scrolling. Reject it here; WorldCalendarScreen reads the
            // same wheel delta and applies zoom without changing scrollbars.
            if (!_wheelDiagnosticLogged)
            {
                _wheelDiagnosticLogged = true;
                Diagnostics.Info("Strategic map wheel diagnostic: scroll panel rejected wheel input; screen-level zoom remains active.");
            }
            return false;
        }

        protected override void OnLateUpdate(float dt)
        {
            if (InnerPanel == null || ClipRect == null)
            {
                base.OnLateUpdate(dt);
                return;
            }

            float canvasWidth = InnerPanel.Size.X;
            float canvasHeight = InnerPanel.Size.Y;
            bool initializeViewport = _previousCanvasWidth <= 0f && _previousCanvasHeight <= 0f
                && canvasWidth > 0f && canvasHeight > 0f
                && ClipRect.Size.X > 0f && ClipRect.Size.Y > 0f;
            bool widthChanged = _previousCanvasWidth > 0f && Math.Abs(canvasWidth - _previousCanvasWidth) > 0.5f;
            bool heightChanged = _previousCanvasHeight > 0f && Math.Abs(canvasHeight - _previousCanvasHeight) > 0.5f;

            if (initializeViewport)
            {
                if (HorizontalScrollbar != null)
                {
                    float horizontalOverflow = Math.Max(0f, canvasWidth - ClipRect.Size.X);
                    HorizontalScrollbar.MaxValue = Math.Max(1f, horizontalOverflow);
                    HorizontalScrollbar.SetValueForced(horizontalOverflow * 0.5f);
                }
                if (VerticalScrollbar != null)
                {
                    float verticalOverflow = Math.Max(0f, canvasHeight - ClipRect.Size.Y);
                    VerticalScrollbar.MaxValue = Math.Max(1f, verticalOverflow);
                    VerticalScrollbar.SetValueForced(verticalOverflow * 0.5f);
                }
            }

            // Gauntlet's ScrollablePanel applies the scrollbar values to the
            // inner-panel position inside its OnLateUpdate. Put the new range
            // and center-preserving value in place first so the resized map is
            // positioned correctly in this frame, rather than snapping one
            // frame later (the visible zoom flicker).
            if (widthChanged && HorizontalScrollbar != null)
            {
                float centerRatio = Clamp01((HorizontalScrollbar.ValueFloat + ClipRect.Size.X * 0.5f) / _previousCanvasWidth);
                float newMaximum = Math.Max(1f, canvasWidth - ClipRect.Size.X);
                float target = Math.Max(0f, Math.Min(newMaximum,
                    centerRatio * canvasWidth - ClipRect.Size.X * 0.5f));
                HorizontalScrollbar.MaxValue = newMaximum;
                HorizontalScrollbar.SetValueForced(target);
            }

            if (heightChanged && VerticalScrollbar != null)
            {
                float centerRatio = Clamp01((VerticalScrollbar.ValueFloat + ClipRect.Size.Y * 0.5f) / _previousCanvasHeight);
                float newMaximum = Math.Max(1f, canvasHeight - ClipRect.Size.Y);
                float target = Math.Max(0f, Math.Min(newMaximum,
                    centerRatio * canvasHeight - ClipRect.Size.Y * 0.5f));
                VerticalScrollbar.MaxValue = newMaximum;
                VerticalScrollbar.SetValueForced(target);
            }

            base.OnLateUpdate(dt);

            if (widthChanged || heightChanged)
            {
                Diagnostics.Info("Strategic map zoom diagnostic: viewport center preserved; canvas="
                    + _previousCanvasWidth.ToString("0") + "x" + _previousCanvasHeight.ToString("0")
                    + " -> " + canvasWidth.ToString("0") + "x" + canvasHeight.ToString("0") + ".");
            }
            else if (initializeViewport)
            {
                Diagnostics.Info("Strategic map viewport diagnostic: initial view centered; canvas="
                    + canvasWidth.ToString("0") + "x" + canvasHeight.ToString("0")
                    + "; clip=" + ClipRect.Size.X.ToString("0") + "x" + ClipRect.Size.Y.ToString("0") + ".");
            }

            _previousCanvasWidth = canvasWidth;
            _previousCanvasHeight = canvasHeight;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    // Draws the same two-building town and crenellated castle silhouettes as
    // the composed map, using Bannerlord's built-in white primitive. No custom
    // atlas or native texture lifecycle is involved.
    public sealed class StrategicLegendDrawWidget : Widget
    {
        private const float TownDesignSize = 36f;
        private const float CastleDesignSize = 30f;
        private static readonly float[,] CastleOutline = { {3,4,24,23} };
        private static readonly float[,] CastleFill = { {5,8,20,17} };
        private static readonly float[,] CastleCutouts = { {8,4,4,6}, {17,4,4,6} };
        private static readonly float[,] CastleHighlight = { {6,10,3,11} };
        private static readonly float[,] CastleDetail = { {12,18,6,7} };
        private bool _diagnosticLogged;

        public string IconKind { get; set; }

        public StrategicLegendDrawWidget(UIContext context) : base(context) { }

        protected override void OnRender(
            TaleWorlds.TwoDimension.TwoDimensionContext context,
            TaleWorlds.TwoDimension.TwoDimensionDrawContext drawContext)
        {
            base.OnRender(context, drawContext);
            TaleWorlds.TwoDimension.Sprite primitive = Context.SpriteData.GetSprite("BlankWhiteSquare");
            if (primitive == null || Size.X <= 0f || Size.Y <= 0f) return;

            TaleWorlds.TwoDimension.SimpleMaterial outline = CreateMaterial(drawContext, 24, 17, 12);
            TaleWorlds.TwoDimension.SimpleMaterial fill = CreateMaterial(drawContext, 183, 136, 68);
            TaleWorlds.TwoDimension.SimpleMaterial highlight = CreateMaterial(drawContext, 222, 180, 99);
            TaleWorlds.TwoDimension.SimpleMaterial detail = CreateMaterial(drawContext, 49, 32, 19);
            outline.Texture = primitive.Texture;
            fill.Texture = primitive.Texture;
            highlight.Texture = primitive.Texture;
            detail.Texture = primitive.Texture;
            bool isCastle = string.Equals(IconKind, "Castle", StringComparison.OrdinalIgnoreCase);
            float scale = Math.Min(Size.X, Size.Y) / (isCastle ? CastleDesignSize : TownDesignSize);

            if (isCastle)
            {
                DrawParts(drawContext, primitive, outline, CastleOutline, scale);
                DrawParts(drawContext, primitive, fill, CastleFill, scale);
                DrawParts(drawContext, primitive, outline, CastleCutouts, scale);
                DrawParts(drawContext, primitive, highlight, CastleHighlight, scale);
                DrawParts(drawContext, primitive, detail, CastleDetail, scale);
            }
            else
            {
                DrawTownMarker(drawContext, primitive, outline, fill, highlight, detail, scale);
            }

            if (!_diagnosticLogged)
            {
                _diagnosticLogged = true;
                Diagnostics.Info("Strategic legend draw diagnostic: kind=" + (IconKind ?? "Town")
                    + "; primitiveValid=" + (primitive.Texture != null && primitive.Texture.IsValid)
                    + "; size=" + Size.X.ToString("0") + "x" + Size.Y.ToString("0") + ".");
            }
        }

        private void DrawTownMarker(
            TaleWorlds.TwoDimension.TwoDimensionDrawContext drawContext,
            TaleWorlds.TwoDimension.Sprite primitive,
            TaleWorlds.TwoDimension.SimpleMaterial outline,
            TaleWorlds.TwoDimension.SimpleMaterial fill,
            TaleWorlds.TwoDimension.SimpleMaterial highlight,
            TaleWorlds.TwoDimension.SimpleMaterial detail,
            float scale)
        {
            // These are the same normalized coordinates used by
            // CalendarStrategicMapTextureProvider.DrawTownMarker. The roof
            // scanlines preserve its triangular houses in Gauntlet without a
            // second texture/provider lifecycle.
            DrawRectangle(drawContext, primitive, outline, 2f, 17f, 15f, 18f, scale);
            DrawRectangle(drawContext, primitive, outline, 18f, 22f, 15f, 13f, scale);
            DrawFilledTriangle(drawContext, primitive, outline, 9f, 0f, 0f, 17f, 19f, 17f, scale);
            DrawFilledTriangle(drawContext, primitive, outline, 25f, 8f, 16f, 22f, 35f, 22f, scale);

            DrawRectangle(drawContext, primitive, fill, 4f, 18f, 11f, 15f, scale);
            DrawRectangle(drawContext, primitive, fill, 20f, 23f, 11f, 10f, scale);
            DrawFilledTriangle(drawContext, primitive, fill, 9f, 4f, 3f, 16f, 16f, 16f, scale);
            DrawFilledTriangle(drawContext, primitive, fill, 25f, 12f, 19f, 21f, 32f, 21f, scale);

            DrawRectangle(drawContext, primitive, highlight, 4f, 19f, 3f, 13f, scale);
            DrawRectangle(drawContext, primitive, highlight, 20f, 24f, 3f, 9f, scale);
            DrawRectangle(drawContext, primitive, detail, 7f, 27f, 4f, 8f, scale);
            DrawRectangle(drawContext, primitive, detail, 26f, 28f, 4f, 7f, scale);
        }

        private void DrawFilledTriangle(
            TaleWorlds.TwoDimension.TwoDimensionDrawContext drawContext,
            TaleWorlds.TwoDimension.Sprite primitive,
            TaleWorlds.TwoDimension.SimpleMaterial material,
            float apexX,
            float apexY,
            float leftX,
            float baseY,
            float rightX,
            float ignoredRightY,
            float scale)
        {
            int firstRow = (int)Math.Floor(apexY);
            int lastRow = (int)Math.Ceiling(baseY);
            float height = Math.Max(1f, baseY - apexY);
            for (int row = firstRow; row <= lastRow; row++)
            {
                float progress = Math.Max(0f, Math.Min(1f, (row - apexY) / height));
                float rowLeft = apexX + ((leftX - apexX) * progress);
                float rowRight = apexX + ((rightX - apexX) * progress);
                DrawRectangle(
                    drawContext,
                    primitive,
                    material,
                    rowLeft,
                    row,
                    Math.Max(1f, rowRight - rowLeft),
                    1.15f,
                    scale);
            }
        }

        private void DrawRectangle(
            TaleWorlds.TwoDimension.TwoDimensionDrawContext drawContext,
            TaleWorlds.TwoDimension.Sprite primitive,
            TaleWorlds.TwoDimension.SimpleMaterial material,
            float left,
            float top,
            float width,
            float height,
            float scale)
        {
            TaleWorlds.TwoDimension.Rectangle2D rectangle = AreaRect;
            rectangle.SetVisualOffset(left * scale, top * scale);
            rectangle.SetVisualScale((width * scale) / Size.X, (height * scale) / Size.Y);
            rectangle.ValidateVisuals();
            drawContext.DrawSprite(primitive, material, rectangle, _scaleToUse);
        }

        private TaleWorlds.TwoDimension.SimpleMaterial CreateMaterial(
            TaleWorlds.TwoDimension.TwoDimensionDrawContext drawContext,
            byte red,
            byte green,
            byte blue)
        {
            TaleWorlds.TwoDimension.SimpleMaterial material = drawContext.CreateSimpleMaterial();
            material.Color = new TaleWorlds.Library.Color(
                red / 255f,
                green / 255f,
                blue / 255f,
                1f);
            material.ColorFactor = 1f;
            material.AlphaFactor = Context.ContextAlpha;
            return material;
        }

        private void DrawParts(
            TaleWorlds.TwoDimension.TwoDimensionDrawContext drawContext,
            TaleWorlds.TwoDimension.Sprite primitive,
            TaleWorlds.TwoDimension.SimpleMaterial material,
            float[,] parts,
            float scale)
        {
            for (int index = 0; index < parts.GetLength(0); index++)
            {
                float left = parts[index, 0] * scale;
                float top = parts[index, 1] * scale;
                float width = parts[index, 2] * scale;
                float height = parts[index, 3] * scale;

                // Follow Widget.OnRender exactly: retain the transformed
                // AreaRect, express each primitive as visual offset/scale,
                // and submit with the widget's active UI scale. Passing zero
                // here causes TwoDimensionDrawContext to emit no pixels.
                TaleWorlds.TwoDimension.Rectangle2D rectangle = AreaRect;
                rectangle.SetVisualOffset(left, top);
                rectangle.SetVisualScale(width / Size.X, height / Size.Y);
                rectangle.ValidateVisuals();
                drawContext.DrawSprite(primitive, material, rectangle, _scaleToUse);
            }
        }
    }

    // Scales XML-defined rectangle primitives in a 30x30 design grid. The
    // icon therefore follows StrategicMapLegendIconSize without custom
    // sprites or TextureProviders.
    public sealed class StrategicLegendPrimitiveIconWidget : Widget
    {
        private const float DesignSize = 30f;
        private static readonly float[,] TownParts =
        {
            {3,10,12,16}, {4,8,10,3}, {6,5,6,3}, {5,11,8,13}, {5,9,8,2},
            {7,7,4,2}, {7,13,2,8}, {9,18,3,6}, {14,14,13,12}, {16,11,9,3},
            {18,8,5,3}, {16,15,9,9}, {17,13,7,2}, {19,10,3,3}, {20,19,3,5}
        };
        private static readonly float[,] CastleParts =
        {
            {3,5,24,22}, {5,7,20,18}, {9,5,4,5}, {17,5,4,5}, {7,10,3,11}, {12,18,6,7}
        };
        private bool _diagnosticLogged;

        public string IconKind { get; set; }

        public StrategicLegendPrimitiveIconWidget(UIContext context) : base(context) { }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            float iconSize = SuggestedWidth > 0f ? SuggestedWidth : DesignSize;
            float[,] parts = string.Equals(IconKind, "Castle", StringComparison.OrdinalIgnoreCase)
                ? CastleParts : TownParts;
            int partCount = Math.Min(ChildCount, parts.GetLength(0));
            for (int index = 0; index < partCount; index++)
            {
                Widget child = GetChild(index);
                child.SuggestedWidth = parts[index, 2] * iconSize / DesignSize;
                child.SuggestedHeight = parts[index, 3] * iconSize / DesignSize;
                child.MarginLeft = parts[index, 0] * iconSize / DesignSize;
                child.MarginTop = parts[index, 1] * iconSize / DesignSize;
            }

            if (!_diagnosticLogged)
            {
                _diagnosticLogged = true;
                Diagnostics.Info("Strategic legend primitive diagnostic: kind=" + (IconKind ?? "Town")
                    + "; children=" + ChildCount + "; configuredParts=" + parts.GetLength(0)
                    + "; iconSize=" + iconSize.ToString("0.0") + ".");
            }
        }
    }
}
