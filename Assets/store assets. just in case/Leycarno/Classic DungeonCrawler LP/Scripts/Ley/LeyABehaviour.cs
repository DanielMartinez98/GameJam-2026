using System.Runtime.InteropServices;
using UnityEngine;

namespace Scripts.Ley
{
    public class LeyABehaviour : MonoBehaviour
    {
        private Transform _myTransform;

        public Transform TheTransform
        {
            get
            {
                if (!_myTransform)
                    _myTransform = transform;
                return _myTransform;
            }
        }
        
        public bool IsAtPosition(Vector3 position)
        {
            var p = TheTransform.position;
            return position.Equals(p) || Mathf.RoundToInt(Vector3.Distance(p, position)) == 0;
        }
        
        public void RemovePrefabInstanceFromScene()
        {
#if UNITY_EDITOR
            if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(TheTransform)) return;
            UnityEditor.PrefabUtility.UnpackPrefabInstance(gameObject,
                UnityEditor.PrefabUnpackMode.Completely,
                UnityEditor.InteractionMode.AutomatedAction);
            DestroyImmediate(gameObject);
#endif
        }
    }
}