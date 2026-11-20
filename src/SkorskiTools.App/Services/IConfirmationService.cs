namespace SkorskiTools.App.Services;

/// <summary>Asks the user before destructive or irreversible operations.</summary>
public interface IConfirmationService
{
    bool Confirm(string title, string message);

    void ShowError(string title, string message);
}
