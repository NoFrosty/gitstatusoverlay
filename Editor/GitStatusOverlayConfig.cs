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
        
        [Tooltip("Icon displayed for renamed/moved files.")]
        public Texture2D iconRenamed;

        [Header("Display Settings")]
        [Tooltip("Size of overlay icons in pixels.")]
        [Range(8, 32)]
        public int iconSize = 16;
        
        [Tooltip("Opacity of overlay icons (0 = transparent, 1 = opaque).")]
        [Range(0f, 1f)]
        public float iconOpacity = 1.0f;
        
        [Tooltip("Position of the overlay icon on each item.")]
        public IconPosition iconPosition = IconPosition.TopRight;

        [Header("Filter Settings")]
        [Tooltip("Which Git statuses should display icons. Use flags to combine multiple statuses.")]
        public GitStatus iconStatus = GitStatus.Modified | GitStatus.Staged | GitStatus.Untracked | 
                                      GitStatus.Renamed | GitStatus.Deleted | GitStatus.Conflicted | 
                                      GitStatus.Error | GitStatus.Copied | GitStatus.Ignored;

        [Header("Folder Options")]
        [Tooltip("Show status icons on folders based on their contents.")]
        public bool showIconsForFolders = true;

        /// <summary>
        /// Resets all configuration values to their defaults.
        /// </summary>
        public void Reset()
        {
            iconSize = 16;
            iconOpacity = 1.0f;
            iconStatus = GitStatus.Modified | GitStatus.Staged | GitStatus.Untracked | 
                        GitStatus.Renamed | GitStatus.Deleted | GitStatus.Conflicted | 
                        GitStatus.Error | GitStatus.Copied | GitStatus.Ignored;
            iconPosition = IconPosition.TopRight;
            showIconsForFolders = true;
            
            // Note: Icons are not reset as they may be assigned from package resources
        }
    }
}
