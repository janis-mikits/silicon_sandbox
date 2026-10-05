using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;
using SiliconSandbox.Presentation;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using BigInteger = System.Numerics.BigInteger;

namespace SiliconSandbox.Bootstrap
{
    // Opt-in graphical-player measurement against the frozen V1 reference.
    public sealed class FirstPlayableBenchmarkRunner : MonoBehaviour
    {
        public const string OutputVariable = "SILICON_SANDBOX_BENCHMARK_OUTPUT";
        public const string NativeDiagnosticVariable =
            "SILICON_SANDBOX_BENCHMARK_NATIVE_DIAGNOSTIC";
        private const double WarmupSeconds = 10d;
        private const double MeasureSeconds = 60d;
        private const string ReferenceName = "first-playable-1000-gates";
        private PlayableWorldBootstrap bootstrap;
        private string outputPath;
        private OneBitWorldSession session;
        private Transform player;
        private Camera view;
        private double fractionalPicoseconds;
        private long simulationTicks;
        private readonly StringBuilder report = new StringBuilder();

        public void Initialize(PlayableWorldBootstrap owner, string output)
        {
            bootstrap = owner ?? throw new ArgumentNullException(nameof(owner));
            outputPath = string.IsNullOrWhiteSpace(output)
                ? throw new ArgumentException("Benchmark output path is required.")
                : output;
        }

