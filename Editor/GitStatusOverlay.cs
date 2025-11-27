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
        /// Priority: 1) User config in Assets, 2) Package default config, 3) Create new in Assets
        /// </summary>
        private static void LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:GitStatusOverlayConfig", new string[] { "Packages" });
            
            UnityEngine.Debug.Log($"[GitStatusOverlay] Found {guids.Length} GitStatusOverlayConfig assets");
            
            // First, try to find user config in Assets folder
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Debug.Log($"[GitStatusOverlay] Checking path: {path}");
                if (path.StartsWith("Assets/"))
                {
                    config = AssetDatabase.LoadAssetAtPath<GitStatusOverlayConfig>(path);
                    if (config != null)
                    {
                        UnityEngine.Debug.Log($"[GitStatusOverlay] Loaded user config from: {path}");
                        return;
                    }
                }
            }
            
            // Second, try to find package default config
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/com.caesiumgames.gitstatusoverlay/"))
                {
                    config = AssetDatabase.LoadAssetAtPath<GitStatusOverlayConfig>(path);
                    if (config != null)
                    {
                        UnityEngine.Debug.Log($"[GitStatusOverlay] Loaded package config from: {path}");
                        return;
                    }
                }
            }
            
            // Third, if no config found anywhere, create a new one in Assets
            if (config == null)
            {
                UnityEngine.Debug.LogWarning("[GitStatusOverlay] No config found, creating new one in Assets/Editor/GitStatusOverlay/");
                GitStatusOverlayConfig asset = ScriptableObject.CreateInstance<GitStatusOverlayConfig>();
                string directory = "Assets/Editor/GitStatusOverlay";
                
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
        /// The next time (in editor time) to fetch from remote.
        /// </summary>
        private static double nextFetchTime = 0;

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
            
            if (config != null && config.enableAutoFetch && EditorApplication.timeSinceStartup > nextFetchTime)
            {
                FetchFromRemote();
                nextFetchTime = EditorApplication.timeSinceStartup + config.autoFetchInterval;
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
                    
                    if (config != null && (config.showPushAvailable || config.detectPotentialConflicts))
                    {
                        CheckRemoteStatus();
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] Error refreshing Git status: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Fetches updates from the remote repository without merging.
        /// </summary>
        private static void FetchFromRemote()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", "fetch")
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
                        UnityEngine.Debug.LogWarning("[GitStatusOverlay] Failed to start Git fetch process.");
                        return;
                    }

                    p.WaitForExit();

                    if (p.ExitCode == 0)
                    {
                        RefreshGitStatus();
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] Error fetching from remote: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks for files that have commits not pushed to origin and files modified in origin.
        /// </summary>
        private static void CheckRemoteStatus()
        {
            if (!config.showPushAvailable && !config.detectPotentialConflicts) return;
            
            try
            {
                string currentBranch = GetCurrentBranch();
                if (string.IsNullOrEmpty(currentBranch)) return;
                
                // Check if remote exists
                if (!HasRemote())
                {
                    return;
                }
                
                string remoteBranch = $"origin/{currentBranch}";
                
                // Check if the remote branch exists
                if (!RemoteBranchExists(remoteBranch))
                {
                    return;
                }
                
                if (config.showPushAvailable)
                {
                    CheckUnpushedCommits(currentBranch, remoteBranch);
                }
                
                if (config.detectPotentialConflicts)
                {
                    CheckOriginChanges(remoteBranch);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] Error checking remote status: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the current Git branch name.
        /// </summary>
        private static string GetCurrentBranch()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return null;
                    
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    
                    return p.ExitCode == 0 ? output : null;
                }
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Checks if the repository has any remote configured.
        /// </summary>
        private static bool HasRemote()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", "remote")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;
                    
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    
                    return p.ExitCode == 0 && !string.IsNullOrEmpty(output);
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a specific remote branch exists.
        /// </summary>
        private static bool RemoteBranchExists(string remoteBranch)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", $"rev-parse --verify {remoteBranch}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;
                    
                    p.WaitForExit();
                    
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks for commits that exist locally but not on the remote.
        /// </summary>
        private static void CheckUnpushedCommits(string currentBranch, string remoteBranch)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", $"log {remoteBranch}..{currentBranch} --name-only --pretty=format:")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return;
                    
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    
                    if (p.ExitCode != 0) return;
                    
                    string[] files = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string file in files)
                    {
                        string path = file.Trim().Replace("\\", "/");
                        if (IsValidAssetPath(path))
                        {
                            AddOrUpdateStatus(path, GitStatus.PushAvailable);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] Error checking unpushed commits: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks for files modified in origin that differ from local.
        /// Only marks files that have changes in origin that we don't have locally.
        /// </summary>
        private static void CheckOriginChanges(string remoteBranch)
        {
            try
            {
                // Check for files that are different between origin and local
                // Use remote..HEAD to find files that exist in remote but not in our local branch
                ProcessStartInfo psi = new ProcessStartInfo("git", $"diff --name-only {remoteBranch}...HEAD")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return;
                    
                    string output = p.StandardOutput.ReadToEnd();
                    string error = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    
                    if (p.ExitCode != 0)
                    {
                        // If there's an error, don't mark any files
                        return;
                    }
                    
                    // Also check if there are commits in origin that we don't have
                    if (!HasIncomingCommits(remoteBranch))
                    {
                        // No incoming commits from origin, so no need to mark files
                        return;
                    }
                    
                    string[] files = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string file in files)
                    {
                        string path = file.Trim().Replace("\\", "/");
                        if (IsValidAssetPath(path))
                        {
                            string key = path.ToLower();
                            AddOrUpdateStatus(path, GitStatus.OriginAvailable);
                            
                            if (gitStatuses.TryGetValue(key, out GitStatus status))
                            {
                                if ((status & GitStatus.Modified) != 0)
                                {
                                    AddOrUpdateStatus(path, GitStatus.Warning);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GitStatusOverlay] Error checking origin changes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if there are incoming commits from the remote branch.
        /// </summary>
        private static bool HasIncomingCommits(string remoteBranch)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", $"log HEAD..{remoteBranch} --oneline")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetProjectRoot()
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;
                    
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    
                    // If there's output, there are incoming commits
                    return p.ExitCode == 0 && !string.IsNullOrEmpty(output);
                }
            }
            catch
            {
                return false;
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
                // Renamed/copied entry - format: "2 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <X><score> <path><sep><origPath>"
                else if (entry.StartsWith("2 "))
                {
                    ParseRenamedEntry(entry, ref i, output);
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
        /// Parses a renamed/copied entry from Git status porcelain v2.
        /// Format: "2 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <X><score> <path><sep><origPath>"
        /// The path and origPath are in the next null-terminated field.
        /// </summary>
        private static void ParseRenamedEntry(string entry, ref int i, string output)
        {
            // Extract XY status codes
            string[] parts = entry.Split(' ');
            if (parts.Length < 9) return;
            
            string xy = parts[1];
            char x = xy.Length > 0 ? xy[0] : ' ';
            
            // Read the next null-terminated field which contains "<path><tab><origPath>"
            int next = output.IndexOf('\0', i);
            if (next == -1) return;
            
            string pathField = output.Substring(i, next - i);
            i = next + 1;
            
            // Split by tab to get newPath and oldPath
            string[] paths = pathField.Split('\t');
            if (paths.Length < 2) return;
            
            string newPath = paths[0].Trim().Replace("\\", "/");
            string oldPath = paths[1].Trim().Replace("\\", "/");
            
            if (!IsValidAssetPath(newPath)) return;
            
            string newKey = newPath.ToLower();
            
            // Check if the file was moved to a different folder or just renamed
            string newFolder = Path.GetDirectoryName(newPath).Replace("\\", "/");
            string oldFolder = Path.GetDirectoryName(oldPath).Replace("\\", "/");
            
            GitStatus status = GitStatus.None;
            
            if (newFolder.Equals(oldFolder, StringComparison.OrdinalIgnoreCase))
            {
                // Same folder = Renamed
                status = GitStatus.Renamed;
                if (x == 'A' || x == 'R') status |= GitStatus.Staged;
            }
            else
            {
                // Different folder = Moved
                status = GitStatus.Moved;
                if (x == 'A' || x == 'R') status |= GitStatus.Staged;
            }
            
            AddOrUpdateStatus(newPath, status);
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
        /// Detects Unity file renames and moves by comparing GUIDs from .meta files.
        /// When a file is deleted and a new file appears with the same GUID:
        /// - If in the same folder (only filename changed) = Renamed
        /// - If in a different folder = Moved
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
                        // Found a rename/move! Determine which one
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

                        // Check if the file was moved to a different folder or just renamed
                        string untrackedFolder = Path.GetDirectoryName(untrackedPath).Replace("\\", "/");
                        string deletedFolder = Path.GetDirectoryName(deletedPath).Replace("\\", "/");
                        
                        if (untrackedFolder.Equals(deletedFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            // Same folder = Renamed
                            gitStatuses[untrackedKey] = GitStatus.Renamed;
                        }
                        else
                        {
                            // Different folder = Moved
                            gitStatuses[untrackedKey] = GitStatus.Moved;
                        }
                        
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

            // Handle folder overlay
            if (Directory.Exists(path))
            {
                DrawFolderOverlay(path, selectionRect);
                return;
            }

            // Handle file overlay
            DrawFileOverlay(path, selectionRect);
        }

        /// <summary>
        /// Draws overlay icon for folders based on the status of their contents.
        /// </summary>
        private static void DrawFolderOverlay(string folderPath, Rect selectionRect)
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

            float size = GetIconSizeForRect(selectionRect);
            Rect iconRect = GetIconRect(selectionRect, size, config.iconPosition);

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
        private static void DrawFileOverlay(string filePath, Rect selectionRect)
        {
            if (!gitStatuses.TryGetValue(filePath, out GitStatus status)) return;
            if (!ShouldShowStatusIcon(status)) return;

            var icons = GetIconsForStatus(status);
            if (icons.Count == 0) return;
            
            float size = GetIconSizeForRect(selectionRect);
            const float iconSpacing = 2f;
            
            for (int i = 0; i < icons.Count; i++)
            {
                Rect iconRect = GetIconRect(selectionRect, size, config.iconPosition, i, iconSpacing);
                DrawIcon(iconRect, icons[i], config.iconOpacity);
            }
        }

        /// <summary>
        /// Gets all appropriate icons for a given Git status, in priority order.
        /// Returns multiple icons when a file has multiple statuses.
        /// </summary>
        private static System.Collections.Generic.List<Texture2D> GetIconsForStatus(GitStatus status)
        {
            var icons = new System.Collections.Generic.List<Texture2D>();
            
            // Priority order: Warning > Untracked > Modified > OriginAvailable > PushAvailable > Ignored > Moved > Renamed
            if ((status & GitStatus.Warning) != 0 && config.iconWarning != null)
                icons.Add(config.iconWarning);
            if ((status & GitStatus.Untracked) != 0 && config.iconAdded != null)
                icons.Add(config.iconAdded);
            if ((status & GitStatus.Modified) != 0 && config.iconModified != null)
                icons.Add(config.iconModified);
            if ((status & GitStatus.OriginAvailable) != 0 && config.iconOriginAvailable != null)
                icons.Add(config.iconOriginAvailable);
            if ((status & GitStatus.PushAvailable) != 0 && config.iconPushAvailable != null)
                icons.Add(config.iconPushAvailable);
            if ((status & GitStatus.Ignored) != 0 && config.iconIgnored != null)
                icons.Add(config.iconIgnored);
            if ((status & GitStatus.Moved) != 0 && config.iconMoved != null)
                icons.Add(config.iconMoved);
            if ((status & GitStatus.Renamed) != 0 && config.iconRenamed != null)
                icons.Add(config.iconRenamed);
            
            return icons;
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
        /// Determines the appropriate icon size based on the selection rectangle dimensions.
        /// Detects whether Unity is in list view or icon view mode.
        /// </summary>
        /// <param name="selectionRect">The rectangle of the Project window item.</param>
        /// <returns>The icon size to use for the current view mode.</returns>
        private static float GetIconSizeForRect(Rect selectionRect)
        {
            // In list view, the rect height is typically smaller (around 16-20 pixels)
            // In icon view, the rect is larger and more square (64+ pixels)
            // We use a threshold of 32 pixels to distinguish between the two modes
            const float viewModeThreshold = 32f;
            
            bool isIconView = selectionRect.height > viewModeThreshold;
            return isIconView ? config.iconSizeIconView : config.iconSizeListView;
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
        /// <param name="iconIndex">The index of the icon when displaying multiple icons.</param>
        /// <param name="iconSpacing">Spacing between multiple icons.</param>
        /// <returns>A rectangle representing the icon's position and size.</returns>
        private static Rect GetIconRect(Rect selectionRect, float size, IconPosition position, int iconIndex = 0, float iconSpacing = 0f)
        {
            const float padding = 2f;
            float x = selectionRect.x;
            float y = selectionRect.y;
            
            float offset = iconIndex * (size + iconSpacing);

            switch (position)
            {
                case IconPosition.TopLeft:
                    x += padding + offset;
                    y += padding;
                    break;
                case IconPosition.TopCenter:
                    x += (selectionRect.width - size) / 2;
                    y += padding;
                    break;
                case IconPosition.TopRight:
                    x += selectionRect.width - size - padding - offset;
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
                    x += selectionRect.width - size - padding - offset;
                    y += (selectionRect.height - size) / 2;
                    break;
                case IconPosition.BottomLeft:
                    x += padding + offset;
                    y += selectionRect.height - size - padding;
                    break;
                case IconPosition.BottomCenter:
                    x += (selectionRect.width - size) / 2;
                    y += selectionRect.height - size - padding;
                    break;
                case IconPosition.BottomRight:
                    x += selectionRect.width - size - padding - offset;
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
