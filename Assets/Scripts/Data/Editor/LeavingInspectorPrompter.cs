#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TrashCount.Data.Generators;

namespace TrashCount.Data.Editor
{
    [CustomEditor(typeof(ItemData))]
    public class LeavingInspectorPrompter : UnityEditor.Editor
    {
        private bool _isModified;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            
            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                _isModified = true;
            }
        }

        private void OnDisable()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _isModified = false;
                return;
            }
            
            if (_isModified && target != null)
            {
                if (!EditorUtility.IsDirty(target))
                {
                    _isModified = false;
                    return;
                }

                bool confirm = EditorUtility.DisplayDialog(
                    "Unsaved Changes",
                    $"Do you want to save changes for '{target.name}' before leaving?",
                    "Yes",
                    "No"
                );

                if (confirm)
                {
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                }

                _isModified = false;
            }
        }
    }
}
#endif