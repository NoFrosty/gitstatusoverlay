namespace CaesiumGames.Editor.GitStatusOverlay
{
    [System.Flags]
    public enum GitStatus
    {
        None = 0,        // Aucun changement
        Untracked = 1 << 0,   // Fichier non suivi
        Modified = 1 << 1,   // Modifié
        Staged = 1 << 2,   // Ajouté à l'index
        Deleted = 1 << 3,   // Supprimé
        Renamed = 1 << 4,   // Renommé
        Copied = 1 << 5,   // Copié
        Conflicted = 1 << 6,   // Conflit de merge
        Ignored = 1 << 7,   // Ignoré par .gitignore
        Error = 1 << 8,   // Erreur ou état inconnu
    }

}
