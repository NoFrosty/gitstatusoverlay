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
            DrawRemoteTrackingSection();
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
            config.iconMoved = (Texture2D)EditorGUILayout.ObjectField(
                "Moved Icon", config.iconMoved, typeof(Texture2D), false);
            config.iconOriginAvailable = (Texture2D)EditorGUILayout.ObjectField(
                "Origin Available Icon", config.iconOriginAvailable, typeof(Texture2D), false);
            config.iconPushAvailable = (Texture2D)EditorGUILayout.ObjectField(
                "Push Available Icon", config.iconPushAvailable, typeof(Texture2D), false);
            config.iconWarning = (Texture2D)EditorGUILayout.ObjectField(
                "Warning Icon", config.iconWarning, typeof(Texture2D), false);

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

            config.iconSizeListView = EditorGUILayout.IntSlider(
                new GUIContent("List View Icon Size (px)", "Size of overlay icons in pixels for list view"),
                config.iconSizeListView, 8, 32);

            config.iconSizeIconView = EditorGUILayout.IntSlider(
                new GUIContent("Icon View Icon Size (px)", "Size of overlay icons in pixels for icon view"),
                config.iconSizeIconView, 8, 32);

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
        /// Draws the Remote Tracking section.
        /// </summary>
        private void DrawRemoteTrackingSection()
        {
            GUILayout.Space(16);
            GUILayout.Label("Remote Tracking", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            config.enableAutoFetch = EditorGUILayout.Toggle(
                new GUIContent("Enable Auto Fetch", "Automatically fetch from remote to check for origin changes"),
                config.enableAutoFetch);

            if (config.enableAutoFetch)
            {
                EditorGUI.indentLevel++;
                config.autoFetchInterval = EditorGUILayout.IntSlider(
                    new GUIContent("Fetch Interval (seconds)", "Time between automatic fetches"),
                    config.autoFetchInterval, 60, 3600);
                EditorGUI.indentLevel--;
            }

            config.showPushAvailable = EditorGUILayout.Toggle(
                new GUIContent("Show Push Available", "Show icon for files with commits not yet pushed to origin"),
                config.showPushAvailable);

            config.detectPotentialConflicts = EditorGUILayout.Toggle(
                new GUIContent("Detect Potential Conflicts", "Show warning icon for files modified both locally and in origin"),
                config.detectPotentialConflicts);

            // Show info message if any remote feature is enabled
            if (config.enableAutoFetch || config.showPushAvailable || config.detectPotentialConflicts)
            {
                EditorGUILayout.HelpBox(
                    "Remote tracking features require a remote repository to be configured. " +
                    "If no remote is configured, these features will be silently disabled.",
                    MessageType.Info);
            }

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