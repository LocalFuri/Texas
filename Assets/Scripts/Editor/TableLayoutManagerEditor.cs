using UnityEditor;
using UnityEngine;

namespace TexasHoldem
{
    [CustomEditor(typeof(TableLayoutManager))]
    public class TableLayoutManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            prop.NextVisible(true); // skip m_Script

            while (prop.NextVisible(false))
            {
                EditorGUILayout.PropertyField(prop, true);

                if (prop.name == "_cardWidth")
                {
                    float cardHeight = prop.floatValue * (95f / 65f);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.FloatField("Card Height (auto)", cardHeight);
                }

                if (prop.name == "_chipSize")
                {
                    float displaySize = prop.floatValue * 1.25f;
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.FloatField("Chip Display Size (auto)", displaySize);
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "HudPanel width/X come from Card_1. Per-seat HudGlow → Panel Right/Bottom Border Px (default 14) " +
                "extend the dark fill past card edges. Bet column: avatar → chips → amount, centred on avatar X. " +
                "Bet Gap Below Avatar, Chip Size, and Stack Overlap Y (2–4 px) tune the bet column. " +
                "Dealer Outside Gap keeps the token clear of the avatar edge (left for most seats, " +
                "right for mirrorHud seats). Avatar Diameter sizes " +
                "the frame and rings on every seat. Community Card Gap " +
                "spaces the flop/turn/river row; Community Card Scale sizes board cards only (Hole Cards → Card Width). " +
                "Community Card Y moves the whole row up/down. " +
                "Pot label position: move PotText " +
                "in the Scene view or its Rect Transform. Apply Layout to refresh seats.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.45f, 0.95f, 0.55f);
            if (GUILayout.Button("Apply Layout", GUILayout.Height(32)))
            {
                var mgr = (TableLayoutManager)target;
                mgr.ApplyLayout();
                EditorUtility.SetDirty(mgr.gameObject);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
        }

        private void OnSceneGUI()
        {
            try
            {
                var mgr = (TableLayoutManager)target;
                if (mgr == null) return;

                PlayerView[] views = mgr.GetPlayerViews();
                if (views == null || views.Length == 0) return;

                var so = serializedObject;
                if (so == null) return;
                so.Update();

                var canvasProp = so.FindProperty("_canvasRect");
                if (canvasProp == null || canvasProp.objectReferenceValue == null) return;

                var canvasRect = canvasProp.objectReferenceValue as RectTransform;
                if (canvasRect == null) return;

                var seatsProp = so.FindProperty("_seats");
                if (seatsProp == null || !seatsProp.isArray) return;
                int seatCount = seatsProp.arraySize;

                float scale = canvasRect.localScale.x;

                for (int i = 0; i < views.Length && i < seatCount && i < TableLayoutManager.SeatCount; i++)
                {
                    if (views[i] == null) continue;
                    var rt = views[i].transform as RectTransform;
                    if (rt == null) continue;

                    var seatProp = seatsProp.GetArrayElementAtIndex(i);
                    if (seatProp == null) continue;
                    var sizeProp = seatProp.FindPropertyRelative("size");
                    if (sizeProp == null) continue;

                    var   size = sizeProp.vector2Value;
                    float r    = Mathf.Min(size.x, size.y) * 0.5f * scale;
                    Vector3 wPos = canvasRect.TransformPoint(
                        new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f));

                    Handles.color = i == 0
                        ? new Color(0.2f, 1f, 0.4f, 0.85f)
                        : new Color(0.3f, 0.8f, 1f, 0.85f);

                    Handles.DrawWireDisc(wPos, Vector3.forward, r);
                    string seatLabel = views[i].ResolveDisplayName();
                    if (string.IsNullOrWhiteSpace(seatLabel))
                        seatLabel = i == 0 ? "You" : $"Bot {i}";
                    Handles.Label(wPos + Vector3.up * (r + 4f * scale), seatLabel);
                }
            }
            catch (System.Exception) { /* suppress stale-assembly errors during recompile */ }
        }
    }
}
