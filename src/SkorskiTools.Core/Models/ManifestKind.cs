namespace SkorskiTools.Core.Models;

/// <summary>The catalog section a resource belongs to.</summary>
public enum ManifestKind
{
    Application,
    Browser,
    Portable,
    Auxiliary
}

/// <summary>CPU architectures supported by a bundled file.</summary>
public enum TargetArchitecture
{
    Any,
    X64,
    X86,
    Arm64
}

/// <summary>Supported installation detection strategies.</summary>
public enum DetectionType
{
    Registry,
    MsiProductCode,
    FileExists,
    Path
}

/// <summary>How a registry display name is compared.</summary>
public enum NameMatchMode
{
    Exact,
    StartsWith
}

/// <summary>Supported uninstall mechanisms.</summary>
public enum UninstallMode
{
    RegisteredUninstaller,
    MsiProductCode
}
