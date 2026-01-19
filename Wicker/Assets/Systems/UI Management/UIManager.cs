using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    [Header("UI Managers")]
    [SerializeField] private EquipmentPanelManager _equipmentPanel;
    [SerializeField] private PauseMenuManager _pauseMenu;

    // Reference to your PlayerControls
    private PlayerControls _playerControls;

    private void Awake()
    {
        // Initialize PlayerControls
        _playerControls = new PlayerControls();

        _equipmentPanel.Initialize();

        _equipmentPanel.gameObject.SetActive(false);
        _pauseMenu.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // Enable the player's UI controls
        _playerControls.UI.Enable();

        // Subscribe to input events
        _playerControls.UI.Equipment.performed += OnToggleEquipment;
        _playerControls.UI.Pause.performed += OnPause;
    }

    private void OnDisable()
    {
        // Unsubscribe from input events
        _playerControls.UI.Equipment.performed -= OnToggleEquipment;
        _playerControls.UI.Pause.performed -= OnPause;

        // Disable the player controls
        _playerControls.Disable();
    }

    private void OnToggleEquipment(InputAction.CallbackContext context)
    {
        // Don't open equipment if pause menu is open
        if (_pauseMenu != null && _pauseMenu.IsPaused) return;

        if (_equipmentPanel != null)
        {
            _equipmentPanel.TogglePanel();
        }
    }

    private void OnPause(InputAction.CallbackContext context)
    {
        // If equipment panel is open, close it first
        if (_equipmentPanel != null && _equipmentPanel.IsPanelOpen)
        {
            _equipmentPanel.ClosePanel();
            return;
        }

        // Toggle pause menu
        if (_pauseMenu != null)
        {
            _pauseMenu.TogglePause();
        }
        else
        {
            Debug.LogWarning("PauseMenuManager not assigned to UIManager");
        }
    }

    // Public methods for other scripts to access UI state
    public bool IsAnyMenuOpen()
    {
        bool equipmentOpen = _equipmentPanel != null && _equipmentPanel.IsPanelOpen;
        bool paused = _pauseMenu != null && _pauseMenu.IsPaused;

        return equipmentOpen || paused;
    }

    public void CloseAllMenus()
    {
        if (_equipmentPanel != null && _equipmentPanel.IsPanelOpen)
        {
            _equipmentPanel.ClosePanel();
        }

        if (_pauseMenu != null && _pauseMenu.IsPaused)
        {
            _pauseMenu.ResumeGame();
        }
    }
}