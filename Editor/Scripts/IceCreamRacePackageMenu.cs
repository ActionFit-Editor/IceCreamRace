#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ActionFit.IceCreamRace.Editor
{
    public static class IceCreamRacePackageMenu
    {
        private const string MenuRoot = "Tools/Package/ActionFit Ice Cream Race/";
        private const string ReadmePath = "Packages/com.actionfit.icecream-race/README.md";
        private const int ReadmePriority = 904;

        [MenuItem(MenuRoot + "README", false, ReadmePriority)]
        private static void OpenReadme()
        {
            var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmePath);
            if (readme == null)
            {
                EditorUtility.DisplayDialog("Package README", $"README was not found.\n{ReadmePath}", "OK");
                return;
            }

            Selection.activeObject = readme;
            AssetDatabase.OpenAsset(readme);
        }
    }
}
#endif
