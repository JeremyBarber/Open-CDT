namespace CDT_Agnostic.Accessors;

/// <summary>
/// Defines an accessor capable of structurally modifying the topology of the unmanaged manifold.
/// Exposes an integrated inspecting lens for validation during mutation.
/// </summary>
/// <typeparam name="TInspector">The highly-optimized read-only accessor for the specific dimension.</typeparam>
public interface IMutatingAccessor<out TInspector> where TInspector : unmanaged, IInspectingAccessor
{
    /// <summary>
    /// Gets a zero-allocation, read-only lens into the current state of the manifold.
    /// </summary>
    TInspector Inspect { get; }
}