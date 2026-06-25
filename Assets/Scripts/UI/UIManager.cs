// Assets/Scripts/UI/UIManager.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class DemoMode
{
    public string demoName;
    [Tooltip("The parent GameObject that contains all the logic for this demo.")]
    public GameObject demoRootObject;
}

[RequireComponent(typeof(UIDocument))]
public class UIManager : MonoBehaviour
{
    [Header("Dependencies")]
    public WorldGenerator worldGenerator;
    public HexGridVisualizer visualizer;
    [Tooltip("The parent GameObject holding the main planet generation, cameras, etc.")]
    public GameObject mainWorldRoot; 

    [Header("Debug Demos")]
    public List<DemoMode> availableDemos = new List<DemoMode>();

    private UIDocument _uiDocument;
    private VisualElement _mainMenuScreen;
    private VisualElement _pauseMenuScreen;
    private VisualElement _panelNew;
    private VisualElement _panelLoad;
    private VisualElement _panelSettings;
    private VisualElement _panelDebug;
    private VisualElement _debugDemosContainer;
    private VisualElement _hoverScannerPanel;
    private Label _hoverTitle;
    private Label _hoverData;
    private bool _scannerEnabled = true;
    private VisualElement _loadingScreen;
    private VisualElement _loadingSpinner;
    private float _spinnerRotation = 0f;
    private TextField _seedInput;
    private TextField _loadNameInput;
    private TextField _saveNameInput;
    private Toggle _fullscreenToggle;
    private Slider _volumeSlider;
    
    private bool _isGameActive = false;
    private bool _isPaused = false;
    private GameObject _currentActiveDemo;

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        var root = _uiDocument.rootVisualElement;
        
        _mainMenuScreen = root.Q<VisualElement>("main-menu-screen");
        _pauseMenuScreen = root.Q<VisualElement>("pause-menu-screen");
        
        _panelNew = root.Q<VisualElement>("panel-new");
        _panelLoad = root.Q<VisualElement>("panel-load");
        _panelSettings = root.Q<VisualElement>("panel-settings");
        _panelDebug = root.Q<VisualElement>("panel-debug");
        _debugDemosContainer = root.Q<VisualElement>("debug-demos-container");
        
        _hoverScannerPanel = root.Q<VisualElement>("hover-scanner-panel");
        _hoverTitle = root.Q<Label>("hover-title");
        _hoverData = root.Q<Label>("hover-data");
        _loadingScreen = root.Q<VisualElement>("loading-screen");
        _loadingSpinner = root.Q<VisualElement>("loading-spinner");
        _seedInput = root.Q<TextField>("seed-input");
        _loadNameInput = root.Q<TextField>("load-name-input");
        _saveNameInput = root.Q<TextField>("save-name-input");
        _fullscreenToggle = root.Q<Toggle>("toggle-fullscreen");
        _volumeSlider = root.Q<Slider>("slider-volume");
        
        if (_fullscreenToggle != null) _fullscreenToggle.value = Screen.fullScreen;
        if (_volumeSlider != null) _volumeSlider.value = AudioListener.volume;
        
        root.Q<Button>("tab-new").clicked += () => ShowMainContent(_panelNew);
        root.Q<Button>("tab-load").clicked += () => ShowMainContent(_panelLoad);
        root.Q<Button>("tab-settings").clicked += () => ShowMainContent(_panelSettings);
        
        Button tabDebug = root.Q<Button>("tab-debug");
        if (tabDebug != null) tabDebug.clicked += () => ShowMainContent(_panelDebug);
        
        root.Q<Button>("btn-quit").clicked += QuitGame;
        root.Q<Button>("btn-generate").clicked += GenerateNewWorld;
        root.Q<Button>("btn-load").clicked += LoadWorld;
        root.Q<Button>("btn-resume").clicked += TogglePause;
        root.Q<Button>("btn-save").clicked += SaveWorld;
        root.Q<Button>("btn-main-menu").clicked += ReturnToMainMenu;
        
        _fullscreenToggle?.RegisterValueChangedCallback(evt => Screen.fullScreen = evt.newValue);
        _volumeSlider?.RegisterValueChangedCallback(evt => AudioListener.volume = evt.newValue);
        
        if (worldGenerator == null) worldGenerator = FindObjectOfType<WorldGenerator>();
        if (visualizer == null) visualizer = FindObjectOfType<HexGridVisualizer>();
        
        // Populate debug demos list
        if (_debugDemosContainer != null)
        {
            _debugDemosContainer.Clear();
            for (int i = 0; i < availableDemos.Count; i++)
            {
                int index = i;
                Button btn = new Button(() => LaunchDemo(index));
                btn.text = availableDemos[i].demoName;
                btn.AddToClassList("action-btn");
                _debugDemosContainer.Add(btn);
            }
        }
        
