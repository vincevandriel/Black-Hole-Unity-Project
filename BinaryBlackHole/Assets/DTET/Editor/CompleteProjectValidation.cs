using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype.Editor
{
    public static class CompleteProjectValidation
    {
        [MenuItem("DT-ET/Validate Complete Project")]
        public static void Validate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int references = 0;
            foreach (string folder in new[] { "Assets", "ProjectSettings" })
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(path);
                    if (extension != ".unity" && extension != ".prefab" && extension != ".asset" && extension != ".mat" && extension != ".meta") continue;
                    foreach (Match match in Regex.Matches(File.ReadAllText(path), @"\bguid:\s*([0-9a-fA-F]{32})\b"))
                    {
                        string guid = match.Groups[1].Value;
                        if (guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                        if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                            throw new InvalidOperationException("Missing asset " + guid + " referenced by " + path);
                        references++;
                    }
                }
            int prefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DTET" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try { CheckScripts(root, path); prefabs++; }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity");
            foreach (var root in scene.GetRootGameObjects()) CheckScripts(root, scene.path);
            var objects = UnityEngine.Object.FindAnyObjectByType<ObservatorySceneObjects>();
            if (objects == null) throw new InvalidOperationException("The saved observatory is missing.");
            objects.ValidateBindings();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
            if (profile == null || profile.components.Count == 0) throw new InvalidOperationException("The render volume profile is missing.");
            foreach (var component in profile.components)
                if (component == null) throw new InvalidOperationException("The render volume profile contains a missing component.");
            Debug.Log("DTET_COMPLETE_PROJECT_PASS references=" + references + " prefabs=" + prefabs + " volumeComponents=" + profile.components.Count);
        }

        private static void CheckScripts(GameObject root, string path)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new InvalidOperationException("Missing component script on " + child.name + " in " + path);
        }
    }
}
