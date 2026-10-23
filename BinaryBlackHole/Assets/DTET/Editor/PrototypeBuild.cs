using System;
using System.IO;
using UnityEditor;
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

        private static void BuildPlayer(string folder, string executable, BuildOptions options, string receipt)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene) == null)
                throw new InvalidOperationException("DT-ET staging scene is missing: " + Scene);

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
                "Assets/Resources/DTET/ExhibitDisplay.shader",
                "Assets/Resources/DTET/InteractiveOptics.shader",
                "Assets/Resources/DTET/GeneratedFluidVolume.shader",
                "Assets/Resources/DTET/SolidSurface.shader",
                "Assets/Resources/DTET/WristSurface.shader"
            };
            foreach (var path in shaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("Missing or invalid DT-ET shader: " + path);
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var destination = Path.Combine(projectRoot, "Builds", folder, executable);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("DT-ET player build failed: " + report.summary.result);
            Debug.Log(receipt + " " + destination);
        }
    }
}
