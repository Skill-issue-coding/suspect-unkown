// Static publish/subscribe for sounds, so no singleton GameObject has to exist in every scene
// (decision 10). Sources call Publish; listeners subscribe in OnEnable and unsubscribe in OnDisable.
using System;

public static class NoiseBus
{
    public static event Action<NoiseEvent> Published;
    public static void Publish(NoiseEvent noise) => Published?.Invoke(noise);
}
