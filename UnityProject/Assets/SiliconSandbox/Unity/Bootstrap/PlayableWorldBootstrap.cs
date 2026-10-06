using System;
using System.IO;
using System.Collections.Generic;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Interaction;
using SiliconSandbox.Persistence;
using SiliconSandbox.Presentation;
using UnityEngine;

namespace SiliconSandbox.Bootstrap
{
    public sealed class PlayableWorldBootstrap : MonoBehaviour
    {
        private const string SmokeOutputVariable = "SILICON_SANDBOX_SMOKE_OUTPUT";
        private const string PersistenceSmokeOutputVariable =
            "SILICON_SANDBOX_PERSISTENCE_SMOKE_OUTPUT";

        public OneBitWorldContext Context { get; private set; }
        public OneBitWorldSession Session { get; private set; }
        public OneBitPlayerInventory Inventory { get; private set; }
        public OneBitWorldView WorldView { get; private set; }
        public OneBitWorldInteraction Interaction { get; private set; }
        private GameObject wallsRoot;
        private Transform playerTransform;
        private Camera playerCamera;
        private CreativeCameraController controller;
        private string storageRootOverride;
        private double nextAutosaveRealtime;

        public void SetStorageRootForVerification(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                throw new ArgumentException("Verification storage root must exist.");
            storageRootOverride = Path.GetFullPath(root);
        }

        private string StorageRoot => storageRootOverride ??
            UnityEngine.Application.persistentDataPath;

        private void Start()
        {
            Context = OneBitWorldContext.NewFreeplay("New World",
                new WorldBounds(32, 32, 16));
            Session = Context.Session;
            Inventory = Context.Inventory;
            BuildGeneratedWorld(Session.Design.Bounds);
            var renderedWorld = new GameObject("Authored one-bit world");
            WorldView = renderedWorld.AddComponent<OneBitWorldView>();
            WorldView.Attach(Session);

            var playerObject = new GameObject("Creative player");
            playerObject.transform.position = new Vector3(15.5f, 1.05f, 4f);
            playerTransform = playerObject.transform;
            var capsule = playerObject.AddComponent<CharacterController>();
            capsule.height = 3f;
            capsule.radius = 0.32f;
            capsule.center = new Vector3(0f, 1.5f, 0f);
            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Playable world needs a MainCamera.");
            camera.transform.SetParent(playerObject.transform, false);
            camera.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.fieldOfView = 70f;
            playerCamera = camera;
            controller = playerObject.AddComponent<CreativeCameraController>();
            controller.SetCameraPivot(camera.transform);
            controller.SetWorldBounds(Session.Design.Bounds);
            Interaction = playerObject.AddComponent<OneBitWorldInteraction>();
            Interaction.Attach(Session, controller, Inventory);
            Interaction.SetPersistenceActions(SaveCurrentWorldFile,
                ReopenCurrentWorldFile, PublishPackage, ListSavedWorlds,
                OpenRecoveryChoice, () => Context.WorldName, RenameCurrentWorld);
            nextAutosaveRealtime = Time.realtimeSinceStartupAsDouble + 300d;

            var output = Environment.GetEnvironmentVariable(SmokeOutputVariable);
            if (!string.IsNullOrEmpty(output))
            {
                var valid = Session.Circuit != null &&
                    Session.Design.Components.Count == 0 &&
                    WorldView.Session == Session && Interaction.Session == Session &&
                    GetComponent<Collider>() != null;
                File.WriteAllText(output, valid ? "PASS\n" : "FAIL\n");
                UnityEngine.Application.Quit(valid ? 0 : 1);
                return;
            }
            var persistenceOutput = Environment.GetEnvironmentVariable(
                PersistenceSmokeOutputVariable);
            if (!string.IsNullOrEmpty(persistenceOutput))
            {
                RunPersistenceSmoke(persistenceOutput);
                return;
            }
            var benchmarkOutput = Environment.GetEnvironmentVariable(
                FirstPlayableBenchmarkRunner.OutputVariable);
            if (!string.IsNullOrEmpty(benchmarkOutput))
                gameObject.AddComponent<FirstPlayableBenchmarkRunner>()
                    .Initialize(this, benchmarkOutput);
        }

