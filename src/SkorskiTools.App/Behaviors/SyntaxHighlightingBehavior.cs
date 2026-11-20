using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace SkorskiTools.App.Behaviors;

/// <summary>Applies AvalonEdit syntax highlighting from a script language name.</summary>
public static class SyntaxHighlightingBehavior
{
    public static readonly DependencyProperty LanguageProperty =
        DependencyProperty.RegisterAttached(
            "Language",
            typeof(string),
            typeof(SyntaxHighlightingBehavior),
            new PropertyMetadata(null, OnLanguageChanged));

    public static void SetLanguage(DependencyObject element, string value) =>
        element.SetValue(LanguageProperty, value);

    public static string GetLanguage(DependencyObject element) =>
        (string)element.GetValue(LanguageProperty);

    private static void OnLanguageChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextEditor editor)
            return;

        var language = args.NewValue as string;
        editor.SyntaxHighlighting = string.IsNullOrWhiteSpace(language)
            ? null
            : HighlightingManager.Instance.GetDefinition(Map(language));
    }

    /// <summary>AvalonEdit's Text is not a dependency property, so it is set here.</summary>
    public static readonly DependencyProperty CodeProperty =
        DependencyProperty.RegisterAttached(
            "Code",
            typeof(string),
            typeof(SyntaxHighlightingBehavior),
            new PropertyMetadata(string.Empty, OnCodeChanged));

    public static void SetCode(DependencyObject element, string value) =>
        element.SetValue(CodeProperty, value);

    public static string GetCode(DependencyObject element) =>
        (string)element.GetValue(CodeProperty);

    private static void OnCodeChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is TextEditor editor)
            editor.Text = args.NewValue as string ?? string.Empty;
    }

    private static string Map(string language) => language.ToLowerInvariant() switch
    {
        "powershell" or "ps1" => "PowerShell",
        "cmd" or "batch" or "bat" => "Batch",
        "json" => "JSON",
        "xml" => "XML",
        _ => language
    };
}
