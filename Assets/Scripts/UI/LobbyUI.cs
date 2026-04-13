using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Fusion;
using UnityEngine.EventSystems;

namespace Projectiles.UI
{
    [RequireComponent(typeof(FusionBootstrap))]
    public class FusionBootstrapUI : MonoBehaviour
    {
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Room Input")]
        [SerializeField] private TMP_InputField _roomNameInput;

        [Header("Buttons")]
        [SerializeField] private Button _startSinglePlayerButton;
        [SerializeField] private Button _startSharedClientButton;
        [SerializeField] private Button _startServerButton;
        [SerializeField] private Button _startHostButton;
        [SerializeField] private Button _startClientButton;
        [SerializeField] private Button _startAutoHostOrClientButton;
        [SerializeField] private Button _shutdownButton;

        [Header("Multi-Peer Mode")]
        [SerializeField] private GameObject _multiPeerPanel;
        [SerializeField] private TMP_InputField _clientCountInput;

        [Header("Settings")]
        [SerializeField] private bool _enableHotkeys = true;
        [SerializeField] private bool _enableHotkeysInBuild = false;
        [SerializeField] private bool _autoHideWhenConnected = true;
        [SerializeField] private GameObject _mainPanel;

        private FusionBootstrap _bootstrap;
        private bool _isMultiplePeerMode;
        private string _clientCount = "1";
        private Canvas _parentCanvas;

        void Awake()
        {
            _bootstrap = GetComponent<FusionBootstrap>();
            if (_bootstrap == null)
                _bootstrap = FindObjectOfType<FusionBootstrap>();

            _isMultiplePeerMode = NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple;

            // Setup button listeners. Show loading screen as soon as any start action is taken.
            if (_startSinglePlayerButton) _startSinglePlayerButton.onClick.AddListener(() => { ShowLoadingScreen("Starting session..."); _bootstrap.StartSinglePlayer(); });
            if (_startSharedClientButton) _startSharedClientButton.onClick.AddListener(() => { ShowLoadingScreen("Connecting..."); OnStartSharedClient(); });
            if (_startServerButton) _startServerButton.onClick.AddListener(() => { ShowLoadingScreen("Starting server..."); OnStartServer(); });
            if (_startHostButton) _startHostButton.onClick.AddListener(() => { ShowLoadingScreen("Starting host..."); OnStartHost(); });
            if (_startClientButton) _startClientButton.onClick.AddListener(() => { ShowLoadingScreen("Connecting..."); OnStartClient(); });
            if (_startAutoHostOrClientButton) _startAutoHostOrClientButton.onClick.AddListener(() => { ShowLoadingScreen("Connecting..."); OnStartAutoHostOrClient(); });
            if (_shutdownButton) _shutdownButton.onClick.AddListener(() => _bootstrap.ShutdownAll());

            // Setup inputs
            if (_roomNameInput)
            {
                _roomNameInput.text = _bootstrap.DefaultRoomName;
                _roomNameInput.onValueChanged.AddListener(value => _bootstrap.DefaultRoomName = value);
            }

            if (_clientCountInput)
            {
                _clientCountInput.text = _bootstrap.AutoClients.ToString();
                _clientCountInput.onValueChanged.AddListener(OnClientCountChanged);
            }

            if (_multiPeerPanel) _multiPeerPanel.SetActive(_isMultiplePeerMode);

            _parentCanvas = GetComponentInParent<Canvas>();
        }



