using UnityEditor;
using UnityEngine;

namespace CaesiumGames.Editor.GitStatusOverlay
{
    /// <summary>
    /// Editor window for configuring Git status overlay icons and options.
    /// </summary>
    public class GitStatusOverlayWindow : EditorWindow
    {
        private GitStatusOverlayConfig config;

        [MenuItem("Window/Git Status Overlay")]
        public static void ShowWindow()
        {
            GetWindow<GitStatusOverlayWindow>("Git Status Overlay");
        }

        void OnEnable()
        {
            config = GitStatusOverlay.Config;
        }

        void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("No GitStatusOverlayConfig found. Please create one via Assets > Create > GitStatusOverlay > Config.", MessageType.Warning);
                if (GUILayout.Button("Create Config"))
                {
                    var asset = ScriptableObject.CreateInstance<GitStatusOverlayConfig>();
                    AssetDatabase.CreateAsset(asset, "Assets/Editor/GitStatusOverlay/GitStatusOverlayConfig.asset");
                    AssetDatabase.SaveAssets();
                    config = asset;
                    GitStatusOverlay.SetConfig(asset);
                }
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label("Git Status Overlay", EditorStyles.boldLabel);

            GUILayout.Space(16);
            GUILayout.Label("Icons", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            config.iconAdded = (Texture2D)EditorGUILayout.ObjectField("Added Icon", config.iconAdded, typeof(Texture2D), false);
            config.iconModified = (Texture2D)EditorGUILayout.ObjectField("Modified Icon", config.iconModified, typeof(Texture2D), false);
            config.iconIgnored = (Texture2D)EditorGUILayout.ObjectField("Ignored Icon", config.iconIgnored, typeof(Texture2D), false);
            config.iconRenamed = (Texture2D)EditorGUILayout.ObjectField("Renamed Icon", config.iconRenamed, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }


            GUILayout.Space(16);
            GUILayout.Label("Icon Size", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            config.iconSize = EditorGUILayout.IntSlider("Icon Size (px)", config.iconSize, 8, 32);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }

            GUILayout.Space(16);
            GUILayout.Label("Icon Opacity", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            config.iconOpacity = EditorGUILayout.Slider("Opacity", config.iconOpacity, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }

            GUILayout.Space(16);
            GUILayout.Label("Icon Position", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            config.iconPosition = (IconPosition)EditorGUILayout.EnumPopup("Position", config.iconPosition);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }

            GUILayout.Space(16);
            GUILayout.Label("Show Icons For Folders", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            config.showIconsForFolders = EditorGUILayout.Toggle("Show Icons For Folders", config.showIconsForFolders);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }

            GUILayout.Space(16);
            GUILayout.Label("Show Icons For Status", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            config.iconStatus = (GitStatus)EditorGUILayout.EnumFlagsField("Statuses", config.iconStatus);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }


            GUILayout.Space(16);

            if (GUILayout.Button("Refresh Git Status"))
            {
                GitStatusOverlay.InvokeRefresh();
            }


            GUILayout.Space(8);
            if (GUILayout.Button("Reset Icon Fields"))
            {
                config.Reset();
                EditorUtility.SetDirty(config);
                Repaint();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Open Config"))
            {
                // Find the config asset in the project
                string[] guids = AssetDatabase.FindAssets("t:GitStatusOverlayConfig");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    Object configAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (configAsset != null)
                    {
                        Selection.activeObject = configAsset;
                        EditorGUIUtility.PingObject(configAsset);
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Git Status Overlay", "No GitStatusOverlayConfig asset found in the project.", "OK");
                }
            }
        }
    }
}