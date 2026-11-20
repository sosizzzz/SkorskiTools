namespace SkorskiTools.Core.Services;

/// <summary>Creates and removes desktop shortcuts for deployed portables.</summary>
public interface IShortcutService
{
    void Create(string shortcutPath, string targetPath, string workingDirectory, string description);

    void Delete(string shortcutPath);
}
