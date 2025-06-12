using System;

namespace Asahi.BotEmoteManagement;

// goes on the manager to source gen
[AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateEmoteManagerAttribute(Type specificationType) : Attribute
{ }