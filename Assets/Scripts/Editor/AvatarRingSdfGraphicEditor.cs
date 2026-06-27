using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace TexasHoldem
{
    [CustomEditor(typeof(AvatarRingSdfGraphic), true)]
    [CanEditMultipleObjects]
    public class AvatarRingSdfGraphicEditor : RawImageEditor
    {
        // Ring-specific serialized properties
        private SerializedProperty _shader;
        private SerializedProperty _look;
        private SerializedProperty _strokeWidthPx;
        private SerializedProperty _fillAmount;
        private SerializedProperty _chromeColorTop;
        private SerializedProperty _chromeColorBot;
        private SerializedProperty _goldColorTop;
        private SerializedProperty _goldColorBot;
        private SerializedProperty _outerRadiusPx;

        private static readonly GUIContent LabelShader        = new GUIContent("Shader");
        private static readonly GUIContent LabelLook          = new GUIContent("Look");
        private static readonly GUIContent LabelStroke        = new GUIContent("Stroke Width (px)");
        private static readonly GUIContent LabelFill          = new GUIContent("Fill Amount");
        private static readonly GUIContent LabelChromeTop     = new GUIContent("Metal Bright");
        private static readonly GUIContent LabelChromeBot     = new GUIContent("Metal Dark");
        private static readonly GUIContent LabelGoldTop       = new GUIContent("Metal Bright");
        private static readonly GUIContent LabelGoldBot       = new GUIContent("Metal Dark");
        private static readonly GUIContent LabelOuterRadius   = new GUIContent("Outer Radius (px)", "Set to -1 to auto-derive from RectTransform size.");

        protected override void OnEnable()
        {
            base.OnEnable();
            _shader        = serializedObject.FindProperty("_shader");
            _look          = serializedObject.FindProperty("_look");
            _strokeWidthPx = serializedObject.FindProperty("_strokeWidthPx");
            _fillAmount    = serializedObject.FindProperty("_fillAmount");
            _chromeColorTop = serializedObject.FindProperty("_chromeColorTop");
            _chromeColorBot = serializedObject.FindProperty("_chromeColorBot");
            _goldColorTop  = serializedObject.FindProperty("_goldColorTop");
            _goldColorBot  = serializedObject.FindProperty("_goldColorBot");
            _outerRadiusPx = serializedObject.FindProperty("_outerRadiusPx");
        }

        public override void OnInspectorGUI()
        {
            // Draw the standard RawImage fields (Texture, Color, Material, etc.)
            base.OnInspectorGUI();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Ring Settings", EditorStyles.boldLabel);

            serializedObject.Update();

            EditorGUILayout.PropertyField(_shader, LabelShader);
            EditorGUILayout.PropertyField(_look, LabelLook);
            EditorGUILayout.PropertyField(_strokeWidthPx, LabelStroke);
            EditorGUILayout.PropertyField(_outerRadiusPx, LabelOuterRadius);

            var look = (AvatarRingSdfGraphic.RingLook)_look.enumValueIndex;

            if (look == AvatarRingSdfGraphic.RingLook.Gold)
            {
                EditorGUILayout.PropertyField(_fillAmount, LabelFill);
                EditorGUILayout.PropertyField(_goldColorTop, LabelGoldTop);
                EditorGUILayout.PropertyField(_goldColorBot, LabelGoldBot);
            }
            else
            {
                EditorGUILayout.PropertyField(_chromeColorTop, LabelChromeTop);
                EditorGUILayout.PropertyField(_chromeColorBot, LabelChromeBot);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (Object t in targets)
                    ((AvatarRingSdfGraphic)t).SetMaterialDirty();
            }
        }
    }
}