        private void RunPersistenceSmoke(string outputPath)
        {
            try
            {
                var root = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                SetStorageRootForVerification(root);
                Session.PlaceComponent(BuiltInPinCatalog.And,
                    new GridCell(6, 1, 6), GridOrientation.Default);
                var id = Session.Design.Components[0].Id;
                SaveCurrentWorldFile();
                ReopenCurrentWorldFile();
                if (Session.Design.Components.Count != 1 ||
                    Session.Design.Components[0].Id != id ||
                    !Session.Scheduler.Now.Equals(
                        SiliconSandbox.Simulation.SimulationTime.Zero))
                    throw new InvalidDataException(
                        "Built-player save/reopen lost authored state.");
                File.WriteAllText(outputPath,
                    "PASS " + Context.WorldId.ToString("D") + "\n");
                UnityEngine.Application.Quit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(outputPath, "FAIL " + error.Message + "\n");
                UnityEngine.Application.Quit(1);
            }
        }

        public WorldSaveSnapshot CaptureCurrentWorld()
        {
            if (Context == null || playerTransform == null || playerCamera == null)
                throw new InvalidOperationException("Playable world is not ready.");
            var position = playerTransform.position;
            var look = playerCamera.transform.forward.normalized;
            return Context.Capture(new SavedPlayerPose(position.x,
                position.y, position.z, look.x, look.y, look.z));
        }

        public string RenameCurrentWorld(string name)
        {
            if (Context == null) throw new InvalidOperationException("World is not ready.");
            Context.RenameWorld(name);
            return "World named: " + Context.WorldName;
        }

        public void OpenWorld(WorldSaveSnapshot saved)
        {
            if (WorldView == null || Interaction == null || controller == null)
                throw new InvalidOperationException("Playable world is not ready.");
            Context = OneBitWorldContext.Open(saved);
            Session = Context.Session;
            Inventory = Context.Inventory;
            BuildGeneratedWorld(Session.Design.Bounds);
            controller.SetWorldBounds(Session.Design.Bounds);
            controller.Teleport(new Vector3((float)saved.Player.X,
                (float)saved.Player.Y, (float)saved.Player.Z));
            controller.SetViewDirection(new Vector3(
                (float)saved.Player.LookX, (float)saved.Player.LookY,
                (float)saved.Player.LookZ).normalized);
            WorldView.Attach(Session);
            Interaction.Attach(Session, controller, Inventory);
            nextAutosaveRealtime = Time.realtimeSinceStartupAsDouble + 300d;
        }

        public string SaveCurrentWorldFile()
        {
            var saved = CaptureCurrentWorld();
            var root = StorageRoot;
            Directory.CreateDirectory(Path.Combine(root, "worlds"));
            var path = ModuleLibraryStore.WorldPath(root, saved.WorldId);
            var archive = WorldV1ArchiveCodec.Write(saved);
            VerifiedWorldFileStore.Save(path, archive, input =>
            {
                var checkedWorld = WorldV1ArchiveCodec.Read(input);
                if (checkedWorld.Snapshot.WorldId != saved.WorldId ||
                    checkedWorld.UnavailableModuleVersionIds.Count != 0)
                    throw new InvalidDataException(
                        "Prepared save failed exact-version validation.");
            });
            return "World saved: " + saved.WorldName;
        }

        public string ReopenCurrentWorldFile()
        {
            if (Context == null) throw new InvalidOperationException("World is not ready.");
            return OpenWorldFile(Context.WorldId);
        }

        public IReadOnlyList<WorldRecoveryChoice> ListSavedWorlds() =>
            WorldRecoveryStore.List(StorageRoot);

