using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    // Called when START button is clicked
    public void StartGame()
    {
        SceneManager.LoadScene("Gameplay");
    }

    // Called when EXIT button is clicked
    public void ExitGame()
    {
        Debug.Log("Exit Game clicked");
        Application.Quit();
    }
}
