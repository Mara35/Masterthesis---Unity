using UnityEngine;

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
        if (other.GetComponent<PlayerOrbController>() == null) return;

        Debug.Log("[BoxStartTrigger] PlayerOrb hat Box berührt – Spiel startet!");

        if (gameManager != null) gameManager.StartGame();
        if (competitionTimer != null) competitionTimer.StartTimer();

        enabled = false; // nur einmal auslösen
    }
}