        // Hide all demo root objects initially
        foreach (var demo in availableDemos)
        {
            if (demo.demoRootObject != null)
                demo.demoRootObject.SetActive(false);
        }

        ReturnToMainMenu();
    }

    private void Update()
    {
        if (_loadingScreen != null && _loadingScreen.style.display == DisplayStyle.Flex && _loadingSpinner != null)
        {
            _spinnerRotation += Time.deltaTime * 360f;
            _loadingSpinner.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(_spinnerRotation)));
        }
        
        if (Keyboard.current == null) return;
        
        if (_isGameActive && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            _scannerEnabled = !_scannerEnabled;
            if (!_scannerEnabled && _hoverScannerPanel != null)
                _hoverScannerPanel.style.display = DisplayStyle.None;
        }
        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            if (visualizer != null)
            {
                visualizer.SetUnderwaterMode(!visualizer.isUnderwater);
            }
        }
        ProcessHoverScanner();
    }

    private void ProcessHoverScanner()
    {
        if (!_scannerEnabled || !_isGameActive || _isPaused || Camera.main == null || Mouse.current == null)
            return;
            
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 viewportPos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);
        Ray ray = Camera.main.ViewportPointToRay(viewportPos);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f))
        {
            HexCoordinates coords;
            HexTileInfo info = hit.collider.GetComponentInParent<HexTileInfo>();
            bool isRiver = hit.collider.gameObject.name == "RiverSystem";
            if (info != null)
            {
                coords = info.coordinates;
            }
            else
            {
                coords = HexCoordinates.FromPosition(hit.point, worldGenerator.hexOuterRadius);
            }
            
            if (worldGenerator.gridData != null)
            {
                HexCell cell = worldGenerator.gridData.GetCell(coords);
                if (cell != null)
                {
                    _hoverScannerPanel.style.display = DisplayStyle.Flex;
                    _hoverTitle.text = isRiver ? $"SECTOR [{coords.q}, {coords.r}] <color=#2ED1E5>[RIVER]</color>" : $"SECTOR [{coords.q}, {coords.r}]";
                    string riverStatus = cell.riverVolume > 0 ? $"<color=#2ED1E5>DETECTED ({cell.riverVolume}v)</color>" : "NULL";
                    _hoverData.text =
                        $"BIOME:     {cell.biome}\n" +
                        $"ALTITUDE:  {cell.elevation:F1}m\n" +
                        $"MOISTURE:  {cell.moisture:F2}\n" +
                        $"THERMALS:  {cell.temperature:F2}\n" +
                        $"HYDROLOGY: {riverStatus}";
                    return;
                }
            }
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 targetHitPoint = ray.GetPoint(distance);
                HexCoordinates coords = HexCoordinates.FromPosition(targetHitPoint, worldGenerator.hexOuterRadius);
                if (worldGenerator.gridData != null)
                {
                    HexCell cell = worldGenerator.gridData.GetCell(coords);
                    if (cell != null)
                    {
                        _hoverScannerPanel.style.display = DisplayStyle.Flex;
                        _hoverTitle.text = $"SECTOR [{coords.q}, {coords.r}]";
                        string riverStatus = cell.riverVolume > 0 ? $"<color=#2ED1E5>DETECTED ({cell.riverVolume}v)</color>" : "NULL";
                        _hoverData.text =
                            $"BIOME:     {cell.biome}\n" +
                            $"ALTITUDE:  {cell.elevation:F1}m\n" +
                            $"MOISTURE:  {cell.moisture:F2}\n" +
                            $"THERMALS:  {cell.temperature:F2}\n" +
                            $"HYDROLOGY: {riverStatus}";
                        return;
                    }
                }
            }
        }
        _hoverScannerPanel.style.display = DisplayStyle.None;
    }

    private void ShowMainContent(VisualElement panelToShow)
    {
        _panelNew.style.display = DisplayStyle.None;
        _panelLoad.style.display = DisplayStyle.None;
        _panelSettings.style.display = DisplayStyle.None;
        if (_panelDebug != null) _panelDebug.style.display = DisplayStyle.None;
        
        if (panelToShow != null) panelToShow.style.display = DisplayStyle.Flex;
    }

    private void ReturnToMainMenu()
    {
        _isGameActive = false;
        _isPaused = false;
        Time.timeScale = 1f;
        
        if (_currentActiveDemo != null)
        {
            _currentActiveDemo.SetActive(false);
            _currentActiveDemo = null;
        }

        // Re-enable the main world generation elements if we're back in the desktop/main view
        if (mainWorldRoot != null) mainWorldRoot.SetActive(true);

        if (_hoverScannerPanel != null) _hoverScannerPanel.style.display = DisplayStyle.None;
        if (visualizer != null) visualizer.ClearGrid();
        
        _mainMenuScreen.style.display = DisplayStyle.Flex;
        _pauseMenuScreen.style.display = DisplayStyle.None;
        
        ShowMainContent(_panelNew);
    }

    private void StartGame()
    {
        _isGameActive = true;
        _isPaused = false;
        Time.timeScale = 1f;
        _mainMenuScreen.style.display = DisplayStyle.None;
        _pauseMenuScreen.style.display = DisplayStyle.None;
    }

    private void LaunchDemo(int index)
    {
        if (index < 0 || index >= availableDemos.Count) return;
        
        if (_currentActiveDemo != null) _currentActiveDemo.SetActive(false);
        
        // Disable the main game world/camera so it doesn't conflict with the demo
        if (mainWorldRoot != null) mainWorldRoot.SetActive(false);

        _currentActiveDemo = availableDemos[index].demoRootObject;
        if (_currentActiveDemo != null) _currentActiveDemo.SetActive(true);
        
        StartGame();
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            Time.timeScale = 0f;
            if (_hoverScannerPanel != null) _hoverScannerPanel.style.display = DisplayStyle.None;
            _pauseMenuScreen.style.display = DisplayStyle.Flex;
        }
        else
        {
            Time.timeScale = 1f;
            _pauseMenuScreen.style.display = DisplayStyle.None;
        }
    }

    private void GenerateNewWorld()
    {
        if (worldGenerator == null || visualizer == null) return;
        
        // Ensure the main world root is active when generating a real world
        if (mainWorldRoot != null) mainWorldRoot.SetActive(true);

        int seed = 12345;
        if (_seedInput != null && !string.IsNullOrEmpty(_seedInput.value))
        {
            if (!int.TryParse(_seedInput.value, out seed)) seed = _seedInput.value.GetHashCode();
        }
        StartCoroutine(GenerateWorldRoutine(seed));
    }

    private IEnumerator GenerateWorldRoutine(int seed)
    {
        var sidebar = _mainMenuScreen.Q<VisualElement>(className: "sidebar");
        if (sidebar != null) sidebar.style.display = DisplayStyle.None;
        ShowMainContent(null);
        
        if (_loadingScreen != null) _loadingScreen.style.display = DisplayStyle.Flex;
        yield return null;
        
        worldGenerator.currentSeed = seed;
        yield return StartCoroutine(worldGenerator.GenerateWorldAsync());
        yield return StartCoroutine(visualizer.RenderGridAsync(worldGenerator.gridData));
        
        if (_loadingScreen != null) _loadingScreen.style.display = DisplayStyle.None;
        if (sidebar != null) sidebar.style.display = DisplayStyle.Flex;
        StartGame();
    }

    private void SaveWorld()
    {
        if (worldGenerator != null)
        {
            string saveName = !string.IsNullOrEmpty(_saveNameInput.value) ? _saveNameInput.value : "default_save";
            worldGenerator.SaveWorld(saveName);
            TogglePause();
        }
    }

    private void LoadWorld()
    {
        if (worldGenerator != null && visualizer != null)
        {
            // Ensure the main world root is active when loading a real world
            if (mainWorldRoot != null) mainWorldRoot.SetActive(true);

            string saveName = !string.IsNullOrEmpty(_loadNameInput.value) ? _loadNameInput.value : "default_save";
            StartCoroutine(LoadWorldRoutine(saveName));
        }
    }

    private IEnumerator LoadWorldRoutine(string saveName)
    {
        var sidebar = _mainMenuScreen.Q<VisualElement>(className: "sidebar");
        if (sidebar != null) sidebar.style.display = DisplayStyle.None;
        ShowMainContent(null);
        
        if (_loadingScreen != null) _loadingScreen.style.display = DisplayStyle.Flex;
        yield return null;
        
        if (worldGenerator.LoadWorld(saveName))
        {
            yield return StartCoroutine(visualizer.RenderGridAsync(worldGenerator.gridData));
            if (_saveNameInput != null) _saveNameInput.value = saveName;
            if (_loadingScreen != null) _loadingScreen.style.display = DisplayStyle.None;
            if (sidebar != null) sidebar.style.display = DisplayStyle.Flex;
            StartGame();
        }
        else
        {
            Debug.LogWarning("Failed to load archive: " + saveName);
            if (_loadingScreen != null) _loadingScreen.style.display = DisplayStyle.None;
            if (sidebar != null) sidebar.style.display = DisplayStyle.Flex;
            ShowMainContent(_panelLoad);
        }
    }

    private void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
