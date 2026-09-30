using UnityEngine;

namespace TacticalShooter.Systems.Interaction
{
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptMessage = "Press E to interact";

        public string PromptText => promptMessage;

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[TestInteractable] Successfully interacted with '{gameObject.name}' by '{interactor.name}'!");
        }
    }
}
