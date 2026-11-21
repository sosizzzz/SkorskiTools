using System.Media;

namespace SkorskiTools.App.Services;

/// <summary>Uses the standard Windows notification sounds.</summary>
public sealed class CompletionSoundService : ICompletionSoundService
{
    public void Play(bool success)
    {
        if (success)
            SystemSounds.Asterisk.Play();
        else
            SystemSounds.Exclamation.Play();
    }
}
