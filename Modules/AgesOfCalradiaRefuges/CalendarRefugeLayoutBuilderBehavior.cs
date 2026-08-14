using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TwelveMonthCalendar
{
    /// <summary>
    /// Small refuge-only authoring tool. It records native prefab transforms
    /// in a separate draft file and never modifies an active Bannerlord prefab.
    /// </summary>
    internal sealed class CalendarRefugeLayoutBuilderBehavior : MissionBehavior
    {
        private const float PlacementDistance = 60f;
        private const float SelectionRadius = 4f;
        private const float RotationStepRadians = 0.2617994f;
        private const float HeightStep = 0.25f;

        private static readonly string[] PlaceablePrefabs =
        {
            "castle_plank_wall_a",
            "battania_castle_corner_a_l1",
            "battania_castle_stairs_a_l1",
            "tent_vlandia_a",
            "tents_pict_a",
            "tents_pict_b",
            "sturgia_village_tent_a",
            "wood_storage_a",
            "wooden_platform_2_a",
            "wooden_platform_2_fence_a",
            "wooden_platform_2_stairs_a"
        };

        private static readonly string[] PlaceableNames =
        {
            "Palisade Wall",
            "Corner Guard Tower",
            "Wooden Stairs",
            "Vlandian Command Tent",
            "Pict Camp Tent",
            "Pict Shelter Tent",
            "Sturgian Camp Tent",
            "Wood Storage",
            "Wooden Platform",
            "Platform Fence",
            "Platform Stairs"
        };

        private static readonly BuilderCategory[] PrefabCategories =
        {
            BuilderCategory.Wood,
            BuilderCategory.Stone,
            BuilderCategory.Wood,
            BuilderCategory.Misc,
            BuilderCategory.Misc,
            BuilderCategory.Misc,
            BuilderCategory.Misc,
            BuilderCategory.Wood,
            BuilderCategory.Wood,
            BuilderCategory.Wood,
            BuilderCategory.Wood
        };

        private readonly string _sceneId;
        private readonly CalendarRefugeMissionController _controller;
        private readonly bool _editingAllowed;
        private readonly List<PlacedRefugeEntity> _placed = new List<PlacedRefugeEntity>();
        private readonly Stack<BuilderUndoAction> _undoActions = new Stack<BuilderUndoAction>();

        private bool _loaded;
        private bool _editing;
        private int _selectedPrefabIndex;
        private BuilderCategory _selectedCategory;
        private float _yaw;
        private float _heightOffset;
        private MatrixFrame _anchorFrame;
        private GameEntity _previewEntity;
        private string _previewPrefabId = string.Empty;
        private Vec3 _previewPosition = Vec3.Invalid;
        private bool _selectionMode;
        private bool _movingSelected;
        private float _moveOriginalX;
        private float _moveOriginalY;
        private float _moveOriginalZ;
        private PlacedRefugeEntity _hoveredEntity;
        private PlacedRefugeEntity _selectedEntity;
        private PlacedRefugeEntity _outlinedEntity;
        private Agent _frozenPlayerAgent;
        private AgentControllerType _previousPlayerController;

        internal static bool IsEditing { get; private set; }
        internal event Action StateChanged;

        internal bool IsBuilderActive => _editing;
        internal bool IsEditingAllowed => _editingAllowed;
        internal bool IsSelectionMode => _selectionMode;
        internal bool IsReady => _loaded;
        internal string SelectedPrefabId => PlaceablePrefabs[_selectedPrefabIndex];
        internal string SelectedDisplayName => PlaceableNames[_selectedPrefabIndex];
        internal string SelectedCounter => GetCategoryPosition(_selectedPrefabIndex, _selectedCategory).ToString(CultureInfo.InvariantCulture)
            + " / " + GetCategoryCount(_selectedCategory).ToString(CultureInfo.InvariantCulture);
        internal string CategoryText => GetCategoryName(_selectedCategory);
        internal string RotationText => (_yaw * 57.29578f).ToString("F0", CultureInfo.InvariantCulture) + " degrees";
        internal string HeightText => (_heightOffset >= 0f ? "+" : string.Empty)
            + _heightOffset.ToString("0.00", CultureInfo.InvariantCulture) + " m";
        internal string EditorModeText => _movingSelected
            ? "MOVE SELECTED - CLICK TO SAVE"
            : (_selectionMode ? "SELECT / DELETE / MOVE" : "PLACE NEW");
        internal string HoveredItemText => _hoveredEntity == null
            ? (_selectionMode ? "Hover over one of your placed objects" : "")
            : "Hover: " + GetFriendlyName(_hoveredEntity.PrefabId)
                + " | " + GetCollisionLabel(_hoveredEntity);
        internal string CollisionValidationText
        {
            get
            {
                int collisionCount = 0;
                for (int index = 0; index < _placed.Count; index++)
                {
                    if (_placed[index].HasRuntimeCollision)
                    {
                        collisionCount++;
                    }
                }

                return "COLLISION: " + collisionCount.ToString(CultureInfo.InvariantCulture)
                    + " physical / " + _placed.Count.ToString(CultureInfo.InvariantCulture)
                    + " placed  |  NAVMESH: BAKE REQUIRED";
            }
        }
        internal bool CanUndo => _undoActions.Count > 0;

        public CalendarRefugeLayoutBuilderBehavior(
            string sceneId,
            CalendarRefugeMissionController controller)
        {
            _sceneId = sceneId ?? string.Empty;
            _controller = controller;
            _editingAllowed = !CalendarRefugeMission.IsModuleOwnedSceneReady(_sceneId);
            IsEditing = false;
        }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (Mission == null || Mission.InputManager == null)
            {
                return;
            }

            if (!_loaded && _controller.TryGetLayoutAnchorFrame(out _anchorFrame))
            {
                _loaded = true;
                LoadDraft();
            }

            // The HUD mission view owns the F7 shortcut. It has access to
            // the active scene-layer input context as well as the physical
            // keyboard fallback, which is necessary when Gauntlet owns focus
            // while the builder panel is open.
        }

        private void SelectPrefab(int direction)
        {
            int candidate = _selectedPrefabIndex;
            do
            {
                candidate = (candidate + direction + PlaceablePrefabs.Length) % PlaceablePrefabs.Length;
            }
            while (PrefabCategories[candidate] != _selectedCategory && candidate != _selectedPrefabIndex);
            _selectedPrefabIndex = candidate;
            NotifyStateChanged();
            ShowSelection();
        }

        internal void SelectWoodCategory() => SelectCategory(BuilderCategory.Wood);
        internal void SelectStoneCategory() => SelectCategory(BuilderCategory.Stone);
        internal void SelectMiscCategory() => SelectCategory(BuilderCategory.Misc);

        private void SelectCategory(BuilderCategory category)
        {
            _selectedCategory = category;
            for (int index = 0; index < PrefabCategories.Length; index++)
            {
                if (PrefabCategories[index] == category)
                {
                    _selectedPrefabIndex = index;
                    break;
                }
            }
            NotifyStateChanged();
            ShowSelection();
        }

        internal void ToggleEditing()
        {
            if (!_editing && !_editingAllowed)
            {
                ShowStatus("This refuge uses a fixed authored scene. Edit its linked fort and terrain in the Modding Kit.");
                return;
            }

            _editing = !_editing;
            IsEditing = _editing;
            SetPlayerEditingLock(_editing);
            if (!_editing)
            {
                CancelSelectedMove();
                RemovePreviewEntity();
                SetSelectionOutline(null);
                _previewPosition = Vec3.Invalid;
            }
            NotifyStateChanged();
            ShowStatus(_editing
                ? "Refuge Builder ON: use the panel or Left/Right, Z/X, F and Delete. F7 closes it."
                : "Refuge Builder OFF. Layout draft saved.");
        }

        internal void SelectPrevious()
        {
            SelectPrefab(-1);
        }

        internal void SelectNext()
        {
            SelectPrefab(1);
        }

        internal void RotateLeft()
        {
            _yaw -= RotationStepRadians;
            NotifyStateChanged();
            ShowSelection();
        }

        internal void RotateRight()
        {
            _yaw += RotationStepRadians;
            NotifyStateChanged();
            ShowSelection();
        }

        internal void RaisePreview()
        {
            _heightOffset += HeightStep;
            NotifyStateChanged();
        }

        internal void LowerPreview()
        {
            _heightOffset -= HeightStep;
            NotifyStateChanged();
        }

        internal void PlaceSelected()
        {
            if (_editing && _loaded)
            {
                if (_previewPosition.IsValid)
                {
                    PlaceSelectedPrefabAt(_previewPosition);
                }
                else
                {
                    PlaceSelectedPrefab();
                }
            }
        }

        internal void DeleteTargeted()
        {
            if (_editing && _loaded)
            {
                if (_selectedEntity != null || _hoveredEntity != null)
                {
                    DeletePlacedEntity(_selectedEntity ?? _hoveredEntity);
                    return;
                }
                DeleteTargetedPrefab();
            }
        }

        internal void ToggleSelectionMode()
        {
            CancelSelectedMove();
            _selectionMode = !_selectionMode;
            _selectedEntity = null;
            _hoveredEntity = null;
            SetSelectionOutline(null);
            if (_selectionMode)
            {
                RemovePreviewEntity();
            }
            NotifyStateChanged();
            ShowStatus(_selectionMode
                ? "Select mode: hover a placed object, left-click it, then press Delete. B returns to placement."
                : "Placement mode: move the preview and left-click to place. B opens select mode.");
        }

        internal void BeginMoveSelected()
        {
            if (!_editing || !_loaded)
            {
                return;
            }
            if (_selectedEntity == null || _selectedEntity.Entity == null)
            {
                ShowStatus("Select one of your placed objects first, then press M to move it.");
                return;
            }

            _movingSelected = true;
            _moveOriginalX = _selectedEntity.LocalX;
            _moveOriginalY = _selectedEntity.LocalY;
            _moveOriginalZ = _selectedEntity.LocalZ;
            SetSelectionOutline(_selectedEntity);
            NotifyStateChanged();
            ShowStatus("Move mode: aim at a new position and left-click to save the selected object.");
        }

        internal void UndoLastChange()
        {
            if (!_editing || !_loaded)
            {
                return;
            }

            if (_undoActions.Count == 0)
            {
                ShowStatus("Nothing to undo in this editing session.");
                return;
            }

            BuilderUndoAction action = _undoActions.Pop();
            PlacedRefugeEntity record = action.Record;
            if (action.Kind == BuilderUndoKind.Placement)
            {
                int currentIndex = _placed.IndexOf(record);
                if (currentIndex < 0)
                {
                    ShowStatus("That placement is no longer available to undo.");
                    NotifyStateChanged();
                    return;
                }
                _placed.RemoveAt(currentIndex);
                if (!TrySaveDraft())
                {
                    _placed.Insert(currentIndex, record);
                    _undoActions.Push(action);
                    ShowStatus("Undo was cancelled because the XML draft could not be saved.");
                    return;
                }
                RemoveEntity(record.Entity);
                if (ReferenceEquals(_hoveredEntity, record)) _hoveredEntity = null;
                if (ReferenceEquals(_selectedEntity, record)) _selectedEntity = null;
                ShowStatus("Undo: removed " + GetFriendlyName(record.PrefabId) + ".");
            }
            else
            {
                Vec3 position = GetWorldPosition(record);
                bool hasRuntimeCollision;
                GameEntity entity = InstantiatePlacedPrefab(
                    record.PrefabId,
                    CreateWorldFrame(position, record.Yaw),
                    out hasRuntimeCollision);
                if (entity == null)
                {
                    _undoActions.Push(action);
                    ShowStatus("Undo could not restore that native prop.");
                    return;
                }
                record.Entity = entity;
                record.HasRuntimeCollision = hasRuntimeCollision;
                int restoreIndex = Math.Max(0, Math.Min(action.ListIndex, _placed.Count));
                _placed.Insert(restoreIndex, record);
                if (!TrySaveDraft())
                {
                    _placed.RemoveAt(restoreIndex);
                    RemoveEntity(entity);
                    record.Entity = null;
                    _undoActions.Push(action);
                    ShowStatus("Undo was cancelled because the XML draft could not be saved.");
                    return;
                }
                _selectedEntity = record;
                ShowStatus("Undo: restored " + GetFriendlyName(record.PrefabId) + ".");
            }
            NotifyStateChanged();
        }

        internal void ResetBuilderLayout()
        {
            if (!_editing || !_loaded)
            {
                return;
            }
            if (_placed.Count == 0)
            {
                ShowStatus("The builder layout is already empty.");
                return;
            }

            List<PlacedRefugeEntity> removed = new List<PlacedRefugeEntity>(_placed);
            _placed.Clear();
            if (!TrySaveDraft())
            {
                _placed.AddRange(removed);
                ShowStatus("Reset was cancelled because the XML draft could not be saved.");
                return;
            }

            SetSelectionOutline(null);
            _hoveredEntity = null;
            _selectedEntity = null;
            _undoActions.Clear();
            for (int index = 0; index < removed.Count; index++)
            {
                RemoveEntity(removed[index].Entity);
            }
            ShowStatus("Builder layout reset. Your authored refuge layout was not changed.");
            NotifyStateChanged();
        }

        internal void UpdateMousePlacement(
            bool hasGroundPosition,
            Vec3 groundPosition,
            bool pointerOverUi,
            bool leftClicked)
        {
            if (!_editing || !_loaded)
            {
                RemovePreviewEntity();
                return;
            }

            if (_movingSelected)
            {
                UpdateSelectedMove(hasGroundPosition, groundPosition, pointerOverUi, leftClicked);
                return;
            }


            if (_selectionMode)
            {
                RemovePreviewEntity();
                PlacedRefugeEntity previousHovered = _hoveredEntity;
                _hoveredEntity = pointerOverUi ? null : FindPlacedEntityAlongAimRay();
                if (!ReferenceEquals(previousHovered, _hoveredEntity))
                {
                    SetSelectionOutline(_hoveredEntity);
                    NotifyStateChanged();
                }
                if (leftClicked && !pointerOverUi && _hoveredEntity != null)
                {
                    _selectedEntity = _hoveredEntity;
                    SetSelectionOutline(_selectedEntity);
                    NotifyStateChanged();
                    ShowStatus("Selected: " + GetFriendlyName(_selectedEntity.PrefabId) + ". Press M to move it or Delete to remove it.");
                }
                return;
            }

            if (!hasGroundPosition || pointerOverUi)
            {
                if (_previewEntity != null)
                {
                    _previewEntity.SetVisibilityExcludeParents(false);
                }
                if (!hasGroundPosition)
                {
                    _previewPosition = Vec3.Invalid;
                }
                return;
            }

            EnsurePreviewEntity();
            if (_previewEntity == null)
            {
                return;
            }

            Vec3 adjustedPosition = groundPosition + Vec3.Up * _heightOffset;
            _previewPosition = adjustedPosition;
            MatrixFrame previewFrame = CreateWorldFrame(adjustedPosition, _yaw);
            _previewEntity.SetGlobalFrame(previewFrame, true);
            _previewEntity.SetVisibilityExcludeParents(true);

            if (leftClicked)
            {
                PlaceSelectedPrefabAt(adjustedPosition);
            }
        }

        private void UpdateSelectedMove(
            bool hasGroundPosition,
            Vec3 groundPosition,
            bool pointerOverUi,
            bool leftClicked)
        {
            if (_selectedEntity == null || _selectedEntity.Entity == null)
            {
                _movingSelected = false;
                NotifyStateChanged();
                return;
            }
            if (!hasGroundPosition || pointerOverUi)
            {
                return;
            }

            Vec3 adjustedPosition = groundPosition + Vec3.Up * _heightOffset;
            _selectedEntity.Entity.SetGlobalFrame(CreateWorldFrame(adjustedPosition, _selectedEntity.Yaw), true);
            if (!leftClicked)
            {
                return;
            }

            SetRecordWorldPosition(_selectedEntity, adjustedPosition);
            if (!TrySaveDraft())
            {
                _selectedEntity.LocalX = _moveOriginalX;
                _selectedEntity.LocalY = _moveOriginalY;
                _selectedEntity.LocalZ = _moveOriginalZ;
                _selectedEntity.Entity.SetGlobalFrame(
                    CreateWorldFrame(GetWorldPosition(_selectedEntity), _selectedEntity.Yaw),
                    true);
                _movingSelected = false;
                NotifyStateChanged();
                ShowStatus("Move was cancelled because the XML draft could not be saved.");
                return;
            }

            _movingSelected = false;
            NotifyStateChanged();
            ShowStatus("Moved and saved: " + GetFriendlyName(_selectedEntity.PrefabId) + ".");
        }

        private void CancelSelectedMove()
        {
            if (!_movingSelected || _selectedEntity == null || _selectedEntity.Entity == null)
            {
                _movingSelected = false;
                return;
            }

            _selectedEntity.LocalX = _moveOriginalX;
            _selectedEntity.LocalY = _moveOriginalY;
            _selectedEntity.LocalZ = _moveOriginalZ;
            _selectedEntity.Entity.SetGlobalFrame(
                CreateWorldFrame(GetWorldPosition(_selectedEntity), _selectedEntity.Yaw),
                true);
            _movingSelected = false;
        }

        private void EnsurePreviewEntity()
        {
            string selectedPrefab = PlaceablePrefabs[_selectedPrefabIndex];
            if (_previewEntity != null
                && string.Equals(_previewPrefabId, selectedPrefab, StringComparison.Ordinal))
            {
                return;
            }

            RemovePreviewEntity();
            MatrixFrame frame = CreateWorldFrame(
                _previewPosition.IsValid ? _previewPosition : _anchorFrame.origin,
                _yaw);
            _previewEntity = InstantiateVisual(selectedPrefab, frame);
            _previewPrefabId = _previewEntity == null ? string.Empty : selectedPrefab;
        }

        private void RemovePreviewEntity()
        {
            if (_previewEntity != null)
            {
                RemoveEntity(_previewEntity);
                _previewEntity = null;
            }
            _previewPrefabId = string.Empty;
        }

        internal void ExportCombinedPrefab()
        {
            if (!_loaded)
            {
                ShowStatus("The refuge layout is not ready to export yet.");
                return;
            }

            List<RefugePrefabPlacement> basePlacements = _controller.GetRuntimeLayoutPlacements();
            if (basePlacements.Count == 0)
            {
                ShowStatus("Export stopped: no validated refuge base was found.");
                return;
            }

            string path = GetCombinedPrefabExportPath();
            string tempPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                    Encoding = new System.Text.UTF8Encoding(false)
                };
                using (XmlWriter writer = XmlWriter.Create(tempPath, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("prefabs");
                    writer.WriteStartElement("game_entity");
                    writer.WriteAttributeString("name", "rct_refuge_fort_combined");
                    writer.WriteAttributeString("old_prefab_name", string.Empty);
                    writer.WriteStartElement("tags");
                    writer.WriteStartElement("tag");
                    writer.WriteAttributeString("name", "rct_refuge_layout");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    WriteTransform(writer, 0f, 0f, 0f, 0f, 1f, 1f, 1f);
                    writer.WriteStartElement("children");

                    int itemIndex = 0;
                    for (int index = 0; index < basePlacements.Count; index++)
                    {
                        RefugePrefabPlacement placement = basePlacements[index];
                        WritePrefabReference(writer, placement.PrefabId, placement.Frame, itemIndex++);
                    }

                    for (int index = 0; index < _placed.Count; index++)
                    {
                        PlacedRefugeEntity placement = _placed[index];
                        writer.WriteStartElement("game_entity");
                        writer.WriteAttributeString("prefab", placement.PrefabId);
                        writer.WriteAttributeString("_index_", itemIndex++.ToString(CultureInfo.InvariantCulture));
                        WriteTransform(
                            writer,
                            placement.LocalX,
                            placement.LocalY,
                            placement.LocalZ,
                            placement.Yaw,
                            1f,
                            1f,
                            1f);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                XmlDocument validation = new XmlDocument();
                validation.Load(tempPath);
                XmlNodeList exportedItems = validation.SelectNodes(
                    "/prefabs/game_entity[@name='rct_refuge_fort_combined']/children/game_entity");
                int expectedCount = basePlacements.Count + _placed.Count;
                if (exportedItems == null || exportedItems.Count != expectedCount)
                {
                    throw new InvalidDataException("The combined prefab failed its item-count validation.");
                }

                ReplaceFileAtomically(tempPath, path);
                ExportNavmeshBakeChecklist(path, basePlacements.Count);
                ShowStatus(
                    "Combined refuge prefab exported: " + expectedCount.ToString(CultureInfo.InvariantCulture)
                    + " objects. Collision and navmesh checklist exported alongside it.");
                Diagnostics.Info("Exported combined refuge prefab to " + path + ".");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Combined refuge prefab export failed safely.", exception);
                TryDeleteTemporaryFile(tempPath);
                ShowStatus("Combined prefab export failed; the original prefab and layout draft were not changed.");
            }
        }

        private void WritePrefabReference(
            XmlWriter writer,
            string prefabId,
            MatrixFrame worldFrame,
            int itemIndex)
        {
            Vec3 delta = worldFrame.origin - _anchorFrame.origin;
            float localX = Vec3.DotProduct(delta, _anchorFrame.rotation.s);
            float localY = Vec3.DotProduct(delta, _anchorFrame.rotation.f);
            float localZ = worldFrame.origin.z - _anchorFrame.origin.z;
            Vec3 worldForward = worldFrame.rotation.f;
            float forwardScale = worldForward.Length;
            if (forwardScale > 0.0001f)
            {
                worldForward /= forwardScale;
            }
            float yaw = (float)Math.Atan2(
                Vec3.DotProduct(worldForward, _anchorFrame.rotation.s),
                Vec3.DotProduct(worldForward, _anchorFrame.rotation.f));

            writer.WriteStartElement("game_entity");
            writer.WriteAttributeString("prefab", prefabId);
            writer.WriteAttributeString("_index_", itemIndex.ToString(CultureInfo.InvariantCulture));
            WriteTransform(
                writer,
                localX,
                localY,
                localZ,
                yaw,
                worldFrame.rotation.s.Length,
                forwardScale,
                worldFrame.rotation.u.Length);
            writer.WriteEndElement();
        }

        private static void WriteTransform(
            XmlWriter writer,
            float x,
            float y,
            float z,
            float yaw,
            float scaleX,
            float scaleY,
            float scaleZ)
        {
            writer.WriteStartElement("transform");
            writer.WriteAttributeString(
                "position",
                FormatFloat(x) + ", " + FormatFloat(y) + ", " + FormatFloat(z));
            writer.WriteAttributeString(
                "rotation_euler",
                "0.0000, 0.0000, " + FormatFloat(yaw));
            if (Math.Abs(scaleX - 1f) > 0.0001f
                || Math.Abs(scaleY - 1f) > 0.0001f
                || Math.Abs(scaleZ - 1f) > 0.0001f)
            {
                writer.WriteAttributeString(
                    "scale",
                    FormatFloat(scaleX) + ", " + FormatFloat(scaleY) + ", " + FormatFloat(scaleZ));
            }
            writer.WriteEndElement();
        }

        private static void ReplaceFileAtomically(string tempPath, string path)
        {
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, path + ".bak", true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        private static void TryDeleteTemporaryFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void ShowSelection()
        {
            ShowStatus(
                "Selected: " + PlaceablePrefabs[_selectedPrefabIndex]
                + " | rotation " + (_yaw * 57.29578f).ToString("F0", CultureInfo.InvariantCulture) + " degrees");
        }

        private void PlaceSelectedPrefab()
        {
            Vec3 rayStart;
            Vec3 rayEnd;
            Vec3 hitPosition;
            if (!TryGetAimRay(out rayStart, out rayEnd)
                || !TryGetRayHit(rayStart, rayEnd, out hitPosition))
            {
                ShowStatus("No valid ground or object is under the crosshair.");
                return;
            }

            PlaceSelectedPrefabAt(hitPosition);
        }

        private void PlaceSelectedPrefabAt(Vec3 hitPosition)
        {
            MatrixFrame frame = CreateWorldFrame(hitPosition, _yaw);
            bool hasRuntimeCollision;
            GameEntity entity = InstantiatePlacedPrefab(
                PlaceablePrefabs[_selectedPrefabIndex],
                frame,
                out hasRuntimeCollision);
            if (entity == null)
            {
                ShowStatus("That native prop could not be placed.");
                return;
            }

            PlacedRefugeEntity record = CreateRecord(
                Guid.NewGuid().ToString("N"),
                PlaceablePrefabs[_selectedPrefabIndex],
                hitPosition,
                _yaw,
                entity,
                hasRuntimeCollision);
            _placed.Add(record);
            if (!TrySaveDraft())
            {
                _placed.Remove(record);
                RemoveEntity(entity);
                ShowStatus("Placement was cancelled because the XML draft could not be saved.");
                return;
            }

            _undoActions.Push(new BuilderUndoAction(BuilderUndoKind.Placement, record, _placed.Count - 1));
            ShowStatus("Placed and saved: " + record.PrefabId);
            NotifyStateChanged();
        }

        private void DeleteTargetedPrefab()
        {
            Vec3 rayStart;
            Vec3 rayEnd;
            if (!TryGetAimRay(out rayStart, out rayEnd))
            {
                return;
            }

            Vec3 ray = rayEnd - rayStart;
            float rayLength = ray.Length;
            if (rayLength < 0.001f)
            {
                return;
            }

            Vec3 direction = ray / rayLength;
            PlacedRefugeEntity best = null;
            float bestDistanceSquared = SelectionRadius * SelectionRadius;
            for (int index = 0; index < _placed.Count; index++)
            {
                PlacedRefugeEntity candidate = _placed[index];
                if (candidate.Entity == null)
                {
                    continue;
                }

                Vec3 fromStart = candidate.Entity.GlobalPosition - rayStart;
                float alongRay = Vec3.DotProduct(fromStart, direction);
                if (alongRay < 0f || alongRay > rayLength)
                {
                    continue;
                }

                Vec3 nearestPoint = rayStart + direction * alongRay;
                float distanceSquared = (candidate.Entity.GlobalPosition - nearestPoint).LengthSquared;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    best = candidate;
                }
            }

            if (best == null)
            {
                ShowStatus("Aim closer to a prop placed by Refuge Builder.");
                return;
            }


            DeletePlacedEntity(best);
        }

        private PlacedRefugeEntity FindNearestPlacedEntity(Vec3 position, float radius)
        {
            PlacedRefugeEntity nearest = null;
            float nearestDistanceSquared = radius * radius;
            for (int index = 0; index < _placed.Count; index++)
            {
                PlacedRefugeEntity candidate = _placed[index];
                if (candidate.Entity == null)
                {
                    continue;
                }
                float distanceSquared = (candidate.Entity.GlobalPosition - position).LengthSquared;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }
            return nearest;
        }

        private void DeletePlacedEntity(PlacedRefugeEntity best)
        {
            if (best == null)
            {
                return;
            }

            int oldIndex = _placed.IndexOf(best);
            if (oldIndex < 0)
            {
                return;
            }
            _placed.RemoveAt(oldIndex);
            if (!TrySaveDraft())
            {
                _placed.Insert(oldIndex, best);
                ShowStatus("Deletion was cancelled because the XML draft could not be saved.");
                return;
            }

            RemoveEntity(best.Entity);
            if (ReferenceEquals(_hoveredEntity, best)) _hoveredEntity = null;
            if (ReferenceEquals(_selectedEntity, best)) _selectedEntity = null;
            if (ReferenceEquals(_outlinedEntity, best)) SetSelectionOutline(null);
            _undoActions.Push(new BuilderUndoAction(BuilderUndoKind.Deletion, best, oldIndex));
            ShowStatus("Removed from scene and XML: " + best.PrefabId);
            NotifyStateChanged();
        }

        private static string GetFriendlyName(string prefabId)
        {
            for (int index = 0; index < PlaceablePrefabs.Length; index++)
            {
                if (string.Equals(PlaceablePrefabs[index], prefabId, StringComparison.Ordinal))
                {
                    return PlaceableNames[index];
                }
            }
            return prefabId ?? string.Empty;
        }

        private static int GetCategoryCount(BuilderCategory category)
        {
            int count = 0;
            for (int index = 0; index < PrefabCategories.Length; index++)
            {
                if (PrefabCategories[index] == category) count++;
            }
            return count;
        }

        private static int GetCategoryPosition(int prefabIndex, BuilderCategory category)
        {
            int position = 0;
            for (int index = 0; index <= prefabIndex; index++)
            {
                if (PrefabCategories[index] == category) position++;
            }
            return Math.Max(1, position);
        }

        private static string GetCategoryName(BuilderCategory category)
        {
            switch (category)
            {
                case BuilderCategory.Wood: return "WOOD";
                case BuilderCategory.Stone: return "STONE";
                default: return "MISC";
            }
        }

        private void SetSelectionOutline(PlacedRefugeEntity item)
        {
            if (ReferenceEquals(_outlinedEntity, item))
            {
                return;
            }
            if (_outlinedEntity != null && _outlinedEntity.Entity != null)
            {
                ApplyContour(_outlinedEntity.Entity, null);
            }
            _outlinedEntity = item;
            if (_outlinedEntity != null && _outlinedEntity.Entity != null)
            {
                // Bannerlord's contour effect is the native white selection
                // outline used for highlighted world objects. Prefab roots
                // often have no mesh, so apply it to every child entity too.
                ApplyContour(_outlinedEntity.Entity, 0xFFFFFFFFu);
            }
        }

        private void SetPlayerEditingLock(bool editing)
        {
            if (editing)
            {
                Agent player = Mission == null ? null : Mission.MainAgent;
                if (player == null || _frozenPlayerAgent != null)
                {
                    return;
                }
                _frozenPlayerAgent = player;
                _previousPlayerController = player.Controller;
                player.MovementInputVector = Vec2.Zero;
                player.Controller = AgentControllerType.AI;
                player.SetIsAIPaused(true);
                return;
            }

            if (_frozenPlayerAgent == null)
            {
                return;
            }
            _frozenPlayerAgent.SetIsAIPaused(false);
            _frozenPlayerAgent.Controller = _previousPlayerController;
            _frozenPlayerAgent = null;
        }

        private static void ApplyContour(GameEntity root, uint? color)
        {
            foreach (GameEntity entity in root.GetEntityAndChildren())
            {
                entity.SetContourColor(color, color.HasValue);
            }
        }

        private PlacedRefugeEntity FindPlacedEntityAlongAimRay()
        {
            Vec3 rayStart;
            Vec3 rayEnd;
            if (!TryGetAimRay(out rayStart, out rayEnd))
            {
                return null;
            }
            Vec3 ray = rayEnd - rayStart;
            float rayLength = ray.Length;
            if (rayLength < 0.001f)
            {
                return null;
            }

            Vec3 direction = ray / rayLength;
            PlacedRefugeEntity nearest = null;
            float nearestDistanceSquared = SelectionRadius * SelectionRadius;
            for (int index = 0; index < _placed.Count; index++)
            {
                PlacedRefugeEntity candidate = _placed[index];
                if (candidate.Entity == null)
                {
                    continue;
                }
                Vec3 fromStart = candidate.Entity.GlobalPosition - rayStart;
                float alongRay = Vec3.DotProduct(fromStart, direction);
                if (alongRay < 0f || alongRay > rayLength)
                {
                    continue;
                }
                Vec3 nearestPoint = rayStart + direction * alongRay;
                float distanceSquared = (candidate.Entity.GlobalPosition - nearestPoint).LengthSquared;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private bool TryGetAimRay(out Vec3 rayStart, out Vec3 rayEnd)
        {
            // Always use Bannerlord's active mission camera. This works in the
            // ordinary third-person view and also follows an optional RTS
            // Camera/free-camera view without taking a dependency on that mod.
            MatrixFrame camera = Mission.GetCameraFrame();
            rayStart = camera.origin;
            rayEnd = rayStart + camera.rotation.f * PlacementDistance;
            return true;
        }

        private bool TryGetRayHit(Vec3 rayStart, Vec3 rayEnd, out Vec3 hitPosition)
        {
            float distance = 0f;
            hitPosition = Vec3.Invalid;
            WeakGameEntity hitEntity = default(WeakGameEntity);
            Mission.Scene.RayCastForClosestEntityOrTerrain(
                rayStart,
                rayEnd,
                out distance,
                out hitPosition,
                out hitEntity,
                0.01f,
                (BodyFlags)79617);
            return hitPosition.IsValid;
        }

        private MatrixFrame CreateWorldFrame(Vec3 position, float yaw)
        {
            Vec3 forward = _anchorFrame.rotation.f * (float)Math.Cos(yaw)
                + _anchorFrame.rotation.s * (float)Math.Sin(yaw);
            forward.z = 0f;
            forward.Normalize();
            MatrixFrame frame = MatrixFrame.Identity;
            frame.rotation = Mat3.CreateMat3WithForward(forward);
            frame.origin = position;
            return frame;
        }

        private PlacedRefugeEntity CreateRecord(
            string id,
            string prefabId,
            Vec3 worldPosition,
            float yaw,
            GameEntity entity,
            bool hasRuntimeCollision)
        {
            Vec3 delta = worldPosition - _anchorFrame.origin;
            return new PlacedRefugeEntity
            {
                Id = id,
                PrefabId = prefabId,
                LocalX = Vec3.DotProduct(delta, _anchorFrame.rotation.s),
                LocalY = Vec3.DotProduct(delta, _anchorFrame.rotation.f),
                LocalZ = worldPosition.z - _anchorFrame.origin.z,
                Yaw = yaw,
                Entity = entity,
                HasRuntimeCollision = hasRuntimeCollision
            };
        }

        private Vec3 GetWorldPosition(PlacedRefugeEntity record)
        {
            return _anchorFrame.origin
                + _anchorFrame.rotation.s * record.LocalX
                + _anchorFrame.rotation.f * record.LocalY
                + Vec3.Up * record.LocalZ;
        }

        private void SetRecordWorldPosition(PlacedRefugeEntity record, Vec3 worldPosition)
        {
            Vec3 delta = worldPosition - _anchorFrame.origin;
            record.LocalX = Vec3.DotProduct(delta, _anchorFrame.rotation.s);
            record.LocalY = Vec3.DotProduct(delta, _anchorFrame.rotation.f);
            record.LocalZ = worldPosition.z - _anchorFrame.origin.z;
        }

        private void LoadDraft()
        {
            _undoActions.Clear();
            if (!_editingAllowed)
            {
                // Defense in depth: fixed authored scenes must never receive
                // the old standalone builder items, even if this behavior is
                // accidentally registered by a future mission change.
                Diagnostics.Info("Refuge builder draft was not loaded because this is a fixed authored scene.");
                return;
            }

            if (string.Equals(_sceneId, "battle_terrain_biome_130", StringComparison.Ordinal))
            {
                // The authored rct_refuge_fort is already placed on this
                // terrain.  A prior freeform builder draft contains separate
                // stair placements and must not be layered over the fort.
                // Keep the XML intact for later editing/export; simply start
                // this authored-fort visit with an empty builder overlay.
                Diagnostics.Info("Preserved refuge builder draft; it was not loaded over rct_refuge_fort.");
                return;
            }

            string path = GetDraftPath();
            if (!File.Exists(path))
            {
                TrySaveDraft();
                return;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlNodeList nodes = document.SelectNodes("/refuge_layout/item");
                if (nodes == null)
                {
                    return;
                }

                foreach (XmlNode node in nodes)
                {
                    string prefabId = GetAttribute(node, "prefab");
                    if (!IsAllowedPrefab(prefabId))
                    {
                        continue;
                    }

                    PlacedRefugeEntity record = new PlacedRefugeEntity
                    {
                        Id = GetAttribute(node, "id"),
                        PrefabId = prefabId,
                        LocalX = ParseFloat(node, "x"),
                        LocalY = ParseFloat(node, "y"),
                        LocalZ = ParseFloat(node, "z"),
                        Yaw = ParseFloat(node, "yaw")
                    };
                    Vec3 position = GetWorldPosition(record);
                    record.Entity = InstantiatePlacedPrefab(
                        prefabId,
                        CreateWorldFrame(position, record.Yaw),
                        out record.HasRuntimeCollision);
                    if (record.Entity != null)
                    {
                        _placed.Add(record);
                    }
                }

                Diagnostics.Info("Loaded " + _placed.Count + " refuge builder items from " + path + ".");
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge builder draft could not be loaded safely.", exception);
                ShowStatus("The refuge layout draft could not be loaded; the scene remains usable.");
            }
        }

        private bool TrySaveDraft()
        {
            string path = GetDraftPath();
            string tempPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                    NewLineOnAttributes = false
                };
                using (XmlWriter writer = XmlWriter.Create(tempPath, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("refuge_layout");
                    writer.WriteAttributeString("version", "1");
                    writer.WriteAttributeString("source_scene", _sceneId);
                    for (int index = 0; index < _placed.Count; index++)
                    {
                        PlacedRefugeEntity item = _placed[index];
                        writer.WriteStartElement("item");
                        writer.WriteAttributeString("id", item.Id);
                        writer.WriteAttributeString("prefab", item.PrefabId);
                        writer.WriteAttributeString("x", FormatFloat(item.LocalX));
                        writer.WriteAttributeString("y", FormatFloat(item.LocalY));
                        writer.WriteAttributeString("z", FormatFloat(item.LocalZ));
                        writer.WriteAttributeString("yaw", FormatFloat(item.Yaw));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                if (File.Exists(path))
                {
                    string backupPath = path + ".bak";
                    File.Replace(tempPath, path, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
                return true;
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge builder draft could not be saved.", exception);
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }
                return false;
            }
        }

        private static GameEntity InstantiateVisual(string prefabId, MatrixFrame frame)
        {
            try
            {
                return GameEntity.Instantiate(Mission.Current.Scene, prefabId, frame, false);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Refuge builder could not instantiate " + prefabId + ".", exception);
                return null;
            }
        }

        private static GameEntity InstantiatePlacedPrefab(
            string prefabId,
            MatrixFrame frame,
            out bool hasRuntimeCollision)
        {
            hasRuntimeCollision = false;
            try
            {
                GameEntity physicalEntity = GameEntity.Instantiate(
                    Mission.Current.Scene,
                    prefabId,
                    callScriptCallbacks: false,
                    createPhysics: true,
                    scriptInclusingTag: string.Empty);
                if (physicalEntity != null)
                {
                    physicalEntity.SetGlobalFrame(frame, true);
                    hasRuntimeCollision = true;
                    return physicalEntity;
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Info("Builder collision fallback for " + prefabId + ": " + exception.Message);
            }

            return InstantiateVisual(prefabId, frame);
        }

        private static string GetCollisionLabel(PlacedRefugeEntity item)
        {
            return item != null && item.HasRuntimeCollision
                ? "COLLISION ENABLED"
                : "VISUAL ONLY - BAKE/REPLACE IN SCENE EDITOR";
        }

        private static void RemoveEntity(GameEntity entity)
        {
            if (entity == null)
            {
                return;
            }
            entity.RemoveAllChildren();
            entity.Remove(0);
        }

        private static bool IsAllowedPrefab(string prefabId)
        {
            for (int index = 0; index < PlaceablePrefabs.Length; index++)
            {
                if (string.Equals(PlaceablePrefabs[index], prefabId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetDraftPath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord",
                "Configs",
                "AgesOfCalradia",
                "RefugeLayoutDraft.xml");
        }

        private static string GetCombinedPrefabExportPath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord",
                "Configs",
                "AgesOfCalradia",
                "CombinedPrefab",
                "rct_refuge_fort_combined.xml");
        }

        private void ExportNavmeshBakeChecklist(string prefabPath, int basePlacementCount)
        {
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            string checklistPath = System.IO.Path.Combine(directory, "rct_refuge_navmesh_bake_checklist.txt");
            string temporaryPath = checklistPath + ".tmp";
            try
            {
                using (StreamWriter writer = new StreamWriter(temporaryPath, false, new System.Text.UTF8Encoding(false)))
                {
                    writer.WriteLine("REFUGE NAVMESH + COLLISION BAKE CHECKLIST");
                    writer.WriteLine("Scene: " + _sceneId);
                    writer.WriteLine("Combined prefab: " + System.IO.Path.GetFileName(prefabPath));
                    writer.WriteLine("Base refuge pieces: " + basePlacementCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("Builder placements: " + _placed.Count.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine();
                    writer.WriteLine("SCENE EDITOR STEPS");
                    writer.WriteLine("1. Import/place the combined prefab in the target refuge scene.");
                    writer.WriteLine("2. Confirm every wall, tower, stair, and building has collision.");
                    writer.WriteLine("3. Paint navmesh over courtyard ground, stairs, tower platforms, and gate approaches.");
                    writer.WriteLine("4. Remove navmesh from walls, tents, props, and blocked ground.");
                    writer.WriteLine("5. Save the scene and test player + NPC routes before using it in campaign.");
                    writer.WriteLine();
                    writer.WriteLine("BUILDER PLACEMENTS");
                    for (int index = 0; index < _placed.Count; index++)
                    {
                        PlacedRefugeEntity item = _placed[index];
                        writer.WriteLine("- " + item.PrefabId + " | " + GetCollisionLabel(item)
                            + " | local " + FormatFloat(item.LocalX) + ", "
                            + FormatFloat(item.LocalY) + ", " + FormatFloat(item.LocalZ));
                    }
                }
                ReplaceFileAtomically(temporaryPath, checklistPath);
            }
            catch (Exception exception)
            {
                Diagnostics.Error("Navmesh bake checklist export failed safely.", exception);
                TryDeleteTemporaryFile(temporaryPath);
                throw;
            }
        }

        private static string GetAttribute(XmlNode node, string name)
        {
            XmlAttribute attribute = node.Attributes == null ? null : node.Attributes[name];
            return attribute == null ? string.Empty : attribute.Value;
        }

        private static float ParseFloat(XmlNode node, string name)
        {
            float value;
            return float.TryParse(
                GetAttribute(node, name),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
                ? value
                : 0f;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private static void ShowStatus(string message)
        {
            InformationManager.DisplayMessage(new InformationMessage(message));
        }

        private sealed class PlacedRefugeEntity
        {
            internal string Id;
            internal string PrefabId;
            internal float LocalX;
            internal float LocalY;
            internal float LocalZ;
            internal float Yaw;
            internal GameEntity Entity;
            internal bool HasRuntimeCollision;
        }

        private enum BuilderUndoKind
        {
            Placement,
            Deletion
        }

        private enum BuilderCategory
        {
            Wood,
            Stone,
            Misc
        }

        private sealed class BuilderUndoAction
        {
            internal readonly BuilderUndoKind Kind;
            internal readonly PlacedRefugeEntity Record;
            internal readonly int ListIndex;

            internal BuilderUndoAction(BuilderUndoKind kind, PlacedRefugeEntity record, int listIndex)
            {
                Kind = kind;
                Record = record;
                ListIndex = listIndex;
            }
        }
    }
}
