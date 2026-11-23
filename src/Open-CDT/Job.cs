namespace Open_CDT;

/// <summary>
/// Identifies the discrete execution and plotting stages registered in <see cref="EntryPoint"/>.
/// </summary>
internal enum Job
{
    CosmologicalConstantTuner,
    PhaseClassifier,
    Thermalizer,
    Sampling
}