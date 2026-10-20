// One minigame the player plays at a device: lockpicking, glass cutting. PlayerMinigame runs the session
// around it (player held still, cursor, cancel, success hold, busy prompts) and calls these; the
// implementer owns its model, its target in the world and the view that draws it
public interface IMinigame
{
    MinigameScreen Screen { get; }   // opened and closed by PlayerMinigame
    IMinigameModel Model { get; }    // read for completion; set before PlayerMinigame.Begin
    bool LocksCursor { get; }        // true: hidden and locked, for mouse-delta input. False: free pointer
    ConductFlags Conduct { get; }    // what NPCs see the player doing while it runs

    void Tick(float deltaTime);      // feed this frame's input to the model
    void Complete();                 // act on the world once the model is complete (open the door, remove the pane)
    void Stop();                     // the session ended, completed or not: release the target, stop the view
}
