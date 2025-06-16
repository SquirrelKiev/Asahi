namespace Asahi.BotEmoteManagement;

#pragma warning disable CS9113 // "Parameter is unread." Parameter is used by source gen.
// goes on the manager to source gen
[AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateEmoteManagerAttribute(Type specificationType) : Attribute
{ }
#pragma warning restore CS9113 // Parameter is unread.
