using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class startbutton : MonoBehaviour
{
    [Tooltip("Name of the gameplay scene to load. Make sure this scene is added to __File > Build Settings__.")]
    public string gameplaySceneName = "Gameplay";

    // Call this from your UI Button __OnClick__.
    public void OnStartButtonPressed()
    {
        Debug.Log("Start button pressed. Attempting to load scene: " + gameplaySceneName);

        if (string.IsNullOrEmpty(gameplaySceneName))
        {
            Debug.LogWarning("StartButton: gameplaySceneName is not set.");
            return;
        }

        if (!IsSceneInBuildSettings(gameplaySceneName))
        {
            Debug.LogError($"StartButton: Scene '{gameplaySceneName}' is not in Build Settings. Add it via __File > Build Settings__ or use the exact scene name.");
            return;
        }

        // Ensure game time is normal and hide cursor for gameplay
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartCoroutine(LoadSceneAsync(gameplaySceneName));
    }

    IEnumerator LoadSceneAsync(string sceneName)
    {
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError("StartButton: LoadSceneAsync returned null for scene: " + sceneName);
            yield break;
        }

        // Optional: show progress in console for debugging
        while (!op.isDone)
        {
            Debug.Log($"Loading '{sceneName}' progress: {op.progress:F2}");
            yield return null;
        }
    }

    bool IsSceneInBuildSettings(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
