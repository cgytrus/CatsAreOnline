using System.Globalization;

using BepInEx;
using BepInEx.Configuration;

using CaLAPI.API;
using CaLAPI.API.ProphecySystem;

using Cat;

using CatsAreOnline.Chat;
using CatsAreOnline.Patches;

using HarmonyLib;

using PipeSystem;

using UnityEngine;

using CatControls = CaLAPI.API.Cat.CatControls;
using CatPartManager = CaLAPI.API.Cat.CatPartManager;

namespace CatsAreOnline {
    [BepInPlugin("mod.cgytrus.plugin.calOnline", "Cats are Online", "0.3.0")]
    [BepInDependency("mod.cgytrus.plugins.calapi", "0.1.4")]
    public class MultiplayerPlugin : BaseUnityPlugin {
        public static ConfigEntry<bool> connected;
        private ConfigEntry<string> _username;
        private ConfigEntry<string> _displayName;
        private ConfigEntry<string> _address;
        private ConfigEntry<bool> _displayOwnCat;
        private ConfigEntry<bool> _playerCollisions;
        private ConfigEntry<KeyboardShortcut> _toggleChat;
        private ConfigEntry<int> _chatCapacity;
        private ConfigEntry<int> _historyCapacity;
        private ConfigEntry<KeyboardShortcut> _historyUp;
        private ConfigEntry<KeyboardShortcut> _historyDown;
        private ConfigEntry<float> _messageFadeOutDelay;
        private ConfigEntry<float> _messageFadeOutSpeed;
        
        private ConfigEntry<Player.InterpolationSettings.InterpolationMode> _interpolationMode;
        private ConfigEntry<double> _interpolationTime;
        private ConfigEntry<int> _interpolationPacketsToAverage;
        private ConfigEntry<double> _interpolationMaxTime;

        private Client _client;

        private State _state;
        private float _scale;
        private Color _color;
        private bool _update;

