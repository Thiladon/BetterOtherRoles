using System.Globalization;

namespace BorInstaller;

/// <summary>
///     Texts of the installer, in French when Windows is in French, otherwise in English.
/// </summary>
internal static class Texts
{
    private static readonly bool French = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

    /// <summary>
    ///     Gets the culture used to format the texts.
    /// </summary>
    public static CultureInfo Culture => CultureInfo.CurrentUICulture;

    public static string Title => "Better Other Roles - MiraAPI";

    public static string GameFolder => French ? "Among Us d'origine (Steam, Epic…) :" : "Original Among Us (Steam, Epic…):";

    public static string TargetFolder => French ? "Dossier d'installation (copie du jeu avec le mod) :" : "Install folder (copy of the game with the mod):";

    public static string TargetDescription => French ? "Choisis le dossier où installer le jeu avec le mod." : "Choose the folder where the game with the mod goes.";

    public static string NoTarget => French ? "Choisis un dossier d'installation." : "Choose an install folder.";

    public static string CopyingGame => French ? "Copie du jeu…" : "Copying the game…";

    public static string Browse => French ? "Parcourir…" : "Browse…";

    public static string BrowseDescription => French ? "Choisis le dossier d'Among Us (celui qui contient Among Us.exe)." : "Choose the Among Us folder (the one with Among Us.exe).";

    public static string Version => French ? "Version :" : "Version:";

    public static string Betas => French ? "Inclure les bêtas" : "Include betas";

    public static string Install => French ? "Installer" : "Install";

    public static string Close => French ? "Fermer" : "Close";

    public static string LoadingReleases => French ? "Recherche des versions…" : "Looking for the versions…";

    public static string NoRelease => French ? "Aucune version trouvée." : "No version found.";

    public static string Ready => French ? "Prêt." : "Ready.";

    public static string NotGameFolder => French ? "Ce dossier ne contient pas Among Us.exe." : "This folder has no Among Us.exe.";

    public static string GameRunning => French ? "Ferme Among Us avant d'installer." : "Close Among Us before installing.";

    public static string WaitingForGame => French ? "Attente de la fermeture du jeu…" : "Waiting for the game to close…";

    public static string ReadingRelease => French ? "Lecture de la version…" : "Reading the version…";

    public static string DownloadingBepInEx => French ? "Téléchargement de BepInEx…" : "Downloading BepInEx…";

    public static string InstallingBepInEx => French ? "Installation de BepInEx…" : "Installing BepInEx…";

    public static string DownloadingPlugins => French ? "Téléchargement du mod…" : "Downloading the mod…";

    public static string InstallingPlugins => French ? "Installation du mod…" : "Installing the mod…";

    public static string Done => French ? "{0} installé." : "{0} installed.";

    public static string UnknownBits => French ? "Impossible de savoir si le jeu est en 32 ou 64 bits (Among Us.exe illisible)." : "Cannot tell whether the game is 32 or 64 bits (Among Us.exe cannot be read).";

    public static string Error =>French ? "Erreur : {0}" : "Error: {0}";
}
