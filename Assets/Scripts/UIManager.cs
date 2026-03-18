using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Frames 1–10 (optional - can be auto-bound)")]
    public TMP_Text[] ball1Texts = new TMP_Text[10];
    public TMP_Text[] ball2Texts = new TMP_Text[10];
    public TMP_Text[] frameScoreTexts = new TMP_Text[10];

    [Header("Total Score (optional)")]
    public TMP_Text totalScoreText;

    [Header("Optional ScoreBoard")]
    [Tooltip("Optional ScoreBoard component (the in-scene scoreboard object - e.g. 'sscoreboard'). If empty the script will try to find one at Awake.")]
    public ScoreBoard scoreBoard;

    [Header("Optional: If you placed TMP elements under a root GameObject, assign it here")]
    public Transform scoreboardRoot;

    // Simple polling to refresh UI when scoring changes
    int lastTotal = -1;
    int lastFrame = -1;
    int lastBall = -1;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Try to auto-find a ScoreBoard if one wasn't assigned in inspector
        if (scoreBoard == null)
        {
            scoreBoard = FindObjectOfType<ScoreBoard>();
            if (scoreBoard == null)
            {
                var go = GameObject.Find("sscoreboard");
                if (go != null) scoreBoard = go.GetComponent<ScoreBoard>();
            }
        }

        // If scoreboardRoot set, try to auto-bind TMP fields from children
        if (scoreboardRoot != null)
        {
            AutoBindFromRoot(scoreboardRoot);
        }
    }

    // Attempt to populate ball/frame TMP arrays by searching children under a common root.
    void AutoBindFromRoot(Transform root)
    {
        for (int i = 0; i < 10; i++)
        {
            // Try common naming patterns for a frame container
            string[] frameNames = new string[]
            {
                $"Frame_{i+1}", $"Frame{i+1}", $"Frame {i+1}", $"frame_{i+1}", $"frame{i+1}"
            };

            Transform frameT = null;
            foreach (var n in frameNames)
            {
                frameT = root.Find(n);
                if (frameT != null) break;
            }

            // fallback: find a child whose name contains the frame number
            if (frameT == null)
            {
                foreach (Transform child in root)
                {
                    if (child.name.Contains((i + 1).ToString()))
                    {
                        frameT = child;
                        break;
                    }
                }
            }

            // If we found a frame container, try to find Ball1/Ball2/Total children by name patterns
            if (frameT != null)
            {
                ball1Texts[i] = FindTMPInChildren(frameT, new string[] { "Ball1", "Ball 1", "Ball_1", "ball1" });
                ball2Texts[i] = FindTMPInChildren(frameT, new string[] { "Ball2", "Ball 2", "Ball_2", "ball2" });
                frameScoreTexts[i] = FindTMPInChildren(frameT, new string[] { "Total", "FrameTotal", "Frame_Total", "Score", "frameTotal" });

                // As a final fallback, if the frame container has at least 3 TMP children, map by order:
                if ((ball1Texts[i] == null || ball2Texts[i] == null || frameScoreTexts[i] == null))
                {
                    var tmps = frameT.GetComponentsInChildren<TMP_Text>(true);
                    if (tmps.Length >= 1 && ball1Texts[i] == null)
                        ball1Texts[i] = tmps.Length > 0 ? tmps[0] : ball1Texts[i];
                    if (tmps.Length >= 2 && ball2Texts[i] == null)
                        ball2Texts[i] = tmps.Length > 1 ? tmps[1] : ball2Texts[i];
                    if (tmps.Length >= 3 && frameScoreTexts[i] == null)
                        frameScoreTexts[i] = tmps.Length > 2 ? tmps[2] : frameScoreTexts[i];
                }
            }
            else
            {
                // no frame container found; try to find TMP named like Ball1_FrameX anywhere under root
                ball1Texts[i] = FindTMPInChildren(root, new string[] { $"Ball1_{i+1}", $"Ball1 Frame{i+1}", $"Ball1Frame{i+1}" });
                ball2Texts[i] = FindTMPInChildren(root, new string[] { $"Ball2_{i+1}", $"Ball2 Frame{i+1}", $"Ball2Frame{i+1}" });
                frameScoreTexts[i] = FindTMPInChildren(root, new string[] { $"FrameTotal_{i+1}", $"Total_{i+1}", $"Frame{i+1}_Total" });
            }
        }

        // try to find a total score TMP under root by common names
        if (totalScoreText == null)
        {
            totalScoreText = FindTMPInChildren(root, new string[] { "TotalScore", "totalScore", "ScoreTotal", "Total" });
        }
    }

    TMP_Text FindTMPInChildren(Transform parent, string[] namePatterns)
    {
        foreach (var p in namePatterns)
        {
            var t = parent.Find(p);
            if (t != null)
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
        }

        // fallback: search all children for a name that contains any of the patterns
        var all = parent.GetComponentsInChildren<TMP_Text>(true);
        foreach (var tmp in all)
        {
            foreach (var p in namePatterns)
            {
                if (tmp.name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return tmp;
            }
        }
        return null;
    }

    // Called after each roll
    // frameIndex: 0-based (0..9), ballIndex: 1 or 2, pins: number of pins knocked down this roll (0..10)
    public void UpdateBallScore(int frameIndex, int ballIndex, int pins)
    {
        if (frameIndex < 0 || frameIndex >= 10) return;
        if (ballIndex != 1 && ballIndex != 2) return;

        // Determine text for this ball, handling strikes and spares.
        string value;
        if (ballIndex == 1)
        {
            // First ball: strike if 10
            value = (pins == 10) ? "X" : pins.ToString();
            if (ball1Texts[frameIndex] != null) ball1Texts[frameIndex].text = value;
        }
        else // ballIndex == 2
        {
            int firstPins = -1;
            if (ball1Texts[frameIndex] != null)
            {
                int.TryParse(ball1Texts[frameIndex].text, out firstPins);
            }

            if (firstPins >= 0 && firstPins + pins == 10)
            {
                value = "/"; // spare
            }
            else if (pins == 10)
            {
                value = "X";
            }
            else
            {
                value = pins.ToString();
            }

            if (ball2Texts[frameIndex] != null) ball2Texts[frameIndex].text = value;
        }

        // Update ScoreBoard (if present) and also update ScoreBoard UI if available
        if (scoreBoard != null)
        {
            scoreBoard.SetBallScore(frameIndex, ballIndex, value);
        }
    }

    // Called after frame ends
    public void UpdateFrameTotal(int frameIndex, int score)
    {
        if (frameIndex < 0 || frameIndex >= 10) return;
        if (frameScoreTexts[frameIndex] != null) frameScoreTexts[frameIndex].text = score.ToString();

        if (scoreBoard != null)
        {
            scoreBoard.SetFrameTotal(frameIndex, score);
        }
    }

    public void UpdateTotalScore(int total)
    {
        if (totalScoreText != null) totalScoreText.text = total.ToString();
        // if ScoreBoard holds a special total, update it as well (ScoreBoard currently doesn't expose total)
    }
}
