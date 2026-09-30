using System.Collections;
using UnityEngine;

namespace TacticalShooter.Systems.Interaction
{
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("Door Configuration")]
        [SerializeField] private Transform doorLeaf;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float duration = 0.8f;

        [Header("Interaction Prompts")]
        [SerializeField] private string openPrompt = "Press E to open door";
        [SerializeField] private string closePrompt = "Press E to close door";

        private bool isOpen = false;
        private bool isAnimating = false;
        private Quaternion closedRotation;
        private Quaternion openRotation;

        public string PromptText => isOpen ? closePrompt : openPrompt;

        private void Awake()
        {
            if (doorLeaf == null)
            {
                doorLeaf = transform;
            }

            closedRotation = doorLeaf.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }

        public void Interact(GameObject interactor)
        {
            if (isAnimating) return;

            isOpen = !isOpen;
            StartCoroutine(AnimateDoor(isOpen ? openRotation : closedRotation));
        }

        private IEnumerator AnimateDoor(Quaternion targetRotation)
        {
            isAnimating = true;
            Quaternion startRotation = doorLeaf.localRotation;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                // Smooth step for natural easing
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                doorLeaf.localRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
                yield return null;
            }

            doorLeaf.localRotation = targetRotation;
            isAnimating = false;
        }
    }
}
