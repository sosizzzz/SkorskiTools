using CommunityToolkit.Mvvm.ComponentModel;

namespace SkorskiTools.App.ViewModels;

/// <summary>Base class for all view models, providing property change notification.</summary>
public abstract class ViewModelBase : ObservableObject
{
    public abstract string Title { get; }
}
