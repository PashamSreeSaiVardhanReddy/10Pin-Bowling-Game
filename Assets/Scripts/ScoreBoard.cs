using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates and manages a 10-frame bowling scoreboard using Unity Canvas UI + TextMeshProUGUI.
/// - Recommended usage: create a Canvas in the scene, add an empty Panel (RectTransform) and assign it to `parentPanel`.
/// - Call `BuildScoreboard()` from the inspector context menu or at runtime to create the table layout.
/// - ScoringManager or other game code should call `SetBallScore(frameIndex, ballIndex, value)` and `SetFrameTotal(frameIndex, total)`.
/// </summary>
public class ScoreBoard : MonoBehaviour
{
    // Parent panel that will contain the horizontal frames. Assign in Inspector.
    public RectTransform parentPanel;

    // Exposed TMP references so other systems can update the UI. Each array length is 10 (frames 1..10).
    public TextMeshProUGUI[] frameNumberTexts = new TextMeshProUGUI[10];
    public TextMeshProUGUI[] ball1Texts = new TextMeshProUGUI[10];
    public TextMeshProUGUI[] ball2Texts = new TextMeshProUGUI[10];
    // Optional third ball (used only in 10th frame)
    public TextMeshProUGUI[] ball3Texts = new TextMeshProUGUI[10];
    public TextMeshProUGUI[] frameTotalTexts = new TextMeshProUGUI[10];
    // Per-frame small message (e.g., STRIKE, SPARE, GUTTER)
    public TextMeshProUGUI[] frameMessageTexts = new TextMeshProUGUI[10];

    // Overall total score field (optional - will be created if BuildScoreboard is used)
    public TextMeshProUGUI totalScoreText;
    // Global status message (center/top) to display messages like CLEAN GAME
    public TextMeshProUGUI globalStatusText;

    // Styling / sizing defaults (tweak from Inspector)
    public int ballFontSize = 18;
    public int totalFontSize = 24;
    public int frameNumberFontSize = 14;
    public int messageFontSize = 14;
    public float frameSpacing = 6f;
    public float framePadding = 6f;

    // Local cached value to avoid repeated string allocations
    int lastDisplayedTotal = int.MinValue;

