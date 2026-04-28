using UnityEngine;

public class DialogueTest : MonoBehaviour
{
    [SerializeField] DialogueSystem dialogueSystem;
    [SerializeField] DialogueSequence testSequence;

    private void Start()
    {
        dialogueSystem.OnSequenceComplete += () =>
            Debug.Log("Sequence complete!");

        dialogueSystem.Play(testSequence);
    }
}