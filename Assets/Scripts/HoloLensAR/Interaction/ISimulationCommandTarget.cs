using HoloLensAR;

namespace HoloLensAR.Interaction
{
    /// <summary>
    /// Receives an application command without depending on its input device.
    /// </summary>
    public interface ISimulationCommandTarget
    {
        bool TryExecute(SimulationCommand command, CommandSource source);
    }
}