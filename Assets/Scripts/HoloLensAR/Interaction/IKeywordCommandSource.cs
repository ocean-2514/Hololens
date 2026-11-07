using System;
using HoloLensAR;

namespace HoloLensAR.Interaction
{
    /// <summary>
    /// Provides normalized discrete commands without exposing a speech SDK to consumers.
    /// </summary>
    public interface IKeywordCommandSource
    {
        bool IsAvailable { get; }
        event Action<SimulationCommand> CommandRecognized;
    }
}