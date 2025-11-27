namespace CaesiumGames.Editor.GitStatusOverlay
{
    /// <summary>
    /// Represents the various Git statuses that can be applied to files and folders.
    /// Multiple statuses can be combined using bitwise operations.
    /// </summary>
    [System.Flags]
    public enum GitStatus
    {
        /// <summary>No changes detected.</summary>
        None = 0,
        
        /// <summary>Untracked file (not yet added to Git).</summary>
        Untracked = 1 << 0,
        
        /// <summary>Modified file with uncommitted changes.</summary>
        Modified = 1 << 1,
        
        /// <summary>File added to the staging area (index).</summary>
        Staged = 1 << 2,
        
        /// <summary>File deleted from the working directory.</summary>
        Deleted = 1 << 3,
        
        /// <summary>File renamed (Git detected rename).</summary>
        Renamed = 1 << 4,
        
        /// <summary>File copied.</summary>
        Copied = 1 << 5,
        
        /// <summary>File has merge conflicts.</summary>
        Conflicted = 1 << 6,
        
        /// <summary>File ignored by .gitignore.</summary>
        Ignored = 1 << 7,
        
        /// <summary>Error or unknown state.</summary>
        Error = 1 << 8,
        
        /// <summary>File moved (Unity-specific detection via GUID matching).</summary>
        Moved = 1 << 9,
        
        /// <summary>File has changes in remote origin that can be fetched.</summary>
        OriginAvailable = 1 << 10,
        
        /// <summary>File has local commits that can be pushed to origin.</summary>
        PushAvailable = 1 << 11,
        
        /// <summary>Warning: File is modified locally and also modified in origin (potential conflict).</summary>
        Warning = 1 << 12,
    }
}
