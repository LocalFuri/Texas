using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Refocuses the Scene view after exiting Play mode so design work
    /// resumes in Scene view rather than Game view.
    /// Also plays a preview sound when Escape is pressed in Scene mode.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneViewFocus
    {
        private const string EscapeSoundPath = "Assets/Sounds/Pop04.wav";

        static SceneViewFocus()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            SceneView.duringSceneGui               += OnSceneGUI;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode &&
                state != PlayModeStateChange.EnteredPlayMode) return;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                sceneView.Focus();
        }

        private static void OnSceneGUI(SceneView _)
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape)
                return;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EscapeSoundPath);
            if (clip != null)
                PlayPreviewClip(clip);
        }

        /// <summary>Plays an AudioClip in the Editor without entering Play mode via Unity's internal AudioUtil.</summary>
        private static void PlayPreviewClip(AudioClip clip)
        {
            Assembly assembly = typeof(AudioImporter).Assembly;
            System.Type audioUtil = assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null) return;

            MethodInfo method = audioUtil.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);

            method?.Invoke(null, new object[] { clip, 0, false });
        }
    }
}
