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

        [MenuItem("DT-ET/Validate Assets and Build QA Player")]
        public static void BuildQaPlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene) == null)
                throw new InvalidOperationException("DT-ET staging scene is missing: " + Scene);

            var shaderPaths = new[]
            {
                "Assets/Resources/DTET/ConceptualAccretion.shader",
                "Assets/Resources/DTET/GlowLine.shader",
                "Assets/Resources/DTET/Starfield.shader",
                "Assets/Resources/DTET/SolidSurface.shader"
            };
            foreach (var path in shaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("Missing or invalid DT-ET shader: " + path);
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var destination = Path.Combine(projectRoot, "Builds", "VisualQA", "BinaryBlackHole_VisualQA.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("DT-ET QA player build failed: " + report.summary.result);
            Debug.Log("DTET_QA_BUILD_SUCCEEDED " + destination);
        }
    }
}
