namespace Asahi.BotEmoteManagement;

[System.AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateHashedIdsAttribute : Attribute
{ }

[System.AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
#pragma warning disable CS9113 // Parameter is unread.
public sealed class HashedIdAttribute(string constantFieldName) : Attribute
#pragma warning restore CS9113 // Parameter is unread.
{
    public string? UnhashedValue { get; set; } = null;
}
