#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TrashCount.Data.Generators;

namespace TrashCount.Data.Editor
{
    public class UniversalEnumSaveProcessor : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            bool generatedAny = false;

            foreach (string path in paths)
            {
                if (!path.EndsWith(".asset")) continue;

                Object targetAsset = AssetDatabase.LoadMainAssetAtPath(path);
                
                if (targetAsset is IEnumGeneratable generatableAsset)
                {
                    Debug.Log($"[UniversalSaveProcessor] Generating enum for '{targetAsset.name}'...");
                    
                    generatableAsset.GenerateEnum();
                    generatedAny = true;
                }
            }

            if (generatedAny)
            {
                AssetDatabase.Refresh();
            }

            return paths;
        }
    }
}
#endif