    // Build scoreboard at runtime (or call via ContextMenu in editor)
    [ContextMenu("BuildScoreboard")]
    public void BuildScoreboard()
    {
        if (parentPanel == null)
        {
            Debug.LogError("ScoreBoard: parentPanel is not assigned. Create a Panel under a Canvas and assign it.");
            return;
        }

        // Clear any existing children
        for (int i = parentPanel.childCount - 1; i >= 0; i--)
            DestroyImmediate(parentPanel.GetChild(i).gameObject);

        // Ensure parent has HorizontalLayoutGroup
        var hLayout = parentPanel.GetComponent<HorizontalLayoutGroup>();
        if (hLayout == null) hLayout = parentPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = true;
        hLayout.spacing = frameSpacing;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.padding = new RectOffset((int)framePadding, (int)framePadding, (int)framePadding, (int)framePadding);

        // Optional ContentSizeFitter for parent
        var csfParent = parentPanel.GetComponent<ContentSizeFitter>();
        if (csfParent == null) csfParent = parentPanel.gameObject.AddComponent<ContentSizeFitter>();
        csfParent.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csfParent.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        for (int i = 0; i < 10; i++)
        {
            // FRAME PANEL
            var frameGO = new GameObject("Frame_" + (i + 1), typeof(RectTransform));
            frameGO.transform.SetParent(parentPanel, false);
            var frameRT = frameGO.GetComponent<RectTransform>();
            frameRT.sizeDelta = new Vector2(120, 100);

            // Vertical layout to create 4 rows
            var vLayout = frameGO.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 2;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlHeight = true;
            vLayout.childControlWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childForceExpandWidth = false;

            // Row 1: Frame number (centered)
            var frameNumberGO = new GameObject("FrameNumber", typeof(RectTransform));
            frameNumberGO.transform.SetParent(frameGO.transform, false);
            var frameNumberText = frameNumberGO.AddComponent<TextMeshProUGUI>();
            frameNumberText.text = (i + 1).ToString();
            frameNumberText.fontSize = frameNumberFontSize;
            frameNumberText.alignment = TextAlignmentOptions.Center;
            frameNumberText.enableAutoSizing = false;
            frameNumberText.raycastTarget = false;
            SafeAssign(frameNumberTexts, i, frameNumberText);

            // Row 2: Ball1 and Ball2 side-by-side (centered). For 10th frame, allow a third bonus ball.
            var ballsRowGO = new GameObject("BallsRow", typeof(RectTransform));
            ballsRowGO.transform.SetParent(frameGO.transform, false);
            var ballsHLayout = ballsRowGO.AddComponent<HorizontalLayoutGroup>();
            ballsHLayout.spacing = 8;
            ballsHLayout.childAlignment = TextAnchor.MiddleCenter;
            ballsHLayout.childControlWidth = false;
            ballsHLayout.childControlHeight = true;
            ballsHLayout.childForceExpandWidth = false;
            ballsHLayout.childForceExpandHeight = false;

            // Ball 1
            var ball1GO = new GameObject("Ball1", typeof(RectTransform));
            ball1GO.transform.SetParent(ballsRowGO.transform, false);
            var ball1Text = ball1GO.AddComponent<TextMeshProUGUI>();
            ball1Text.text = "";
            ball1Text.fontSize = ballFontSize;
            ball1Text.alignment = TextAlignmentOptions.Center;
            ball1Text.enableAutoSizing = false;
            ball1Text.raycastTarget = false;
            var le1 = ball1GO.AddComponent<LayoutElement>();
            le1.preferredWidth = 36;
            SafeAssign(ball1Texts, i, ball1Text);

            // Ball 2
            var ball2GO = new GameObject("Ball2", typeof(RectTransform));
            ball2GO.transform.SetParent(ballsRowGO.transform, false);
            var ball2Text = ball2GO.AddComponent<TextMeshProUGUI>();
            ball2Text.text = "";
            ball2Text.fontSize = ballFontSize;
            ball2Text.alignment = TextAlignmentOptions.Center;
            ball2Text.enableAutoSizing = false;
            ball2Text.raycastTarget = false;
            var le2 = ball2GO.AddComponent<LayoutElement>();
            le2.preferredWidth = 36;
            SafeAssign(ball2Texts, i, ball2Text);

            // For 10th frame, create a third bonus ball slot which shows "--" when not used.
            if (i == 9)
            {
                var ball3GO = new GameObject("Ball3", typeof(RectTransform));
                ball3GO.transform.SetParent(ballsRowGO.transform, false);
                var ball3Text = ball3GO.AddComponent<TextMeshProUGUI>();
                ball3Text.text = "--"; // default when no bonus throw occurs
                ball3Text.fontSize = ballFontSize;
                ball3Text.alignment = TextAlignmentOptions.Center;
                ball3Text.enableAutoSizing = false;
                ball3Text.raycastTarget = false;
                var le3 = ball3GO.AddComponent<LayoutElement>();
                le3.preferredWidth = 36;
                SafeAssign(ball3Texts, i, ball3Text);
            }

            // Row 3: Frame message (small)
            var msgGO = new GameObject("FrameMessage", typeof(RectTransform));
            msgGO.transform.SetParent(frameGO.transform, false);
            var msgText = msgGO.AddComponent<TextMeshProUGUI>();
            msgText.text = "";
            msgText.fontSize = messageFontSize;
            msgText.alignment = TextAlignmentOptions.Center;
            msgText.enableAutoSizing = false;
            msgText.raycastTarget = false;
            SafeAssign(frameMessageTexts, i, msgText);

            // Row 4: Frame total (larger, slightly bold, bottom-center)
            var totalGO = new GameObject("FrameTotal", typeof(RectTransform));
            totalGO.transform.SetParent(frameGO.transform, false);
            var totalText = totalGO.AddComponent<TextMeshProUGUI>();
            totalText.text = "";
            totalText.fontSize = totalFontSize;
            totalText.fontStyle = FontStyles.Bold;
            totalText.alignment = TextAlignmentOptions.Bottom;
            totalText.enableAutoSizing = false;
            totalText.raycastTarget = false;
            SafeAssign(frameTotalTexts, i, totalText);
        }

        // Create an overall TotalScore field as the last child so it is visible on the scoreboard
        var overallTotalGO = new GameObject("TotalScore", typeof(RectTransform));
        overallTotalGO.transform.SetParent(parentPanel, false);
        var overallRT = overallTotalGO.GetComponent<RectTransform>();
        overallRT.sizeDelta = new Vector2(160, 80);
        var overallText = overallTotalGO.AddComponent<TextMeshProUGUI>();
        overallText.text = "0";
        overallText.fontSize = totalFontSize;
        overallText.fontStyle = FontStyles.Bold;
        overallText.alignment = TextAlignmentOptions.Center;
        overallText.enableAutoSizing = false;
        overallText.raycastTarget = false;
        totalScoreText = overallText;

        // Create a global status text above or below the parent panel to show CLEAN GAME or other messages
        var globalGO = new GameObject("GlobalStatus", typeof(RectTransform));
        globalGO.transform.SetParent(parentPanel, false);
        var globalRT = globalGO.GetComponent<RectTransform>();
        globalRT.sizeDelta = new Vector2(200, 28);
        var globalText = globalGO.AddComponent<TextMeshProUGUI>();
        globalText.text = "";
        globalText.fontSize = messageFontSize;
        globalText.fontStyle = FontStyles.Bold;
        globalText.alignment = TextAlignmentOptions.Center;
        globalText.enableAutoSizing = false;
        globalText.raycastTarget = false;
        globalStatusText = globalText;

        Debug.Log("ScoreBoard: Built 10-frame UI under parentPanel.");
    }

