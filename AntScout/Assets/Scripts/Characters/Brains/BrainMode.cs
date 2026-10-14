namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Specifies the driving intelligence active on an AntCharacter chassis.
    /// </summary>
    public enum BrainMode
    {
        [UnityEngine.Tooltip("Driven by human input devices (gamepad, keyboard, mouse) via AntPlayerBrain.")]
        Player = 0,

        [UnityEngine.Tooltip("Driven autonomously by sensory awareness and colony macro objectives via AntAiBrain.")]
        AI = 1
    }
}
