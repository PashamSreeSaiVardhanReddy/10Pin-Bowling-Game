using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoringManager : MonoBehaviour
{
    public static ScoringManager Instance;

    public PinSetManager pinSet;

    // How many frames constitute a full game (used to detect "CLEAN GAME")
    public int framesPerGame = 10;

    int ballsThrown = 0;

    // Total fallen pins counted at the end of the last roll — used to compute newly fallen pins each throw
    int previousTotalFallen = 0;

    // Scores recorded per throw in the current frame
    List<int> throwScores = new List<int>();

    // Completed frame scores (each entry = frame total)
    List<int> frameScores = new List<int>();

    // Completed frame results (Strike/Spare/Open/etc.)
    enum FrameResult { Open, Strike, Spare, ThirdThrowClear }
    List<FrameResult> frameResults = new List<FrameResult>();

    // Per-frame raw throw values (each entry = int[] of throws for that frame; 10th frame may contain 3 entries)
    // Exposed so UI can query what was knocked down each ball in completed frames.
    public List<int[]> frameThrows = new List<int[]>();

    // Tracks the running coroutine so we don't start multiple delays
    private Coroutine spawnPinsCoroutine;

    // Track consecutive strikes and spares across frames
    int consecutiveStrikes = 0;
    int consecutiveSpares = 0;

    // NEW: whether the ball contacted any pin during the current throw
    bool ballHitThisThrow = false;

    // NEW: public fields exposed for UI to read via external UI systems
    public int totalScore = 0;
    public int currentFrame = 1;
    public int currentBall = 0;

    // Public reference to the Game Over canvas - assign in Inspector for reliability
    public Canvas gameOverCanvas;
    // Optional TMP field on the GameOverCanvas where the final total should be shown
    public TextMeshProUGUI gameOverTotalText;

    // Optional TMP to show roll messages like STRIKE / SPARE / GUTTER
    public TextMeshProUGUI rollMessageText;
    // coroutine to clear roll message
    Coroutine rollMessageCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Kept for compatibility with Pin.RegisterPinDown, but authoritative counts are taken
    // from the Pin instances at EndRoll().
    public void RegisterPinDown()
    {
        // no-op (kept for compatibility).
    }

    // NEW: call this from your ball script when it collides with a pin (or pin collider)
    public void RegisterBallHit()
    {
        ballHitThisThrow = true;
    }

    // Allow UI to query completed frame throws (0-based frameIndex)
    public int[] GetFrameThrows(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= frameThrows.Count) return new int[0];
        return frameThrows[frameIndex];
    }

    // Allow UI to query completed frame total
    public int GetFrameTotal(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= frameScores.Count) return 0;
        return frameScores[frameIndex];
    }

    public void EndRoll()
    {
        ballsThrown++;

        // Compute which frame we are currently playing (1-based)
        int activeFrame = Mathf.Clamp(frameScores.Count + 1, 1, framesPerGame);

        // Compute total fallen pins from the actual spawned pins
        int totalFallen = 0;
        if (pinSet != null && pinSet.spawnedPins != null)
        {
            foreach (GameObject pinObj in pinSet.spawnedPins)
            {
                if (pinObj == null) continue;
                Pin pinComp = pinObj.GetComponent<Pin>();
                if (pinComp != null && pinComp.isFallen)
                    totalFallen++;
            }
        }

        // Newly fallen in this throw = current total fallen - previously observed fallen
        int fallenThisThrow = totalFallen - previousTotalFallen;
        if (fallenThisThrow < 0) fallenThisThrow = 0; // defensive
        previousTotalFallen = totalFallen;

        throwScores.Add(fallenThisThrow);

        // Gutter detection: if no pins fell this throw AND the ball did not contact any pin -> gutter
        bool isGutter = (fallenThisThrow == 0 && !ballHitThisThrow);

        // Compute some quick flags
        bool isStrike = (ballsThrown == 1 && totalFallen == 10);
        bool isSpare = (ballsThrown == 2 && totalFallen == 10);

        // Only log STRIKE / SPARE / GUTTER to console per user request
        if (isStrike)
        {
            Debug.Log("STRIKE");
            if (rollMessageText != null)
            {
                rollMessageText.text = "STRIKE";
                if (rollMessageCoroutine != null) StopCoroutine(rollMessageCoroutine);
                rollMessageCoroutine = StartCoroutine(ClearRollMessageAfterDelay(2f));
            }
        }
        else if (isSpare)
        {
            Debug.Log("SPARE");
            if (rollMessageText != null)
            {
                rollMessageText.text = "SPARE";
                if (rollMessageCoroutine != null) StopCoroutine(rollMessageCoroutine);
                rollMessageCoroutine = StartCoroutine(ClearRollMessageAfterDelay(2f));
            }
        }
        else if (isGutter)
        {
            Debug.Log("GUTTER");
            if (rollMessageText != null)
            {
                rollMessageText.text = "GUTTER";
                if (rollMessageCoroutine != null) StopCoroutine(rollMessageCoroutine);
                rollMessageCoroutine = StartCoroutine(ClearRollMessageAfterDelay(2f));
            }
        }

        // Update exposed state for external UI consumers
        totalScore = GetTotalScore();
        currentFrame = activeFrame;
        currentBall = ballsThrown;

        // Update UI if UIManager exists: per-ball display and running total update
        int frameIndex = Mathf.Clamp(activeFrame - 1, 0, framesPerGame - 1);
        int ballIndexForUI = Mathf.Clamp(ballsThrown, 1, 3); // allow 3 for 10th frame

        SafeSetBallScore(frameIndex, ballIndexForUI, fallenThisThrow);
        SafeSetTotalScore(totalScore);

        // If all pins are down at any point -> consider special-case handling for 10th frame
        if (totalFallen == 10)
        {
            FrameResult resultForFrame = FrameResult.Open;

            if (isStrike)
            {
                // Strike: increment consecutive strike counter, reset spares
                consecutiveStrikes++;
                consecutiveSpares = 0;

                resultForFrame = FrameResult.Strike;
            }
            else if (isSpare)
            {
                // Spare only if pins cleared within the first two throws
                consecutiveSpares++;
                consecutiveStrikes = 0;

                resultForFrame = FrameResult.Spare;
            }
            else
            {
                // All pins cleared on the third throw — not a spare by your rule
                consecutiveStrikes = 0;
                consecutiveSpares = 0;

                resultForFrame = FrameResult.ThirdThrowClear;
            }

            // If this is the tenth frame, allow the extra throw(s) for strike/spare:
            if (activeFrame == framesPerGame)
            {
                // In the 10th frame: do not end the frame if the player is entitled to an extra throw.
                // Player gets a third throw in the 10th frame if a strike or spare occurred in the first two throws.
                // End the frame only after three throws have been completed.
                if (ballsThrown < 3)
                {
                    // Allow the frame to continue (do not call EndFrameAndScheduleReset yet)

                    // Reset ball-hit flag for next throw
                    ballHitThisThrow = false;

                    // IMPORTANT: for 10th-frame bonus throws we must reset the pinset (full rack)
                    // and previousTotalFallen so the next throw measures newly fallen pins correctly.
                    if (pinSet != null)
                    {
                        pinSet.SpawnPins();
                    }
                    previousTotalFallen = 0;

                    return;
                }
                // else ballsThrown == 3 -> fall through to end frame below
            }

            // For non-10th frames or when 10th frame has reached its max throws, finish the frame.
            int frameTotal = 0;
            foreach (int s in throwScores) frameTotal += s;

            // reset ball-hit flag for next throw/frame
            ballHitThisThrow = false;

            // End the frame
            EndFrameAndScheduleReset(frameTotal, resultForFrame);

            // Update exposed state (UIManager removed)
            totalScore = GetTotalScore();
            currentFrame = Mathf.Clamp(frameScores.Count + 1, 1, framesPerGame);
            currentBall = ballsThrown; // typically 0 after reset

            return;
        }

        // Only end frame after two throws for normal frames (not tenth). For the final frame, allow up to three throws only when strike/spare occurred.
        if (activeFrame != framesPerGame)
        {
            if (ballsThrown >= 2)
            {
                int frameTotal = 0;
                foreach (int s in throwScores) frameTotal += s;

                // Non-strike / non-spare frame => reset consecutive counters
                consecutiveStrikes = 0;
                consecutiveSpares = 0;

                // reset ball-hit flag for next frame
                ballHitThisThrow = false;

                EndFrameAndScheduleReset(frameTotal, FrameResult.Open);

                // Update exposed state (UIManager removed)
                totalScore = GetTotalScore();
                currentFrame = Mathf.Clamp(frameScores.Count + 1, 1, framesPerGame);
                currentBall = ballsThrown; // typically 0 after reset

                return;
            }
        }
        else
        {
            // activeFrame == framesPerGame (10th)
            // If no strike/spare happened in first two throws, end after two throws.
            if (ballsThrown >= 2 && !(throwScores.Count >= 2 && (throwScores[0] == 10 || throwScores[0] + throwScores[1] == 10)))
            {
                int frameTotal = 0;
                foreach (int s in throwScores) frameTotal += s;

                // No extra throw in 10th -> reset consecutive counters
                consecutiveStrikes = 0;
                consecutiveSpares = 0;

                // reset ball-hit flag for next frame
                ballHitThisThrow = false;

                EndFrameAndScheduleReset(frameTotal, FrameResult.Open);

                // Update exposed state (UIManager removed)
                totalScore = GetTotalScore();
                currentFrame = Mathf.Clamp(frameScores.Count + 1, 1, framesPerGame);
                currentBall = ballsThrown; // typically 0 after reset

                return;
            }
        }

        // Report cumulative score so far in this frame (for non-ending throws)
        int cumulative = 0;
        foreach (int s in throwScores) cumulative += s;
        Debug.Log("Score after throw " + ballsThrown + ": " + cumulative);

        // Reset ball-hit flag for the next throw (if you call EndRoll again for next throw)
        ballHitThisThrow = false;

        // Update exposed state for external UI consumers
        totalScore = GetTotalScore();
        currentFrame = Mathf.Clamp(frameScores.Count + 1, 1, framesPerGame);
        currentBall = ballsThrown;

        // Note: frame continues until either all pins are knocked down (strike/spare/third-throw all-down)
        // or the allowed number of throws for the frame are completed (2 throws normally, up to 3 in 10th with spare/strike).
    }

    void EndFrameAndScheduleReset(int frameTotal, FrameResult result)
    {
        // Make a copy of the per-throw scores for this frame so UI can read them later.
        int[] throwsCopy = throwScores.ToArray();
        frameThrows.Add(throwsCopy);

        frameScores.Add(frameTotal);
        frameResults.Add(result);
        Debug.Log("Frame ended. Frame score: " + frameTotal + "  Total score: " + GetTotalScore());

        // Update UI: frame total and total score (use safe fallbacks)
        int finishedFrameIndex = Mathf.Clamp(frameScores.Count - 1, 0, framesPerGame - 1);
        SafeSetFrameTotal(finishedFrameIndex, frameTotal);
        SafeSetTotalScore(GetTotalScore());

        // Check for "CLEAN GAME" if we've completed a full game
        if (frameResults.Count >= framesPerGame)
        {
            bool allStrikeOrSpare = true;
            for (int i = 0; i < framesPerGame && i < frameResults.Count; i++)
            {
                if (frameResults[i] != FrameResult.Strike && frameResults[i] != FrameResult.Spare)
                {
                    allStrikeOrSpare = false;
                    break;
                }
            }

            if (allStrikeOrSpare)
            {
                Debug.Log("CLEAN GAME");
                // do not send global UI message
            }
        }

        // Reset per-frame state immediately so further EndRoll calls won't double-count
        ballsThrown = 0;
        previousTotalFallen = 0;
        throwScores.Clear();

        // Spawn next pin set after delay (only one coroutine at a time)
        if (spawnPinsCoroutine == null)
            spawnPinsCoroutine = StartCoroutine(SpawnPinsDelayed(5f));

        // NEW: check for Game Over condition (after UI updates)
        if (frameScores.Count >= framesPerGame)
        {
            ShowGameOver();
        }
    }

    int GetTotalScore()
    {
        int sum = 0;
        foreach (int f in frameScores) sum += f;
        return sum;
    }

    IEnumerator SpawnPinsDelayed(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (pinSet != null)
        {
            pinSet.SpawnPins();
            Debug.Log("Pins reset for next frame");
        }
        else
        {
            Debug.LogWarning("PinSetManager is null when trying to respawn pins.");
        }

        spawnPinsCoroutine = null;
    }

    // Helper: try UIManager first, fall back to ScoreBoard if present
    void SafeSetBallScore(int frameIndex, int ballIndex, int fallenThisThrow)
    {
        if (UIManager.Instance != null)
        {
            // UIManager currently supports only ballIndex 1 or 2
            if (ballIndex == 1 || ballIndex == 2)
            {
                UIManager.Instance.UpdateBallScore(frameIndex, ballIndex, fallenThisThrow);
                return;
            }
        }

        var sb = FindObjectOfType<ScoreBoard>();
        if (sb != null)
        {
            // ScoreBoard expects string value. Compute display considering spares/strikes for second/third balls.
            string value = fallenThisThrow.ToString();

            if (ballIndex == 1)
            {
                value = (fallenThisThrow == 10) ? "X" : fallenThisThrow.ToString();
            }
            else if (ballIndex == 2)
            {
                // if first ball was 10 (strike) and this is 10th frame, second ball may also be X
                if (frameIndex == 9 && throwScores.Count >= 2 && throwScores[0] == 10)
                {
                    value = (fallenThisThrow == 10) ? "X" : fallenThisThrow.ToString();
                }
                else if (throwScores.Count >= 2 && throwScores[0] + throwScores[1] == 10)
                {
                    value = "/"; // spare
                }
                else
                {
                    value = (fallenThisThrow == 10) ? "X" : fallenThisThrow.ToString();
                }
            }
            else if (ballIndex == 3)
            {
                // third ball only for 10th frame
                if (fallenThisThrow == 10)
                    value = "X";
                else if (throwScores.Count >= 3 && throwScores[1] + throwScores[2] == 10 && throwScores[1] != 10)
                    value = "/";
                else
                    value = fallenThisThrow.ToString();
            }

            sb.SetBallScore(frameIndex, ballIndex, value);
        }
    }

    void SafeSetFrameTotal(int frameIndex, int frameTotal)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateFrameTotal(frameIndex, frameTotal);
            return;
        }

        var sb = FindObjectOfType<ScoreBoard>();
        if (sb != null)
        {
            sb.SetFrameTotal(frameIndex, frameTotal);
        }
    }

    void SafeSetTotalScore(int total)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTotalScore(total);
            return;
        }

        // Update ScoreBoard's totalScoreText if present
        var sb = FindObjectOfType<ScoreBoard>();
        if (sb != null)
        {
            if (sb.totalScoreText != null)
            {
                sb.totalScoreText.text = total.ToString();
                return;
            }

            if (sb.parentPanel != null)
            {
                var totalTMP = sb.parentPanel.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (totalTMP != null && totalTMP.name.IndexOf("Total", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    totalTMP.text = total.ToString();
                }
            }
        }
    }

    void ShowGameOver()
    {
        // Enable the Game Over canvas and pause the game
        if (gameOverCanvas != null)
        {
            // NEW: set the gameOverTotalText if assigned
            if (gameOverTotalText != null)
                gameOverTotalText.text = GetTotalScore().ToString();
            else
            {
                // Optional: find a suitable TMP under the canvas by name patterns and set it
                foreach (Transform child in gameOverCanvas.transform)
                {
                    var tmp = child.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                    if (tmp != null && tmp.gameObject.activeInHierarchy)
                    {
                        // Check if this TMP is likely the total score text (name contains "Total" or similar)
                        if (tmp.name.IndexOf("Total", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            tmp.text = GetTotalScore().ToString();
                            break; // found and set, exit loop
                        }
                    }
                }
            }

            gameOverCanvas.gameObject.SetActive(true);
            Time.timeScale = 0; // Optional: pause the game
        }
        else
        {
            // Fallback: find the GameOverCanvas GameObject by name and set it active
            GameObject go = GameObject.Find("GameOverCanvas");
            if (go != null)
            {
                go.SetActive(true);
                Time.timeScale = 0; // Optional: pause the game
            }
            else
            {
                Debug.LogWarning("GameOverCanvas not found");
            }
        }
    }

    IEnumerator ClearRollMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (rollMessageText != null)
            rollMessageText.text = string.Empty;

        rollMessageCoroutine = null;
    }
}
