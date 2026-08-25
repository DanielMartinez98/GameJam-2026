using UnityEditor;
using UnityEngine;

namespace Scripts.Ley.Builder
{
    public class LeyDirectionBuilder : LeyABehaviour
    {
#if UNITY_EDITOR

        [Space] [SerializeField] protected LeyDir direction;

        private void OnValidate() => EditorApplication.delayCall += _OnValidate;

        private void _OnValidate()
        {
            if (Application.isPlaying || this == null)
                return;

            TheTransform.localRotation = LeyDirection.GetRotationOf(direction);
        }

#endif
    }
}