        void Update()
        {
            if (_bootstrap == null) return;

            // Update status
            if (_statusText) _statusText.text = $"Fusion Status: {_bootstrap.CurrentStage}";

            // Update button states
            bool disconnected = _bootstrap.CurrentStage == FusionBootstrap.Stage.Disconnected;
            bool connected = _bootstrap.CurrentStage == FusionBootstrap.Stage.AllConnected;

            SetInteractable(_startSinglePlayerButton, disconnected);
            SetInteractable(_startSharedClientButton, disconnected);
            SetInteractable(_startServerButton, disconnected);
            SetInteractable(_startHostButton, disconnected);
            SetInteractable(_startClientButton, disconnected);
            SetInteractable(_startAutoHostOrClientButton, disconnected);
            SetInteractable(_shutdownButton, connected);
            if (_roomNameInput) _roomNameInput.interactable = disconnected;
            if (_clientCountInput) _clientCountInput.interactable = disconnected;

            // Hotkeys
            bool allowHotkeys = Application.isEditor || _enableHotkeysInBuild;
            if (_enableHotkeys && allowHotkeys && disconnected && IsTypingInInputField() == false)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.iKey.wasPressedThisFrame) { ShowLoadingScreen("Starting session..."); _bootstrap.StartSinglePlayer(); }
                    if (kb.hKey.wasPressedThisFrame) { ShowLoadingScreen("Starting host..."); OnStartHost(); }
                    if (kb.sKey.wasPressedThisFrame) { ShowLoadingScreen("Starting server..."); OnStartServer(); }
                    if (kb.cKey.wasPressedThisFrame) { ShowLoadingScreen("Connecting..."); OnStartClient(); }
                    if (kb.aKey.wasPressedThisFrame) { ShowLoadingScreen("Connecting..."); OnStartAutoHostOrClient(); }
                    if (kb.pKey.wasPressedThisFrame) { ShowLoadingScreen("Connecting..."); OnStartSharedClient(); }
                }
            }

            // Auto-hide
            if (_autoHideWhenConnected && _mainPanel)
                _mainPanel.SetActive(_bootstrap.CurrentStage != FusionBootstrap.Stage.AllConnected);
        }

        void SetInteractable(Button btn, bool state) { if (btn) btn.interactable = state; }

        void ShowLoadingScreen(string message)
        {
            // Don't disable _mainPanel or the LobbyUI GameObject — FusionBootstrap's StartHost/Client
            // coroutines need this object active. Instead disable the parent Canvas's rendering.
            LoadingScreen.GetOrCreate().Show(message);
            if (_parentCanvas != null)
                _parentCanvas.enabled = false;
        }

        bool IsTypingInInputField()
        {
            if (_roomNameInput != null && _roomNameInput.isFocused)
                return true;

            if (_clientCountInput != null && _clientCountInput.isFocused)
                return true;

            // Fallback: if any input field is selected, don't treat keypresses as lobby hotkeys.
            var es = EventSystem.current;
            if (es == null)
                return false;

            var selected = es.currentSelectedGameObject;
            if (selected == null)
                return false;

            return selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null;
        }

        int GetClientCount() => int.TryParse(_clientCount, out int c) ? c : 1;

        void OnClientCountChanged(string value)
        {
            _clientCount = System.Text.RegularExpressions.Regex.Replace(value, "[^0-9]", "");
            if (string.IsNullOrEmpty(_clientCount)) _clientCount = "1";
            if (_clientCountInput) _clientCountInput.text = _clientCount;
            if (_bootstrap && int.TryParse(_clientCount, out int count)) _bootstrap.AutoClients = count;
        }

        void OnStartHost() { if (_isMultiplePeerMode) _bootstrap.StartHostPlusClients(GetClientCount()); else _bootstrap.StartHost(); }
        void OnStartServer() { if (_isMultiplePeerMode) _bootstrap.StartServerPlusClients(GetClientCount()); else _bootstrap.StartServer(); }
        void OnStartClient() { if (_isMultiplePeerMode) _bootstrap.StartMultipleClients(GetClientCount()); else _bootstrap.StartClient(); }
        void OnStartAutoHostOrClient() { if (_isMultiplePeerMode) _bootstrap.StartMultipleAutoClients(GetClientCount()); else _bootstrap.StartAutoClient(); }
        void OnStartSharedClient() { if (_isMultiplePeerMode) _bootstrap.StartMultipleSharedClients(GetClientCount()); else _bootstrap.StartSharedClient(); }
    }
}