using CDT_Agnostic.Physics;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.State.Physics;

internal unsafe partial struct Dynamics
{
    /// <summary>
    /// A zero-allocation, read-only lens into the unmanaged 2+1D thermodynamic state (<see cref="Dynamics"/>).
    /// Provides strictly <c>O(1)</c> by-reference routing to the precomputed <see cref="MoveWeights"/>
    /// Look-Up Tables (LUTs) for each volume-changing Pachner move.
    /// </summary>
    /// <remarks>
    /// The <c>(4,4)</c> spatial flip preserves all simplex and vertex counts (<c>ΔN = 0</c>),
    /// so <c>ΔS = 0</c> identically and no <see cref="MoveWeights"/> table is required for <c>Move44</c>.
    /// </remarks>
    /// <param name="dynamics">Pointer to the owning unmanaged <see cref="Dynamics"/> state.</param>
    [StructLayout(LayoutKind.Sequential)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly struct PhysicsAccessor(Dynamics* dynamics)
    {
        /// <summary>Gets a read-only reference to the precomputed thermodynamic weights for the <c>(2,3)</c> move.</summary>
        public ref readonly MoveWeights Move23
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref dynamics->Move23Weights;
        }

        /// <summary>Gets a read-only reference to the precomputed thermodynamic weights for the <c>(3,2)</c> move.</summary>
        public ref readonly MoveWeights Move32
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref dynamics->Move32Weights;
        }

        /// <summary>Gets a read-only reference to the precomputed thermodynamic weights for the <c>(2,6)</c> move.</summary>
        public ref readonly MoveWeights Move26
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref dynamics->Move26Weights;
        }

        /// <summary>Gets a read-only reference to the precomputed thermodynamic weights for the <c>(6,2)</c> move.</summary>
        public ref readonly MoveWeights Move62
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref dynamics->Move62Weights;
        }
    }
}