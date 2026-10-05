using System;
using System.Collections.Generic;
using System.Linq;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Presentation;
using SiliconSandbox.Persistence;
using UnityEngine;

namespace SiliconSandbox.Interaction
{
    // First playable interaction shell. Every authored action goes through
    // OneBitWorldSession; ray hits supply IDs but never imply a connection.
    public sealed class OneBitWorldInteraction : MonoBehaviour
    {
        private const float ReachCells = 15f;
        private OneBitWorldSession session;
        private OneBitPlayerInventory inventory;
        private CreativeCameraController player;
        private Camera viewCamera;
        private WorldSelectablePart hovered;
        private RaycastHit hit;
        private bool hasHit;
        private JoinMember? wireStart;
        private GameObject ghostRoot;
        private GameObject rotationRoot;
        private OneBitRotationPreview rotationPreview;
        private GameObject selectionRoot;
        private GridCell? selectionFirst;
        private GridCell? selectionSecond;
        private OneBitPackageDraft packageDraft;
        private string packageName = "Module";
        private string packageError = "";
        private readonly List<Renderer> ghostRenderers = new List<Renderer>();
        private GridCell ghostCell;
        private GridOrientation ghostOrientation;
        private string ghostType;
        private PlacedOneBitModuleInstance ghostModule;
        private string ghostModuleName;
        private bool ghostValid;
        private bool ghostCached;
        private float invalidUntil;
        private bool inventoryOpen;
        private bool configureOpen;
        private bool inspectOpen;
        private bool pauseMenuOpen;
        private bool hudVisible = true;
        private Guid configureSourceId;
        private LogicBit configureValue;
        private bool configureInitialOn;
        private WorldPartKind inspectedKind;
        private Guid inspectedOwner;
        private Guid inspectedPart;
        private string frequencyText = "10";
        private double fractionalPicoseconds;
        private ulong observedRevision = ulong.MaxValue;

        public OneBitWorldSession Session => session;
        public OneBitPlayerInventory Inventory => inventory;
        public WorldSelectablePart HoveredPart => hovered;

        public void Attach(OneBitWorldSession activeSession,
            CreativeCameraController cameraController,
            OneBitPlayerInventory playerInventory = null)
        {
            ClearRotationPreview();
            if (selectionRoot != null)
            {
                selectionRoot.SetActive(false);
                Destroy(selectionRoot);
                selectionRoot = null;
            }
            selectionFirst = null;
            selectionSecond = null;
            packageDraft = null;
            packageError = "";
            wireStart = null;
            hovered = null;
            hasHit = false;
            inventoryOpen = false;
            configureOpen = false;
            inspectOpen = false;
            pauseMenuOpen = false;
            observedRevision = ulong.MaxValue;
            fractionalPicoseconds = 0d;
            HideGhost();
            session = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
            inventory = playerInventory ?? OneBitPlayerInventory.NewFreeplay();
            player = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
            viewCamera = player.CameraPivot.GetComponent<Camera>();
            if (viewCamera == null) throw new ArgumentException("Player camera is missing.");
            frequencyText = session.Scheduler.FrequencyHz;
            player.SetInterfaceOpen(false);
        }

        private void Update()
        {
            if (session == null) return;
            AdvancePlayback();
            if (observedRevision != session.Revision)
            {
                observedRevision = session.Revision;
                ghostCached = false;
            }
            if (Input.GetKeyDown(KeyCode.F1)) hudVisible = !hudVisible;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Escape();
                return;
            }
            if (inventoryOpen && Input.GetKeyDown(KeyCode.E))
            {
                inventoryOpen = false;
                player.SetInterfaceOpen(false);
                return;
            }
            if (configureOpen || inventoryOpen || pauseMenuOpen ||
                packageDraft != null) return;
            if (Input.GetKeyDown(KeyCode.E))
            {
                inventoryOpen = true;
                player.SetInterfaceOpen(true);
                HideGhost();
                return;
            }

            for (var i = 0; i < 9; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    SelectSlot(i);
            TargetAtCrosshair();
            if (rotationPreview != null)
            {
                HideGhost();
                if (Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    var ready = rotationPreview;
                    TryEdit(() => session.ConfirmRotation(ready));
                    ClearRotationPreview();
                }
                else if (Input.GetKeyDown(KeyCode.Z) ||
                    Input.GetKeyDown(KeyCode.X))
                    BeginRotation(rotationPreview.ObjectId,
                        Input.GetKeyDown(KeyCode.Z));
                return;
            }
            if (Input.GetKeyDown(KeyCode.R) && SelectionCell(out var first))
            {
                selectionFirst = first;
                selectionSecond = null;
                DrawSelectionPreview();
            }
            if (Input.GetKeyDown(KeyCode.T) && SelectionCell(out var second))
            {
                selectionSecond = second;
                DrawSelectionPreview();
            }
            if ((Input.GetKeyDown(KeyCode.Return) ||
                 Input.GetKeyDown(KeyCode.KeypadEnter)) &&
                selectionFirst.HasValue && selectionSecond.HasValue)
            {
                try
                {
                    var region = new CellRegion(selectionFirst.Value,
                        selectionSecond.Value);
                    packageDraft = session.PreviewPackage(region, packageName);
                    packageError = "";
                    player.SetInterfaceOpen(true);
                    HideGhost();
                }
                catch (ArgumentException exception)
                { packageError = exception.Message; InvalidAction(); }
                catch (NotSupportedException exception)
                { packageError = exception.Message; InvalidAction(); }
                return;
            }
            UpdateGhost();

            if (hovered != null &&
                (hovered.Kind == WorldPartKind.ComponentBody ||
                 hovered.Kind == WorldPartKind.ModuleBody) &&
                (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.X)))
            {
                BeginRotation(hovered.OwnerId, Input.GetKeyDown(KeyCode.Z));
                return;
            }

