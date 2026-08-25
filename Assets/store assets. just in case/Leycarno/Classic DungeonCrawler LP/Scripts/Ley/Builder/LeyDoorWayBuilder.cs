using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Scripts.Ley.Builder
{
    public class LeyDoorWayBuilder : LeyABehaviour
    {
#if UNITY_EDITOR

        [Space] [SerializeField] protected LeyDir direction;
        [Space] [SerializeField] [Range(0, 3)] protected int door;
        [SerializeField] protected bool doorKnobIn;
        [SerializeField] protected bool doorKnobOut;

        [Space] [SerializeField] protected List<LeyDoor> doorPrefabs;
        [SerializeField] protected LeyDoorSwitch doorKnobPrefab;

        private void OnValidate() => EditorApplication.delayCall += _OnValidate;

        private void _OnValidate()
        {
            if (Application.isPlaying || this == null)
                return;

            TheTransform.localRotation = LeyDirection.GetRotationOf(direction);

            DestroyParts();
            var simpleDoor = SetPartFromList(door, doorPrefabs, "door");
            if (!simpleDoor)
                return;

            var knobIn = SetPart(doorKnobIn, doorKnobPrefab, "knobIn");
            var knobOut = SetPart(doorKnobOut, doorKnobPrefab, "knobOut");

            if (knobIn) knobIn.SetDoor(simpleDoor);
            if (!knobOut) return;
            knobOut.SetDoor(simpleDoor);
            knobOut.TheTransform.localRotation = Quaternion.Euler(0, 180, 0);
        }

        private void DestroyParts()
        {
            foreach (var d in TheTransform.GetComponentsInChildren<LeyDoor>())
                d.RemovePrefabInstanceFromScene();
            foreach (var ds in TheTransform.GetComponentsInChildren<LeyDoorSwitch>())
                ds.RemovePrefabInstanceFromScene();
        }

        private T SetPart<T>(bool active, [CanBeNull] T prefab, string theName) where T : LeyABehaviour
        {
            if (!active || !prefab)
                return null;
            if (!(PrefabUtility.InstantiatePrefab(prefab) is T prefabInstance))
                return null;

            prefabInstance.TheTransform.SetParent(TheTransform);
            prefabInstance.TheTransform.localPosition = Vector3.zero;
            prefabInstance.TheTransform.localRotation = Quaternion.identity;
            prefabInstance.gameObject.name = theName;
            return prefabInstance;
        }

        private T SetPartFromList<T>(int variation, IReadOnlyList<T> prefabs, string theName) where T : LeyABehaviour
        {
            if (variation < 1 || variation > prefabs.Count)
                return null;
            if (!(PrefabUtility.InstantiatePrefab(prefabs[variation - 1]) is T prefabInstance))
                return null;

            prefabInstance.TheTransform.SetParent(TheTransform);
            prefabInstance.TheTransform.localPosition = Vector3.zero;
            prefabInstance.TheTransform.localRotation = Quaternion.identity;
            prefabInstance.gameObject.name = theName;
            return prefabInstance;
        }

#endif
    }
}