        private IEnumerator Start()
        {
            if (bootstrap == null) yield break;
            if (UnityEngine.Application.isBatchMode)
            {
                Finish("UNVERIFIED: graphical player required; batch mode has no rendered FPS.", 1);
                yield break;
            }
            // A terminal-launched graphical player may not own focus. Keep the
            // fixed-duration measurement advancing while the window is behind it.
            UnityEngine.Application.runInBackground = true;
            var standard = Array.IndexOf(QualitySettings.names, "Standard");
            if (standard < 0)
            {
                Finish("UNVERIFIED: Standard quality preset is unavailable.", 1);
                yield break;
            }
            QualitySettings.SetQualityLevel(standard, true);
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = -1;
            // macOS Retina desktops may reject a 1920x1080 window when the
            // logical desktop is smaller; fullscreen-window keeps the display
            // mode while requesting a 1920x1080 rendered content resolution.
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
            var resolutionDeadline = Time.realtimeSinceStartupAsDouble + 5d;
            do { yield return null; }
            while ((Screen.width != 1920 || Screen.height != 1080) &&
                Time.realtimeSinceStartupAsDouble < resolutionDeadline);
            var diagnosticNative = Environment.GetEnvironmentVariable(
                NativeDiagnosticVariable) == "1";
            if ((Screen.width != 1920 || Screen.height != 1080) &&
                !diagnosticNative)
            {
                Finish("UNVERIFIED: actual display resolution is " +
                    Screen.width + "x" + Screen.height + ", not 1920x1080.", 1);
                yield break;
            }

            WorldSaveSnapshot snapshot;
            string referenceHash;
            try
            {
                var directory = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                    "Benchmarks");
                var archive = File.ReadAllBytes(Path.Combine(directory,
                    ReferenceName + ".ssworld"));
                referenceHash = File.ReadAllText(Path.Combine(directory,
                    ReferenceName + ".sha256")).Trim();
                if (!string.Equals(WorldManifestIntegrity.Sha256Hex(archive),
                    referenceHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Reference SHA-256 mismatch.");
                using (var stream = new MemoryStream(archive))
                    snapshot = WorldV1ArchiveCodec.Read(stream).Snapshot;
                ValidateReference(snapshot);
            }
            catch (Exception error)
            {
                Finish("UNVERIFIED: reference archive failed validation: " +
                    error.Message, 1);
                yield break;
            }
            bootstrap.OpenWorld(snapshot);
            bootstrap.Interaction.enabled = false;
            var controller = bootstrap.Interaction.GetComponent<
                SiliconSandbox.Interaction.CreativeCameraController>();
            controller.enabled = false;
            player = bootstrap.Interaction.transform;
            view = Camera.main;
            session = bootstrap.Session;
            yield return null;

            if (diagnosticNative)
                report.AppendLine("DIAGNOSTIC ONLY: strict 1920x1080 guard" +
                    " bypassed; actual measured resolution is below.");
            report.AppendLine("REFERENCE ARCHIVE SHA-256=" + referenceHash);
            report.AppendLine("Frame and clock measurements alone do not establish full performance acceptance.");
            report.AppendLine("OS=" + SystemInfo.operatingSystem);
            report.AppendLine("CPU=" + SystemInfo.processorType);
            report.AppendLine("CPU logical cores=" + SystemInfo.processorCount);
            report.AppendLine("GPU=" + SystemInfo.graphicsDeviceName);
            report.AppendLine("RAM MiB=" + SystemInfo.systemMemorySize);
            report.AppendLine("Unity=" + UnityEngine.Application.unityVersion);
            report.AppendLine("Resolution=" + Screen.width + "x" + Screen.height);
            report.AppendLine("Preset=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            report.AppendLine("VSync=" + QualitySettings.vSyncCount +
                " frame cap=" + UnityEngine.Application.targetFrameRate +
                " render scale=100% built-in renderer");
            report.AppendLine("Shadows=" + QualitySettings.shadows +
                " shadow distance=" + QualitySettings.shadowDistance +
                " MSAA=" + QualitySettings.antiAliasing +
                " texture mip limit=" + QualitySettings.globalTextureMipmapLimit +
                " anisotropic=" + QualitySettings.anisotropicFiltering +
                " LOD bias=" + QualitySettings.lodBias);
            report.AppendLine("Standalone AND gates=500 module instances=10" +
                " AND gates per instance=50 equivalent gates=1000");
            var segments = 0;
            foreach (var route in snapshot.Design.Topology.Connectors)
                segments += route.Spans.Count;
            report.AppendLine("World connectors=" +
                snapshot.Design.Topology.Connectors.Count +
                " visible spans=" + segments +
                " harness width=1 registers=0");
            report.AppendLine("Total renderer objects=" +
                FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length);
            report.AppendLine("Camera path: stationary (30,0.3,12) facing east;" +
                " flying linear (8,0.3,5)->(100,0.3,60), looking 12 cells ahead.");
            report.AppendLine("Activity: first standalone B=1, other B inputs float;" +
                " active Y transitions are 0/1 or 0/X on 10 Hz edges.");

            yield return RunCase("idle/stationary", false, false);
            yield return RunCase("active/stationary", true, false);
            yield return RunCase("idle/flying", false, true);
            yield return RunCase("active/flying", true, true);
            report.AppendLine("Supplementary shape views use the same frozen" +
                " world, 1080p Standard preset and stopped clock; they compare" +
                " sparse standalone gates with the repeated opaque module bodies.");
            report.AppendLine("Shape cameras: sparse (8,0.3,55) facing south;" +
                " repeated modules (100,0.3,31) facing east.");
            yield return RunCase("sparse-standalone", false, false,
                new Vector3(8f, 0.3f, 55f), Vector3.back, 5d, 20d,
                "SHAPE");
            yield return RunCase("repeated-modules", false, false,
                new Vector3(100f, 0.3f, 31f), Vector3.right, 5d, 20d,
                "SHAPE");
            yield return RunOperations();
            Finish("REFERENCE MEASUREMENT COMPLETE", 0);
        }

        private static void ValidateReference(WorldSaveSnapshot snapshot)
        {
            var standalone = 0;
            foreach (var component in snapshot.Design.Components)
                if (component.TypeId == BuiltInPinCatalog.And) standalone++;
            if (standalone != 500 || snapshot.Design.Modules.Count != 10 ||
                snapshot.ModuleVersions.Count != 1 ||
                snapshot.Design.Topology.Connectors.Count != 1011)
                throw new InvalidDataException("Reference world distribution changed.");
            foreach (var version in snapshot.ModuleVersions.Values)
            {
                var internalGates = 0;
                foreach (var component in version.Components)
                    if (component.TypeId == BuiltInPinCatalog.And)
                        internalGates++;
                if (internalGates != 50)
                    throw new InvalidDataException("Reference module changed.");
            }
        }

        private IEnumerator RunCase(string name, bool active, bool flying,
            Vector3? stationaryPosition = null,
            Vector3? stationaryLook = null,
            double warmupSeconds = WarmupSeconds,
            double measureSeconds = MeasureSeconds,
            string reportPrefix = "CASE")
        {
            session.Scheduler.ResetSimulation();
            if (active) session.Scheduler.StartClock();
            fractionalPicoseconds = 0d;
            simulationTicks = 0L;
            var samples = new List<double>();
            var mainThreadMs = new List<double>();
            var gpuMs = new List<double>();
            var timing = new FrameTiming[1];
            var start = Time.realtimeSinceStartupAsDouble;
            var last = start;
            var measuredStart = 0d;
            BigInteger startingEdges = BigInteger.Zero;
            var sampling = false;
            var visibleAtStart = 0;
            while (true)
            {
                yield return null;
                var now = Time.realtimeSinceStartupAsDouble;
                var elapsed = now - start;
                var frameSeconds = now - last;
                last = now;
                if (frameSeconds <= 0d) continue;
                AdvanceSimulation(frameSeconds);
                SetCamera(flying, Math.Max(0d,
                    (elapsed - warmupSeconds) / measureSeconds),
                    stationaryPosition, stationaryLook);
                if (!sampling && elapsed >= warmupSeconds)
                {
                    sampling = true;
                    visibleAtStart = CountVisibleRenderers();
                    measuredStart = Time.realtimeSinceStartupAsDouble;
                    last = measuredStart;
                    startingEdges = session.Scheduler.ClockEdgesProcessed;
                    simulationTicks = 0L;
                    continue;
                }
                if (!sampling) continue;
                samples.Add(frameSeconds);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    if (timing[0].cpuMainThreadFrameTime > 0d)
                        mainThreadMs.Add(timing[0].cpuMainThreadFrameTime);
                    if (timing[0].gpuFrameTime > 0d)
                        gpuMs.Add(timing[0].gpuFrameTime);
                }
                if (now - measuredStart >= measureSeconds) break;
            }
            session.Scheduler.StopClock();
            var seconds = last - measuredStart;
            var visibleAtEnd = CountVisibleRenderers();
            var metrics = new FirstPlayableFrameMetrics(samples, seconds);
            var edges = session.Scheduler.ClockEdgesProcessed - startingEdges;
            report.AppendLine(reportPrefix + "=" + name +
                " warmup=" + F(warmupSeconds) + "s measured=" +
                F(seconds) + "s" +
                " frames=" + metrics.FrameCount +
                " average FPS=" + F(metrics.AverageFramesPerSecond) +
                " median ms=" + F(metrics.MedianMilliseconds) +
                " p95 ms=" + F(metrics.P95Milliseconds) +
                " p99 ms=" + F(metrics.P99Milliseconds) +
                " max ms=" + F(metrics.MaxMilliseconds) +
                " edges=" + edges +
                " achieved cycles/s=" + F((double)edges / 2d / seconds) +
                " simulation CPU ms=" + F(simulationTicks * 1000d /
                    Stopwatch.Frequency) +
                " visible renderers start/end=" + visibleAtStart + "/" +
                    visibleAtEnd +
                " main-thread mean ms=" + MeanOrUnavailable(mainThreadMs) +
                " GPU mean ms=" + MeanOrUnavailable(gpuMs) +
                " reserved memory MiB=" + F(
                    Profiler.GetTotalReservedMemoryLong() / 1048576d));
            File.WriteAllText(outputPath, report.ToString());
        }