            if (Input.GetKeyDown(KeyCode.U))
                TryEdit(() => { if (!session.TryUndo()) InvalidAction(); });
            if (Input.GetKeyDown(KeyCode.J))
                TryEdit(() => { if (!session.TryRedo()) InvalidAction(); });

            if (Input.GetKeyDown(KeyCode.P))
            {
                if (session.Scheduler.ClockRunning) session.Scheduler.StopClock();
                else session.Scheduler.StartClock();
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket) && session.Scheduler.IsPaused)
                session.Scheduler.StepClockEdge();
            if (Input.GetKeyDown(KeyCode.RightBracket) && session.Scheduler.IsPaused)
                session.Scheduler.StepClockCycle();
            if (Input.GetKeyDown(KeyCode.I) && hovered != null)
            {
                inspectedKind = hovered.Kind;
                inspectedOwner = hovered.OwnerId;
                inspectedPart = hovered.PartId;
                inspectOpen = true;
            }
            if (Input.GetKeyDown(KeyCode.C) && hovered != null &&
                hovered.Kind == WorldPartKind.ComponentBody &&
                IsSource(hovered.OwnerId))
                OpenSourceConfigure(hovered.OwnerId);
            if (Input.GetMouseButtonDown(0) && hovered != null &&
                hovered.Kind == WorldPartKind.ConnectorSpan)
                TryEdit(() => session.BreakSpan(hovered.OwnerId, hovered.PartId));
            if (Input.GetMouseButtonDown(1)) RightClick();
        }

        private void AdvancePlayback()
        {
            if (session.Scheduler.IsPaused) return;
            var amount = Time.unscaledDeltaTime *
                (double)SiliconSandbox.Simulation.SimulationTime.PicosecondsPerSecond +
                fractionalPicoseconds;
            if (amount < 1d) { fractionalPicoseconds = amount; return; }
            var whole = (long)Math.Floor(amount);
            fractionalPicoseconds = amount - whole;
            var target = session.Scheduler.Now.AddPicoseconds(
                new System.Numerics.BigInteger(whole));
            session.Scheduler.AdvanceUntil(target);
        }

        private void TargetAtCrosshair()
        {
            hovered = null;
            hasHit = Physics.Raycast(viewCamera.transform.position,
                viewCamera.transform.forward, out hit, ReachCells);
            if (hasHit)
                hovered = hit.collider.GetComponent<WorldSelectablePart>();
        }

        private void RightClick()
        {
            if (hovered != null && hovered.Kind == WorldPartKind.ComponentBody &&
                IsSource(hovered.OwnerId))
            {
                TryEdit(() => session.ToggleSource(hovered.OwnerId));
                return;
            }
            if (SelectedCatalogId() == OneBitCatalogItemIds.Wire)
            {
                if (!wireStart.HasValue)
                {
                    if (hovered == null || !IsPinKind(hovered.Kind))
                    {
                        InvalidAction();
                        return;
                    }
                    var pin = PickedPin(hovered);
                    if (PinOccupied(pin)) { InvalidAction(); return; }
                    wireStart = pin;
                }
                else
                {
                    var start = wireStart.Value;
                    if (hovered == null) InvalidAction();
                    else if (IsPinKind(hovered.Kind))
                    {
                        var pin = PickedPin(hovered);
                        TryEdit(() => session.ConnectPins(start, pin));
                    }
                    else if (hovered.Kind == WorldPartKind.ConnectorNode)
                    {
                        var target = JoinMember.ConnectorNode(
                            hovered.OwnerId, hovered.PartId);
                        TryEdit(() => session.ConnectPinToNode(start, target));
                    }
                    else InvalidAction();
                    wireStart = null;
                }
                return;
            }
            if (SelectedCatalogId() == OneBitCatalogItemIds.WorldClockLink)
            {
                if (hovered == null || !IsPinKind(hovered.Kind) ||
                    !IsClockTarget(hovered))
                {
                    InvalidAction();
                    return;
                }
                if (hovered.Kind == WorldPartKind.ModulePort)
                    TryEdit(() => session.AttachWorldClockPort(
                        hovered.OwnerId, hovered.PartId));
                else TryEdit(() => session.AttachWorldClockPin(hovered.OwnerId));
                return;
            }
            var typeId = SelectedComponentType();
            var selectedModule = SelectedModuleVersion();
            if ((typeId == null && selectedModule == null) ||
                !ghostCached || !ghostValid)
            {
                InvalidAction();
                return;
            }
            var cell = ghostCell;
            var orientation = ghostOrientation;
            if (selectedModule != null)
            {
                var name = ghostModuleName;
                TryEdit(() => session.PlaceModule(selectedModule, name,
                    cell, orientation));
            }
            else TryEdit(() => session.PlaceComponent(typeId, cell, orientation));
            ghostCached = false;
        }

        private bool PinOccupied(JoinMember pin)
        {
            foreach (var join in session.Design.Topology.Joins)
                foreach (var member in join.Members)
                    if (member.Equals(pin)) return true;
            return false;
        }

        private static bool IsPinKind(WorldPartKind kind) =>
            kind == WorldPartKind.ComponentPin ||
            kind == WorldPartKind.ModulePort;

        private static JoinMember PickedPin(WorldSelectablePart part) =>
            part.Kind == WorldPartKind.ModulePort
                ? JoinMember.ModulePortBit(part.OwnerId, part.PartId, 0)
                : JoinMember.ComponentPin(part.OwnerId, part.PartId);

        private bool IsClockTarget(WorldSelectablePart part)
        {
            if (part.Kind == WorldPartKind.ComponentPin)
                return IsSrClockPin(part.OwnerId, part.PartId);
            foreach (var module in session.Design.Modules)
                if (module.Id == part.OwnerId)
                    foreach (var port in module.InterfacePorts)
                        if (port.Id == part.PartId)
                            return port.Name == "CLK" &&
                                port.Direction != OneBitPortDirection.Output;
            return false;
        }

        private void TryEdit(Action action)
        {
            try { action(); }
            catch (ArgumentException) { InvalidAction(); }
            catch (InvalidOperationException) { InvalidAction(); }
            catch (FormatException) { InvalidAction(); }
            catch (NotSupportedException) { InvalidAction(); }
        }

        private void InvalidAction()
        {
            invalidUntil = Time.unscaledTime + 0.18f;
            if (hovered != null) hovered.FlashInvalid();
        }

        private void UpdateGhost()
        {
            var typeId = SelectedComponentType();
            var moduleVersion = SelectedModuleVersion();
            if ((typeId == null && moduleVersion == null) ||
                !hasHit || !PlacementCell(out var cell))
            {
                HideGhost();
                return;
            }
            var selectedId = moduleVersion == null ? typeId :
                "module:" + moduleVersion.VersionId.ToString("D");
            var orientation = InitialOrientation(cell);
            if (!ghostCached || !cell.Equals(ghostCell) ||
                !orientation.Equals(ghostOrientation) || ghostType != selectedId)
            {
                ghostCell = cell;
                ghostOrientation = orientation;
                ghostType = selectedId;
                ghostCached = true;
                ghostModule = null;
                try
                {
                    if (moduleVersion == null)
                        OneBitWorldEdits.PlaceComponent(session.Design, typeId,
                            cell, orientation);
                    else
                    {
                        ghostModuleName = NextModuleName(moduleVersion.Name);
                        var ports = new List<OneBitPortInterface>();
                        foreach (var port in moduleVersion.Ports)
                            ports.Add(new OneBitPortInterface(port.Id,
                                port.Name, port.Direction, port.LocalCell,
                                port.PointQ));
                        ghostModule = new PlacedOneBitModuleInstance(
                            Guid.NewGuid(), Guid.NewGuid(),
                            moduleVersion.FamilyId, moduleVersion.VersionId,
                            ghostModuleName, cell, orientation,
                            moduleVersion.SizeCells, ports);
                        OneBitWorldEdits.PlaceModule(session.Design,
                            moduleVersion, ghostModuleName, cell, orientation);
                    }
                    ghostValid = true;
                }
                catch (ArgumentException) { ghostValid = false; }
                catch (NotSupportedException) { ghostValid = false; }
                DrawGhost();
            }
            if (ghostRoot != null) ghostRoot.SetActive(true);
            var color = ghostValid && Time.unscaledTime >= invalidUntil
                ? new Color(0.2f, 0.9f, 0.55f, 0.42f)
                : new Color(1f, 0.15f, 0.15f, 0.48f);
            foreach (var renderer in ghostRenderers)
                renderer.material.color = color;
        }

        private void DrawGhost()
        {
            if (ghostRoot != null) Destroy(ghostRoot);
            ghostRoot = new GameObject("Placement preview");
            ghostRenderers.Clear();
            if (ghostModule != null)
            {
                foreach (var cell in ghostModule.OccupiedCells())
                    AddGhostPrimitive(PrimitiveType.Cube,
                        new Vector3(cell.X + 0.5f, cell.Y + 0.5f,
                            cell.Z + 0.5f), Vector3.one * 0.82f);
                foreach (var port in ghostModule.BuildPortBits())
                    AddGhostPrimitive(PrimitiveType.Sphere,
                        PointPosition(port.Cell, port.PointQ),
                        Vector3.one * 0.18f);
                return;
            }
            AddGhostPrimitive(PrimitiveType.Cube,
                new Vector3(ghostCell.X + 0.5f, ghostCell.Y + 0.5f, ghostCell.Z + 0.5f),
                Vector3.one * 0.78f);
            if (SelectedComponentType() == null) return;
            foreach (var pin in BuiltInPinCatalog.Pins(ghostType, 1))
            {
                var point = ghostOrientation.TransformPoint(new QuarterPoint(
                    pin.Qx, pin.Qy, pin.Qz));
                AddGhostPrimitive(PrimitiveType.Sphere,
                    new Vector3(ghostCell.X + point.X * 0.25f,
                        ghostCell.Y + point.Y * 0.25f,
                        ghostCell.Z + point.Z * 0.25f),
                    Vector3.one * 0.18f);
            }
        }

        private void AddGhostPrimitive(PrimitiveType type, Vector3 position, Vector3 scale)
        {
            var item = GameObject.CreatePrimitive(type);
            item.transform.SetParent(ghostRoot.transform, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            Destroy(item.GetComponent<Collider>());
            var renderer = item.GetComponent<Renderer>();
            var shader = Shader.Find("Transparent/Diffuse");
            if (shader != null) renderer.material = new Material(shader);
            ghostRenderers.Add(renderer);
        }

        private void HideGhost()
        {
            ghostCached = false;
            if (ghostRoot != null) ghostRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (ghostRoot != null) Destroy(ghostRoot);
            if (rotationRoot != null) Destroy(rotationRoot);
            if (selectionRoot != null) Destroy(selectionRoot);
        }

        private bool SelectionCell(out GridCell cell)
        {
            if (!hasHit) { cell = default; InvalidAction(); return false; }
            if (hovered != null && hovered.Kind == WorldPartKind.ComponentBody)
                foreach (var component in session.Design.Components)
                    if (component.Id == hovered.OwnerId)
                    { cell = component.AnchorCell; return true; }
            if (hovered != null && hovered.Kind == WorldPartKind.ModuleBody)
            {
                var center = hit.collider.bounds.center;
                cell = new GridCell(Mathf.FloorToInt(center.x),
                    Mathf.FloorToInt(center.y), Mathf.FloorToInt(center.z));
                return true;
            }
            return PlacementCell(out cell);
        }

        private void DrawSelectionPreview()
        {
            if (selectionRoot != null) Destroy(selectionRoot);
            if (!selectionFirst.HasValue) return;
            var region = new CellRegion(selectionFirst.Value,
                selectionSecond ?? selectionFirst.Value);
            selectionRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            selectionRoot.name = "Package region preview";
            selectionRoot.transform.position = new Vector3(
                (region.Min.X + region.Max.X + 1) * 0.5f,
                (region.Min.Y + region.Max.Y + 1) * 0.5f,
                (region.Min.Z + region.Max.Z + 1) * 0.5f);
            selectionRoot.transform.localScale = new Vector3(
                region.SizeCells.X, region.SizeCells.Y, region.SizeCells.Z);
            Destroy(selectionRoot.GetComponent<Collider>());
            var renderer = selectionRoot.GetComponent<Renderer>();
            var shader = Shader.Find("Transparent/Diffuse");
            if (shader != null) renderer.material = new Material(shader);
            renderer.material.color = new Color(0.15f, 0.55f, 1f, 0.14f);
        }

        private void BeginRotation(Guid objectId, bool clockwise)
        {
            var orientation = GridOrientation.Default;
            var found = false;
            if (rotationPreview != null && rotationPreview.ObjectId == objectId)
            {
                orientation = rotationPreview.Orientation;
                found = true;
            }
            else
            {
                foreach (var component in session.Design.Components)
                    if (component.Id == objectId)
                    { orientation = component.Orientation; found = true; break; }
                foreach (var module in session.Design.Modules)
                    if (module.Id == objectId)
                    { orientation = module.Orientation; found = true; break; }
            }
            if (!found) { InvalidAction(); return; }
            try
            {
                var next = clockwise ? orientation.ClockwiseYaw() :
                    orientation.CounterclockwiseYaw();
                var preview = session.PreviewRotation(objectId, next);
                rotationPreview = preview;
                DrawRotationPreview();
                HideGhost();
            }
            catch (ArgumentException) { InvalidAction(); }
            catch (InvalidOperationException) { InvalidAction(); }
        }

        private void DrawRotationPreview()
        {
            if (rotationRoot != null) Destroy(rotationRoot);
            rotationRoot = new GameObject("Rotation preview");
            foreach (var component in rotationPreview.Candidate.Components)
                if (component.Id == rotationPreview.ObjectId)
                {
                    AddRotationPart(new Vector3(component.AnchorCell.X + 0.5f,
                        component.AnchorCell.Y + 0.5f,
                        component.AnchorCell.Z + 0.5f), Vector3.one * 0.85f,
                        PrimitiveType.Cube, new Color(0.2f, 0.9f, 0.55f, 0.42f));
                    foreach (var pin in component.BuildPins())
                        AddRotationPart(PointPosition(pin.Cell, pin.PointQ),
                            Vector3.one * 0.25f, PrimitiveType.Sphere,
                            new Color(0.2f, 0.9f, 0.55f, 0.7f));
                }
            foreach (var module in rotationPreview.Candidate.Modules)
                if (module.Id == rotationPreview.ObjectId)
                {
                    foreach (var cell in module.OccupiedCells())
                        AddRotationPart(new Vector3(cell.X + 0.5f,
                            cell.Y + 0.5f, cell.Z + 0.5f), Vector3.one * 0.88f,
                            PrimitiveType.Cube,
                            new Color(0.2f, 0.9f, 0.55f, 0.42f));
                    foreach (var port in module.BuildPortBits())
                        AddRotationPart(PointPosition(port.Cell, port.PointQ),
                            Vector3.one * 0.25f, PrimitiveType.Sphere,
                            new Color(0.2f, 0.9f, 0.55f, 0.7f));
                }
            foreach (var join in session.Design.Topology.Joins)
            {
                if (!rotationPreview.LostJoinIds.Contains(join.Id)) continue;
                foreach (var member in join.Members)
                    if (member.Kind == JoinTargetKind.ConnectorNode)
                        foreach (var route in session.Design.Topology.Connectors)
                            if (route.Id == member.OwnerId)
                                foreach (var node in route.Nodes)
                                    if (node.Id == member.PartId)
                                        AddRotationPart(PointPosition(node.Cell,
                                            node.PointQ), Vector3.one * 0.35f,
                                            PrimitiveType.Sphere,
                                            new Color(1f, 0.1f, 0.1f, 0.8f));
            }
        }

        private static Vector3 PointPosition(GridCell cell, QuarterPoint point) =>
            new Vector3(cell.X + point.X * 0.25f,
                cell.Y + point.Y * 0.25f, cell.Z + point.Z * 0.25f);

        private void AddRotationPart(Vector3 position, Vector3 scale,
            PrimitiveType type, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(rotationRoot.transform, false);
            part.transform.position = position;
            part.transform.localScale = scale;
            Destroy(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            var shader = Shader.Find("Transparent/Diffuse");
            if (shader != null) renderer.material = new Material(shader);
            renderer.material.color = color;
        }

        private void ClearRotationPreview()
        {
            rotationPreview = null;
            if (rotationRoot != null)
            {
                Destroy(rotationRoot);
                rotationRoot = null;
            }
        }

        private bool PlacementCell(out GridCell cell)
        {
            if (hovered != null && hovered.Kind == WorldPartKind.ComponentBody)
            {
                foreach (var component in session.Design.Components)
                    if (component.Id == hovered.OwnerId)
                    {
                        var offset = FaceOffset(hit.normal);
                        cell = new GridCell(component.AnchorCell.X + offset.X,
                            component.AnchorCell.Y + offset.Y,
                            component.AnchorCell.Z + offset.Z);
                        return true;
                    }
            }
            if (hovered != null && hovered.Kind == WorldPartKind.ModuleBody)
            {
                var center = hit.collider.bounds.center;
                var offset = FaceOffset(hit.normal);
                cell = new GridCell(Mathf.FloorToInt(center.x) + offset.X,
                    Mathf.FloorToInt(center.y) + offset.Y,
                    Mathf.FloorToInt(center.z) + offset.Z);
                return true;
            }
            var sample = hit.point + hit.normal * 0.02f;
            cell = new GridCell(Mathf.FloorToInt(sample.x),
                Mathf.FloorToInt(sample.y), Mathf.FloorToInt(sample.z));
            return true;
        }

        private GridOrientation InitialOrientation(GridCell cell)
        {
            var toCell = new Vector3(cell.X + 0.5f, cell.Y + 0.5f,
                cell.Z + 0.5f) - viewCamera.transform.position;
            var forward = Mathf.Abs(toCell.x) > Mathf.Abs(toCell.z)
                ? toCell.x >= 0f ? GridDirection.East : GridDirection.West
                : toCell.z >= 0f ? GridDirection.North : GridDirection.South;
            return new GridOrientation(forward, GridDirection.Up);
        }

        private static GridCell FaceOffset(Vector3 normal)
        {
            if (Mathf.Abs(normal.x) >= Mathf.Abs(normal.y) &&
                Mathf.Abs(normal.x) >= Mathf.Abs(normal.z))
                return new GridCell(normal.x >= 0f ? 1 : -1, 0, 0);
            if (Mathf.Abs(normal.y) >= Mathf.Abs(normal.z))
                return new GridCell(0, normal.y >= 0f ? 1 : -1, 0);
            return new GridCell(0, 0, normal.z >= 0f ? 1 : -1);
        }

        private string SelectedComponentType()
        {
            var id = SelectedCatalogId();
            return id == BuiltInPinCatalog.Source ||
                id == BuiltInPinCatalog.And ||
                id == BuiltInPinCatalog.SrFlipFlop ? id : null;
        }

        private OneBitModuleVersion SelectedModuleVersion()
        {
            var item = inventory.SelectedItem;
            if (item == null || item.Kind != SavedInventoryKind.ModuleVersion)
                return null;
            return session.ModuleVersions.TryGetValue(item.VersionId,
                out var version) && version.FamilyId == item.FamilyId
                ? version : null;
        }

        private string NextModuleName(string baseName)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in session.Design.Modules)
                occupied.Add(module.InstanceName);
            for (var index = 1; ; index++)
            {
                var name = baseName + " " + index;
                if (!occupied.Contains(name)) return name;
            }
        }

        private string SelectedCatalogId()
        {
            var item = inventory.SelectedItem;
            return item != null && item.Kind == SavedInventoryKind.CatalogItem
                ? item.ItemTypeId : null;
        }

        private void SelectSlot(int index)
        {
            inventory.SelectHotbar(index);
            wireStart = null;
            ghostCached = false;
        }

        private string SlotLabel(int index)
        {
            var item = inventory.Slots[index];
            if (item == null) return "";
            if (item.Kind == SavedInventoryKind.ModuleVersion)
            {
                if (session.ModuleVersions.TryGetValue(item.VersionId,
                    out var version)) return version.Name;
                return "MISSING module";
            }
            switch (item.ItemTypeId)
            {
                case BuiltInPinCatalog.Source: return "Source";
                case OneBitCatalogItemIds.Wire: return "Wire";
                case BuiltInPinCatalog.And: return "AND";
                case BuiltInPinCatalog.SrFlipFlop: return "SR";
                case OneBitCatalogItemIds.WorldClockLink: return "Clock Link";
                default: return "Unknown item";
            }
        }

        private bool IsSource(Guid id)
        {
            foreach (var component in session.Design.Components)
                if (component.Id == id)
                    return component.TypeId == BuiltInPinCatalog.Source;
            return false;
        }

        private bool IsSrClockPin(Guid objectId, Guid pinId)
        {
            foreach (var component in session.Design.Components)
                if (component.Id == objectId &&
                    component.TypeId == BuiltInPinCatalog.SrFlipFlop &&
                    component.PinIds["CLK"] == pinId)
                    return true;
            return false;
        }

        private void OpenSourceConfigure(Guid id)
        {
            configureSourceId = id;
            foreach (var component in session.Design.Components)
                if (component.Id == id)
                {
                    configureValue = component.SourceOnValue;
                    configureInitialOn = component.SourceInitialOn;
                    break;
                }
            configureOpen = true;
            player.SetInterfaceOpen(true);
            HideGhost();
        }

        private void Escape()
        {
            if (packageDraft != null)
            {
                packageDraft = null;
                packageError = "";
                player.SetInterfaceOpen(false);
                return;
            }
            if (rotationPreview != null)
            {
                ClearRotationPreview();
                return;
            }
            if (configureOpen || inventoryOpen || pauseMenuOpen || inspectOpen)
            {
                configureOpen = false;
                inventoryOpen = false;
                pauseMenuOpen = false;
                inspectOpen = false;
                player.SetInterfaceOpen(false);
                return;
            }
            if (session.Scheduler.IsPaused) session.Scheduler.ResumeSimulation();
            else
            {
                session.Scheduler.PauseSimulation();
                pauseMenuOpen = true;
                player.SetInterfaceOpen(true);
            }
        }

        private void OnGUI()
        {
            if (session == null) return;
            if (hudVisible && packageDraft == null)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 8f,
                    Screen.height * 0.5f - 10f, 18f, 18f), "+");
                var width = 9 * 72f;
                var left = (Screen.width - width) * 0.5f;
                for (var i = 0; i < 9; i++)
                {
                    var title = (i + 1) + " " + SlotLabel(i);
                    if (GUI.Button(new Rect(left + i * 72f,
                        Screen.height - 54f, 70f, 46f), title))
                    {
                        SelectSlot(i);
                    }
                }
                GUI.Label(new Rect(Screen.width - 175f, Screen.height - 90f,
                    165f, 28f), "Held: " +
                    SlotLabel(inventory.SelectedHotbarSlot));
                GUI.Label(new Rect(12f, 12f, 390f, 48f),
                    "Time " + session.Scheduler.Now +
                    "  Clock " + (session.Scheduler.ClockRunning ? "running" : "stopped") +
                    "  " + session.Scheduler.ClockLevel.ToSymbol() +
                    (session.Scheduler.IsPaused ? "  PAUSED" : ""));
                if (wireStart.HasValue)
                    GUI.Label(new Rect(12f, 62f, 400f, 28f),
                        "Wire start selected. Aim at a free pin or connector node.");
                QuickLook();
            }
            if (rotationPreview != null)
                GUI.Box(new Rect(Screen.width * 0.5f - 230f, 20f, 460f, 52f),
                    "Rotation preview: " + rotationPreview.LostJoinIds.Count +
                    " connector attachment(s) lost. Enter confirm; Esc cancel.");
            if (packageDraft == null && selectionFirst.HasValue)
                GUI.Label(new Rect(12f, 142f, 500f, 25f),
                    selectionSecond.HasValue
                        ? "Region selected. Enter previews package ports."
                        : "First region corner selected. Aim and press T.");
            if (packageDraft == null && packageError.Length > 0)
                GUI.Label(new Rect(12f, 170f, 600f, 45f), packageError);
            if (inventoryOpen) DrawInventory();
            if (configureOpen) DrawSourceConfigure();
            if (pauseMenuOpen) DrawPauseMenu();
            if (inspectOpen) DrawInspect();
            if (packageDraft != null) DrawPackagePreview();
        }

        private void QuickLook()
        {
            if (hovered == null) return;
            OneBitInspection detail;
            try
            {
                if (hovered.Kind == WorldPartKind.ConnectorNode ||
                    hovered.Kind == WorldPartKind.ConnectorSpan)
                    detail = session.Inspector.InspectConnector(hovered.OwnerId);
                else if (hovered.Kind == WorldPartKind.ComponentPin)
                    detail = session.Inspector.InspectPin(hovered.OwnerId, hovered.PartId);
                else if (hovered.Kind == WorldPartKind.ModulePort)
                    detail = session.Inspector.InspectModulePort(
                        hovered.OwnerId, hovered.PartId);
                else return;
            }
            catch (KeyNotFoundException) { return; }
            DrawHoverOutline(hovered);
            GUI.Box(new Rect(12f, 94f, 360f, 48f),
                "1-bit  " + detail.Value.ToSymbol() + "  tag " + detail.Tag +
                "  connections " + detail.ConnectedPins.Count);
        }

        private void DrawHoverOutline(WorldSelectablePart part)
        {
            if (viewCamera == null) return;
            var renderer = part.GetComponent<Renderer>();
            if (renderer == null || !renderer.isVisible) return;
            var bounds = renderer.bounds;
            var minimumX = float.PositiveInfinity;
            var minimumY = float.PositiveInfinity;
            var maximumX = float.NegativeInfinity;
            var maximumY = float.NegativeInfinity;
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3(x, y, z));
                        var projected = viewCamera.WorldToScreenPoint(corner);
                        if (projected.z <= 0f) return;
                        minimumX = Mathf.Min(minimumX, projected.x);
                        maximumX = Mathf.Max(maximumX, projected.x);
                        var guiY = Screen.height - projected.y;
                        minimumY = Mathf.Min(minimumY, guiY);
                        maximumY = Mathf.Max(maximumY, guiY);
                    }
            var centerX = (minimumX + maximumX) * 0.5f;
            var centerY = (minimumY + maximumY) * 0.5f;
            var halfWidth = Mathf.Max(9f, (maximumX - minimumX) * 0.5f + 3f);
            var halfHeight = Mathf.Max(9f, (maximumY - minimumY) * 0.5f + 3f);
            var rect = new Rect(centerX - halfWidth, centerY - halfHeight,
                halfWidth * 2f, halfHeight * 2f);
            var previous = GUI.color;
            GUI.color = new Color(1f, 0.95f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f,
                rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y,
                2f, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawInventory()
        {
            var rect = new Rect(Screen.width * 0.5f - 340f,
                Screen.height * 0.5f - 190f, 680f, 380f);
            GUI.Box(rect, "Inventory  (E or Esc to close)");
            for (var i = 0; i < 36; i++)
            {
                var row = i / 9;
                var column = i % 9;
                var label = SlotLabel(i);
                if (GUI.Button(new Rect(rect.x + 16f + column * 72f,
                    rect.y + 45f + row * 75f, 66f, 62f), label) && i < 9)
                {
                    SelectSlot(i);
                    inventoryOpen = false;
                    player.SetInterfaceOpen(false);
                }
            }
        }

        private void DrawPackagePreview()
        {
            var rect = new Rect((Screen.width - 940f) * 0.5f,
                (Screen.height - 690f) * 0.5f, 940f, 690f);
            GUI.Box(rect, "Package configuration preview");
            GUI.Box(new Rect(rect.x + 15f, rect.y + 28f, 910f, 118f),
                "Captured circuit and exact port candidates");
            var size = packageDraft.Snapshot.SizeCells;
            GUI.Label(new Rect(rect.x + 30f, rect.y + 55f, 850f, 50f),
                "Size " + size.X + " × " + size.Y + " × " + size.Z +
                " cells; " + packageDraft.Snapshot.Components.Count +
                " components; " + packageDraft.Snapshot.Topology.Connectors.Count +
                " copied connectors; " +
                packageDraft.Snapshot.PortCandidates.Count +
                " exposed endpoints. Source world remains unchanged.");
            GUI.Label(new Rect(rect.x + 30f, rect.y + 112f, 130f, 25f),
                "Module name:");
            packageName = GUI.TextField(new Rect(rect.x + 155f, rect.y + 110f,
                300f, 27f), packageName);
            packageDraft.Name = packageName;
            var faces = new[] { GridDirection.West, GridDirection.East,
                GridDirection.North, GridDirection.South, GridDirection.Up,
                GridDirection.Down };
            for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++)
            {
                var face = faces[faceIndex];
                var box = new Rect(rect.x + 15f + faceIndex % 3 * 305f,
                    rect.y + 160f + faceIndex / 3 * 215f, 295f, 205f);
                GUI.Box(box, GridOrientation.FaceName(face));
                var row = 0;
                for (var i = 0; i < packageDraft.Ports.Count; i++)
                {
                    var choice = packageDraft.Ports[i];
                    if (PortFace(choice.PointQ) != face) continue;
                    if (row >= 5) break;
                    var y = box.y + 28f + row++ * 32f;
                    var name = GUI.TextField(new Rect(box.x + 7f, y, 68f, 26f),
                        choice.Name);
                    if (name != choice.Name)
                        packageDraft.ReplacePort(i, new OneBitPortChoice(name,
                            choice.Direction, choice.LocalCell, choice.PointQ,
                            choice.BitZeroTarget));
                    if (GUI.Button(new Rect(box.x + 79f, y, 48f, 26f),
                        choice.Direction.ToString().Substring(0, 2)))
                    {
                        var next = (OneBitPortDirection)
                            (((int)choice.Direction + 1) % 3);
                        packageDraft.ReplacePort(i, new OneBitPortChoice(
                            name, next, choice.LocalCell, choice.PointQ,
                            choice.BitZeroTarget));
                    }
                    if (GUI.Button(new Rect(box.x + 130f, y, 60f, 26f),
                        EndpointName(choice.BitZeroTarget)))
                        CyclePortMapping(i);
                    if (GUI.Button(new Rect(box.x + 193f, y, 54f, 26f),
                        "Face >")) MovePortToNextFace(i);
                    if (GUI.Button(new Rect(box.x + 250f, y, 35f, 26f), "X"))
                    { packageDraft.RemovePort(i); break; }
                }
            }
            GUI.Label(new Rect(rect.x + 20f, rect.y + 605f, 900f, 30f),
                packageError.Length == 0
                    ? "Edit names, directions, mappings, and faces; then validate the draft."
                    : packageError);
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 643f,
                180f, 35f), "Validate draft"))
            {
                try
                {
                    var candidate = packageDraft.BuildCandidate(Guid.NewGuid());
                    packageError = "Valid fixed design: " + candidate.Ports.Count +
                        " ports. Close to return to the world.";
                }
                catch (ArgumentException exception)
                { packageError = exception.Message; }
            }
            if (GUI.Button(new Rect(rect.x + 740f, rect.y + 643f,
                180f, 35f), "Close"))
            {
                packageDraft = null;
                packageError = "";
                player.SetInterfaceOpen(false);
            }
        }

        private static GridDirection PortFace(QuarterPoint point)
        {
            if (point.X == 0) return GridDirection.West;
            if (point.X == 4) return GridDirection.East;
            if (point.Y == 0) return GridDirection.Down;
            if (point.Y == 4) return GridDirection.Up;
            if (point.Z == 0) return GridDirection.South;
            return GridDirection.North;
        }

        private string EndpointName(JoinMember target)
        {
            if (target.Kind == JoinTargetKind.ComponentPin)
                foreach (var component in packageDraft.Snapshot.Components)
                    if (component.Id == target.OwnerId)
                        foreach (var pin in component.PinIds)
                            if (pin.Value == target.PartId) return pin.Key;
            return target.Kind == JoinTargetKind.ConnectorNode
                ? "wire" : "port";
        }

        private void CyclePortMapping(int index)
        {
            var candidates = packageDraft.Snapshot.PortCandidates;
            if (candidates.Count == 0) return;
            var choice = packageDraft.Ports[index];
            var found = -1;
            for (var i = 0; i < candidates.Count; i++)
                if (candidates[i].InternalEndpoint.Equals(choice.BitZeroTarget))
                { found = i; break; }
            var target = candidates[(found + 1) % candidates.Count];
            packageDraft.ReplacePort(index, new OneBitPortChoice(choice.Name,
                choice.Direction, choice.LocalCell, choice.PointQ,
                target.InternalEndpoint));
            packageError = "";
        }

        private void MovePortToNextFace(int index)
        {
            var faces = new[] { GridDirection.West, GridDirection.East,
                GridDirection.North, GridDirection.South, GridDirection.Up,
                GridDirection.Down };
            var choice = packageDraft.Ports[index];
            var current = PortFace(choice.PointQ);
            var start = Array.IndexOf(faces, current);
            var size = packageDraft.Snapshot.SizeCells;
            for (var step = 1; step < faces.Length; step++)
            {
                var face = faces[(start + step) % faces.Length];
                for (var x = 0; x < size.X; x++)
                    for (var y = 0; y < size.Y; y++)
                        for (var z = 0; z < size.Z; z++)
                            for (var a = 1; a <= 3; a += 2)
                                for (var b = 1; b <= 3; b += 2)
                                {
                                    if (face == GridDirection.West && x != 0 ||
                                        face == GridDirection.East && x != size.X - 1 ||
                                        face == GridDirection.Down && y != 0 ||
                                        face == GridDirection.Up && y != size.Y - 1 ||
                                        face == GridDirection.South && z != 0 ||
                                        face == GridDirection.North && z != size.Z - 1)
                                        continue;
                                    var cell = new GridCell(x, y, z);
                                    var point = face == GridDirection.West
                                        ? new QuarterPoint(0, a, b) :
                                        face == GridDirection.East
                                        ? new QuarterPoint(4, a, b) :
                                        face == GridDirection.Down
                                        ? new QuarterPoint(a, 0, b) :
                                        face == GridDirection.Up
                                        ? new QuarterPoint(a, 4, b) :
                                        face == GridDirection.South
                                        ? new QuarterPoint(a, b, 0) :
                                        new QuarterPoint(a, b, 4);
                                    var taken = false;
                                    for (var other = 0;
                                         other < packageDraft.Ports.Count; other++)
                                        if (other != index &&
                                            packageDraft.Ports[other].LocalCell.Equals(cell) &&
                                            packageDraft.Ports[other].PointQ.Equals(point))
                                            taken = true;
                                    if (taken) continue;
                                    packageDraft.ReplacePort(index,
                                        new OneBitPortChoice(choice.Name,
                                            choice.Direction, cell, point,
                                            choice.BitZeroTarget));
                                    packageError = "";
                                    return;
                                }
            }
            packageError = "No free position on another exterior face.";
        }

        private void DrawSourceConfigure()
        {
            var rect = new Rect(Screen.width * 0.5f - 210f,
                Screen.height * 0.5f - 125f, 420f, 250f);
            GUI.Box(rect, "Configure Constant Logic Source");
            GUI.Label(new Rect(rect.x + 20f, rect.y + 45f, 390f, 25f),
                "On value: " + configureValue.ToSymbol());
            var choices = new[] { LogicBit.Zero, LogicBit.One, LogicBit.X, LogicBit.Z };
            for (var i = 0; i < choices.Length; i++)
                if (GUI.Button(new Rect(rect.x + 20f + i * 93f,
                    rect.y + 75f, 85f, 32f), choices[i].ToSymbol()))
                    configureValue = choices[i];
            configureInitialOn = GUI.Toggle(new Rect(rect.x + 20f,
                rect.y + 125f, 370f, 28f), configureInitialOn,
                "Initially On after load or Reset Simulation");
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 190f, 160f, 36f), "Apply"))
            {
                var id = configureSourceId;
                var value = configureValue;
                var initial = configureInitialOn;
                TryEdit(() => session.ConfigureSource(id, value, initial));
                configureOpen = false;
                player.SetInterfaceOpen(false);
            }
            if (GUI.Button(new Rect(rect.x + 240f, rect.y + 190f, 160f, 36f),
                "Cancel"))
            {
                configureOpen = false;
                player.SetInterfaceOpen(false);
            }
        }

        private void DrawPauseMenu()
        {
            var rect = new Rect(Screen.width * 0.5f - 190f,
                Screen.height * 0.5f - 115f, 380f, 230f);
            GUI.Box(rect, "Simulation paused");
            GUI.Label(new Rect(rect.x + 20f, rect.y + 45f, 140f, 28f),
                "World clock Hz:");
            frequencyText = GUI.TextField(new Rect(rect.x + 165f,
                rect.y + 45f, 180f, 26f), frequencyText);
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 85f, 155f, 32f),
                "Apply frequency"))
                TryEdit(() => session.Scheduler.SetFrequency(frequencyText));
            if (GUI.Button(new Rect(rect.x + 195f, rect.y + 85f, 155f, 32f),
                "Reset Simulation"))
                TryEdit(() => session.Scheduler.ResetSimulation());
            if (GUI.Button(new Rect(rect.x + 105f, rect.y + 155f, 170f, 40f),
                "Resume"))
            {
                session.Scheduler.ResumeSimulation();
                pauseMenuOpen = false;
                player.SetInterfaceOpen(false);
            }
        }

        private void DrawInspect()
        {
            OneBitInspection detail;
            try
            {
                if (inspectedKind == WorldPartKind.ConnectorNode ||
                    inspectedKind == WorldPartKind.ConnectorSpan)
                    detail = session.Inspector.InspectConnector(inspectedOwner);
                else if (inspectedKind == WorldPartKind.ComponentPin)
                    detail = session.Inspector.InspectPin(inspectedOwner, inspectedPart);
                else if (inspectedKind == WorldPartKind.ModulePort)
                    detail = session.Inspector.InspectModulePort(
                        inspectedOwner, inspectedPart);
                else return;
            }
            catch (KeyNotFoundException) { inspectOpen = false; return; }
            var names = new List<string>();
            foreach (var pin in detail.ConnectedPins) names.Add(PinName(pin));
            GUI.Box(new Rect(12f, 150f, 550f, 175f),
                "Inspect one-bit net: " + detail.Value.ToSymbol() +
                "   width 1   tag " + detail.Tag + "\n" +
                "Connections: " + string.Join(", ", names) + "\n" +
                "Active drivers: " + detail.ActiveDrivers.Count + "\n" +
                detail.Explanation);
        }

        private string PinName(JoinMember pin)
        {
            if (pin.Kind == JoinTargetKind.ModulePortBit)
                foreach (var module in session.Design.Modules)
                    if (module.Id == pin.OwnerId)
                        foreach (var port in module.InterfacePorts)
                            if (port.Id == pin.PartId)
                                return module.InstanceName + "." + port.Name;
            foreach (var component in session.Design.Components)
                if (component.Id == pin.OwnerId)
                    foreach (var pair in component.PinIds)
                        if (pair.Value == pin.PartId)
                            return component.TypeId + "." + pair.Key;
            return pin.OwnerId.ToString("D") + "." + pin.PartId.ToString("D");
        }
    }
}
