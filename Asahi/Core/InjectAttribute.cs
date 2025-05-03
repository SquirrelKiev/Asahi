using JetBrains.Annotations;

namespace Asahi;

// Any service with this will be auto discovered and marked as a service.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
[MeansImplicitUse]
public sealed class InjectAttribute : Attribute
{
    public InjectAttribute(ServiceLifetime serviceLifetime)
    {
        ServiceLifetime = serviceLifetime;
    }

    public ServiceLifetime ServiceLifetime { get; }
}
