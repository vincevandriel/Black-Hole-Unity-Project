using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DTET.VisualPrototype.Editor
{
    public static class EditableSceneValidation
    {
        public static void Validate()
        {
            const string scene = "Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity";
            const string temporary = "Assets/DTET/Scenes/EditablePersistenceCheck.unity";
            string original = File.ReadAllText(scene);
            EditorSceneManager.OpenScene(scene);
            var objects = UnityEngine.Object.FindAnyObjectByType<ObservatorySceneObjects>();
            if (objects == null) throw new InvalidOperationException("No saved observatory binding.");
            objects.ValidateBindings();
            foreach (var body in new[] { objects.PrimaryBody, objects.SecondaryBody, objects.Remnant })
                if (!PrefabUtility.IsPartOfPrefabInstance(body) || PrefabUtility.GetCorrespondingObjectFromSource(body) == null)
                    throw new InvalidOperationException("Body must be a saved prefab instance: " + body.name);
            var attachment = objects.PrimaryBody.gameObject.AddComponent<AudioSource>();
            attachment.playOnAwake = false; attachment.pitch = 1.23f;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), temporary);
            EditorSceneManager.OpenScene(temporary);
            objects = UnityEngine.Object.FindAnyObjectByType<ObservatorySceneObjects>();
            objects.ValidateBindings();
            attachment = objects.PrimaryBody.GetComponent<AudioSource>();
            if (attachment == null || Math.Abs(attachment.pitch - 1.23f) > 1e-5f)
                throw new InvalidOperationException("Authored body attachment did not survive save/reopen.");
            EditorSceneManager.OpenScene(scene);
            AssetDatabase.DeleteAsset(temporary);
            if (File.ReadAllText(scene) != original) throw new InvalidOperationException("Persistence check changed the source scene.");
            Debug.Log("DTET_EDITABLE_AUTHORING_PASS: scene references, nested prefabs and saved component overrides");
        }
    }
}
