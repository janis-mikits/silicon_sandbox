using System;
using System.IO;
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
        private GameObject wirePreviewRoot;
        private string wirePreviewKey;
        private ulong wirePreviewRevision;
        private GameObject ghostRoot;
        private GameObject rotationRoot;
        private OneBitRotationPreview rotationPreview;
        private GameObject selectionRoot;
        private Material selectionLineMaterial;
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
        private AudioSource invalidAudio;
        private AudioClip invalidClick;
        private bool inventoryOpen;
        private bool configureOpen;
        private bool inspectOpen;
        private bool pauseMenuOpen;
        private Func<string> saveWorld;
        private Func<string> reopenWorld;
        private Func<OneBitPackageDraft, string> publishPackage;
        private Func<IReadOnlyList<WorldRecoveryChoice>> listSavedWorlds;
        private Func<WorldRecoveryChoice, string> loadSavedWorld;
        private Func<string> currentWorldName;
        private Func<string, string> renameWorld;
        private string worldNameText = "";
        private IReadOnlyList<WorldRecoveryChoice> savedWorldChoices =
            Array.Empty<WorldRecoveryChoice>();
        private bool browseWorlds;
        private Vector2 savedWorldScroll;
        private Vector2 internalInspectScroll;
        private Vector2 packageScroll;
        private string persistenceMessage = "";
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
        public bool InspectionOpen => inspectOpen;

        public void OpenInspection(WorldSelectablePart part)
        {
            if (session == null) throw new InvalidOperationException(
                "No world is attached for inspection.");
            if (part == null) throw new ArgumentNullException(nameof(part));
            inspectedKind = part.Kind;
            inspectedOwner = part.OwnerId;
            inspectedPart = part.PartId;
            internalInspectScroll = Vector2.zero;
            inspectOpen = true;
            player.SetInterfaceOpen(true);
            HideGhost();
        }

        public void CloseInspection()
        {
            inspectOpen = false;
            player?.SetInterfaceOpen(false);
        }

        public void Attach(OneBitWorldSession activeSession,
            CreativeCameraController cameraController,
            OneBitPlayerInventory playerInventory = null)
        {
            ClearRotationPreview();
            ClearWirePreview();
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
            browseWorlds = false;
            observedRevision = ulong.MaxValue;
            fractionalPicoseconds = 0d;
            HideGhost();
            session = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
            inventory = playerInventory ?? OneBitPlayerInventory.NewFreeplay();
            player = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
            viewCamera = player.CameraPivot.GetComponent<Camera>();
            if (viewCamera == null) throw new ArgumentException("Player camera is missing.");
            frequencyText = session.Scheduler.FrequencyHz;
            if (currentWorldName != null) worldNameText = currentWorldName();
            player.SetInterfaceOpen(false);
        }

        public void SetPersistenceActions(Func<string> save, Func<string> reopen,
            Func<OneBitPackageDraft, string> publish,
            Func<IReadOnlyList<WorldRecoveryChoice>> list,
            Func<WorldRecoveryChoice, string> load,
            Func<string> worldName, Func<string, string> rename)
        {
            saveWorld = save ?? throw new ArgumentNullException(nameof(save));
            reopenWorld = reopen ?? throw new ArgumentNullException(nameof(reopen));
            publishPackage = publish ?? throw new ArgumentNullException(nameof(publish));
            listSavedWorlds = list ?? throw new ArgumentNullException(nameof(list));
            loadSavedWorld = load ?? throw new ArgumentNullException(nameof(load));
            currentWorldName = worldName ?? throw new ArgumentNullException(nameof(worldName));
            renameWorld = rename ?? throw new ArgumentNullException(nameof(rename));
            worldNameText = currentWorldName();
        }

        private void Update()
        {
            if (session == null) return;
            AdvancePlayback();
            if (observedRevision != session.Revision)
            {
                observedRevision = session.Revision;
                ghostCached = false;
                wirePreviewKey = null;
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
            if (configureOpen || inventoryOpen || pauseMenuOpen || inspectOpen ||
                packageDraft != null) return;
            if (Input.GetKeyDown(KeyCode.E))
            {
                wireStart = null;
                ClearWirePreview();
                inventoryOpen = true;
                player.SetInterfaceOpen(true);
                HideGhost();
                return;
            }

            for (var i = 0; i < 9; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    SelectSlot(i);
            TargetAtCrosshair();
            UpdateWirePreview();
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
                OpenInspection(hovered);
                return;
            }
            if (Input.GetKeyDown(KeyCode.C) && hovered != null &&
                hovered.Kind == WorldPartKind.ComponentBody &&
                IsSource(hovered.OwnerId))
                OpenSourceConfigure(hovered.OwnerId);
            if (Input.GetMouseButtonDown(0) && hovered != null)
            {
                var target = hovered;
                switch (target.Kind)
                {
                    case WorldPartKind.ConnectorSpan:
                        TryEdit(() => session.BreakSpan(target.OwnerId,
                            target.PartId));
                        break;
                    case WorldPartKind.ConnectorNode:
                        TryEdit(() => session.BreakConnector(target.OwnerId));
                        break;
                    case WorldPartKind.ComponentBody:
                    case WorldPartKind.ComponentPin:
                        TryEdit(() => session.BreakComponent(target.OwnerId));
                        break;
                    case WorldPartKind.ModuleBody:
                    case WorldPartKind.ModulePort:
                        TryEdit(() => session.BreakModule(target.OwnerId));
                        break;
                }
            }
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
            RightClickWithModifier(Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift));
        }

        private void RightClickWithModifier(bool shiftHeld)
        {
            var selectedId = SelectedCatalogId();
            var holdingConnector = selectedId == OneBitCatalogItemIds.Wire ||
                selectedId == OneBitCatalogItemIds.WorldClockLink;
            if (shiftHeld && holdingConnector && hovered != null &&
                IsConnectorKind(hovered.Kind))
            {
                // The harness feature owns connector-on-connector concatenation.
                // Never turn this reserved action into an ordinary junction.
                InvalidAction();
                return;
            }
            if (!shiftHeld && hovered != null &&
                hovered.Kind == WorldPartKind.ComponentBody &&
                IsSource(hovered.OwnerId))
            {
                TryEdit(() => session.ToggleSource(hovered.OwnerId));
                return;
            }
            if (shiftHeld && holdingConnector &&
                (hovered == null || !IsPinKind(hovered.Kind)))
            {
                if (!hasHit || !PlacementCell(out var openCell))
                { InvalidAction(); return; }
                var placed = false;
                TryEdit(() =>
                {
                    session.PlaceConnector(BuildOpenConnector(openCell, selectedId),
                        Array.Empty<ElectricalJoin>());
                    placed = true;
                });
                if (placed)
                {
                    wireStart = null;
                    ClearWirePreview();
                    ghostCached = false;
                }
                return;
            }
            if (selectedId == OneBitCatalogItemIds.Wire)
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
                    if (hovered == null && hasHit &&
                        hit.collider.GetComponent<GeneratedFloorSurface>() != null)
                    {
                        PlacementCell(out var targetCell);
                        TryEdit(() => session.PlaceWireStub(start, targetCell));
                    }
                    else if (hovered == null) InvalidAction();
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
                    ClearWirePreview();
                }
                return;
            }
            if (selectedId == OneBitCatalogItemIds.WorldClockLink)
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
                else TryEdit(() => session.AttachWorldClockPin(
                    hovered.OwnerId, hovered.PartId));
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

        private static bool IsConnectorKind(WorldPartKind kind) =>
            kind == WorldPartKind.ConnectorNode ||
            kind == WorldPartKind.ConnectorSpan;

        private ConnectorRoute BuildOpenConnector(GridCell cell, string selectedId)
        {
            var occupiedChannels = new bool[4];
            foreach (var route in session.Design.Topology.Connectors)
                foreach (var node in route.Nodes)
                    if (node.Cell.Equals(cell)) occupiedChannels[node.Channel] = true;
            var channel = Array.FindIndex(occupiedChannels, occupied => !occupied);
            if (channel < 0)
                throw new ArgumentException("No free channel for an open connector.");

            if (selectedId == OneBitCatalogItemIds.WorldClockLink)
            {
                var clockNode = new RouteNode(Guid.NewGuid(), cell, channel,
                    new QuarterPoint(2, 2, 2));
                return new ConnectorRoute(Guid.NewGuid(), "netLink", 1,
                    new[] { clockNode }, Array.Empty<RouteSpan>(), "", null,
                    "@world-clock", "world", "worldClock", 2);
            }
            if (selectedId != OneBitCatalogItemIds.Wire)
                throw new ArgumentException("The selected item is not a connector.");
            var first = new RouteNode(Guid.NewGuid(), cell, channel,
                new QuarterPoint(1, 2, 2));
            var last = new RouteNode(Guid.NewGuid(), cell, channel,
                new QuarterPoint(3, 2, 2));
            return new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { first, last },
                new[] { new RouteSpan(Guid.NewGuid(), first.Id, last.Id) },
                geometryVersion: 2);
        }

        private void UpdateWirePreview()
        {
            if (!wireStart.HasValue || SelectedCatalogId() !=
                OneBitCatalogItemIds.Wire || !hasHit)
            {
                ClearWirePreview();
                return;
            }
            var start = wireStart.Value;
            Func<OneBitPinRouteProposal> propose = null;
            string key;
            if (hovered != null && IsPinKind(hovered.Kind))
            {
                var target = PickedPin(hovered);
                key = "pin:" + target.OwnerId.ToString("N") + ":" +
                    target.PartId.ToString("N");
                propose = () => OneBitPinRoutePlanner.Plan(
                    session.Design, start, target);
            }
            else if (hovered != null &&
                     hovered.Kind == WorldPartKind.ConnectorNode)
            {
                var target = JoinMember.ConnectorNode(hovered.OwnerId,
                    hovered.PartId);
                key = "node:" + target.OwnerId.ToString("N") + ":" +
                    target.PartId.ToString("N");
                propose = () => OneBitPinRoutePlanner.PlanToConnectorNode(
                    session.Design, start, target);
            }
            else if (hovered == null &&
                     hit.collider.GetComponent<GeneratedFloorSurface>() != null)
            {
                PlacementCell(out var cell);
                key = "cell:" + cell.X + ":" + cell.Y + ":" + cell.Z;
                propose = () => OneBitPinRoutePlanner.PlanToOpenCell(
                    session.Design, start, cell);
            }
            else
            {
                ClearWirePreview();
                return;
            }
            if (wirePreviewRoot != null && key == wirePreviewKey &&
                wirePreviewRevision == session.Revision)
                return;
            ClearWirePreview();
            wirePreviewKey = key;
            wirePreviewRevision = session.Revision;
            wirePreviewRoot = new GameObject("Wire route preview");
            try { DrawWirePreview(propose().Route); }
            catch (ArgumentException)
            {
                AddWirePreviewPart(PrimitiveType.Sphere,
                    hovered == null ? hit.point + Vector3.up * 0.2f :
                    hovered.transform.position,
                    Vector3.one * 0.25f,
                    new Color(1f, 0.15f, 0.15f, 0.65f));
            }
        }

        private void DrawWirePreview(ConnectorRoute route)
        {
            Vector3 PreviewPosition(RouteNode node) =>
                WireMeshGeometry.NodePosition(node.Cell,node.PointQ,node.Channel,route.GeometryVersion);
            var green = new Color(0.2f, 0.9f, 0.55f, 0.62f);
            var nodes = new Dictionary<Guid, RouteNode>();
            foreach (var node in route.Nodes)
            {
                nodes.Add(node.Id, node);
                AddWirePreviewPart(PrimitiveType.Sphere,
                    PreviewPosition(node),
                    Vector3.one * 0.08f, green);
            }
            foreach (var span in route.Spans)
            {
                var first = nodes[span.FromNodeId];
                var second = nodes[span.ToNodeId];
                var from = PreviewPosition(first);
                var to = PreviewPosition(second);
                if ((to - from).sqrMagnitude < 0.000001f)
                {
                    AddWirePreviewPart(PrimitiveType.Sphere, from,
                        Vector3.one * 0.125f, green);
                    continue;
                }
                if(route.GeometryVersion==2)
                { DrawWirePreviewCylinder(from,to,green); continue; }
                if (first.Cell.Equals(second.Cell) &&
                    (first.PointQ.IsCenter && second.PointQ.IsFacePoint ||
                     second.PointQ.IsCenter && first.PointQ.IsFacePoint))
                {
                    var center = first.PointQ.IsCenter ? from : to;
                    var face = first.PointQ.IsFacePoint ? from : to;
                    var point = first.PointQ.IsFacePoint
                        ? first.PointQ : second.PointQ;
                    var normal = WireMeshGeometry.FaceNormal(point);
                    var bend1 = center + normal * 0.15625f;
                    var lead = Mathf.Min(0.125f, Vector3.Dot(face - center, normal) * 0.25f);
                    var bend2 = face - normal * lead;
                    DrawWirePreviewCylinder(center, bend1, green);
                    DrawWirePreviewCylinder(bend1, bend2, green);
                    DrawWirePreviewCylinder(bend2, face, green);
                    continue;
                }
                var xBend = new Vector3(to.x, from.y, from.z);
                var yBend = new Vector3(to.x, to.y, from.z);
                DrawWirePreviewCylinder(from, xBend, green);
                DrawWirePreviewCylinder(xBend, yBend, green);
                DrawWirePreviewCylinder(yBend, to, green);
            }
        }

        private void DrawWirePreviewCylinder(Vector3 from, Vector3 to,
            Color color)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return;
            var part = AddWirePreviewPart(PrimitiveType.Cylinder,
                (from + to) * 0.5f,
                new Vector3(0.125f, delta.magnitude * 0.5f, 0.125f), color);
            part.transform.rotation = Quaternion.FromToRotation(Vector3.up,
                delta);
        }

        private GameObject AddWirePreviewPart(PrimitiveType type,
            Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(wirePreviewRoot.transform, false);
            part.transform.position = position;
            part.transform.localScale = scale;
            Destroy(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            var shader = Shader.Find("Transparent/Diffuse");
            if (shader != null) renderer.material = new Material(shader);
            renderer.material.color = color;
            return part;
        }

        private void ClearWirePreview()
        {
            wirePreviewKey = null;
            if (wirePreviewRoot == null) return;
            wirePreviewRoot.SetActive(false);
            Destroy(wirePreviewRoot);
            wirePreviewRoot = null;
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
                return IsInputPin(part.OwnerId, part.PartId);
            foreach (var module in session.Design.Modules)
                if (module.Id == part.OwnerId)
                    foreach (var port in module.InterfacePorts)
                        if (port.Id == part.PartId)
                            return port.Direction != OneBitPortDirection.Output;
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
            if (invalidAudio == null)
            {
                invalidAudio = gameObject.AddComponent<AudioSource>();
                invalidAudio.playOnAwake = false;
                invalidAudio.spatialBlend = 0f;
                invalidAudio.volume = 0.15f;
                const int sampleRate = 22050;
                var samples = new float[882]; // Quiet 40 ms invalid-action click.
                for (var i = 0; i < samples.Length; i++)
                {
                    var envelope = Mathf.Min(1f, i / 32f) * Mathf.Pow(1f - (float)i / samples.Length, 2f);
                    samples[i] = 0.3f * envelope * Mathf.Sin(2f * Mathf.PI * 220f * i / sampleRate);
                }
                invalidClick = AudioClip.Create("Invalid action", samples.Length, 1, sampleRate, false);
                invalidClick.SetData(samples, 0);
                if (viewCamera != null && FindAnyObjectByType<AudioListener>() == null)
                    viewCamera.gameObject.AddComponent<AudioListener>();
            }
            if (!invalidAudio.isPlaying) invalidAudio.PlayOneShot(invalidClick);
        }

        private void UpdateGhost()
        {
            var selectedCatalogId = SelectedCatalogId();
            var shiftHeld = Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
            if (shiftHeld && hasHit &&
                (selectedCatalogId == OneBitCatalogItemIds.Wire ||
                 selectedCatalogId == OneBitCatalogItemIds.WorldClockLink) &&
                (hovered == null ||
                 !IsPinKind(hovered.Kind) && !IsConnectorKind(hovered.Kind)) &&
                PlacementCell(out var openCell))
            {
                var openType = "open:" + selectedCatalogId;
                if (!ghostCached || !ghostCell.Equals(openCell) ||
                    ghostType != openType)
                {
                    ghostCell = openCell;
                    ghostType = openType;
                    ghostCached = true;
                    ghostValid = false;
                    try
                    {
                        var route = BuildOpenConnector(openCell, selectedCatalogId);
                        OneBitWorldEdits.PlaceConnector(session.Design, route,
                            Array.Empty<ElectricalJoin>());
                        ghostValid = true;
                    }
                    catch (ArgumentException) { }
                    catch (NotSupportedException) { }
                    DrawOpenConnectorGhost(openCell,
                        selectedCatalogId == OneBitCatalogItemIds.Wire);
                }
                if (ghostRoot != null) ghostRoot.SetActive(true);
                var openColor = ghostValid && Time.unscaledTime >= invalidUntil
                    ? new Color(0.2f, 0.9f, 0.55f, 0.42f)
                    : new Color(1f, 0.15f, 0.15f, 0.48f);
                foreach (var renderer in ghostRenderers)
                    renderer.material.color = openColor;
                return;
            }
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
                            moduleVersion.ExteriorSizeCells, ports);
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

        private void DrawOpenConnectorGhost(GridCell cell, bool wire)
        {
            if (ghostRoot != null) Destroy(ghostRoot);
            ghostRoot = new GameObject("Open connector preview");
            ghostRenderers.Clear();
            var center = new Vector3(cell.X + 0.5f, cell.Y + 0.5f,
                cell.Z + 0.5f);
            if (wire)
            {
                var part = AddGhostPrimitive(PrimitiveType.Cylinder, center,
                    new Vector3(0.125f, 0.25f, 0.125f));
                part.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else AddGhostPrimitive(PrimitiveType.Sphere, center,
                Vector3.one * 0.26f);
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

        private GameObject AddGhostPrimitive(PrimitiveType type, Vector3 position, Vector3 scale)
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
            return item;
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
            if (selectionLineMaterial != null) Destroy(selectionLineMaterial);
            if (invalidClick != null) Destroy(invalidClick);
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
            selectionRoot = new GameObject("Package region preview");
            if (selectionLineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException(
                    "Transparent selection-line shader is unavailable.");
                selectionLineMaterial = new Material(shader);
            }
            var min = new Vector3(region.Min.X, region.Min.Y, region.Min.Z);
            var max = new Vector3(region.Max.X + 1, region.Max.Y + 1,
                region.Max.Z + 1);
            var corners = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };
            var edges = new[]
            {
                (0, 1), (0, 2), (0, 4), (1, 3), (1, 5), (2, 3),
                (2, 6), (3, 7), (4, 5), (4, 6), (5, 7), (6, 7)
            };
            foreach (var edge in edges)
            {
                var lineObject = new GameObject("Selection edge");
                lineObject.transform.SetParent(selectionRoot.transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, corners[edge.Item1]);
                line.SetPosition(1, corners[edge.Item2]);
                line.startWidth = 0.045f;
                line.endWidth = 0.045f;
                line.sharedMaterial = selectionLineMaterial;
                line.startColor = new Color(0.15f, 0.55f, 1f, 0.5f);
                line.endColor = line.startColor;
            }
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

        private bool IsInputPin(Guid objectId, Guid pinId)
        {
            foreach (var component in session.Design.Components)
                if (component.Id == objectId)
                    foreach (var geometry in BuiltInPinCatalog.Pins(
                        component.TypeId, component.TypeVersion))
                        if (component.PinIds[geometry.Key] == pinId &&
                            geometry.Direction == PinDirection.Input)
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
            if (inspectOpen)
            {
                CloseInspection();
                return;
            }
            if (packageDraft != null)
            {
                packageDraft = null;
                packageError = "";
                ClearSelection();
                player.SetInterfaceOpen(false);
                return;
            }
            if (rotationPreview != null)
            {
                ClearRotationPreview();
                return;
            }
            if (wireStart.HasValue)
            {
                wireStart = null;
                ClearWirePreview();
                return;
            }
            if (configureOpen || inventoryOpen || pauseMenuOpen)
            {
                var wasPausedMenuOpen = pauseMenuOpen;
                configureOpen = false;
                inventoryOpen = false;
                pauseMenuOpen = false;
                inspectOpen = false;
                player.SetInterfaceOpen(false);
                if (wasPausedMenuOpen && session.Scheduler.Diagnostic == null)
                    session.Scheduler.ResumeSimulation();
                return;
            }
            if (selectionFirst.HasValue)
            {
                ClearSelection();
                return;
            }
            if (session.Scheduler.IsPaused)
            {
                if (session.Scheduler.Diagnostic != null)
                {
                    pauseMenuOpen = true;
                    player.SetInterfaceOpen(true);
                }
                else session.Scheduler.ResumeSimulation();
            }
            else
            {
                session.Scheduler.PauseSimulation();
                pauseMenuOpen = true;
                player.SetInterfaceOpen(true);
            }
        }

        private void ClearSelection()
        {
            selectionFirst = null;
            selectionSecond = null;
            if (selectionRoot == null) return;
            selectionRoot.SetActive(false);
            Destroy(selectionRoot);
            selectionRoot = null;
        }

        private void OnGUI()
        {
            if (session == null) return;
            if (session.Scheduler.Diagnostic != null)
            {
                var oldColor = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.55f);
                GUI.Box(new Rect(12f, 108f, Mathf.Min(Screen.width - 24f, 720f),
                    58f), session.Scheduler.Diagnostic);
                GUI.color = oldColor;
            }
            if (hudVisible && packageDraft == null)
            {
                var crosshairStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 60
                };
                crosshairStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(Screen.width * 0.5f - 45f,
                    Screen.height * 0.5f - 45f, 90f, 90f), "+",
                    crosshairStyle);
                var width = 9 * 72f;
                var left = (Screen.width - width) * 0.5f;
                for (var i = 0; i < 9; i++)
                {
                    var title = (i + 1) + " " + SlotLabel(i);
                    if (!inspectOpen && GUI.Button(new Rect(left + i * 72f,
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
                        "Wire start selected. Aim at a pin, node, or floor cell.");
                QuickLook();
                if (persistenceMessage.Length > 0)
                    GUI.Box(new Rect(12f, Screen.height - 112f, 600f, 42f),
                        persistenceMessage);
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
            var area = new Rect(24f, 24f, Screen.width - 48f,
                Screen.height - 48f);
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = oldColor;
            var font = Mathf.Clamp(Mathf.RoundToInt(Screen.height *
                (36f / 1080f)), 26, 42);
            var labelStyle = new GUIStyle(GUI.skin.label)
            { fontSize = font, wordWrap = true };
            labelStyle.normal.textColor = Color.white;
            var buttonStyle = new GUIStyle(GUI.skin.button)
            { fontSize = font - 3, wordWrap = true };
            var fieldStyle = new GUIStyle(GUI.skin.textField)
            { fontSize = font - 3 };
            GUI.Label(new Rect(area.x + 24f, area.y + 12f,
                area.width - 48f, 48f), "Package configuration preview",
                labelStyle);
            var captured = packageDraft.Snapshot.SizeCells;
            var exterior = packageDraft.ExteriorSizeCells;
            GUI.Label(new Rect(area.x + 24f, area.y + 60f,
                area.width - 48f, 76f),
                "Captured circuit " + captured.X + " × " + captured.Y +
                " × " + captured.Z + " cells; exterior " + exterior.X +
                " × " + exterior.Y + " × " + exterior.Z + "; " +
                packageDraft.Snapshot.Components.Count + " components; " +
                packageDraft.Ports.Count + " connected ports. Source stays in place.",
                labelStyle);
            GUI.Label(new Rect(area.x + 24f, area.y + 137f, 260f, 50f),
                "Module name:", labelStyle);
            packageName = GUI.TextField(new Rect(area.x + 300f, area.y + 137f,
                Mathf.Min(570f, area.width - 330f), 50f), packageName,
                fieldStyle);
            packageDraft.Name = packageName;

            var faces = new[] { GridDirection.West, GridDirection.East,
                GridDirection.North, GridDirection.South, GridDirection.Up,
                GridDirection.Down };
            var columns = area.width >= 1360f ? 2 : 1;
            var rowHeights = new List<float>();
            for (var start = 0; start < faces.Length; start += columns)
            {
                var height = 0f;
                for (var j = 0; j < columns && start + j < faces.Length; j++)
                    height = Mathf.Max(height, 58f +
                        Mathf.Max(1, CountPortsOnFace(faces[start + j])) * 72f);
                rowHeights.Add(height);
            }
            var totalHeight = 0f;
            foreach (var height in rowHeights) totalHeight += height + 16f;
            var scrollArea = new Rect(area.x + 24f, area.y + 202f,
                area.width - 48f, area.height - 332f);
            var contentWidth = scrollArea.width - 22f;
            var content = new Rect(0f, 0f, contentWidth,
                Mathf.Max(scrollArea.height, totalHeight));
            packageScroll = GUI.BeginScrollView(scrollArea, packageScroll,
                content);
            var y = 0f;
            for (var group = 0; group < rowHeights.Count; group++)
            {
                var cardWidth = (contentWidth - (columns - 1) * 16f) / columns;
                for (var col = 0; col < columns; col++)
                {
                    var index = group * columns + col;
                    if (index >= faces.Length) break;
                    DrawPackageFace(faces[index], new Rect(
                        col * (cardWidth + 16f), y, cardWidth,
                        rowHeights[group]), labelStyle, buttonStyle,
                        fieldStyle);
                }
                y += rowHeights[group] + 16f;
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(area.x + 24f, area.yMax - 118f,
                area.width - 48f, 48f),
                packageError.Length == 0
                    ? "Every connected port stays; rename, set direction, or move its face."
                    : packageError, labelStyle);
            var buttonWidth = Mathf.Min(310f, (area.width - 96f) / 3f);
            var buttonY = area.yMax - 63f;
            if (GUI.Button(new Rect(area.x + 24f, buttonY, buttonWidth, 50f),
                "Validate draft", buttonStyle))
            {
                try
                {
                    var candidate = packageDraft.BuildCandidate(Guid.NewGuid());
                    packageError = "Valid fixed design: " + candidate.Ports.Count +
                        " ports. Publish when ready.";
                }
                catch (ArgumentException exception)
                { packageError = exception.Message; }
            }
            if (GUI.Button(new Rect(area.x + (area.width - buttonWidth) * 0.5f,
                buttonY, buttonWidth, 50f), "Publish module", buttonStyle))
            {
                try
                {
                    persistenceMessage = publishPackage == null
                        ? "Package storage is unavailable."
                        : publishPackage(packageDraft);
                    packageDraft = null;
                    ClearSelection();
                    player.SetInterfaceOpen(false);
                }
                catch (Exception error) when (error is IOException ||
                    error is ArgumentException || error is InvalidOperationException ||
                    error is UnauthorizedAccessException)
                { packageError = error.Message; }
            }
            if (GUI.Button(new Rect(area.xMax - buttonWidth - 24f, buttonY,
                buttonWidth, 50f), "Close", buttonStyle))
            {
                packageDraft = null;
                packageError = "";
                ClearSelection();
                player.SetInterfaceOpen(false);
            }
        }

        private int CountPortsOnFace(GridDirection face)
        {
            var count = 0;
            foreach (var port in packageDraft.Ports)
                if (PortFace(port.PointQ) == face) count++;
            return count;
        }

        private void DrawPackageFace(GridDirection face, Rect card,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle fieldStyle)
        {
            GUI.Box(card, "");
            GUI.Label(new Rect(card.x + 12f, card.y + 5f,
                card.width - 24f, 42f), GridOrientation.FaceName(face),
                labelStyle);
            var row = 0;
            for (var i = 0; i < packageDraft.Ports.Count; i++)
            {
                var choice = packageDraft.Ports[i];
                if (PortFace(choice.PointQ) != face) continue;
                var y = card.y + 54f + row++ * 72f;
                var nameWidth = card.width * 0.26f;
                var directionWidth = card.width * 0.16f;
                var targetWidth = card.width * 0.17f;
                var faceWidth = card.width * 0.22f;
                var name = GUI.TextField(new Rect(card.x + 10f, y,
                    nameWidth, 52f), choice.Name, fieldStyle);
                if (name != choice.Name)
                    packageDraft.ReplacePort(i, new OneBitPortChoice(name,
                        choice.Direction, choice.LocalCell, choice.PointQ,
                        choice.BitZeroTarget));
                var directionX = card.x + 18f + nameWidth;
                if (GUI.Button(new Rect(directionX, y, directionWidth, 52f),
                    choice.Direction.ToString(), buttonStyle))
                {
                    var next = (OneBitPortDirection)
                        (((int)choice.Direction + 1) % 3);
                    packageDraft.ReplacePort(i, new OneBitPortChoice(name,
                        next, choice.LocalCell, choice.PointQ,
                        choice.BitZeroTarget));
                }
                var targetX = directionX + directionWidth + 8f;
                GUI.Label(new Rect(targetX, y, targetWidth, 52f),
                    EndpointName(choice.BitZeroTarget), labelStyle);
                if (GUI.Button(new Rect(targetX + targetWidth + 8f, y,
                    faceWidth, 52f), "Move face", buttonStyle))
                    MovePortToNextFace(i);
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

        private void MovePortToNextFace(int index)
        {
            var faces = new[] { GridDirection.West, GridDirection.East,
                GridDirection.North, GridDirection.South, GridDirection.Up,
                GridDirection.Down };
            var choice = packageDraft.Ports[index];
            var current = PortFace(choice.PointQ);
            var start = Array.IndexOf(faces, current);
            var size = packageDraft.ExteriorSizeCells;
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
                Screen.height * 0.5f - 310f, 380f, 620f);
            GUI.Box(rect, "Simulation paused");
            GUI.Label(new Rect(rect.x + 20f, rect.y + 45f, 105f, 24f),
                "World name:");
            worldNameText = GUI.TextField(new Rect(rect.x + 125f,
                rect.y + 45f, 155f, 25f), worldNameText);
            if (GUI.Button(new Rect(rect.x + 285f, rect.y + 45f, 70f, 25f),
                "Apply"))
                RunPersistence(() => renameWorld(worldNameText));
            GUI.Label(new Rect(rect.x + 20f, rect.y + 95f, 140f, 28f),
                "World clock Hz:");
            frequencyText = GUI.TextField(new Rect(rect.x + 165f,
                rect.y + 95f, 180f, 26f), frequencyText);
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 135f, 155f, 32f),
                "Apply frequency"))
                TryEdit(() => session.Scheduler.SetFrequency(frequencyText));
            if (GUI.Button(new Rect(rect.x + 195f, rect.y + 135f, 155f, 32f),
                "Reset Simulation"))
                TryEdit(() =>
                {
                    session.Scheduler.ResetSimulation();
                    session.Scheduler.PauseSimulation();
                });
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 185f,
                155f, 36f), "Step clock edge"))
                TryEdit(() => session.Scheduler.StepClockEdge());
            if (GUI.Button(new Rect(rect.x + 195f, rect.y + 185f,
                155f, 36f), "Step full cycle"))
                TryEdit(() => session.Scheduler.StepClockCycle());
            GUI.Label(new Rect(rect.x + 20f, rect.y + 234f, 340f, 28f),
                "Settled time " + session.Scheduler.Now +
                "   CLK " + session.Scheduler.ClockLevel.ToSymbol());
            if (GUI.Button(new Rect(rect.x + 20f, rect.y + 272f, 155f, 34f),
                "Save World"))
                RunPersistence(saveWorld);
            if (GUI.Button(new Rect(rect.x + 195f, rect.y + 272f, 155f, 34f),
                "Reopen Saved"))
                RunPersistence(reopenWorld);
            if (GUI.Button(new Rect(rect.x + 105f, rect.y + 316f, 170f, 32f),
                "Browse saves/recovery"))
            {
                try
                {
                    savedWorldChoices = listSavedWorlds();
                    browseWorlds = true;
                }
                catch (Exception error) when (error is IOException ||
                    error is ArgumentException || error is UnauthorizedAccessException)
                { persistenceMessage = error.Message; }
            }
            if (browseWorlds)
            {
                var area = new Rect(rect.x + 20f, rect.y + 360f, 340f, 150f);
                var content = new Rect(0f, 0f, 315f,
                    Mathf.Max(150f, savedWorldChoices.Count * 34f));
                savedWorldScroll = GUI.BeginScrollView(area, savedWorldScroll, content);
                for (var i = 0; i < savedWorldChoices.Count; i++)
                {
                    var choice = savedWorldChoices[i];
                    if (GUI.Button(new Rect(0f, i * 34f, 310f, 30f),
                        choice.WorldName + "  " + choice.Kind + "  " +
                        choice.SavedUtc.ToString("MM-dd HH:mm") + " UTC"))
                    {
                        try { persistenceMessage = loadSavedWorld(choice); }
                        catch (Exception error) when (error is IOException ||
                            error is ArgumentException ||
                            error is InvalidOperationException ||
                            error is UnauthorizedAccessException)
                        { persistenceMessage = error.Message; }
                    }
                }
                GUI.EndScrollView();
            }
            GUI.Label(new Rect(rect.x + 20f, rect.y + 520f, 340f, 42f),
                persistenceMessage);
            if (GUI.Button(new Rect(rect.x + 105f, rect.y + 565f, 170f, 40f),
                "Resume"))
            {
                session.Scheduler.ResumeSimulation();
                pauseMenuOpen = false;
                player.SetInterfaceOpen(false);
            }
        }

        private void RunPersistence(Func<string> command)
        {
            try
            {
                persistenceMessage = command == null
                    ? "Save controls are unavailable." : command();
            }
            catch (Exception error) when (error is IOException ||
                error is ArgumentException || error is InvalidOperationException ||
                error is UnauthorizedAccessException)
            { persistenceMessage = error.Message; }
        }

        private void DrawInspect()
        {
            if (inspectedKind == WorldPartKind.ModuleBody)
            {
                DrawModuleInternalInspect();
                return;
            }
            if (inspectedKind == WorldPartKind.ComponentBody)
            {
                DrawComponentInspect();
                return;
            }
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
            catch (KeyNotFoundException) { CloseInspection(); return; }
            var names = new List<string>();
            foreach (var pin in detail.ConnectedPins) names.Add(PinName(pin));
            DrawInspectionPanel("Inspect one-bit net: " +
                detail.Value.ToSymbol(), new[]
                {
                    "Width 1   Tag " + detail.Tag,
                    "Connections: " + string.Join(", ", names),
                    "Active drivers: " + detail.ActiveDrivers.Count,
                    detail.Explanation
                });
        }

        private void DrawComponentInspect()
        {
            PlacedOneBitComponent component = null;
            foreach (var item in session.Design.Components)
                if (item.Id == inspectedOwner) { component = item; break; }
            if (component == null) { CloseInspection(); return; }
            var names = component.PinIds.Keys.ToList();
            names.Sort(StringComparer.Ordinal);
            var rows = new List<string>();
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                var detail = session.Inspector.InspectPin(component.Id,
                    component.PinIds[name]);
                var connections = new List<string>();
                foreach (var pin in detail.ConnectedPins)
                    connections.Add(PinName(pin));
                rows.Add(
                    name + "=" + detail.Value.ToSymbol() + "  drivers " +
                    detail.ActiveDrivers.Count + "  " + detail.Explanation +
                    "\nConnected: " + string.Join(", ", connections));
            }
            DrawInspectionPanel("Inspect " + component.TypeId + "  width 1",
                rows);
        }

        private void DrawModuleInternalInspect()
        {
            PlacedOneBitModuleInstance instance = null;
            foreach (var item in session.Design.Modules)
                if (item.Id == inspectedOwner) { instance = item; break; }
            if (instance == null) { CloseInspection(); return; }
            if (!session.ModuleVersions.TryGetValue(instance.VersionId,
                out var version))
            {
                DrawInspectionPanel(instance.InstanceName +
                    " — exact module missing", new[]
                    { "Exterior ports and connections remain as a placeholder." });
                return;
            }
            var lines = new List<string>();
            foreach (var component in version.Components)
            {
                var pins = new List<string>();
                foreach (var pair in component.PinIds)
                    pins.Add(pair.Key + "=" + session.Inspector.InspectInternalPin(
                        instance.InstanceId, component.Id, pair.Value).ToSymbol());
                pins.Sort(StringComparer.Ordinal);
                lines.Add(component.TypeId + "  local (" +
                    component.AnchorCell.X + "," + component.AnchorCell.Y +
                    "," + component.AnchorCell.Z + ")  " +
                    string.Join("  ", pins));
            }
            DrawInspectionPanel("Inside " + instance.InstanceName +
                "  version " + instance.VersionId.ToString("D").Substring(0, 8),
                lines);
        }

        private void DrawInspectionPanel(string title,
            IReadOnlyList<string> rows)
        {
            var area = new Rect(Screen.width * 0.04f, Screen.height * 0.06f,
                Screen.width * 0.92f, Screen.height * 0.88f);
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.86f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previousColor;
            var fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height *
                (48f / 1080f)), 32, 56);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            style.normal.textColor = Color.white;
            var margin = Mathf.Max(25f, fontSize * 0.7f);
            var contentWidth = area.width - margin * 2f - 24f;
            var titleHeight = style.CalcHeight(new GUIContent(title), contentWidth);
            GUI.Label(new Rect(area.x + margin, area.y + margin,
                contentWidth, titleHeight), title, style);
            var scrollArea = new Rect(area.x + margin,
                area.y + margin + titleHeight + 14f,
                area.width - margin * 2f,
                area.height - margin * 2f - titleHeight - 14f);
            var heights = new List<float>();
            var contentHeight = 0f;
            foreach (var row in rows)
            {
                var height = style.CalcHeight(new GUIContent(row), contentWidth);
                heights.Add(height);
                contentHeight += height + fontSize * 0.4f;
            }
            var content = new Rect(0f, 0f, contentWidth,
                Mathf.Max(scrollArea.height, contentHeight));
            internalInspectScroll = GUI.BeginScrollView(scrollArea,
                internalInspectScroll, content);
            var y = 0f;
            for (var i = 0; i < rows.Count; i++)
            {
                GUI.Label(new Rect(0f, y, contentWidth, heights[i]),
                    rows[i], style);
                y += heights[i] + fontSize * 0.4f;
            }
            GUI.EndScrollView();
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
