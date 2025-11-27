using UnityEngine;

namespace CaesiumGames.Editor.GitStatusOverlay
{
    /// <summary>
    /// Configuration asset for Git Status Overlay.
    /// Stores icon assignments, display settings, and behavior options.
    /// </summary>
    [CreateAssetMenu(fileName = "GitStatusOverlayConfig", menuName = "GitStatusOverlay/Config", order = 1)]
    public class GitStatusOverlayConfig : ScriptableObject
    {
        [Header("Icons")]
        [Tooltip("Icon displayed for untracked (newly added) files.")]
        public Texture2D iconAdded;
        
        [Tooltip("Icon displayed for modified files.")]
        public Texture2D iconModified;
        
        [Tooltip("Icon displayed for ignored files (.gitignore).")]
        public Texture2D iconIgnored;
        
        [Tooltip("Icon displayed for renamed files (same folder, different name).")]
        public Texture2D iconRenamed;
        
        [Tooltip("Icon displayed for moved files (different folder).")]
        public Texture2D iconMoved;
        
        [Tooltip("Icon displayed for files with changes available in origin.")]
        public Texture2D iconOriginAvailable;
        
        [Tooltip("Icon displayed for files with local commits ready to push.")]
        public Texture2D iconPushAvailable;
        
        [Tooltip("Icon displayed for files with potential conflicts (modified locally and in origin).")]
        public Texture2D iconWarning;

        [Header("Display Settings")]
        [Tooltip("Size of overlay icons in pixels for list view (when files are shown as lines).")]
        [Range(8, 32)]
        public int iconSizeListView = 16;
        
        [Tooltip("Size of overlay icons in pixels for icon view (when files are shown with large previews).")]
        [Range(8, 64)]
        public int iconSizeIconView = 24;
        
        [Tooltip("Opacity of overlay icons (0 = transparent, 1 = opaque).")]
        [Range(0f, 1f)]
        public float iconOpacity = 1.0f;
        
        [Tooltip("Position of the overlay icon on each item.")]
        public IconPosition iconPosition = IconPosition.TopRight;

        [Header("Filter Settings")]
        [Tooltip("Which Git statuses should display icons. Use flags to combine multiple statuses.")]
        public GitStatus iconStatus = GitStatus.Modified | GitStatus.Staged | GitStatus.Untracked | 
                                      GitStatus.Renamed | GitStatus.Moved | GitStatus.Deleted | GitStatus.Conflicted | 
                                      GitStatus.Error | GitStatus.Copied | GitStatus.Ignored | GitStatus.OriginAvailable | 
                                      GitStatus.PushAvailable | GitStatus.Warning;

        [Header("Folder Options")]
        [Tooltip("Show status icons on folders based on their contents.")]
        public bool showIconsForFolders = true;

        [Header("Remote Tracking Options")]
        [Tooltip("Enable automatic fetching from remote to check for origin changes.")]
        public bool enableAutoFetch = false;
        
        [Tooltip("Time interval (in seconds) between automatic fetches.")]
        [Range(60, 3600)]
        public int autoFetchInterval = 300;
        
        [Tooltip("Show icon for files with commits not yet pushed to origin.")]
        public bool showPushAvailable = false;
        
        [Tooltip("Show warning icon for files modified both locally and in origin.")]
        public bool detectPotentialConflicts = true;

        /// <summary>
        /// Resets all configuration values to their defaults.
        /// </summary>
        public void Reset()
        {
            iconSizeListView = 16;
            iconSizeIconView = 24;
            iconOpacity = 1.0f;
            iconStatus = GitStatus.Modified | GitStatus.Staged | GitStatus.Untracked | 
                        GitStatus.Renamed | GitStatus.Moved | GitStatus.Deleted | GitStatus.Conflicted | 
                        GitStatus.Error | GitStatus.Copied | GitStatus.Ignored | GitStatus.OriginAvailable | 
                        GitStatus.PushAvailable | GitStatus.Warning;
            iconPosition = IconPosition.TopRight;
            showIconsForFolders = true;
            enableAutoFetch = false;
            autoFetchInterval = 300;
            showPushAvailable = false;
            detectPotentialConflicts = true;
            
            // Note: Icons are not reset as they may be assigned from package resources
        }
    }
}
