// What every minigame's rules expose to the shared parts around them: how far along it is and whether it
// is done. MinigameScreen reads Progress for the camera push-in and IsComplete for the punch;
// PlayerMinigame reads IsComplete to end the session. Each model keeps its own vocabulary beside this
public interface IMinigameModel
{
    float Progress { get; }   // 0 = just started, 1 = done
    bool IsComplete { get; }
}