    // Helper: safely assign an element into an array if index in range
    void SafeAssign(TextMeshProUGUI[] arr, int index, TextMeshProUGUI value)
    {
        if (arr == null) return;
        if (index < 0 || index >= arr.Length) return;
        arr[index] = value;
    }

    // Helper: safely set text in an array element
    void SafeSetArrayText(TextMeshProUGUI[] arr, int index, string value)
    {
        if (arr == null) return;
        if (index < 0 || index >= arr.Length) return;
        if (arr[index] == null) return;
        arr[index].text = value ?? "";
    }

    /// <summary>
    /// Set ball score text for a frame.
    /// frameIndex: 0-based (0..9)
    /// ballIndex: 1, 2 or 3 (third only valid for 10th frame)
    /// value: string to display (e.g. "X", "/", "0", "7")
    /// </summary>
    public void SetBallScore(int frameIndex, int ballIndex, string value)
    {
        if (frameIndex < 0 || frameIndex >= 10)
        {
            Debug.LogWarning("SetBallScore: frameIndex out of range: " + frameIndex);
            return;
        }

        if (ballIndex == 1)
        {
            SafeSetArrayText(ball1Texts, frameIndex, value);
        }
        else if (ballIndex == 2)
        {
            SafeSetArrayText(ball2Texts, frameIndex, value);
        }
        else if (ballIndex == 3)
        {
            // third ball only exists for 10th frame (index 9)
            SafeSetArrayText(ball3Texts, frameIndex, value);
        }
        else
        {
            Debug.LogWarning("SetBallScore: ballIndex must be 1, 2 or 3");
        }
    }

