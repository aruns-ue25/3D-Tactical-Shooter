using UnityEngine;

namespace TacticalShooter.Systems.Interaction
{
    public interface IInteractable
    {
        string PromptText { get; }
        void Interact(GameObject interactor);
    }
}