        private int CountVisibleRenderers()
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(view);
            var count = 0;
            foreach (var renderer in FindObjectsByType<Renderer>(
                FindObjectsSortMode.None))
                if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                    count++;
            return count;
        }

        private IEnumerator RunOperations()
        {
            report.AppendLine("Edit positions: (15,1,50)->(17,1,50) and" +
                " (31,1,50)->(33,1,50), crossing x=16 and x=32 grid lines.");
            report.AppendLine("Renderer region width=" +
                OneBitWorldView.RenderRegionSizeCells +
                " cells; keyed per-object reconciliation retains distant objects.");
            foreach (var x in new[] { 15, 31 })
            {
                var start = Stopwatch.GetTimestamp();
                session.PlaceComponent(BuiltInPinCatalog.And,
                    new GridCell(x, 1, 50), GridOrientation.Default);
                yield return null;
                RecordLatency("place AND x=" + x, start, 100d);
                var gate = session.Design.Components[
                    session.Design.Components.Count - 1];

                start = Stopwatch.GetTimestamp();
                session.PlaceWireStub(JoinMember.ComponentPin(gate.Id,
                    gate.PinIds["Y"]), new GridCell(x + 2, 1, 50));
                yield return null;
                RecordLatency("place connector x=" + x, start, 100d);
                var route = session.Design.Topology.Connectors[
                    session.Design.Topology.Connectors.Count - 1];

                start = Stopwatch.GetTimestamp();
                session.BreakSpan(route.Id, route.Spans[0].Id);
                yield return null;
                RecordLatency("break connector x=" + x, start, 100d);

                start = Stopwatch.GetTimestamp();
                if (!session.TryUndo())
                    throw new InvalidOperationException("Benchmark undo failed.");
                yield return null;
                RecordLatency("undo break x=" + x, start, 100d);
            }

            bootstrap.SetStorageRootForVerification(
                Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            var saveStart = Stopwatch.GetTimestamp();
            bootstrap.SaveCurrentWorldFile();
            RecordLatency("manual save", saveStart, 2000d);
            var loadStart = Stopwatch.GetTimestamp();
            bootstrap.ReopenCurrentWorldFile();
            yield return null;
            RecordLatency("load and first rendered frame", loadStart, 5000d);
        }

        private void RecordLatency(string label, long started, double budgetMs)
        {
            var ms = (Stopwatch.GetTimestamp() - started) * 1000d /
                Stopwatch.Frequency;
            report.AppendLine("OP=" + label + " latency ms=" + F(ms) +
                " provisional target ms=" + F(budgetMs) +
                " result=" + (ms <= budgetMs ? "within" : "over"));
        }

        private static string MeanOrUnavailable(List<double> samples)
        {
            if (samples.Count == 0) return "unavailable";
            var sum = 0d;
            foreach (var sample in samples) sum += sample;
            return F(sum / samples.Count);
        }

        private void AdvanceSimulation(double frameSeconds)
        {
            var picoseconds = frameSeconds *
                (double)SiliconSandbox.Simulation.SimulationTime.PicosecondsPerSecond +
                fractionalPicoseconds;
            var whole = (long)Math.Floor(picoseconds);
            fractionalPicoseconds = picoseconds - whole;
            var start = Stopwatch.GetTimestamp();
            session.Scheduler.AdvanceUntil(session.Scheduler.Now.AddPicoseconds(
                new BigInteger(whole)));
            simulationTicks += Stopwatch.GetTimestamp() - start;
        }

        private void SetCamera(bool flying, double progress,
            Vector3? stationaryPosition, Vector3? stationaryLook)
        {
            var t = flying ? Mathf.Clamp01((float)progress) : 0f;
            var position = flying
                ? Vector3.Lerp(new Vector3(8f, 0.3f, 5f),
                    new Vector3(100f, 0.3f, 60f), t)
                : stationaryPosition ?? new Vector3(30f, 0.3f, 12f);
            player.position = position;
            var look = flying ? new Vector3(12f, 0f, 7f) :
                stationaryLook ?? Vector3.right;
            view.transform.rotation = Quaternion.LookRotation(look);
        }

        private static string F(double number) =>
            number.ToString("F3", CultureInfo.InvariantCulture);

        private void Finish(string status, int exitCode)
        {
            report.AppendLine(status);
            try
            {
                File.WriteAllText(outputPath, report.ToString());
                Debug.Log("First-playable benchmark report: " + outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("Benchmark report could not be written: " +
                    exception.Message);
                exitCode = 1;
            }
            UnityEngine.Application.Quit(exitCode);
        }
    }
}
