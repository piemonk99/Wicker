using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _quitButton;

    [Header("Settings")]
    [SerializeField] private bool _pauseGameTime = true;

    private bool _isPaused = false;

    private void Awake()
    {
        // Ensure panel starts disabled
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(false);
        }
    }

    private void Start()
    {
        // Set up button listeners
        if (_resumeButton != null)
        {
            _resumeButton.onClick.AddListener(ResumeGame);
        }

        if (_settingsButton != null)
        {
            _settingsButton.onClick.AddListener(OpenSettings);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(QuitGame);
        }
    }

    public void TogglePause()
    {
        if (_isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (_isPaused) return;

        _isPaused = true;

        // Show pause menu
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(true);
        }

        // Pause game time
        if (_pauseGameTime)
        {
            Time.timeScale = 0f;
        }

        // Optional: Disable player input
        // You can raise an event here if needed
        Debug.Log("Game Paused");
    }

    public void ResumeGame()
    {
        if (!_isPaused) return;

        _isPaused = false;

        // Hide pause menu
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(false);
        }

        // Resume game time
        if (_pauseGameTime)
        {
            Time.timeScale = 1f;
        }

        // Optional: Enable player input
        Debug.Log("Game Resumed");
    }

    private void OpenSettings()
    {
        Debug.Log("Opening Settings...");
        // You would typically open a settings sub-menu here
        // For now, just log it
    }

    private void QuitGame()
    {
        Debug.Log("Quitting Game...");

        // If in editor
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public bool IsPaused => _isPaused;
}