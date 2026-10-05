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
        private readonly List<Renderer> ghostRenderers = new List<Renderer>();
        private GridCell ghostCell;
        private GridOrientation ghostOrientation;
        private string ghostType;
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
            session = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
            inventory = playerInventory ?? OneBitPlayerInventory.NewFreeplay();
            player = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
            viewCamera = player.CameraPivot.GetComponent<Camera>();
            if (viewCamera == null) throw new ArgumentException("Player camera is missing.");
            frequencyText = session.Scheduler.FrequencyHz;
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
            if (configureOpen || inventoryOpen || pauseMenuOpen) return;
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
            if (typeId == null || !ghostCached || !ghostValid)
            {
                InvalidAction();
                return;
            }
            var cell = ghostCell;
            var orientation = ghostOrientation;
            TryEdit(() => session.PlaceComponent(typeId, cell, orientation));
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
        }

        private void InvalidAction()
        {
            invalidUntil = Time.unscaledTime + 0.18f;
            if (hovered != null) hovered.FlashInvalid();
        }

        private void UpdateGhost()
        {
            var typeId = SelectedComponentType();
            if (typeId == null || !hasHit || !PlacementCell(out var cell))
            {
                HideGhost();
                return;
            }
            var orientation = InitialOrientation(cell);
            if (!ghostCached || !cell.Equals(ghostCell) ||
                !orientation.Equals(ghostOrientation) || ghostType != typeId)
            {
                ghostCell = cell;
                ghostOrientation = orientation;
                ghostType = typeId;
                ghostCached = true;
                try
                {
                    OneBitWorldEdits.PlaceComponent(session.Design, typeId,
                        cell, orientation);
                    ghostValid = true;
                }
                catch (ArgumentException) { ghostValid = false; }
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
            AddGhostPrimitive(PrimitiveType.Cube,
                new Vector3(ghostCell.X + 0.5f, ghostCell.Y + 0.5f, ghostCell.Z + 0.5f),
                Vector3.one * 0.78f);
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
            if (hudVisible)
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
            if (inventoryOpen) DrawInventory();
            if (configureOpen) DrawSourceConfigure();
            if (pauseMenuOpen) DrawPauseMenu();
            if (inspectOpen) DrawInspect();
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
            GUI.Box(new Rect(12f, 94f, 360f, 48f),
                "1-bit  " + detail.Value.ToSymbol() + "  tag " + detail.Tag +
                "  connections " + detail.ConnectedPins.Count);
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
