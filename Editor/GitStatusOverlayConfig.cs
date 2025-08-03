using UnityEngine;

namespace CaesiumGames.Editor.GitStatusOverlay
{
    [CreateAssetMenu(fileName = "GitStatusOverlayConfig", menuName = "GitStatusOverlay/Config", order = 1)]
    public class GitStatusOverlayConfig : ScriptableObject
    {
        public Texture2D iconAdded;
        public Texture2D iconModified;
        public Texture2D iconIgnored;
        public Texture2D iconRenamed;
        public int iconSize = 16;
        public float iconOpacity = 1.0f;
        public GitStatus iconStatus = GitStatus.Modified | GitStatus.Staged | GitStatus.Untracked | GitStatus.Renamed | GitStatus.Deleted | GitStatus.Conflicted | GitStatus.Error | GitStatus.Copied | GitStatus.Ignored;
        public IconPosition iconPosition = IconPosition.TopRight;

        public bool showIconsForFolders = true;

        public void Reset()
        {
            iconSize = 16;
            iconOpacity = 1.0f;
            iconStatus = GitStatus.Modified | GitStatus.Staged | GitStatus.Untracked | GitStatus.Renamed | GitStatus.Deleted | GitStatus.Conflicted | GitStatus.Error | GitStatus.Copied | GitStatus.Ignored;
            iconPosition = IconPosition.TopRight;
            showIconsForFolders = true;
        }
    }
}
