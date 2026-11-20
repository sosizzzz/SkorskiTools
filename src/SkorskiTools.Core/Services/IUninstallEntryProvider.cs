using SkorskiTools.Core.Models;

namespace SkorskiTools.Core.Services;

/// <summary>Provides the registry uninstall entries. Abstracted so detection is testable.</summary>
public interface IUninstallEntryProvider
{
    IReadOnlyList<UninstallEntry> GetEntries();
}
