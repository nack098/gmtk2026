using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
public class MenuManager : MonoBehaviour
{
    [Header("── MainMenu ───────────────────────")]
    public GameObject menuPanel;
    public GameObject StartButtom;
    public GameObject QuitButtom;
    public GameObject SettingsButtom;

    [Header("── SettingsMenu ───────────────────────")]
    public GameObject SettingsPanel;
    public Button resetDefaultsButton;
    public Button backButton;
    [Header("── Audio Sliders ───────────────────────")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("── Audio Value Labels (% display) ──────")]
    public TextMeshProUGUI masterValueText;
    public TextMeshProUGUI musicValueText;
    public TextMeshProUGUI sfxValueText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
    void Start()
    {
        SettingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }
    public void GameStart()
    {
        SceneManager.LoadScene("ZenGameScene");
    }
    
    public void GameQuit()
    {
#if UNITY_EDITOR        
        UnityEditor.EditorApplication.isPlaying = false;
#endif        
        Application.Quit();

    }
    public void OpenSettingMenu()
    {
        SettingsPanel.SetActive(true);
        menuPanel.SetActive(false);
    }

    public void CloseSettingMenu()
    {
        SettingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }


    void OnEnable()
    {
        // Sliders
        if (masterSlider) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider)  musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider)    sfxSlider.onValueChanged.AddListener(OnSfxChanged);


        // Buttons
        if (resetDefaultsButton) resetDefaultsButton.onClick.AddListener(OnResetDefaults);
        if (backButton)          backButton.onClick.AddListener(OnBackClicked);

        SyncFromAudioManager();
    }

    void OnDisable()
    {
        if (masterSlider)        masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (musicSlider)         musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxSlider)           sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
    
        if (resetDefaultsButton) resetDefaultsButton.onClick.RemoveListener(OnResetDefaults);
        if (backButton)          backButton.onClick.RemoveListener(OnBackClicked);
    }

    // ── Sync ค่า slider/dropdown จาก AudioManager + QualitySettings ──────
    void SyncFromAudioManager()
    {
        var sm = AudioManager.Instance;
        if (masterSlider) masterSlider.SetValueWithoutNotify(sm.MasterVolume);
        if (musicSlider)  musicSlider.SetValueWithoutNotify(sm.MusicVolume);
        if (sfxSlider)    sfxSlider.SetValueWithoutNotify(sm.SfxVolume);
        UpdateLabel(masterValueText, sm.MasterVolume);
        UpdateLabel(musicValueText,  sm.MusicVolume);
        UpdateLabel(sfxValueText,    sm.SfxVolume);

    
    }

    // ── Callbacks ─────────────────────────────────────────────────────────
    void OnMasterChanged(float v) { AudioManager.Instance.SetMasterVolume(v); UpdateLabel(masterValueText, v); }
    void OnMusicChanged (float v) { AudioManager.Instance.SetMusicVolume(v);  UpdateLabel(musicValueText,  v); }
    void OnSfxChanged   (float v) { AudioManager.Instance.SetSfxVolume(v);    UpdateLabel(sfxValueText,    v); }

    void OnQualityChanged(int level)
    {
        QualitySettings.SetQualityLevel(level);
        PlayerPrefs.SetInt("QualityLevel", level);
        PlayerPrefs.Save();
    }

    void OnResetDefaults()
    {
        AudioManager.Instance.ResetToDefaults();
        SyncFromAudioManager();
    }

    void OnBackClicked()
    {
        CloseSettingMenu();
    }

    // ── Helper ────────────────────────────────────────────────────────────
    static void UpdateLabel(TextMeshProUGUI label, float v01)
    {
        if (label) label.text = $"{Mathf.RoundToInt(v01 * 100)}%";
    }
}
