using System;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Small original Gauntlet panel for the refuge layout builder. The view
    /// contains no scene data; all placement still goes through the behavior's
    /// validated whitelist and atomic draft writer.
    /// </summary>
    internal sealed class CalendarRefugeBuilderHudView : MissionView
    {
        private readonly CalendarRefugeLayoutBuilderBehavior _builder;
        private readonly CalendarRefugeFlyoverView _flyover;
        private GauntletLayer _layer;
        private CalendarRefugeBuilderHudVM _dataSource;
        private bool _builderToggleWasDown;

        internal CalendarRefugeBuilderHudView(
            CalendarRefugeLayoutBuilderBehavior builder,
            CalendarRefugeFlyoverView flyover)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _flyover = flyover ?? throw new ArgumentNullException(nameof(flyover));
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            try
            {
                _dataSource = new CalendarRefugeBuilderHudVM(_builder);
                _layer = new GauntletLayer("RefugeBuilderHud", 250, false);
                _layer.LoadMovie("RefugeBuilderHud", _dataSource);
                MissionScreen.AddLayer(_layer);
                _builder.StateChanged += HandleBuilderStateChanged;
                ApplyInputState();
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge Builder HUD could not be initialized.", exception);
                RemoveHud();
            }
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            _dataSource?.Refresh();
            ProcessBuilderToggle();
            if (_builder.IsBuilderActive && MissionScreen != null)
            {
                ProcessEditorHotkeys();
                Vec3 groundPosition;
                Vec3 groundNormal;
                bool hasGroundPosition = MissionScreen.GetProjectedMousePositionOnGround(
                    out groundPosition,
                    out groundNormal,
                    BodyFlags.None,
                    false);
                _builder.UpdateMousePlacement(
                    hasGroundPosition,
                    groundPosition,
                    _layer != null && _layer.IsHitThisFrame,
                    IsKeyPressed(MissionScreen.SceneLayer.Input, InputKey.LeftMouseButton));
            }
        }

        public override void OnMissionScreenFinalize()
        {
            _builder.StateChanged -= HandleBuilderStateChanged;
            RemoveHud();
            base.OnMissionScreenFinalize();
        }

        private void HandleBuilderStateChanged()
        {
            _dataSource?.Refresh();
            _flyover.SetBuilderMode(_builder.IsBuilderActive);
            ApplyInputState();
        }

        private void ProcessEditorHotkeys()
        {
            IInputContext editorInput = MissionScreen.SceneLayer.Input;
            if (IsKeyPressed(editorInput, InputKey.Left)) _builder.SelectPrevious();
            if (IsKeyPressed(editorInput, InputKey.Right)) _builder.SelectNext();
            if (IsKeyPressed(editorInput, InputKey.Z)) _builder.RotateLeft();
            if (IsKeyPressed(editorInput, InputKey.X)) _builder.RotateRight();
            if (IsKeyPressed(editorInput, InputKey.PageUp)) _builder.RaisePreview();
            if (IsKeyPressed(editorInput, InputKey.PageDown)) _builder.LowerPreview();
            float mouseWheel = editorInput.GetDeltaMouseScroll();
            if (mouseWheel > 0.01f) _builder.SelectNext();
            if (mouseWheel < -0.01f) _builder.SelectPrevious();
            if (IsKeyPressed(editorInput, InputKey.B)) _builder.ToggleSelectionMode();
            if (IsKeyPressed(editorInput, InputKey.M)) _builder.BeginMoveSelected();
            if (IsKeyPressed(editorInput, InputKey.F)) _builder.PlaceSelected();
            if (IsKeyPressed(editorInput, InputKey.Delete)) _builder.DeleteTargeted();
            if (IsKeyDown(editorInput, InputKey.LeftControl)
                && IsKeyPressed(editorInput, InputKey.Z)) _builder.UndoLastChange();
            if (IsKeyDown(editorInput, InputKey.LeftControl)
                && IsKeyPressed(editorInput, InputKey.R)) _builder.ResetBuilderLayout();
            if (IsKeyDown(editorInput, InputKey.LeftControl)
                && IsKeyPressed(editorInput, InputKey.S))
            {
                _builder.ExportCombinedPrefab();
            }
        }

        private void ProcessBuilderToggle()
        {
            if (MissionScreen == null || MissionScreen.SceneLayer == null)
            {
                return;
            }

            bool toggleDown = IsKeyDown(MissionScreen.SceneLayer.Input, InputKey.F7);
            if (toggleDown && !_builderToggleWasDown)
            {
                _builder.ToggleEditing();
            }
            _builderToggleWasDown = toggleDown;
        }

        private void ApplyInputState()
        {
            if (_layer == null)
            {
                return;
            }

            if (_builder.IsBuilderActive)
            {
                // The editor is a real interactive UI.  Keeping this layer
                // focused makes every visible button clickable; the helpers
                // above still read scene/global input for placement hotkeys.
                _layer.IsFocusLayer = true;
                _layer.InputRestrictions.SetInputRestrictions(false, (InputUsageMask)0);
                MissionScreen.MouseVisible = true;
                ScreenManager.TrySetFocus(_layer);
            }
            else
            {
                _layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
                if (MissionScreen != null)
                {
                    MissionScreen.MouseVisible = false;
                }
                RestoreMissionInputFocus();
            }
        }

        private static bool IsKeyPressed(IInputContext context, InputKey key)
        {
            return (context != null && context.IsKeyPressed(key))
                || TaleWorlds.InputSystem.Input.IsKeyPressed(key);
        }

        private static bool IsKeyDown(IInputContext context, InputKey key)
        {
            return (context != null && context.IsKeyDown(key))
                || TaleWorlds.InputSystem.Input.IsKeyDown(key)
                || TaleWorlds.InputSystem.Input.IsKeyDownImmediate(key);
        }

        private void RestoreMissionInputFocus()
        {
            if (MissionScreen == null || MissionScreen.SceneLayer == null)
            {
                return;
            }

            MissionScreen.SceneLayer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(MissionScreen.SceneLayer);
        }

        private void RemoveHud()
        {
            if (_layer != null)
            {
                _layer.InputRestrictions.ResetInputRestrictions();
                ScreenManager.TryLoseFocus(_layer);
                if (MissionScreen != null)
                {
                    MissionScreen.RemoveLayer(_layer);
                }
                _layer = null;
            }

            if (MissionScreen != null)
            {
                MissionScreen.MouseVisible = false;
            }

            RestoreMissionInputFocus();

            _dataSource?.OnFinalize();
            _dataSource = null;
        }
    }

    internal sealed class CalendarRefugeBuilderHudVM : ViewModel
    {
        private readonly CalendarRefugeLayoutBuilderBehavior _builder;
        private bool _isVisible;
        private bool _isReady;
        private string _selectedName = string.Empty;
        private string _selectedPrefab = string.Empty;
        private string _selectedCounter = string.Empty;
        private string _rotation = string.Empty;
        private string _height = string.Empty;
        private string _selectionDetails = string.Empty;
        private string _editorMode = string.Empty;
        private string _hoveredItem = string.Empty;
        private string _collisionValidation = string.Empty;

        internal CalendarRefugeBuilderHudVM(CalendarRefugeLayoutBuilderBehavior builder)
        {
            _builder = builder;
            Refresh();
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            private set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
            }
        }

        [DataSourceProperty]
        public bool IsReady
        {
            get => _isReady;
            private set
            {
                if (_isReady == value) return;
                _isReady = value;
                OnPropertyChangedWithValue(value, nameof(IsReady));
            }
        }

        [DataSourceProperty]
        public string SelectedName
        {
            get => _selectedName;
            private set
            {
                if (_selectedName == value) return;
                _selectedName = value;
                OnPropertyChangedWithValue(value, nameof(SelectedName));
            }
        }

        [DataSourceProperty]
        public string SelectedPrefab
        {
            get => _selectedPrefab;
            private set
            {
                if (_selectedPrefab == value) return;
                _selectedPrefab = value;
                OnPropertyChangedWithValue(value, nameof(SelectedPrefab));
            }
        }

        [DataSourceProperty]
        public string SelectedCounter
        {
            get => _selectedCounter;
            private set
            {
                if (_selectedCounter == value) return;
                _selectedCounter = value;
                OnPropertyChangedWithValue(value, nameof(SelectedCounter));
            }
        }

        [DataSourceProperty]
        public string Rotation
        {
            get => _rotation;
            private set
            {
                if (_rotation == value) return;
                _rotation = value;
                OnPropertyChangedWithValue(value, nameof(Rotation));
            }
        }

        [DataSourceProperty]
        public string Height
        {
            get => _height;
            private set
            {
                if (_height == value) return;
                _height = value;
                OnPropertyChangedWithValue(value, nameof(Height));
            }
        }

        [DataSourceProperty]
        public string SelectionDetails
        {
            get => _selectionDetails;
            private set
            {
                if (_selectionDetails == value) return;
                _selectionDetails = value;
                OnPropertyChangedWithValue(value, nameof(SelectionDetails));
            }
        }

        [DataSourceProperty]
        public string EditorMode
        {
            get => _editorMode;
            private set
            {
                if (_editorMode == value) return;
                _editorMode = value;
                OnPropertyChangedWithValue(value, nameof(EditorMode));
            }
        }

        [DataSourceProperty]
        public string HoveredItem
        {
            get => _hoveredItem;
            private set
            {
                if (_hoveredItem == value) return;
                _hoveredItem = value;
                OnPropertyChangedWithValue(value, nameof(HoveredItem));
            }
        }

        [DataSourceProperty]
        public string CollisionValidation
        {
            get => _collisionValidation;
            private set
            {
                if (_collisionValidation == value) return;
                _collisionValidation = value;
                OnPropertyChangedWithValue(value, nameof(CollisionValidation));
            }
        }

        internal void Refresh()
        {
            IsVisible = _builder.IsBuilderActive;
            IsReady = _builder.IsReady;
            SelectedName = _builder.SelectedDisplayName;
            SelectedPrefab = _builder.SelectedPrefabId;
            SelectedCounter = _builder.SelectedCounter;
            Rotation = _builder.RotationText;
            Height = _builder.HeightText;
            SelectionDetails = _builder.CategoryText + "  |  Item " + SelectedCounter + "   Rotation: " + Rotation + "   Height: " + Height;
            EditorMode = _builder.EditorModeText;
            HoveredItem = _builder.HoveredItemText;
            CollisionValidation = _builder.CollisionValidationText;
        }

        public void ExecutePrevious() => _builder.SelectPrevious();
        public void ExecuteNext() => _builder.SelectNext();
        public void ExecuteWoodCategory() => _builder.SelectWoodCategory();
        public void ExecuteStoneCategory() => _builder.SelectStoneCategory();
        public void ExecuteMiscCategory() => _builder.SelectMiscCategory();
        public void ExecuteRotateLeft() => _builder.RotateLeft();
        public void ExecuteRotateRight() => _builder.RotateRight();
        public void ExecuteRaise() => _builder.RaisePreview();
        public void ExecuteLower() => _builder.LowerPreview();
        public void ExecuteToggleMode() => _builder.ToggleSelectionMode();
        public void ExecuteMoveSelected() => _builder.BeginMoveSelected();
        public void ExecutePlace() => _builder.PlaceSelected();
        public void ExecuteDelete() => _builder.DeleteTargeted();
        public void ExecuteUndo() => _builder.UndoLastChange();
        public void ExecuteReset() => _builder.ResetBuilderLayout();
        public void ExecuteExportCombined() => _builder.ExportCombinedPrefab();
        public void ExecuteClose() => _builder.ToggleEditing();
    }
}
