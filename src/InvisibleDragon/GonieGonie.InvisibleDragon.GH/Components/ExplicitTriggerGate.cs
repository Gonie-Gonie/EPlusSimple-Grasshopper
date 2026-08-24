namespace GonieGonie.InvisibleDragon.Grasshopper.Components;

/// <summary>
/// Converts persistent Grasshopper Boolean values into explicit one-shot actions.
/// The first observation establishes a baseline, so reopening a document with a
/// saved True value cannot start an operation.
/// </summary>
internal sealed class ExplicitTriggerGate
{
    private bool observed;
    private bool previousStart;
    private bool previousCancel;

    internal ExplicitTriggerObservation Observe(bool start, bool cancel)
    {
        if (!observed)
        {
            observed = true;
            previousStart = start;
            previousCancel = cancel;
            return default;
        }

        var observation = new ExplicitTriggerObservation(
            start && !previousStart,
            cancel && !previousCancel);
        previousStart = start;
        previousCancel = cancel;
        return observation;
    }

    internal void Reset()
    {
        observed = false;
        previousStart = false;
        previousCancel = false;
    }
}

internal readonly struct ExplicitTriggerObservation
{
    internal ExplicitTriggerObservation(bool start, bool cancel)
    {
        Start = start;
        Cancel = cancel;
    }

    internal bool Start { get; }

    internal bool Cancel { get; }
}
