using System;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DTET.VisualPrototype.Editor
{
    public static class EditableObservatoryBuilder
    {
        private const string Folder = "Assets/DTET/Prefabs";
        private const string Scene = "Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity";

        [MenuItem("DT-ET/Open Editable Observatory")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Scene);
        }

        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/EditableObservatory.prefab") != null)
                throw new InvalidOperationException("The editable observatory already exists. Open it instead of regenerating its objects.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/DTET", "Prefabs");
            var material = new Material(Resources.Load<Shader>("DTET/SolidSurface"));
            material.name = "Black hole surface"; material.SetColor("_Color", Color.black);
            AssetDatabase.CreateAsset(material, Folder + "/BlackHoleSurface.mat");
            GameObject primary = BodyPrefab("PrimaryBody", material);
            GameObject secondary = BodyPrefab("SecondaryBody", material);
            GameObject remnant = BodyPrefab("RemnantBody", material);
            GameObject tutor = InterfacePrefab("Tutor", typeof(GuidedObservationController));
            GameObject wrist = InterfacePrefab("WristControls", typeof(InteractiveWristDisplay));
            var root = new GameObject("Editable Binary Observatory");
            ObservatorySceneObjects bindings = root.AddComponent<ObservatorySceneObjects>();
            InteractiveBinaryExhibit exhibit = root.AddComponent<InteractiveBinaryExhibit>();
            GameObject world = Child(root.transform, "World");
            GameObject bodies = Child(world.transform, "Bodies");
            var a = (GameObject)PrefabUtility.InstantiatePrefab(primary, bodies.transform);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(secondary, bodies.transform);
            var r = (GameObject)PrefabUtility.InstantiatePrefab(remnant, bodies.transform);
            Camera camera = Child(world.transform, "Inspection Camera").AddComponent<Camera>();
            camera.enabled = false; camera.nearClipPlane = .04f; camera.farClipPlane = 20000; camera.fieldOfView = 52;
            camera.transform.position = new Vector3(0, 18, -45); camera.transform.LookAt(Vector3.zero);
            var properties = new SerializedObject(bindings);
            Set(properties, "worldRoot", world); Set(properties, "inspectionCamera", camera);
            Set(properties, "primaryBody", a.GetComponent<SimulationBodyBinding>());
            Set(properties, "secondaryBody", b.GetComponent<SimulationBodyBinding>());
            Set(properties, "remnant", r.GetComponent<SimulationBodyBinding>());
            foreach (var pair in new[] { ("environmentRoot", "Environment"), ("gasRoot", "Gas"), ("mergerRoot", "Merger Surface"),
                ("waveRoot", "Wave Field"), ("opticsRoot", "Lensing") })
                Set(properties, pair.Item1, Child(world.transform, pair.Item2).transform);
            Set(properties, "tutorRoot", ((GameObject)PrefabUtility.InstantiatePrefab(tutor, root.transform)).GetComponent<RectTransform>());
            Set(properties, "wristRoot", ((GameObject)PrefabUtility.InstantiatePrefab(wrist, root.transform)).GetComponent<RectTransform>());
            properties.ApplyModifiedPropertiesWithoutUndo();
            var presenter = new SerializedObject(exhibit); Set(presenter, "sceneObjects", bindings); presenter.ApplyModifiedPropertiesWithoutUndo();
            var data = Resources.Load<TextAsset>("Physics/SXS0305");
            using (var stream = new MemoryStream(data.bytes, false))
            {
                var model = new InteractiveBinaryScenario(NumericalEventHistory.Load(stream));
                var snapshot = model.Evaluate(0);
                Preview(a.transform, snapshot.BodyA); Preview(b.transform, snapshot.BodyB);
                Preview(r.transform, model.Evaluate(model.MergerTimeSeconds).Remnant);
                r.SetActive(false);
            }
            Resources.UnloadAsset(data);
            bindings.ValidateBindings();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/EditableObservatory.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.OpenScene(Scene);
            PrefabUtility.InstantiatePrefab(prefab);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("DTET_EDITABLE_SCENE_CREATED");
        }

        private static void Preview(Transform target, InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return;
            var p = body.Value.PositionRg;
            target.position = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
            target.localScale = Vector3.one * (float)(2 * body.Value.HorizonRadiusRg);
        }

        private static GameObject BodyPrefab(string name, Material material)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere); body.name = name;
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = body.GetComponent<SphereCollider>(); collider.isTrigger = true;
            var binding = body.AddComponent<SimulationBodyBinding>();
            var properties = new SerializedObject(binding);
            Set(properties, "surface", body.GetComponent<Renderer>()); Set(properties, "interactionCollider", collider);
            properties.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(body, Folder + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(body); return prefab;
        }

        private static GameObject InterfacePrefab(string name, Type controller)
        {
            var root = new GameObject(name, typeof(RectTransform)); root.layer = 5; root.AddComponent(controller);
            if (controller == typeof(GuidedObservationController)) { var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root); return prefab;
        }
        private static GameObject Child(Transform parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static void Set(SerializedObject owner, string field, UnityEngine.Object value)
        { owner.FindProperty(field).objectReferenceValue = value; }
    }
}
