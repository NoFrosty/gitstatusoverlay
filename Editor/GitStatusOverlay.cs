using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CaesiumGames.Editor.GitStatusOverlay
{
    /// <summary>
    /// Displays Git status overlays in the Unity Project window for assets and folders.
    /// </summary>
    [InitializeOnLoad]
    public static class GitStatusOverlay
    {
        /// <summary>
        /// Stores the Git status for each asset path (lowercase, no .meta files).
        /// </summary>
        static readonly Dictionary<string, GitStatus> gitStatuses = new Dictionary<string, GitStatus>();

        /// <summary>
        /// The current overlay configuration.
        /// </summary>
        static GitStatusOverlayConfig config;

        // Static constructor: setup overlay and periodic refresh
        static GitStatusOverlay()
        {
            LoadConfig();
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            RefreshGitStatus();
            EditorApplication.update += UpdateLoop;
        }

        /// <summary>
        /// Loads the overlay configuration asset, or creates one if missing.
        /// </summary>
        static void LoadConfig()
        {
            var guids = AssetDatabase.FindAssets("t:GitStatusOverlayConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<GitStatusOverlayConfig>(path);
            }
            else
            {
                var asset = ScriptableObject.CreateInstance<GitStatusOverlayConfig>();
                AssetDatabase.CreateAsset(asset, "Assets/Editor/GitStatusOverlay/GitStatusOverlayConfig.asset");
                AssetDatabase.SaveAssets();
                config = asset;
            }
        }

        /// <summary>
        /// Gets the current overlay configuration.
        /// </summary>
        public static GitStatusOverlayConfig Config => config;

        /// <summary>
        /// Triggers a manual refresh of Git status.
        /// </summary>
        public static void InvokeRefresh() => RefreshGitStatus();

        static double nextUpdateTime = 0;

        /// <summary>
        /// Periodically refreshes Git status every 10 seconds.
        /// </summary>
        static void UpdateLoop()
        {
            if (config == null)
            {
                var guids = AssetDatabase.FindAssets("t:GitStatusOverlayConfig");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = AssetDatabase.LoadAssetAtPath<GitStatusOverlayConfig>(path);
                }
            }
            if (EditorApplication.timeSinceStartup > nextUpdateTime)
            {
                RefreshGitStatus();
                nextUpdateTime = EditorApplication.timeSinceStartup + 10.0;
            }
        }

        /// <summary>
        /// Refreshes the Git status for all assets in the project.
        /// </summary>
        static void RefreshGitStatus()
        {
            gitStatuses.Clear();

            ProcessStartInfo psi = new ProcessStartInfo("git", "status --porcelain=2 -z -uall")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetProjectRoot()
            };

            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                int i = 0;
                while (i < output.Length)
                {
                    int next = output.IndexOf('\0', i);
                    if (next == -1) break;
                    string entry = output.Substring(i, next - i);
                    i = next + 1;

                    if (string.IsNullOrWhiteSpace(entry)) continue;

                    // Ordinary entry (tracked file)
                    if (entry.StartsWith("1 "))
                    {
                        var parts = entry.Split(' ');
                        if (parts.Length < 4) continue;
                        string xy = parts[1];
                        string path = parts[parts.Length - 1].Trim().Replace("\\", "/");
                        if (!path.StartsWith("Assets/")) continue;
                        if (path.EndsWith(".meta")) continue;

                        char x = xy.Length > 0 ? xy[0] : ' ';
                        char y = xy.Length > 1 ? xy[1] : ' ';

                        GitStatus status = GitStatus.None;

                        // Staged status
                        if (x == 'A') status |= GitStatus.Staged;
                        if (x == 'M') status |= GitStatus.Staged | GitStatus.Modified;
                        if (x == 'D') status |= GitStatus.Staged | GitStatus.Deleted;
                        if (x == 'R') status |= GitStatus.Staged | GitStatus.Renamed;
                        if (x == 'C') status |= GitStatus.Staged | GitStatus.Copied;
                        if (x == 'U') status |= GitStatus.Conflicted;

                        // Unstaged status
                        if (y == 'M') status |= GitStatus.Modified;
                        if (y == 'D') status |= GitStatus.Deleted;
                        if (y == 'U') status |= GitStatus.Conflicted;

                        string key = path.ToLower();
                        if (gitStatuses.ContainsKey(key))
                            gitStatuses[key] |= status;
                        else
                            gitStatuses[key] = status;
                    }
                    // Untracked file
                    else if (entry.StartsWith("? "))
                    {
                        string path = entry.Substring(2).Trim().Replace("\\", "/");
                        if (path.StartsWith("Assets/"))
                            gitStatuses[path.ToLower()] = GitStatus.Untracked;
                    }
                    // Ignored file
                    else if (entry.StartsWith("! "))
                    {
                        string path = entry.Substring(2).Trim().Replace("\\", "/");
                        if (path.StartsWith("Assets/"))
                            gitStatuses[path.ToLower()] = GitStatus.Ignored;
                    }
                    // Extend here for other porcelain v2 record types if needed
                }
            }
        }

        /// <summary>
        /// Draws the Git status icon overlay in the Project window for assets and folders.
        /// </summary>
        static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (config == null) return;

            string path = AssetDatabase.GUIDToAssetPath(guid).ToLower();
            if (!path.StartsWith("assets/")) return;

            float size = config.iconSize;
            Rect iconRect = GetIconRect(selectionRect, size, config.iconPosition);

            // Folder overlay logic
            if (Directory.Exists(path))
            {
                if (!config.showIconsForFolders) return;

                var files = Directory.GetFiles(path);
                bool allUntracked = true;
                bool hasUntracked = false;
                bool hasModified = false;

                foreach (var file in files)
                {
                    string filePath = file.Replace("\\", "/").ToLower();
                    if (gitStatuses.TryGetValue(filePath, out GitStatus statusFolder))
                    {
                        if ((statusFolder & GitStatus.Untracked) != 0)
                        {
                            hasUntracked = true;
                        }
                        else
                        {
                            allUntracked = false;
                            if ((statusFolder & GitStatus.Modified) != 0) hasModified = true;
                        }
                    }
                    else
                    {
                        allUntracked = false;
                    }
                }

                // Show "added" icon if all files are untracked (folder just created)
                if (hasUntracked && allUntracked && config.iconAdded != null)
                {
                    DrawIcon(iconRect, config.iconAdded, config.iconOpacity);
                }
                // Show "modified" icon if any file is modified
                else if (hasModified && config.iconModified != null)
                {
                    DrawIcon(iconRect, config.iconModified, config.iconOpacity);
                }
                return;
            }

            // File overlay logic
            gitStatuses.TryGetValue(path, out GitStatus status);
            if (!ShouldShowStatusIcon(status)) return;

            Texture2D icon = null;
            if ((status & GitStatus.Untracked) != 0)
                icon = config.iconAdded;
            else if ((status & GitStatus.Modified) != 0)
                icon = config.iconModified;
            else if ((status & GitStatus.Ignored) != 0)
                icon = config.iconIgnored;
            else if ((status & GitStatus.Renamed) != 0)
                icon = config.iconRenamed;

            if (icon != null)
                DrawIcon(iconRect, icon, config.iconOpacity);
        }

        /// <summary>
        /// Returns true if the status should be shown according to the config mask.
        /// </summary>
        static bool ShouldShowStatusIcon(GitStatus status)
        {
            if (status == GitStatus.None) return false;
            var mask = config.iconStatus;
            return (status & mask) != 0;
        }

        /// <summary>
        /// Draws a texture icon with the specified opacity.
        /// </summary>
        static void DrawIcon(Rect rect, Texture2D icon, float opacity)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, opacity);
            GUI.DrawTexture(rect, icon);
            GUI.color = prevColor;
        }

        /// <summary>
        /// Calculates the icon rectangle based on the selection and config position.
        /// </summary>
        static Rect GetIconRect(Rect selectionRect, float size, IconPosition position)
        {
            float x = selectionRect.x;
            float y = selectionRect.y;

            switch (position)
            {
                case IconPosition.TopLeft:
                    x += 2; y += 2;
                    break;
                case IconPosition.TopCenter:
                    x += (selectionRect.width - size) / 2; y += 2;
                    break;
                case IconPosition.TopRight:
                    x += selectionRect.width - size - 2; y += 2;
                    break;
                case IconPosition.MiddleLeft:
                    x += 2; y += (selectionRect.height - size) / 2;
                    break;
                case IconPosition.MiddleCenter:
                    x += (selectionRect.width - size) / 2; y += (selectionRect.height - size) / 2;
                    break;
                case IconPosition.MiddleRight:
                    x += selectionRect.width - size - 2; y += (selectionRect.height - size) / 2;
                    break;
                case IconPosition.BottomLeft:
                    x += 2; y += selectionRect.height - size - 2;
                    break;
                case IconPosition.BottomCenter:
                    x += (selectionRect.width - size) / 2; y += selectionRect.height - size - 2;
                    break;
                case IconPosition.BottomRight:
                    x += selectionRect.width - size - 2; y += selectionRect.height - size - 2;
                    break;
            }
            return new Rect(x, y, size, size);
        }

        /// <summary>
        /// Gets the root directory of the Unity project.
        /// </summary>
        static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        /// <summary>
        /// Sets the overlay configuration.
        /// </summary>
        public static void SetConfig(GitStatusOverlayConfig newConfig)
        {
            config = newConfig;
        }

        /// <summary>
        /// Returns a read-only view of all git statuses (for external tools/windows).
        /// </summary>
        public static IReadOnlyDictionary<string, GitStatus> GetAllStatuses() => gitStatuses;
    }
}
