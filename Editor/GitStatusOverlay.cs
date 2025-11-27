using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CaesiumGames.Editor.GitStatusOverlay
{
    /// <summary>
    /// Displays Git status overlays in the Unity Project window for assets and folders.
    /// Automatically refreshes Git status every 10 seconds and displays appropriate icons
    /// based on file status (modified, added, renamed, ignored, etc.).
    /// </summary>
    [InitializeOnLoad]
    public static class GitStatusOverlay
    {
        /// <summary>
        /// Stores the Git status for each asset path (lowercase, no .meta files).
        /// Key: Normalized lowercase file path. Value: Git status flags.
        /// </summary>
        private static readonly Dictionary<string, GitStatus> gitStatuses = new Dictionary<string, GitStatus>();

        /// <summary>
        /// Stores GUID mappings from .meta files for rename detection.
        /// Key: GUID. Value: File path.
        /// </summary>
        private static readonly Dictionary<string, string> guidToPathMap = new Dictionary<string, string>();

        /// <summary>
        /// Stores deleted file paths with their GUIDs for rename detection.
        /// Key: File path. Value: GUID.
        /// </summary>
        private static readonly Dictionary<string, string> deletedFiles = new Dictionary<string, string>();

        /// <summary>
        /// Stores untracked file paths with their GUIDs for rename detection.
        /// Key: File path. Value: GUID.
        /// </summary>
        private static readonly Dictionary<string, string> untrackedFiles = new Dictionary<string, string>();

        /// <summary>
        /// The current overlay configuration. Contains icon assignments and display settings.
        /// </summary>
        private static GitStatusOverlayConfig config;

        /// <summary>
        /// Static constructor: Sets up overlay and periodic refresh on Unity Editor load.
        /// </summary>
        static GitStatusOverlay()
        {
            LoadConfig();
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            RefreshGitStatus();
            EditorApplication.update += UpdateLoop;
        }

        /// <summary>
        /// Loads the overlay configuration asset, or creates one if missing.
        /// Searches for existing GitStatusOverlayConfig asset in the project, or creates
        /// a new one in Assets/Editor/GitStatusOverlay/ if none exists.
        /// </summary>
        private static void LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:GitStatusOverlayConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<GitStatusOverlayConfig>(path);
            }
            else
            {
                // Create default config asset
                GitStatusOverlayConfig asset = ScriptableObject.CreateInstance<GitStatusOverlayConfig>();
                string directory = "Assets/Editor/GitStatusOverlay";
                
                // Ensure directory exists
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                AssetDatabase.CreateAsset(asset, $"{directory}/GitStatusOverlayConfig.asset");
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
        /// Useful for forcing an update after Git operations.
        /// </summary>
        public static void InvokeRefresh() => RefreshGitStatus();

        /// <summary>
        /// The next time (in editor time) to refresh Git status.
        /// </summary>
        private static double nextUpdateTime = 0;

        /// <summary>
        /// Update interval in seconds for automatic Git status refresh.
        /// </summary>
        private const double UpdateInterval = 10.0;

        /// <summary>
        /// Periodically refreshes Git status every 10 seconds.
        /// Called from EditorApplication.update.
        /// </summary>
        private static void UpdateLoop()
        {
            if (EditorApplication.timeSinceStartup > nextUpdateTime)
            {
                RefreshGitStatus();
                nextUpdateTime = EditorApplication.timeSinceStartup + UpdateInterval;
            }
        }

        /// <summary>
        /// Refreshes the Git status for all assets in the project.
        /// Executes 'git status --porcelain=2 -z -uall' and parses the output.
        /// Only processes files within the Assets/ folder.
        /// </summary>
        private static void RefreshGitStatus()
        {
            gitStatuses.Clear();
            guidToPathMap.Clear();
            deletedFiles.Clear();
            untrackedFiles.Clear();

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", "status --porcelain=2 -z -uall")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        UnityEngine.Debug.LogWarning("[GitStatusOverlay] Failed to start Git process.");
                        return;
                    }

                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    // Check for errors
                    if (p.ExitCode != 0)
                    {
                        string error = p.StandardError.ReadToEnd();
                        UnityEngine.Debug.LogWarning($"[GitStatusOverlay] Git command failed: {error}");
                        return;
                    }

                    ParseGitStatusOutput(output);
                    
                    DetectUnityRenames();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] Error refreshing Git status: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Parses the output from 'git status --porcelain=2 -z -uall'.
        /// </summary>
        /// <param name="output">The null-terminated output from Git.</param>
        private static void ParseGitStatusOutput(string output)
        {
            int i = 0;
            int entryCount = 0;
            
            while (i < output.Length)
            {
                int next = output.IndexOf('\0', i);
                if (next == -1) break;
                
                string entry = output.Substring(i, next - i);
                i = next + 1;

                if (string.IsNullOrWhiteSpace(entry)) continue;

                entryCount++;

                // Ordinary entry (tracked file) - format: "1 <XY> ..."
                if (entry.StartsWith("1 "))
                {
                    ParseOrdinaryEntry(entry);
                }
                // Untracked file - format: "? <path>"
                else if (entry.StartsWith("? "))
                {
                    ParseUntrackedEntry(entry);
                }
                // Ignored file - format: "! <path>"
                else if (entry.StartsWith("! "))
                {
                    ParseIgnoredEntry(entry);
                }
            }
        }

        /// <summary>
        /// Parses an ordinary (tracked) file entry from Git status.
        /// </summary>
        private static void ParseOrdinaryEntry(string entry)
        {
            // Format: "1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>"
            // We need to extract XY and the full path (which may contain spaces)
            
            string[] parts = entry.Split(' ');
            
            if (parts.Length < 9)
            {
                return;
            }

            string xy = parts[1];
            
            // The path starts at index 8 and may contain spaces, so join all remaining parts
            string path = string.Join(" ", parts, 8, parts.Length - 8).Trim().Replace("\\", "/");
            
            if (!IsValidAssetPath(path))
            {
                return;
            }

            char x = xy.Length > 0 ? xy[0] : ' ';
            char y = xy.Length > 1 ? xy[1] : ' ';

            GitStatus status = GitStatus.None;

            // Parse staged status (index)
            switch (x)
            {
                case 'A': 
                    status |= GitStatus.Staged;
                    break;
                case 'M': 
                    status |= GitStatus.Staged | GitStatus.Modified;
                    break;
                case 'D': 
                    status |= GitStatus.Staged | GitStatus.Deleted;
                    // Store deleted files with their GUIDs for rename detection (get from Git history)
                    string deletedGuid = GetGuidFromMetaFile(path, isDeleted: true);
                    if (!string.IsNullOrEmpty(deletedGuid))
                    {
                        deletedFiles[path] = deletedGuid;
                    }
                    break;
                case 'R': 
                    status |= GitStatus.Staged | GitStatus.Renamed;
                    break;
                case 'C': 
                    status |= GitStatus.Staged | GitStatus.Copied;
                    break;
                case 'U': 
                    status |= GitStatus.Conflicted;
                    break;
            }

            // Parse unstaged status (working tree)
            switch (y)
            {
                case 'M': 
                    status |= GitStatus.Modified;
                    break;
                case 'D': 
                    status |= GitStatus.Deleted;
                    // Store deleted files with their GUIDs for rename detection (get from Git history)
                    string guid = GetGuidFromMetaFile(path, isDeleted: true);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        deletedFiles[path] = guid;
                    }
                    break;
                case 'U': 
                    status |= GitStatus.Conflicted;
                    break;
            }

            AddOrUpdateStatus(path, status);
        }

        /// <summary>
        /// Parses an untracked file entry from Git status.
        /// </summary>
        private static void ParseUntrackedEntry(string entry)
        {
            string path = entry.Substring(2).Trim().Replace("\\", "/");
            
            if (IsValidAssetPath(path))
            {
                gitStatuses[path.ToLower()] = GitStatus.Untracked;
                
                // Store untracked files with their GUIDs for rename detection
                string guid = GetGuidFromMetaFile(path);
                if (!string.IsNullOrEmpty(guid))
                {
                    untrackedFiles[path] = guid;
                }
            }
        }

        /// <summary>
        /// Parses an ignored file entry from Git status.
        /// </summary>
        private static void ParseIgnoredEntry(string entry)
        {
            string path = entry.Substring(2).Trim().Replace("\\", "/");
            if (IsValidAssetPath(path))
            {
                gitStatuses[path.ToLower()] = GitStatus.Ignored;
            }
        }

        /// <summary>
        /// Validates if a path is within the Assets folder and not a .meta file.
        /// </summary>
        private static bool IsValidAssetPath(string path)
        {
            return path.StartsWith("Assets/") && !path.EndsWith(".meta");
        }

        /// <summary>
        /// Adds or updates the Git status for a given path.
        /// </summary>
        private static void AddOrUpdateStatus(string path, GitStatus status)
        {
            string key = path.ToLower();
            if (gitStatuses.ContainsKey(key))
            {
                gitStatuses[key] |= status;
            }
            else
            {
                gitStatuses[key] = status;
            }
        }

        /// <summary>
        /// Detects Unity file renames by comparing GUIDs from .meta files.
        /// When a file is deleted and a new file appears with the same GUID, it's a rename.
        /// </summary>
        private static void DetectUnityRenames()
        {
            int renamesDetected = 0;
            
            // Check each untracked file to see if it matches a deleted file's GUID
            foreach (var untrackedPair in untrackedFiles)
            {
                string untrackedPath = untrackedPair.Key;
                string untrackedGuid = untrackedPair.Value;

                // Look for a deleted file with the same GUID
                foreach (var deletedPair in deletedFiles)
                {
                    string deletedPath = deletedPair.Key;
                    string deletedGuid = deletedPair.Value;

                    if (untrackedGuid == deletedGuid)
                    {
                        // Found a rename! Update the status
                        string untrackedKey = untrackedPath.ToLower();
                        string deletedKey = deletedPath.ToLower();
                        
                        // Remove untracked and deleted status
                        if (gitStatuses.ContainsKey(untrackedKey))
                        {
                            gitStatuses.Remove(untrackedKey);
                        }
                        if (gitStatuses.ContainsKey(deletedKey))
                        {
                            gitStatuses.Remove(deletedKey);
                        }

                        // Mark the new path as renamed
                        gitStatuses[untrackedKey] = GitStatus.Renamed | GitStatus.Moved;
                        
                        renamesDetected++;
                        break; // Found the match, no need to check other deleted files
                    }
                }
            }
        }

        /// <summary>
        /// Extracts the GUID from a Unity .meta file.
        /// For deleted files, retrieves the GUID from Git history.
        /// </summary>
        /// <param name="assetPath">The path to the asset (without .meta extension).</param>
        /// <param name="isDeleted">Whether this is a deleted file (retrieve from Git history).</param>
        /// <returns>The GUID string if found, otherwise null.</returns>
        private static string GetGuidFromMetaFile(string assetPath, bool isDeleted = false)
        {
            string metaPath = assetPath + ".meta";
            
            // For deleted files, get GUID from Git history
            if (isDeleted)
            {
                return GetGuidFromGitHistory(metaPath);
            }
            
            // For existing files, read from filesystem
            try
            {
                string fullMetaPath = Path.Combine(GetProjectRoot(), metaPath);
                
                if (!File.Exists(fullMetaPath))
                {
                    return null;
                }

                // Read the meta file and extract GUID
                string[] lines = File.ReadAllLines(fullMetaPath);
                
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("guid:"))
                    {
                        // Extract GUID value (format: "guid: 1234567890abcdef")
                        string guid = trimmedLine.Substring(5).Trim();
                        return guid;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] GetGuidFromMetaFile - Failed to read GUID from meta file: {assetPath}.meta - {ex.Message}\n{ex.StackTrace}");
            }

            return null;
        }

        /// <summary>
        /// Retrieves the GUID from a deleted .meta file using Git history (HEAD version).
        /// </summary>
        /// <param name="metaPath">The relative path to the .meta file.</param>
        /// <returns>The GUID if found, otherwise null.</returns>
        private static string GetGuidFromGitHistory(string metaPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", $"show HEAD:\"{metaPath}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        return null;
                    }

                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (p.ExitCode != 0)
                    {
                        return null;
                    }

                    // Parse the meta file content from Git
                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("guid:"))
                        {
                            string guid = trimmedLine.Substring(5).Trim();
                            return guid;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] GetGuidFromGitHistory - Exception: {ex.Message}\n{ex.StackTrace}");
            }

            return null;
        }

        /// <summary>
        /// Draws the Git status icon overlay in the Project window for assets and folders.
        /// Called by Unity for each visible item in the Project window.
        /// </summary>
        /// <param name="guid">The GUID of the asset.</param>
        /// <param name="selectionRect">The rectangle of the item in the Project window.</param>
        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (config == null) return;

            string path = AssetDatabase.GUIDToAssetPath(guid).ToLower();
            if (!path.StartsWith("assets/")) return;

            float size = config.iconSize;
            Rect iconRect = GetIconRect(selectionRect, size, config.iconPosition);

            // Handle folder overlay
            if (Directory.Exists(path))
            {
                DrawFolderOverlay(path, iconRect);
                return;
            }

            // Handle file overlay
            DrawFileOverlay(path, iconRect);
        }

        /// <summary>
        /// Draws overlay icon for folders based on the status of their contents.
        /// </summary>
        private static void DrawFolderOverlay(string folderPath, Rect iconRect)
        {
            if (!config.showIconsForFolders) return;

            string[] files = Directory.GetFiles(folderPath);
            bool allUntracked = true;
            bool hasUntracked = false;
            bool hasModified = false;

            foreach (string file in files)
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
                        if ((statusFolder & GitStatus.Modified) != 0)
                        {
                            hasModified = true;
                        }
                    }
                }
                else
                {
                    allUntracked = false;
                }
            }

            // Show "added" icon if all files are untracked (new folder)
            if (hasUntracked && allUntracked && config.iconAdded != null)
            {
                DrawIcon(iconRect, config.iconAdded, config.iconOpacity);
            }
            // Show "modified" icon if any file is modified
            else if (hasModified && config.iconModified != null)
            {
                DrawIcon(iconRect, config.iconModified, config.iconOpacity);
            }
        }

        /// <summary>
        /// Draws overlay icon for individual files based on their Git status.
        /// </summary>
        private static void DrawFileOverlay(string filePath, Rect iconRect)
        {
            if (!gitStatuses.TryGetValue(filePath, out GitStatus status)) return;
            if (!ShouldShowStatusIcon(status)) return;

            Texture2D icon = GetIconForStatus(status);
            if (icon != null)
            {
                DrawIcon(iconRect, icon, config.iconOpacity);
            }
        }

        /// <summary>
        /// Gets the appropriate icon for a given Git status.
        /// Priority: Untracked > Modified > Ignored > Renamed.
        /// </summary>
        private static Texture2D GetIconForStatus(GitStatus status)
        {
            if ((status & GitStatus.Untracked) != 0)
                return config.iconAdded;
            if ((status & GitStatus.Modified) != 0)
                return config.iconModified;
            if ((status & GitStatus.Ignored) != 0)
                return config.iconIgnored;
            if ((status & GitStatus.Renamed) != 0)
                return config.iconRenamed;
            
            return null;
        }

        /// <summary>
        /// Determines if the status should be shown based on the configuration mask.
        /// </summary>
        /// <param name="status">The Git status to check.</param>
        /// <returns>True if the status matches the configured filter; otherwise false.</returns>
        private static bool ShouldShowStatusIcon(GitStatus status)
        {
            if (status == GitStatus.None) return false;
            GitStatus mask = config.iconStatus;
            return (status & mask) != 0;
        }

        /// <summary>
        /// Draws a texture icon with the specified opacity.
        /// Preserves the original GUI color after drawing.
        /// </summary>
        /// <param name="rect">The rectangle to draw the icon in.</param>
        /// <param name="icon">The texture to draw.</param>
        /// <param name="opacity">The opacity value (0-1).</param>
        private static void DrawIcon(Rect rect, Texture2D icon, float opacity)
        {
            Color prevColor = GUI.color;
            GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, opacity);
            GUI.DrawTexture(rect, icon);
            GUI.color = prevColor;
        }

        /// <summary>
        /// Calculates the icon rectangle based on the selection rect and configured position.
        /// </summary>
        /// <param name="selectionRect">The rectangle of the Project window item.</param>
        /// <param name="size">The size of the icon.</param>
        /// <param name="position">The desired position of the icon.</param>
        /// <returns>A rectangle representing the icon's position and size.</returns>
        private static Rect GetIconRect(Rect selectionRect, float size, IconPosition position)
        {
            const float padding = 2f;
            float x = selectionRect.x;
            float y = selectionRect.y;

            switch (position)
            {
                case IconPosition.TopLeft:
                    x += padding;
                    y += padding;
                    break;
                case IconPosition.TopCenter:
                    x += (selectionRect.width - size) / 2;
                    y += padding;
                    break;
                case IconPosition.TopRight:
                    x += selectionRect.width - size - padding;
                    y += padding;
                    break;
                case IconPosition.MiddleLeft:
                    x += padding;
                    y += (selectionRect.height - size) / 2;
                    break;
                case IconPosition.MiddleCenter:
                    x += (selectionRect.width - size) / 2;
                    y += (selectionRect.height - size) / 2;
                    break;
                case IconPosition.MiddleRight:
                    x += selectionRect.width - size - padding;
                    y += (selectionRect.height - size) / 2;
                    break;
                case IconPosition.BottomLeft:
                    x += padding;
                    y += selectionRect.height - size - padding;
                    break;
                case IconPosition.BottomCenter:
                    x += (selectionRect.width - size) / 2;
                    y += selectionRect.height - size - padding;
                    break;
                case IconPosition.BottomRight:
                    x += selectionRect.width - size - padding;
                    y += selectionRect.height - size - padding;
                    break;
            }
            
            return new Rect(x, y, size, size);
        }

        /// <summary>
        /// Gets the root directory of the Unity project (parent of Assets folder).
        /// </summary>
        /// <returns>The full path to the project root.</returns>
        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        /// <summary>
        /// Sets the overlay configuration. Used by the configuration window.
        /// </summary>
        /// <param name="newConfig">The new configuration to use.</param>
        public static void SetConfig(GitStatusOverlayConfig newConfig)
        {
            if (newConfig == null)
            {
                UnityEngine.Debug.LogWarning("[GitStatusOverlay] Attempted to set null config.");
                return;
            }
            config = newConfig;
        }

        /// <summary>
        /// Returns a read-only view of all Git statuses for external tools and windows.
        /// </summary>
        /// <returns>Dictionary mapping file paths to their Git status.</returns>
        public static IReadOnlyDictionary<string, GitStatus> GetAllStatuses() => gitStatuses;
    }
}
