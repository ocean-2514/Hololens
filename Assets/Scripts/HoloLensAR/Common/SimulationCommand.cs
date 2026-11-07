namespace HoloLensAR
{
    /// <summary>
    /// Discrete commands that can be sent by buttons or an optional voice adapter.
    /// </summary>
    public enum SimulationCommand
    {
        ZoomIn,
        ZoomOut,
        RotateLeft,
        RotateRight,
        Reset,
        Undo,
        Confirm,
        Cancel
    }

    /// <summary>
    /// Origin of a discrete command. Direct hand manipulation is tracked separately.
    /// </summary>
    public enum CommandSource
    {
        Voice,
        UserInterface
    }
}