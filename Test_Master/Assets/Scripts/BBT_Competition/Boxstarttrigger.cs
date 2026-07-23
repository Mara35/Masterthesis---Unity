using UnityEngine;

/// <summary>Starts the competition (game manager + timer) when the hand first enters the box trigger.</summary>
public class BoxStartTrigger : MonoBehaviour
{
    public CompetitionGameManager gameManager;
    public CompetitionTimer competitionTimer;

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        if (competitionTimer == null)
            competitionTimer = FindObjectOfType<CompetitionTimer>();
        if (gameManager == null)
            gameManager = FindObjectOfType<CompetitionGameManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the hand (GloveGrabber) starts the game, not stray cubes/colliders.
        if (other.GetComponent<GloveGrabber>() == null) return;

        Debug.Log("[BoxStartTrigger] Hand touched the box, game starts!");

        if (gameManager != null) gameManager.StartGame();
        if (competitionTimer != null) competitionTimer.StartTimer();

        gameObject.SetActive(false); // fire once, then disable the trigger
    }
}