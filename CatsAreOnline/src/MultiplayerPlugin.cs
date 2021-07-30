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
    [BepInPlugin("mod.cgytrus.plugin.calOnline", "Cats are Online", "0.2.4")]
    [BepInDependency("mod.cgytrus.plugins.calapi", "0.1.4")]
    public class MultiplayerPlugin : BaseUnityPlugin {
        public static ConfigEntry<bool> connected;
        private ConfigEntry<string> _username;
        private ConfigEntry<string> _displayName;
        private ConfigEntry<string> _address;
        private ConfigEntry<bool> _displayOwnCat;
        private ConfigEntry<KeyboardShortcut> _toggleChat;
        private ConfigEntry<int> _chatCapacity;
        private ConfigEntry<int> _historyCapacity;
        private ConfigEntry<KeyboardShortcut> _historyUp;
        private ConfigEntry<KeyboardShortcut> _historyDown;
        private ConfigEntry<float> _messageFadeOutDelay;
        private ConfigEntry<float> _messageFadeOutSpeed;

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
            
            connected = Config.Bind("General", "Connected", false, "");
            _username = Config.Bind("General", "Username", "", "Your internal name");
            _displayName = Config.Bind("General", "Display Name", "",
                "Your name that will be displayed to other players");
            _address = Config.Bind("General", "Address", "127.0.0.1:1337", "");
            _displayOwnCat = Config.Bind("General", "Display Own Cat", false, "");
            _toggleChat = Config.Bind("General", "Toggle Chat Button", new KeyboardShortcut(KeyCode.T), "");
            _chatCapacity = Config.Bind("Chat", "Chat Capacity", 10,
                "Maximum amount of chat messages that can be displayed at the same time");
            _historyCapacity = Config.Bind("Chat", "History Capacity", 100,
                "Maximum amount of chat message texts that are stored in history to be able to resend them");
            _historyUp = Config.Bind("General", "History Up", new KeyboardShortcut(KeyCode.UpArrow), "");
            _historyDown = Config.Bind("General", "History Down", new KeyboardShortcut(KeyCode.DownArrow), "");
            _messageFadeOutDelay = Config.Bind("Chat", "Message Fade Out Delay", 5f, "");
            _messageFadeOutSpeed = Config.Bind("Chat", "Message Fade Out Speed", 1f, "");

            connected.Value = false;

            Client.Initialize();
            Commands.Initialize();
            _state = State.Normal;
            _scale = Client.GetScaleFromCatState(_state);
            connected.SettingChanged += (_, __) => {
                if(!SetConnected(connected.Value)) connected.Value = false;
            };

            Client.displayOwnCat = _displayOwnCat.Value;
            _displayOwnCat.SettingChanged += (_, __) => Client.displayOwnCat = _displayOwnCat.Value;

            Chat.Chat.messagesCapacity = _chatCapacity.Value;
            _chatCapacity.SettingChanged += (_, __) => Chat.Chat.messagesCapacity = _chatCapacity.Value;

            Chat.Chat.historyCapacity = _historyCapacity.Value;
            _historyCapacity.SettingChanged += (_, __) => Chat.Chat.historyCapacity = _historyCapacity.Value;

            Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;
            _messageFadeOutDelay.SettingChanged += (_, __) => Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;

            Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;
            _messageFadeOutSpeed.SettingChanged += (_, __) => Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;

            CatPartManager.awake += (caller, args) => {
                Cat.CatPartManager partManager = (Cat.CatPartManager)caller;
                
                if(!partManager.GetComponent<PlayerActor>()) return;
                
                Client.catSprite = args.noMetaballsPartTexture;
                Client.playerPartManager = partManager;
            };

            CatControls.awake += (caller, _) => {
                Cat.CatControls controls = (Cat.CatControls)caller;
                
                if(!controls.GetComponent<PlayerActor>()) return;
                
                GameObject catIcePrefab =
                    (GameObject)AccessTools.Field(typeof(Cat.CatControls), "catIcePrefab").GetValue(caller);
                SpriteRenderer catIceMainRenderer = (SpriteRenderer)AccessTools.Field(typeof(IceBlock), "mainSprite")
                    .GetValue(catIcePrefab.GetComponent<IceBlock>());

                Client.iceSprite = catIceMainRenderer.sprite;
                Client.iceColor = catIceMainRenderer.color;
                Client.playerControls = controls;

                controls.StateSwitchAction += state => {
                    _state = (State)state;
                    _scale = Client.GetScaleFromCatState(_state);
                };
            };

            CaLAPI.API.PauseScreen.roomInfoUpdated += (_, args) => {
                Client.state.room = args.newRoom;
            };

            PauseScreen.pauseScreenExitAction += () => {
                Client.state.room = null;
            };

            CaLAPI.API.CanvasManager.awake += (_, args) => {
                Client.nameTagCamera = Camera.main;
                GameObject nameTags = new GameObject("Name Tags") { layer = LayerMask.NameToLayer("UI") };
                DontDestroyOnLoad(nameTags);

                RectTransform nameTagsTransform = nameTags.AddComponent<RectTransform>();
                nameTagsTransform.anchoredPosition = Vector2.zero;
                nameTagsTransform.localScale = Vector3.one;

                Canvas canvas = nameTags.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Client.nameTagCamera;
                canvas.scaleFactor = 720f;

                Client.nameTags = nameTagsTransform;
            };

            Writer.awake += (_, args) => {
                Client.nameTagFont = args.font;
                Message.font = args.font;
            };

            CatControls.changedColor += (caller, args) => {
                Cat.CatControls controls = (Cat.CatControls)caller;
                
                if(!controls.GetComponent<PlayerActor>()) return;

                _color = args.newColor;
            };

            UI.initialized += (_, __) => {
                Chat.Chat.Initialize();
            };
        }

        private bool SetConnected(bool connected) {
            if(connected) {
                if(!Client.canConnect) return false;
                (string ip, int port) = ParseIp(_address.Value);
                Client.Connect(ip, port, _username.Value, _displayName.Value);
            }
            else Client.Disconnect();

            return true;
        }

        private void Update() {
            Client.Update();
            Client.UpdateAllNameTagsPositions();

            if(_toggleChat.Value.IsDown()) Chat.Chat.chatFocused = true;
            Chat.Chat.UpdateMessagesFadeOut();
            Chat.Chat.UpdateMessageHistory(_historyUp.Value.IsDown(), _historyDown.Value.IsDown());
        }

        private void FixedUpdate() {
            if(Pipe.catInPipe) Client.state.scale = Client.GetScaleFromCatState(State.Liquid);
            else {
                Client.state.scale = _scale;
                Client.state.color = _color;
            }
            Client.state.movementCatState = _state;
            Client.state.position = Client.currentCatPosition;
            if(!Client.playerControls) return;
            bool ice = CurrentIceUpdates.currentIce;
            Client.state.ice = ice;
            if(ice) {
                Client.state.color = Client.iceColor;
                Client.state.scale = CurrentIceUpdates.currentIce.Size.y * 3.5f;
            }
            
            if(_update) Client.SendStateDeltaToServer(Client.state);
            _update = !_update;
    }

        private void OnApplicationQuit() => SetConnected(false);

        private static (string, int) ParseIp(string ip) {
            string[] ipPort = ip.Split(':');
            return (ipPort[0], int.Parse(ipPort[1], CultureInfo.InvariantCulture));
        }
    }
}