        private void Awake() {
            Harmony.CreateAndPatchAll(typeof(JunctionUpdates));
            Harmony.CreateAndPatchAll(typeof(IceRotationUpdates));
            Harmony.CreateAndPatchAll(typeof(ChatControlBlock));
            Harmony.CreateAndPatchAll(typeof(PipeColorUpdate));
            Harmony.CreateAndPatchAll(typeof(CurrentIceUpdates));
            
            // i'm sincerely sorry for this kind of configuration code
            connected = Config.Bind("General", "Connected", false, "");
            _username = Config.Bind("General", "Username", "", "Your internal name");
            _displayName = Config.Bind("General", "Display Name", "",
                "Your name that will be displayed to other players");
            _address = Config.Bind("General", "Address", "127.0.0.1:1337", "");
            _displayOwnCat = Config.Bind("General", "Display Own Cat", false, "");
            _playerCollisions = Config.Bind("General", "Player Collisions", false, "[EXPERIMENTAL]");
            _toggleChat = Config.Bind("General", "Toggle Chat Button", new KeyboardShortcut(KeyCode.T), "");
            _chatCapacity = Config.Bind("Chat", "Chat Capacity", 10,
                "Maximum amount of chat messages that can be displayed at the same time");
            _historyCapacity = Config.Bind("Chat", "History Capacity", 100,
                "Maximum amount of chat message texts that are stored in history to be able to resend them");
            _historyUp = Config.Bind("General", "History Up", new KeyboardShortcut(KeyCode.UpArrow), "");
            _historyDown = Config.Bind("General", "History Down", new KeyboardShortcut(KeyCode.DownArrow), "");
            _messageFadeOutDelay = Config.Bind("Chat", "Message Fade Out Delay", 5f, "");
            _messageFadeOutSpeed = Config.Bind("Chat", "Message Fade Out Speed", 1f, "");
            
            _interpolationMode = Config.Bind("Advanced", "Interpolation Mode",
                Player.InterpolationSettings.InterpolationMode.Lerp, "");
            _interpolationTime = Config.Bind("Advanced", "Interpolation Time", 2d, "");
            _interpolationPacketsToAverage = Config.Bind("Advanced", "Interpolation Packets To Average", 20, "");
            _interpolationMaxTime = Config.Bind("Advanced", "Interpolation Max Time", 5d, "");

            connected.Value = false;

            _client = new Client();
            _client.Initialize();
            PatchesClientProvider.client = _client;

            Commands.Initialize();
            
            _state = State.Normal;
            _scale = Client.GetScaleFromCatState(_state);
            connected.SettingChanged += (_, __) => {
                if(!SetConnected(connected.Value)) connected.Value = false;
            };

            _client.displayOwnCat = _displayOwnCat.Value;
            _displayOwnCat.SettingChanged += (_, __) => _client.displayOwnCat = _displayOwnCat.Value;

            _client.playerCollisions = _playerCollisions.Value;
            _playerCollisions.SettingChanged += (_, __) => _client.playerCollisions = _playerCollisions.Value;

            Chat.Chat.messagesCapacity = _chatCapacity.Value;
            _chatCapacity.SettingChanged += (_, __) => Chat.Chat.messagesCapacity = _chatCapacity.Value;

            Chat.Chat.historyCapacity = _historyCapacity.Value;
            _historyCapacity.SettingChanged += (_, __) => Chat.Chat.historyCapacity = _historyCapacity.Value;

            Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;
            _messageFadeOutDelay.SettingChanged += (_, __) => Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;

            Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;
            _messageFadeOutSpeed.SettingChanged += (_, __) => Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;

            Player.interpolationSettings = new Player.InterpolationSettings(_interpolationMode.Value,
                _interpolationTime.Value, _interpolationPacketsToAverage.Value, _interpolationMaxTime.Value);
            _interpolationMode.SettingChanged += (_, __) => Player.interpolationSettings =
                new Player.InterpolationSettings(_interpolationMode.Value, Player.interpolationSettings.time,
                    Player.interpolationSettings.packetsToAverage, Player.interpolationSettings.maxTime);
            _interpolationTime.SettingChanged += (_, __) => Player.interpolationSettings =
                new Player.InterpolationSettings(Player.interpolationSettings.mode, _interpolationTime.Value,
                    Player.interpolationSettings.packetsToAverage, Player.interpolationSettings.maxTime);
            _interpolationPacketsToAverage.SettingChanged += (_, __) => Player.interpolationSettings =
                new Player.InterpolationSettings(Player.interpolationSettings.mode, Player.interpolationSettings.time,
                    _interpolationPacketsToAverage.Value, Player.interpolationSettings.maxTime);
            _interpolationMaxTime.SettingChanged += (_, __) => Player.interpolationSettings =
                new Player.InterpolationSettings(Player.interpolationSettings.mode, Player.interpolationSettings.time,
                    Player.interpolationSettings.packetsToAverage, _interpolationMaxTime.Value);

            CatPartManager.awake += (caller, args) => {
                Cat.CatPartManager partManager = (Cat.CatPartManager)caller;
                
                if(!partManager.GetComponent<PlayerActor>()) return;
                
                _client.catSprite = args.noMetaballsPartTexture;
                _client.playerPartManager = partManager;
            };

            CatControls.awake += (caller, _) => {
                Cat.CatControls controls = (Cat.CatControls)caller;
                
                if(!controls.GetComponent<PlayerActor>()) return;
                
                GameObject catIcePrefab =
                    (GameObject)AccessTools.Field(typeof(Cat.CatControls), "catIcePrefab").GetValue(caller);
                SpriteRenderer catIceMainRenderer = (SpriteRenderer)AccessTools.Field(typeof(IceBlock), "mainSprite")
                    .GetValue(catIcePrefab.GetComponent<IceBlock>());

                _client.iceSprite = catIceMainRenderer.sprite;
                _client.iceColor = catIceMainRenderer.color;
                _client.playerControls = controls;

                controls.StateSwitchAction += state => {
                    _state = (State)state;
                    _scale = Client.GetScaleFromCatState(_state);
                };
            };

            CaLAPI.API.PauseScreen.roomInfoUpdated += (_, args) => {
                _client.state.room = args.newRoom;
            };

            PauseScreen.pauseScreenExitAction += () => {
                _client.state.room = null;
            };

            CaLAPI.API.CanvasManager.awake += (_, args) => {
                _client.nameTagCamera = Camera.main;
                GameObject nameTags = new GameObject("Name Tags") { layer = LayerMask.NameToLayer("UI") };
                DontDestroyOnLoad(nameTags);

                RectTransform nameTagsTransform = nameTags.AddComponent<RectTransform>();
                nameTagsTransform.anchoredPosition = Vector2.zero;
                nameTagsTransform.localScale = Vector3.one;

                Canvas canvas = nameTags.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _client.nameTagCamera;
                canvas.scaleFactor = 720f;

                _client.nameTags = nameTagsTransform;
            };

            Writer.awake += (_, args) => {
                _client.nameTagFont = args.font;
                Message.font = args.font;
            };

            CatControls.changedColor += (caller, args) => {
                Cat.CatControls controls = (Cat.CatControls)caller;
                
                if(!controls.GetComponent<PlayerActor>()) return;

                _color = args.newColor;
            };

            UI.initialized += (_, __) => {
                Chat.Chat.Initialize(_client);
            };
        }

        private bool SetConnected(bool connected) {
            if(connected) {
                if(!_client.canConnect) return false;
                (string ip, int port) = ParseIp(_address.Value);
                _client.Connect(ip, port, _username.Value, _displayName.Value);
            }
            else _client.Disconnect();

            return true;
        }

        private void Update() {
            _client.Update();
            _client.UpdateAllNameTagsPositions();

            if(_toggleChat.Value.IsDown()) Chat.Chat.chatFocused = true;
            Chat.Chat.UpdateMessagesFadeOut();
            Chat.Chat.UpdateMessageHistory(_historyUp.Value.IsDown(), _historyDown.Value.IsDown());
        }

        private void FixedUpdate() {
            if(Pipe.catInPipe) _client.state.scale = Client.GetScaleFromCatState(State.Liquid);
            else {
                _client.state.scale = _scale;
                _client.state.color = _color;
            }
            _client.state.movementCatState = _state;
            _client.state.position = _client.currentCatPosition;
            if(!_client.playerControls) return;
            bool ice = CurrentIceUpdates.currentIce;
            _client.state.ice = ice;
            if(ice) {
                _client.state.color = _client.iceColor;
                _client.state.scale = CurrentIceUpdates.currentIce.Size.y * 3.5f;
            }
            
            if(_update) _client.SendStateDeltaToServer(_client.state);
            _update = !_update;
    }

        private void OnApplicationQuit() => SetConnected(false);

        private static (string, int) ParseIp(string ip) {
            string[] ipPort = ip.Split(':');
            return (ipPort[0], int.Parse(ipPort[1], CultureInfo.InvariantCulture));
        }
    }
}
