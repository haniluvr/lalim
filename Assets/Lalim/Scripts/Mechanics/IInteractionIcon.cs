namespace Lalim.Mechanics
{
    /// <summary>Which icon the HUD action button shows for an interactable.</summary>
    public enum InteractionIcon
    {
        Interact,   // icon_interact.png — pickups, starting minigames, using tools
        Chat        // icon_chat.png — talking to statues / NPCs
    }

    /// <summary>
    /// Optional companion to IInteractable. Interactables that don't implement it show the
    /// Interact icon.
    /// </summary>
    public interface IInteractionIcon
    {
        InteractionIcon Icon { get; }
    }
}

namespace Lalim.Mechanics
{
    /// <summary>
    /// Optional companion to IInteractable: where the floating 3D prompt hovers. Interactables
    /// without it get a spot worked out from their renderer/collider bounds.
    /// </summary>
    public interface IInteractionAnchor
    {
        UnityEngine.Vector3 PromptAnchor { get; }
    }
}
