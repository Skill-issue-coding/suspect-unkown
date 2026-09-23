using System;

// Named IPerceiver rather than IObserver to avoid colliding with System.IObserver<T>.
public interface IPerceiver
{
    event Action<Observation> Perceived;

    bool TryGetLatest(out Observation observation);
}
