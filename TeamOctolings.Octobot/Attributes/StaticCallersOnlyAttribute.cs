namespace TeamOctolings.Octobot.Attributes;

/// <summary>
/// Any property marked with <see cref="StaticCallersOnlyAttribute"/> should only be accessed by static methods.
/// Such properties may be used to provide dependencies where it is not possible to acquire them through normal means.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class StaticCallersOnlyAttribute : Attribute;
