namespace SkorskiTools.App.Services;

/// <summary>Plays a short sound when a full install batch finishes.</summary>
public interface ICompletionSoundService
{
    void Play(bool success);
}
