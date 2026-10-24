using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DTET.VisualPrototype.Editor
{
    public static class PrototypeBuild
    {
        private const string Scene = "Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity";

        [MenuItem("DT-ET/Startup Exhibit/Interactive Suit Observatory")]
        private static void SelectInteractiveExhibit() => SessionState.SetInt("DTET.StartupExhibit", 3);
        [MenuItem("DT-ET/Startup Exhibit/Interactive Suit Observatory", true)]
        private static bool ValidateInteractiveExhibit() => ValidateStartup("Interactive Suit Observatory", 3);

        [MenuItem("DT-ET/Startup Exhibit/M4 Reduced Binary")]
        private static void SelectReducedExhibit() => SessionState.SetInt("DTET.StartupExhibit", 2);
        [MenuItem("DT-ET/Startup Exhibit/M3 Reference Lab")]
        private static void SelectReferenceLab() => SessionState.SetInt("DTET.StartupExhibit", 1);
        [MenuItem("DT-ET/Startup Exhibit/M1 and M2 Lessons")]
        private static void SelectLessons() => SessionState.SetInt("DTET.StartupExhibit", 0);
        [MenuItem("DT-ET/Startup Exhibit/M4 Reduced Binary", true)]
        private static bool ValidateReducedExhibit() => ValidateStartup("M4 Reduced Binary", 2);
        [MenuItem("DT-ET/Startup Exhibit/M3 Reference Lab", true)]
        private static bool ValidateReferenceLab() => ValidateStartup("M3 Reference Lab", 1);
        [MenuItem("DT-ET/Startup Exhibit/M1 and M2 Lessons", true)]
        private static bool ValidateLessons() => ValidateStartup("M1 and M2 Lessons", 0);
        private static bool ValidateStartup(string item, int value)
        {
            Menu.SetChecked("DT-ET/Startup Exhibit/" + item, SessionState.GetInt("DTET.StartupExhibit", 0) == value);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem("DT-ET/Validate Assets and Build QA Player")]
        public static void BuildQaPlayer()
        {
            BuildPlayer("VisualQA", "BinaryBlackHole_VisualQA.exe", BuildOptions.Development, "DTET_QA_BUILD_SUCCEEDED");
        }

        [MenuItem("DT-ET/Build Submission Demo")]
        public static void BuildSubmissionPlayer()
        {
            BuildPlayer("Submission", "DTET_Prototype.exe", BuildOptions.None, "DTET_SUBMISSION_BUILD_SUCCEEDED");
        }

        [MenuItem("DT-ET/Build Desktop Release/Windows")]
        public static void BuildDesktopWindowsPlayer()
        {
            BuildDesktopPlayer(BuildTarget.StandaloneWindows64, false);
        }

        [MenuItem("DT-ET/Build Desktop Release/macOS Universal")]
        public static void BuildDesktopMacPlayer()
        {
            BuildDesktopPlayer(BuildTarget.StandaloneOSX, false);
        }

        // Match release startup while retaining opt-in QA harnesses.
        public static void BuildDesktopWindowsQaPlayer()
        {
            BuildDesktopPlayer(BuildTarget.StandaloneWindows64, true);
        }

        private static void BuildDesktopPlayer(BuildTarget target, bool development)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Install the matching Unity build module for " + target);
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Select " + target + " first, or launch Unity with -buildTarget " + target);
            var named = NamedBuildTarget.Standalone;
            var backend = PlayerSettings.GetScriptingBackend(named);
            string platform = BuildPipeline.GetBuildTargetName(target);
            string architecture = EditorUserBuildSettings.GetPlatformSettings(platform, "Architecture");
            string product = PlayerSettings.productName;
            string identifier = PlayerSettings.GetApplicationIdentifier(named);
            string xcode = EditorUserBuildSettings.GetPlatformSettings(platform, "CreateXcodeProject");
            try
            {
                PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.Mono2x);
                PlayerSettings.productName = "DT-ET Binary Explorer";
                PlayerSettings.SetApplicationIdentifier(named, "org.dtet.binaryexplorer");
                // Matches Unity's DesktopStandaloneBuildWindowExtension architecture setting.
                EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture",
                    target == BuildTarget.StandaloneOSX ? "x64arm64" : "x64");
                if (target == BuildTarget.StandaloneOSX)
                {
                    EditorUserBuildSettings.SetPlatformSettings(platform, "CreateXcodeProject", "false");
                }
                BuildPlayer(development ? "VisualQA" : (target == BuildTarget.StandaloneOSX ? "DesktopMac" : "DesktopWindows"),
                    development ? "BinaryBlackHole_VisualQA.exe" : (target == BuildTarget.StandaloneOSX ? "DTET_Binary_Explorer.app" : "DTET_Binary_Explorer.exe"),
                    development ? BuildOptions.Development : BuildOptions.None,
                    development ? "DTET_QA_BUILD_SUCCEEDED" : "DTET_DESKTOP_BUILD_SUCCEEDED", target, true);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(named, backend);
                EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", architecture);
                PlayerSettings.productName = product;
                PlayerSettings.SetApplicationIdentifier(named, identifier);
                if (target == BuildTarget.StandaloneOSX)
                    EditorUserBuildSettings.SetPlatformSettings(platform, "CreateXcodeProject", xcode);
            }
        }

        private static void BuildPlayer(string folder, string executable, BuildOptions options, string receipt,
            BuildTarget target = BuildTarget.StandaloneWindows64, bool interactiveDefault = false)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene) == null)
                throw new InvalidOperationException("DT-ET staging scene is missing: " + Scene);
            ValidateGuidedTourResources();
            if (interactiveDefault)
            {
                using (var disks = BlackHole.Staging.Simulation.MiniDiskCache.Open(
                    Path.Combine(Application.streamingAssetsPath, "DTET", "MiniDisks", "sxs0305.mdf")))
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var source = Resources.Load<TextAsset>("Physics/SXS0305");
                    if (source == null) throw new InvalidOperationException("The bundled numerical replay is missing.");
                    try
                    {
                        byte[] bytes = source.bytes;
                        string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                        BlackHole.Staging.Simulation.InteractiveBinaryScenario model;
                        using (var stream = new MemoryStream(bytes, false))
                            model = new BlackHole.Staging.Simulation.InteractiveBinaryScenario(
                                BlackHole.Staging.Simulation.NumericalEventHistory.Load(stream));
                        var initial = model.Evaluate(0);
                        bool Near(double a, double b) => Math.Abs(a - b) <= 1e-8 * Math.Max(1, Math.Abs(b));
                        if (disks.SourceSha256 != hash || disks.SourceModelId != initial.ScenarioId
                            || disks.ScenarioMode != initial.Mode || !Near(disks.TotalMassSolar, initial.TotalMassSolar)
                            || !Near(disks.Mass1Solar, initial.Mass1Solar) || !Near(disks.Mass2Solar, initial.Mass2Solar)
                            || !Near(disks.GravitationalRadiusMeters, initial.GravitationalRadiusMeters)
                            || !Near(disks.GravitationalTimeSeconds, initial.GravitationalTimeSeconds)
                            || !Near(disks.InitialSeparationRg, initial.SeparationRg)
                            || !Near(disks.MergerTimeSeconds, model.MergerTimeSeconds)
                            || disks.StartSeconds < 0 || disks.EndSeconds > model.DurationSeconds + 1e-9)
                            throw new InvalidOperationException("Prepared disks do not match the bundled numerical replay.");
                        int cells = checked(disks.GridSize * disks.GridSize);
                        var sigma = new float[cells]; var vx = new float[cells]; var vz = new float[cells];
                        disks.ReadFrame(0, sigma, vx, vz);
                        disks.ReadFrame(disks.FrameCount - 1, sigma, vx, vz);
                        string fluidPath = Path.Combine(Application.streamingAssetsPath, "DTET", "MiniDisks", "sxs0305.mdf");
                        using (var material = BlackHole.Staging.Simulation.MiniDiskMaterialCache.Open(
                            BlackHole.Staging.Simulation.MiniDiskMaterialAdvection.SidecarPath(fluidPath)))
                        {
                            if (!material.MatchesFile(disks, fluidPath))
                                throw new InvalidOperationException("Prepared material does not match the fluid history.");
                            material.ReadFrame(0, sigma); material.ReadFrame(material.FrameCount - 1, sigma);
                            Debug.Log($"DTET_MATERIAL_PREFLIGHT_PASSED fluidSha={material.FluidSha256} frames={material.FrameCount} checkedFrames=first,last");
                        }
                        Debug.Log($"DTET_MINIDISK_PREFLIGHT_PASSED sourceSha={hash} grid={disks.GridSize} frames={disks.FrameCount} start={disks.StartSeconds:R} end={disks.EndSeconds:R} checkedFrames=first,last");
                    }
                    finally { Resources.UnloadAsset(source); }
                }
            }

            var shaderPaths = new[]
            {
                "Assets/Resources/DTET/ConceptualAccretion.shader",
                "Assets/Resources/DTET/GlowLine.shader",
                "Assets/Resources/DTET/Starfield.shader",
                "Assets/Resources/DTET/ReferenceVolume.shader",
                "Assets/Resources/DTET/ReferenceTracer.shader",
                "Assets/Resources/DTET/ReducedBinaryVolume.shader",
                "Assets/Resources/DTET/ReducedDensitySamples.shader",
                "Assets/Resources/DTET/ExhibitSky.shader",
                "Assets/Resources/DTET/StellarBackdrop.shader",
                "Assets/Resources/DTET/WaveSurface.shader",
                "Assets/Resources/DTET/MergerSurface.shader",
                "Assets/Resources/DTET/CurvedDiskVolume.shader",
                "Assets/Resources/DTET/FeaturedBackgroundStar.shader",
                "Assets/Resources/DTET/ExhibitDisplay.shader",
                "Assets/Resources/DTET/InteractiveOptics.shader",
                "Assets/Resources/DTET/GeneratedFluidVolume.shader",
                "Assets/Resources/DTET/SolidSurface.shader",
                "Assets/Resources/DTET/WristSurface.shader",
                "Assets/Resources/ApproachHUD/HudVertexAlpha.shader"
            };
            foreach (var path in shaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                {
                    if (shader != null)
                        foreach (var message in ShaderUtil.GetShaderMessages(shader))
                            Debug.LogError($"{path}:{message.line}: {message.message}");
                    throw new InvalidOperationException("Missing or invalid DT-ET shader: " + path);
                }
            }
            Debug.Log($"DTET_SHADER_PREFLIGHT_PASSED count={shaderPaths.Length}");

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var destination = Path.Combine(projectRoot, "Builds", folder, executable);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            ShaderFixtureStripper.KeepFixtures = (options & BuildOptions.Development) != 0;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene }, locationPathName = destination, target = target, options = options,
                    extraScriptingDefines = interactiveDefault ? new[] { "DTET_DESKTOP_RELEASE" } : Array.Empty<string>()
                });
            }
            finally { ShaderFixtureStripper.KeepFixtures = false; }
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("DT-ET player build failed: " + report.summary.result);
            Debug.Log(receipt + " " + destination);
        }

        [Serializable]
        private sealed class GuidedTourResourceManifest
        {
            public int schemaVersion;
            public string language;
            public GuidedTourResourceEntry[] entries;
        }

        [Serializable]
        private sealed class GuidedTourResourceEntry
        {
            public string id, title, body, narration, clip, titleExperiment, bodyExperiment, narrationExperiment, clipExperiment;
        }

        private static void ValidateGuidedTourResources()
        {
            var asset = Resources.Load<TextAsset>("DTET/GuidedTour");
            if (asset == null) throw new InvalidOperationException("Guided tour resource DTET/GuidedTour is missing.");
            try
            {
                var manifest = JsonUtility.FromJson<GuidedTourResourceManifest>(asset.text);
                if (manifest == null || manifest.schemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.language)
                    || manifest.entries == null || manifest.entries.Length == 0)
                    throw new InvalidOperationException("Guided tour manifest is empty or has an unsupported schema.");
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var clips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in manifest.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.title)
                        || string.IsNullOrWhiteSpace(entry.body) || string.IsNullOrWhiteSpace(entry.narration)
                        || string.IsNullOrWhiteSpace(entry.clip) || !ids.Add(entry.id)
                        || !clips.Add(entry.clip) || !entry.clip.StartsWith("DTET/Narration/", StringComparison.Ordinal)
                        || entry.clip.Contains("..") || entry.clip.Contains("\\") || entry.clip.Contains("."))
                        throw new InvalidOperationException("Guided tour has an empty, duplicate or invalid narration entry.");
                    var clip = Resources.Load<AudioClip>(entry.clip);
                    if (clip == null) throw new InvalidOperationException("Guided tour narration is missing: " + entry.clip);
                    try
                    {
                        if (clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
                            throw new InvalidOperationException("Guided tour narration is empty or invalid: " + entry.clip);
                    }
                    finally { Resources.UnloadAsset(clip); }
                    if (!string.IsNullOrEmpty(entry.clipExperiment))
                    {
                        if (string.IsNullOrWhiteSpace(entry.titleExperiment) || string.IsNullOrWhiteSpace(entry.bodyExperiment)
                            || string.IsNullOrWhiteSpace(entry.narrationExperiment) || !clips.Add(entry.clipExperiment)
                            || !entry.clipExperiment.StartsWith("DTET/Narration/", StringComparison.Ordinal)
                            || entry.clipExperiment.Contains(".") || entry.clipExperiment.Contains("\\"))
                            throw new InvalidOperationException("Invalid experiment narration for " + entry.id);
                        var variant = Resources.Load<AudioClip>(entry.clipExperiment);
                        if (variant == null) throw new InvalidOperationException("Missing experiment narration: " + entry.clipExperiment);
                        try { if (variant.samples <= 0 || variant.channels <= 0 || variant.frequency <= 0)
                            throw new InvalidOperationException("Empty experiment narration: " + entry.clipExperiment); }
                        finally { Resources.UnloadAsset(variant); }
                    }
                }
                foreach (string id in new[] { "system", "black_hole_a", "black_hole_b", "accretion", "waves",
                    "inspiral", "gas_tides", "final_approach", "coalescence", "remnant" })
                    if (!ids.Contains(id)) throw new InvalidOperationException("Required guided tour entry is missing: " + id);
                Debug.Log($"DTET_GUIDED_TOUR_PREFLIGHT_PASSED entries={ids.Count} clips={clips.Count}");
            }
            finally { Resources.UnloadAsset(asset); }
        }
    }
}
