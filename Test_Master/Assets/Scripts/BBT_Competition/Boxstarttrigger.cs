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
        if (other.GetComponent<GloveGrabber>() == null) return;

        Debug.Log("[BoxStartTrigger] Hand hat Box berührt - Spiel startet!");

        if (gameManager != null) gameManager.StartGame();
        if (competitionTimer != null) competitionTimer.StartTimer();

        gameObject.SetActive(false);
    }
}