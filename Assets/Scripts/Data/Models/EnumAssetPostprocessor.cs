#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using TrashCount.Data.Generators;

namespace TrashCount.Data.Editor
{
    public class EnumAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            bool needsRefresh = false;

            foreach (string path in importedAssets)
            {
                if (!path.EndsWith(".asset")) continue;

                UnityEngine.Object targetAsset = AssetDatabase.LoadMainAssetAtPath(path);
                
                if (targetAsset is IEnumGeneratable generatableAsset)
                {
                    try
                    {
                        Debug.Log($"[EnumAssetPostprocessor] Processing enum generation for '{targetAsset.name}'...");
                        
                        generatableAsset.GenerateEnum();
                        needsRefresh = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[EnumAssetPostprocessor] Deferred generation for '{targetAsset.name}' due to compilation/refactoring state: {ex.Message}");
                    }
                }
            }

            if (needsRefresh)
            {
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif