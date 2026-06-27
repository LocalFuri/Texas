using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TexasHoldem
{
    /// <summary>Re-applies action-panel Scene view after exiting Play mode or opening a scene.</summary>
    [InitializeOnLoad]
    public static class ActionPanelScenePreview
    {
        static ActionPanelScenePreview()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall -= RestoreAll;
            EditorApplication.delayCall += RestoreAll;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.delayCall -= RestoreAll;
            EditorApplication.delayCall += RestoreAll;
        }

        private static void RestoreAll()
        {
            EditorApplication.delayCall -= RestoreAll;

#if UNITY_2022_2_OR_NEWER
            UIManager[] managers = Object.FindObjectsByType<UIManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            UIManager[] managers = Object.FindObjectsOfType<UIManager>(true);
#endif
            foreach (UIManager manager in managers)
            {
                if (manager != null)
                    manager.ApplySceneModePreview();
            }
        }
    }
}
