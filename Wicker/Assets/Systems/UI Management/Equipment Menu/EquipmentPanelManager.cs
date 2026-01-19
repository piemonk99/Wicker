using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipmentPanelManager : MonoBehaviour
{
    [System.Serializable]
    public class EquipmentPage
    {
        public string pageName;
        public GameObject pageObject;
        public BaseEquipmentPage pageController;
    }

    [Header("Page Configuration")]
    [SerializeField] private EquipmentPage[] _pages;
    [SerializeField] private int _startingPageIndex = 0;

    [Header("Navigation UI")]
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;
    [SerializeField] private Image[] _pageIndicatorDots; // Optional - can be empty
    [SerializeField] private TextMeshProUGUI _pageTitleText; // Optional

    private int _currentPageIndex = 0;
    private bool _isPanelOpen = false;

    public void Initialize()
    {
        // Initialize all pages if we have any
        if (_pages != null && _pages.Length > 0)
        {
            for (int i = 0; i < _pages.Length; i++)
            {
                var page = _pages[i];
                if (page.pageObject != null)
                {
                    // Only activate the starting page, deactivate all others
                    page.pageObject.SetActive(i == _startingPageIndex);
                }

                // Initialize the page controller if it exists
                if (page.pageController != null)
                {
                    page.pageController.Initialize();
                }
            }
        }

        // Set current page index to match starting page
        _currentPageIndex = _startingPageIndex;

        // Set up navigation buttons
        _leftArrowButton.onClick.AddListener(NavigateToPreviousPage);
        _rightArrowButton.onClick.AddListener(NavigateToNextPage);
    }

    public void TogglePanel()
    {
        if (!Application.isPlaying) return;

        if (_isPanelOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    public void OpenPanel()
    {
        if (_isPanelOpen || !Application.isPlaying) return;

        _isPanelOpen = true;
        gameObject.SetActive(true);
        Time.timeScale = 0f; // Pause game

        // Show starting page if we have pages
        if (_pages != null && _pages.Length > 0)
        {
            SwitchToPage(_startingPageIndex);
        }

        // Enable input if buttons exist
        if (_leftArrowButton != null)
        {
            _leftArrowButton.interactable = true;
        }

        if (_rightArrowButton != null)
        {
            _rightArrowButton.interactable = true;
        }
    }

    public void ClosePanel()
    {
        if (!_isPanelOpen) return;

        _isPanelOpen = false;
        gameObject.SetActive(false);
        Time.timeScale = 1f; // Resume game

        // Hide current page if we have pages
        if (_pages != null && _currentPageIndex < _pages.Length)
        {
            var currentPage = _pages[_currentPageIndex];
            if (currentPage.pageObject != null)
            {
                currentPage.pageObject.SetActive(false);
            }

            if (currentPage.pageController != null)
            {
                currentPage.pageController.OnPageHidden();
            }
        }
    }

    public void NavigateToNextPage()
    {
        if (_pages == null || _pages.Length == 0) return;

        int nextIndex = (_currentPageIndex + 1) % _pages.Length;
        SwitchToPage(nextIndex);
    }

    public void NavigateToPreviousPage()
    {
        if (_pages == null || _pages.Length == 0) return;

        int prevIndex = (_currentPageIndex - 1 + _pages.Length) % _pages.Length;
        SwitchToPage(prevIndex);
    }

    public void SwitchToPage(int pageIndex)
    {
        if (_pages == null || pageIndex < 0 || pageIndex >= _pages.Length) return;

        // Hide current page if it exists
        if (_currentPageIndex < _pages.Length)
        {
            var currentPage = _pages[_currentPageIndex];
            if (currentPage.pageObject != null)
            {
                currentPage.pageObject.SetActive(false);
            }

            if (currentPage.pageController != null)
            {
                currentPage.pageController.OnPageHidden();
            }
        }

        // Show new page if it exists
        _currentPageIndex = pageIndex;
        var newPage = _pages[_currentPageIndex];

        if (newPage.pageObject != null)
        {
            newPage.pageObject.SetActive(true);
        }

        if (newPage.pageController != null)
        {
            newPage.pageController.OnPageShown();
        }

        // Update UI if elements exist
        UpdateNavigationUI();
    }

    private void UpdateNavigationUI()
    {
        // Update page title if it exists
        if (_pageTitleText != null && _pages != null && _currentPageIndex < _pages.Length)
        {
            _pageTitleText.text = _pages[_currentPageIndex].pageName;
        }

        // Update indicator dots if they exist
        if (_pageIndicatorDots != null && _pages != null)
        {
            for (int i = 0; i < _pageIndicatorDots.Length; i++)
            {
                if (_pageIndicatorDots[i] != null)
                {
                    if (i < _pages.Length)
                    {
                        _pageIndicatorDots[i].color = (i == _currentPageIndex) ?
                            Color.white : new Color(1, 1, 1, 0.3f);
                    }
                    else
                    {
                        _pageIndicatorDots[i].gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    public bool IsPanelOpen => _isPanelOpen;
}