    /// <summary>
    /// Helper: find a TextMeshProUGUI in the scene matching name patterns (fall back to first TMP found)
    /// </summary>
    TextMeshProUGUI FindTMPByPatterns(string[] patterns)
    {
        var all = GameObject.FindObjectsOfType<TextMeshProUGUI>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            foreach (var p in patterns)
            {
                if (t.name.IndexOf(p, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }
        }
        if (all.Length > 0) return all[0];
        return null;
    }

    /// <summary>
    /// Set the frame message (e.g., STRIKE, SPARE, GUTTER) for a specific frame.
    /// If the frame message slot is not bound, try to locate a TMP under the parent panel.
    /// </summary>
    public void SetFrameMessage(int frameIndex, string message)
    {
        if (frameIndex < 0 || frameIndex >= 10) return;

        if (frameMessageTexts[frameIndex] == null && parentPanel != null)
        {
            // try to find the frame container by common names
            string frameName = "Frame_" + (frameIndex + 1);
            Transform frameT = parentPanel.Find(frameName);
            if (frameT == null)
            {
                // fallback: search children that contain the frame number
                foreach (Transform child in parentPanel)
                {
                    if (child.name.IndexOf((frameIndex + 1).ToString(), System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        frameT = child;
                        break;
                    }
                }
            }

            if (frameT != null)
            {
                // look for a TMP named FrameMessage under the frame
                var all = frameT.GetComponentsInChildren<TextMeshProUGUI>(true);
                TextMeshProUGUI pick = null;
                foreach (var t in all)
                {
                    if (t.name.IndexOf("FrameMessage", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("msg", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("message", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pick = t; break;
                    }
                }

                if (pick == null && all.Length > 0) pick = all[0];
                if (pick != null) frameMessageTexts[frameIndex] = pick;
            }
        }

        SafeSetArrayText(frameMessageTexts, frameIndex, message);
    }

    // Resize or create a TextMeshProUGUI[] ensuring at least newSize length, preserving existing values
    TextMeshProUGUI[] ResizeArray(TextMeshProUGUI[] src, int newSize)
    {
        var res = new TextMeshProUGUI[newSize];
        if (src != null)
        {
            for (int i = 0; i < src.Length && i < newSize; i++) res[i] = src[i];
        }
        return res;
    }

    /// <summary>
    /// Set the total score for a frame.
    /// frameIndex: 0-based (0..9)
    /// </summary>
    public void SetFrameTotal(int frameIndex, int total)
    {
        if (frameIndex < 0 || frameIndex >= 10)
        {
            Debug.LogWarning("SetFrameTotal: frameIndex out of range: " + frameIndex);
            return;
        }

        SafeSetArrayText(frameTotalTexts, frameIndex, total.ToString());
    }

    /// <summary>
    /// Set a global status message (e.g., CLEAN GAME)
    /// If no explicit globalStatusText assigned, attempt to find a suitable TMP in the scene.
    /// </summary>
    public void SetGlobalMessage(string message)
    {
        if (globalStatusText == null)
        {
            // try common name patterns
            string[] patterns = new string[] { "global", "status", "message", "msg", "console", "log", "output" };
            globalStatusText = FindTMPByPatterns(patterns);

            // If that found the totalScoreText by accident (name contains "Total"), prefer a different match.
            if (globalStatusText != null && globalStatusText.name.IndexOf("total", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // try to find another candidate excluding total
                var all = GameObject.FindObjectsOfType<TextMeshProUGUI>(true);
                TextMeshProUGUI alt = null;
                foreach (var t in all)
                {
                    if (t == null) continue;
                    var n = t.name.ToLower();
                    if (n.Contains("total")) continue;
                    if (n.Contains("global") || n.Contains("status") || n.Contains("message") || n.Contains("msg") || n.Contains("console") || n.Contains("log") || n.Contains("output"))
                    {
                        alt = t; break;
                    }
                }
                if (alt != null) globalStatusText = alt;
            }
        }

        if (globalStatusText != null)
            globalStatusText.text = message ?? "";
        else
            Debug.Log(message);
    }

    /// <summary>
    /// Convenience: reset all ball and total fields.
    /// </summary>
    public void ClearAllFrames()
    {
        for (int i = 0; i < 10; i++)
        {
            SafeSetArrayText(ball1Texts, i, "");
            SafeSetArrayText(ball2Texts, i, "");
            SafeSetArrayText(ball3Texts, i, (i == 9) ? "--" : "");
            SafeSetArrayText(frameTotalTexts, i, "");
            SafeSetArrayText(frameMessageTexts, i, "");
        }

        if (totalScoreText != null) totalScoreText.text = "0";
        if (globalStatusText != null) globalStatusText.text = "";
        lastDisplayedTotal = int.MinValue;
    }

    void Update()
    {
        // Try to auto-assign totalScoreText if not assigned
        if (totalScoreText == null && parentPanel != null)
        {
            var found = parentPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (found != null && found.name.IndexOf("Total", System.StringComparison.OrdinalIgnoreCase) >= 0)
                totalScoreText = found;
        }

        if (ScoringManager.Instance != null && totalScoreText != null)
        {
            int total = ScoringManager.Instance.totalScore;
            if (total != lastDisplayedTotal)
            {
                totalScoreText.text = total.ToString();
                lastDisplayedTotal = total;
            }
        }
    }
}