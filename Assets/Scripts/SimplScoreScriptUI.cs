using UnityEngine;
using TMPro;

public class SimpleScoreUI : MonoBehaviour
{
    public TMP_Text scoreText;

    public void UpdateScore(int score)
    {
        scoreText.text = "SCORE: " + score;
    }
}
