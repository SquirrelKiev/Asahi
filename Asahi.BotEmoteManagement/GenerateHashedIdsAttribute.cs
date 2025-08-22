namespace Asahi.BotEmoteManagement;

[System.AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateHashedIdsAttribute : Attribute
{ }

[System.AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class HashedIdAttribute(string name) : Attribute
{ }
