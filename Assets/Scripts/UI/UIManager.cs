using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

[RequireComponent(typeof(UIDocument))]
public class UIManager : MonoBehaviour
{
    [Header("Dependencies")]
    public WorldGenerator worldGenerator;
    public HexGridVisualizer visualizer;
    
    private UIDocument _uiDocument;
    private VisualElement _mainMenuScreen;
    private VisualElement _pauseMenuScreen;
    private VisualElement _panelNew;
    private VisualElement _panelLoad;
    private VisualElement _panelSettings;
    
    private VisualElement _hoverScannerPanel;
    private Label _hoverTitle;
    private Label _hoverData;
    private bool _scannerEnabled = true;
    
    private TextField _seedInput;
    private TextField _loadNameInput;
    private TextField _saveNameInput;
    private Toggle _fullscreenToggle;
    private Slider _volumeSlider;
    
    private bool _isGameActive = false;
    private bool _isPaused = false;

    private void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        var root = _uiDocument.rootVisualElement;
        
        _mainMenuScreen = root.Q<VisualElement>("main-menu-screen");
        _pauseMenuScreen = root.Q<VisualElement>("pause-menu-screen");
        
        _panelNew = root.Q<VisualElement>("panel-new");
        _panelLoad = root.Q<VisualElement>("panel-load");
        _panelSettings = root.Q<VisualElement>("panel-settings");
        
        _hoverScannerPanel = root.Q<VisualElement>("hover-scanner-panel");
        _hoverTitle = root.Q<Label>("hover-title");
        _hoverData = root.Q<Label>("hover-data");
        
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
        
        ReturnToMainMenu();
    }

    private void Update()
    {
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

        // Added 'U' keybind to toggle the underwater view
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
        if (panelToShow != null) panelToShow.style.display = DisplayStyle.Flex;
    }

    private void ReturnToMainMenu()
    {
        _isGameActive = false;
        _isPaused = false;
        Time.timeScale = 1f;
        
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
        
        int seed = 12345;
        if (_seedInput != null && !string.IsNullOrEmpty(_seedInput.value))
        {
            if (!int.TryParse(_seedInput.value, out seed)) seed = _seedInput.value.GetHashCode();
        }
        
        worldGenerator.currentSeed = seed;
        worldGenerator.GenerateWorld();
        visualizer.RenderGrid(worldGenerator.gridData);
        
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
            string saveName = !string.IsNullOrEmpty(_loadNameInput.value) ? _loadNameInput.value : "default_save";
            if (worldGenerator.LoadWorld(saveName))
            {
                visualizer.RenderGrid(worldGenerator.gridData);
                if (_saveNameInput != null) _saveNameInput.value = saveName;
                StartGame();
            }
            else Debug.LogWarning("Failed to load archive: " + saveName);
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
