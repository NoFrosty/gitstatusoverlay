using System.IO;
using UnityEditor;
using UnityEngine;

namespace CaesiumGames.Editor.GitStatusOverlay
{
    /// <summary>
    /// Editor window for configuring Git status overlay icons and options.
    /// Accessible via Window > Git Status Overlay in the Unity menu.
    /// </summary>
    public class GitStatusOverlayWindow : EditorWindow
    {
        private GitStatusOverlayConfig config;
        private Vector2 scrollPosition;

        /// <summary>
        /// Opens the Git Status Overlay configuration window.
        /// </summary>
        [MenuItem("Window/Git Status Overlay")]
        public static void ShowWindow()
        {
            GitStatusOverlayWindow window = GetWindow<GitStatusOverlayWindow>("Git Status Overlay");
            window.minSize = new Vector2(300, 400);
        }

        private void OnEnable()
        {
            config = GitStatusOverlay.Config;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (config == null)
            {
                DrawNoConfigUI();
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawConfigUI();
            
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the UI when no configuration asset is found.
        /// </summary>
        private void DrawNoConfigUI()
        {
            EditorGUILayout.HelpBox(
                "No GitStatusOverlayConfig found. Please create one via Assets > Create > GitStatusOverlay > Config.",
                MessageType.Warning
            );
            
            if (GUILayout.Button("Create Config"))
            {
                CreateConfigAsset();
            }
        }

        /// <summary>
        /// Creates a new configuration asset.
        /// </summary>
        private void CreateConfigAsset()
        {
            GitStatusOverlayConfig asset = ScriptableObject.CreateInstance<GitStatusOverlayConfig>();
            string directory = "Assets/Editor/GitStatusOverlay";
            
            // Ensure directory exists
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            string assetPath = $"{directory}/GitStatusOverlayConfig.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            
            config = asset;
            GitStatusOverlay.SetConfig(asset);
        }

        /// <summary>
        /// Draws the main configuration UI.
        /// </summary>
        private void DrawConfigUI()
        {
            GUILayout.Space(8);
            GUILayout.Label("Git Status Overlay Configuration", EditorStyles.boldLabel);

            DrawIconsSection();
            DrawDisplaySettingsSection();
            DrawFilterSettingsSection();
            DrawActionsSection();
        }

        /// <summary>
        /// Draws the Icons configuration section.
        /// </summary>
        private void DrawIconsSection()
        {
            GUILayout.Space(16);
            GUILayout.Label("Icons", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            config.iconAdded = (Texture2D)EditorGUILayout.ObjectField(
                "Added Icon", config.iconAdded, typeof(Texture2D), false);
            config.iconModified = (Texture2D)EditorGUILayout.ObjectField(
                "Modified Icon", config.iconModified, typeof(Texture2D), false);
            config.iconIgnored = (Texture2D)EditorGUILayout.ObjectField(
                "Ignored Icon", config.iconIgnored, typeof(Texture2D), false);
            config.iconRenamed = (Texture2D)EditorGUILayout.ObjectField(
                "Renamed Icon", config.iconRenamed, typeof(Texture2D), false);
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }
        }

        /// <summary>
        /// Draws the Display Settings section.
        /// </summary>
        private void DrawDisplaySettingsSection()
        {
            GUILayout.Space(16);
            GUILayout.Label("Display Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            
            config.iconSize = EditorGUILayout.IntSlider(
                new GUIContent("Icon Size (px)", "Size of overlay icons in pixels"), 
                config.iconSize, 8, 32);
            
            config.iconOpacity = EditorGUILayout.Slider(
                new GUIContent("Opacity", "Opacity of overlay icons (0 = transparent, 1 = opaque)"),
                config.iconOpacity, 0f, 1f);
            
            config.iconPosition = (IconPosition)EditorGUILayout.EnumPopup(
                new GUIContent("Position", "Position of the overlay icon on each item"),
                config.iconPosition);
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }
        }

        /// <summary>
        /// Draws the Filter Settings section.
        /// </summary>
        private void DrawFilterSettingsSection()
        {
            GUILayout.Space(16);
            GUILayout.Label("Filter Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            
            config.showIconsForFolders = EditorGUILayout.Toggle(
                new GUIContent("Show Icons For Folders", "Display status icons on folders based on their contents"),
                config.showIconsForFolders);
            
            config.iconStatus = (GitStatus)EditorGUILayout.EnumFlagsField(
                new GUIContent("Visible Statuses", "Which Git statuses should display icons"),
                config.iconStatus);
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                Repaint();
            }
        }

        /// <summary>
        /// Draws the Actions section with buttons.
        /// </summary>
        private void DrawActionsSection()
        {
            GUILayout.Space(16);
            GUILayout.Label("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent("Refresh Git Status", "Manually refresh Git status for all assets")))
            {
                GitStatusOverlay.InvokeRefresh();
            }

            GUILayout.Space(8);
            
            if (GUILayout.Button(new GUIContent("Reset to Defaults", "Reset all settings to default values")))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Configuration",
                    "Are you sure you want to reset all settings to their default values? Icon assignments will be preserved.",
                    "Reset",
                    "Cancel"))
                {
                    config.Reset();
                    EditorUtility.SetDirty(config);
                    Repaint();
                }
            }

            GUILayout.Space(8);
            
            if (GUILayout.Button(new GUIContent("Locate Config Asset", "Find and select the config asset in the Project window")))
            {
                LocateConfigAsset();
            }
        }

        /// <summary>
        /// Locates and selects the configuration asset in the Project window.
        /// </summary>
        private void LocateConfigAsset()
        {
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
                EditorUtility.DisplayDialog(
                    "Git Status Overlay",
                    "No GitStatusOverlayConfig asset found in the project.",
                    "OK");
            }
        }
    }
}