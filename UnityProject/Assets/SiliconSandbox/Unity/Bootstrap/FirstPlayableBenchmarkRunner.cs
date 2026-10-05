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
using UnityEngine;
using Debug = UnityEngine.Debug;
using BigInteger = System.Numerics.BigInteger;

namespace SiliconSandbox.Bootstrap
{
    // Opt-in graphical-player measurement. Until the V1 reference archive is
    // frozen, its output is explicitly preliminary, never an acceptance pass.
    public sealed class FirstPlayableBenchmarkRunner : MonoBehaviour
    {
        public const string OutputVariable = "SILICON_SANDBOX_BENCHMARK_OUTPUT";
        private const double WarmupSeconds = 10d;
        private const double MeasureSeconds = 60d;
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
            var standard = Array.IndexOf(QualitySettings.names, "Standard");
            if (standard < 0)
            {
                Finish("UNVERIFIED: Standard quality preset is unavailable.", 1);
                yield break;
            }
            QualitySettings.SetQualityLevel(standard, true);
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = -1;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return null;
            if (Screen.width != 1920 || Screen.height != 1080)
            {
                Finish("UNVERIFIED: actual display resolution is " +
                    Screen.width + "x" + Screen.height + ", not 1920x1080.", 1);
                yield break;
            }

            var fixture = FirstPlayableBenchmarkFactory.Create();
            var blank = bootstrap.CaptureCurrentWorld();
            var versions = new Dictionary<Guid, OneBitModuleVersion>
            { [fixture.Version.VersionId] = fixture.Version };
            var pose = new SavedPlayerPose(30d, 0.3d, 12d, 1d, 0d, 0d);
            bootstrap.OpenWorld(new WorldSaveSnapshot(blank.WorldId,
                "First-playable benchmark", blank.FloorMaterialId,
                blank.WallStyleId, "10", pose, blank.InventorySlots,
                blank.SelectedHotbarSlot, fixture.World, versions));
            bootstrap.Interaction.enabled = false;
            var controller = bootstrap.Interaction.GetComponent<
                SiliconSandbox.Interaction.CreativeCameraController>();
            controller.enabled = false;
            player = bootstrap.Interaction.transform;
            view = Camera.main;
            session = bootstrap.Session;
            yield return null;

            report.AppendLine("PRELIMINARY: generated in-memory reference; saved V1 archive/hash not frozen.");
            report.AppendLine("This report cannot establish first-playable performance acceptance.");
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
            foreach (var route in fixture.World.Topology.Connectors)
                segments += route.Spans.Count;
            report.AppendLine("World connectors=" +
                fixture.World.Topology.Connectors.Count +
                " visible spans=" + segments +
                " harness width=1 registers=0");
            report.AppendLine("Rendered scene objects=" +
                FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length);
            report.AppendLine("Camera path: stationary (30,0.3,12) facing east;" +
                " flying linear (8,0.3,5)->(100,0.3,60), looking 12 cells ahead.");

            yield return RunCase("idle/stationary", false, false);
            yield return RunCase("active/stationary", true, false);
            yield return RunCase("idle/flying", false, true);
            yield return RunCase("active/flying", true, true);
            Finish("PRELIMINARY RUN COMPLETE", 0);
        }

        private IEnumerator RunCase(string name, bool active, bool flying)
        {
            session.Scheduler.ResetSimulation();
            if (active) session.Scheduler.StartClock();
            fractionalPicoseconds = 0d;
            simulationTicks = 0L;
            var samples = new List<double>();
            var start = Time.realtimeSinceStartupAsDouble;
            var last = start;
            var measuredStart = 0d;
            BigInteger startingEdges = BigInteger.Zero;
            var sampling = false;
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
                    (elapsed - WarmupSeconds) / MeasureSeconds));
                if (!sampling && elapsed >= WarmupSeconds)
                {
                    sampling = true;
                    measuredStart = now;
                    startingEdges = session.Scheduler.ClockEdgesProcessed;
                    simulationTicks = 0L;
                    continue;
                }
                if (!sampling) continue;
                samples.Add(frameSeconds);
                if (now - measuredStart >= MeasureSeconds) break;
            }
            session.Scheduler.StopClock();
            var seconds = last - measuredStart;
            var metrics = new FirstPlayableFrameMetrics(samples, seconds);
            var edges = session.Scheduler.ClockEdgesProcessed - startingEdges;
            report.AppendLine("CASE=" + name +
                " warmup=10s measured=" + F(seconds) + "s" +
                " frames=" + metrics.FrameCount +
                " average FPS=" + F(metrics.AverageFramesPerSecond) +
                " median ms=" + F(metrics.MedianMilliseconds) +
                " p95 ms=" + F(metrics.P95Milliseconds) +
                " p99 ms=" + F(metrics.P99Milliseconds) +
                " max ms=" + F(metrics.MaxMilliseconds) +
                " edges=" + edges +
                " achieved cycles/s=" + F((double)edges / 2d / seconds) +
                " simulation CPU ms=" + F(simulationTicks * 1000d /
                    Stopwatch.Frequency));
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

        private void SetCamera(bool flying, double progress)
        {
            var t = flying ? Mathf.Clamp01((float)progress) : 0f;
            var position = flying
                ? Vector3.Lerp(new Vector3(8f, 0.3f, 5f),
                    new Vector3(100f, 0.3f, 60f), t)
                : new Vector3(30f, 0.3f, 12f);
            player.position = position;
            var look = flying ? new Vector3(12f, 0f, 7f) : Vector3.right;
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
