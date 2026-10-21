using System.IO;
using UnityEditor;
using UnityEngine;

namespace TacticalShooter.Systems.Interaction.Editor
{
    public static class InteractablePrefabUtility
    {
        [MenuItem("Tools/Create Interactable Prefabs")]
        public static void CreatePrefabs()
        {
            string folderPath = "Assets/_Project/Prefabs/Interactables";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            CreateDoorPrefab(folderPath + "/Door.prefab");
            CreateMovableCratePrefab(folderPath + "/MovableCrate.prefab");

            AssetDatabase.Refresh();
            Debug.Log("Successfully created Door.prefab and MovableCrate.prefab under Assets/_Project/Prefabs/Interactables/");
        }

        private static void CreateDoorPrefab(string path)
        {
            GameObject doorRoot = new GameObject("Door");

            GameObject hingePivot = new GameObject("HingePivot");
            hingePivot.transform.SetParent(doorRoot.transform, false);
            hingePivot.transform.localPosition = new Vector3(-0.75f, 0f, 0f);

            GameObject doorLeaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorLeaf.name = "DoorLeaf";
            doorLeaf.transform.SetParent(hingePivot.transform, false);
            doorLeaf.transform.localPosition = new Vector3(0.75f, 1.25f, 0f);
            doorLeaf.transform.localScale = new Vector3(1.5f, 2.5f, 0.15f);

            DoorInteractable doorComp = doorRoot.AddComponent<DoorInteractable>();

            SerializedObject serializedComp = new SerializedObject(doorComp);
            serializedComp.FindProperty("doorLeaf").objectReferenceValue = hingePivot.transform;
            serializedComp.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(doorRoot, path);
            Object.DestroyImmediate(doorRoot);
        }

        private static void CreateMovableCratePrefab(string path)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "MovableCrate";
            crate.transform.localScale = new Vector3(1f, 1f, 1f);

            Rigidbody rb = crate.GetComponent<Rigidbody>();
            if (rb == null) rb = crate.AddComponent<Rigidbody>();
            rb.mass = 10f;

            crate.AddComponent<MovableObject>();

            PrefabUtility.SaveAsPrefabAsset(crate, path);
            Object.DestroyImmediate(crate);
        }
    }
}