        public string OpenRecoveryChoice(WorldRecoveryChoice choice)
        {
            if (choice == null) throw new ArgumentNullException(nameof(choice));
            var found = false;
            foreach (var offered in ListSavedWorlds())
                if (offered.WorldId == choice.WorldId &&
                    offered.Kind == choice.Kind &&
                    string.Equals(offered.Path, choice.Path,
                        StringComparison.Ordinal)) found = true;
            if (!found)
                throw new InvalidOperationException(
                    "Recovery choice is no longer a valid saved archive.");
            return OpenArchiveFile(choice.Path, choice.WorldId);
        }

        public string OpenWorldFile(Guid worldId)
        {
            var root = StorageRoot;
            var path = ModuleLibraryStore.WorldPath(root, worldId);
            if (!File.Exists(path))
                throw new FileNotFoundException("No saved copy of this world exists.", path);
            return OpenArchiveFile(path, worldId);
        }

        private string OpenArchiveFile(string path, Guid worldId)
        {
            var root = StorageRoot;
            AtomicPackageFilePublisher.Recover(root);
            LoadedWorldV1Archive loaded;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read))
            {
                var records = WorldArchiveContainer.Read(input);
                var manifest = WorldManifestJsonReader.Read(records.ManifestJson);
                IReadOnlyDictionary<Guid, byte[]> copies = null;
                try
                {
                    var ids = new List<Guid>();
                    foreach (var version in manifest.ModuleVersions)
                        ids.Add(version.VersionId);
                    copies = ModuleLibraryStore.ExactCopies(root, ids);
                }
                catch (InvalidDataException)
                {
                    // A damaged library index does not invalidate an otherwise
                    // healthy embedded world; unresolved modules use placeholders.
                }
                input.Position = 0;
                loaded = WorldV1ArchiveCodec.Read(input, copies);
            }
            if (loaded.Snapshot.WorldId != worldId)
                throw new InvalidDataException("Saved world identity changed.");
            OpenWorld(loaded.Snapshot);
            return loaded.UnavailableModuleVersionIds.Count == 0
                ? "World reopened at simulation time zero."
                : "World reopened with " + loaded.UnavailableModuleVersionIds.Count +
                  " unavailable exact module version(s).";
        }

        private void Update()
        {
            if (!AutomaticSavingEnabled || Context == null ||
                Time.realtimeSinceStartupAsDouble < nextAutosaveRealtime)
                return;
            nextAutosaveRealtime = Time.realtimeSinceStartupAsDouble + 300d;
            try { WorldRecoveryStore.SaveAutosave(StorageRoot,
                CaptureCurrentWorld(), DateTime.UtcNow); }
            catch (Exception error) { Debug.LogError(
                "SiliconSandbox autosave failed: " + error.Message); }
        }

        private void OnApplicationQuit()
        {
            if (!AutomaticSavingEnabled || Context == null) return;
            try { WorldRecoveryStore.SaveAutosave(StorageRoot,
                CaptureCurrentWorld(), DateTime.UtcNow); }
            catch (Exception error) { Debug.LogError(
                "SiliconSandbox exit save failed: " + error.Message); }
        }

        private bool AutomaticSavingEnabled =>
            !UnityEngine.Application.isEditor &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                SmokeOutputVariable)) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                FirstPlayableBenchmarkRunner.OutputVariable));

        public string PublishPackage(OneBitPackageDraft draft)
        {
            if (draft == null || Context == null)
                throw new ArgumentException("A current package draft is required.");
            var root = StorageRoot;
            Directory.CreateDirectory(root);
            AtomicPackageFilePublisher.Recover(root);
            var staged = OneBitPackageStager.Prepare(Context, draft,
                Guid.NewGuid(), CaptureCurrentWorld().Player);
            var definition = WorldV1JsonWriter.WriteModule(staged.Version);
            var world = WorldV1ArchiveCodec.Write(staged.SavedWorld);
            var index = ModuleLibraryStore.WithAddedVersion(root, definition);
            var receipt = AtomicPackageFilePublisher.Publish(root,
                staged.SavedWorld.WorldId, staged.Version.VersionId,
                world, index, definition,
                verifyWorld =>
                {
                    var loaded = WorldV1ArchiveCodec.Read(verifyWorld);
                    if (loaded.Snapshot.WorldId != staged.SavedWorld.WorldId ||
                        loaded.UnavailableModuleVersionIds.Count != 0 ||
                        !loaded.Snapshot.ModuleVersions.ContainsKey(
                            staged.Version.VersionId))
                        throw new InvalidDataException("Prepared package world is invalid.");
                },
                verifyIndex =>
                {
                    using (var memory = new MemoryStream())
                    {
                        verifyIndex.CopyTo(memory);
                        var found = false;
                        foreach (var item in ModuleLibraryIndexJson.Read(memory.ToArray()))
                            if (item.VersionId == staged.Version.VersionId &&
                                item.FamilyId == staged.Version.FamilyId &&
                                item.Sha256 == WorldManifestIntegrity.Sha256Hex(
                                    definition)) found = true;
                        if (!found) throw new InvalidDataException(
                            "Prepared library index lacks the exact version.");
                    }
                },
                verifyDefinition =>
                {
                    using (var memory = new MemoryStream())
                    {
                        verifyDefinition.CopyTo(memory);
                        var parsed = WorldV1JsonReader.ReadModule(memory.ToArray());
                        if (parsed.VersionId != staged.Version.VersionId ||
                            parsed.FamilyId != staged.Version.FamilyId)
                            throw new InvalidDataException(
                                "Prepared definition identity changed.");
                    }
                });
            Context.ApplyDurablyPublishedPackage(staged, receipt);
            Inventory = Context.Inventory;
            Interaction.Attach(Session, controller, Inventory);
            return "Published module " + staged.Version.Name +
                " into inventory slot " + (staged.InventorySlot + 1) + ".";
        }

        private void BuildGeneratedWorld(WorldBounds bounds)
        {
            if (GetComponent<GeneratedFloorSurface>() == null)
                gameObject.AddComponent<GeneratedFloorSurface>();
            if (wallsRoot != null)
            {
                wallsRoot.SetActive(false);
                Destroy(wallsRoot);
            }
            wallsRoot = new GameObject("Generated sandbox boundaries");
            transform.position = new Vector3(bounds.WidthCells * 0.5f,
                0.5f, bounds.LengthCells * 0.5f);
            transform.localScale = new Vector3(bounds.WidthCells, 1f,
                bounds.LengthCells);
            GetComponent<Renderer>().material.color =
                new Color(0.84f, 0.76f, 0.59f);
            var height = bounds.HeightCells;
            var middleY = height * 0.5f;
            Wall(wallsRoot.transform, "West", new Vector3(-0.5f, middleY,
                bounds.LengthCells * 0.5f),
                new Vector3(1f, height, bounds.LengthCells + 2f));
            Wall(wallsRoot.transform, "East", new Vector3(bounds.WidthCells + 0.5f, middleY,
                bounds.LengthCells * 0.5f),
                new Vector3(1f, height, bounds.LengthCells + 2f));
            Wall(wallsRoot.transform, "South", new Vector3(bounds.WidthCells * 0.5f, middleY,
                -0.5f), new Vector3(bounds.WidthCells + 2f, height, 1f));
            Wall(wallsRoot.transform, "North", new Vector3(bounds.WidthCells * 0.5f, middleY,
                bounds.LengthCells + 0.5f),
                new Vector3(bounds.WidthCells + 2f, height, 1f));
        }

        private static void Wall(Transform root, string face,
            Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Sandbox boundary " + face;
            wall.transform.SetParent(root, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material.color =
                new Color(0.44f, 0.27f, 0.13f);
        }
    }
}
