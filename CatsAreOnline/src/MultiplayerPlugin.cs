using System;
using System.Globalization;
using System.Reflection;

using BepInEx;
using BepInEx.Configuration;

using CaLAPI.API;
using CaLAPI.API.ProphecySystem;

using Cat;

using CatsAreOnline.Chat;
using CatsAreOnline.Patches;
using CatsAreOnline.Shared;
using CatsAreOnline.SyncedObjects;

using HarmonyLib;

using UnityEngine;

using CatControls = CaLAPI.API.Cat.CatControls;
using CatPartManager = CaLAPI.API.Cat.CatPartManager;

namespace CatsAreOnline {
    [BepInPlugin("mod.cgytrus.plugin.calOnline", "Cats are Online", "0.3.0")]
    [BepInDependency("mod.cgytrus.plugins.calapi", "0.1.9")]
    public class MultiplayerPlugin : BaseUnityPlugin {
        public static ConfigEntry<bool> connected;
        private ConfigEntry<string> _username;
        private ConfigEntry<string> _displayName;
        private ConfigEntry<string> _address;
        private ConfigEntry<bool> _displayOwnCat;
        private ConfigEntry<bool> _interactions;
        private ConfigEntry<KeyboardShortcut> _toggleChat;
        private ConfigEntry<int> _chatCapacity;
        private ConfigEntry<int> _historyCapacity;
        private ConfigEntry<KeyboardShortcut> _historyUp;
        private ConfigEntry<KeyboardShortcut> _historyDown;
        private ConfigEntry<float> _messageFadeOutDelay;
        private ConfigEntry<float> _messageFadeOutSpeed;
        
        private ConfigEntry<SyncedObject.InterpolationSettings.InterpolationMode> _interpolationMode;
        private ConfigEntry<double> _interpolationTime;
        private ConfigEntry<int> _interpolationPacketsToAverage;
        private ConfigEntry<double> _interpolationMaxTime;

        private Client _client;

        public static State catState { get; private set; }
        public static float catScale { get; private set; }
        public static Color catColor { get; private set; }
        
        private bool _update;

        private FieldInfo _companionFieldInfo = AccessTools.Field(typeof(Cat.CatControls), "companion");

        private FieldInfo _liquidParticleMaterialFieldInfo =
            AccessTools.Field(typeof(Cat.CatControls), "liquidParticleMaterial");

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
            _interactions = Config.Bind("General", "Interactions", false, "[EXPERIMENTAL]");
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
                SyncedObject.InterpolationSettings.InterpolationMode.Lerp, "");
            _interpolationTime = Config.Bind("Advanced", "Interpolation Time", 3d, "");
            _interpolationPacketsToAverage = Config.Bind("Advanced", "Interpolation Packets To Average", 20, "");
            _interpolationMaxTime = Config.Bind("Advanced", "Interpolation Max Time", 10d, "");

            connected.Value = false;

            _client = new Client();
            PatchesClientProvider.client = _client;

            Commands.Initialize();
            
            catState = State.Normal;
            catScale = Client.GetScaleFromCatState(catState);
            connected.SettingChanged += (_, __) => {
                if(!SetConnected(connected.Value)) connected.Value = false;
            };

            _client.displayOwnCat = _displayOwnCat.Value;
            _displayOwnCat.SettingChanged += (_, __) => _client.displayOwnCat = _displayOwnCat.Value;

            _client.playerCollisions = _interactions.Value;
            _interactions.SettingChanged += (_, __) => _client.playerCollisions = _interactions.Value;

            Chat.Chat.messagesCapacity = _chatCapacity.Value;
            _chatCapacity.SettingChanged += (_, __) => Chat.Chat.messagesCapacity = _chatCapacity.Value;

            Chat.Chat.historyCapacity = _historyCapacity.Value;
            _historyCapacity.SettingChanged += (_, __) => Chat.Chat.historyCapacity = _historyCapacity.Value;

            Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;
            _messageFadeOutDelay.SettingChanged += (_, __) => Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;

            Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;
            _messageFadeOutSpeed.SettingChanged += (_, __) => Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;

            SyncedObject.interpolationSettings = new SyncedObject.InterpolationSettings(_interpolationMode.Value,
                _interpolationTime.Value, _interpolationPacketsToAverage.Value, _interpolationMaxTime.Value);
            _interpolationMode.SettingChanged += (_, __) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(_interpolationMode.Value, SyncedObject.interpolationSettings.time,
                    SyncedObject.interpolationSettings.packetsToAverage, SyncedObject.interpolationSettings.maxTime);
            _interpolationTime.SettingChanged += (_, __) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(SyncedObject.interpolationSettings.mode, _interpolationTime.Value,
                    SyncedObject.interpolationSettings.packetsToAverage, SyncedObject.interpolationSettings.maxTime);
            _interpolationPacketsToAverage.SettingChanged += (_, __) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(SyncedObject.interpolationSettings.mode, SyncedObject.interpolationSettings.time,
                    _interpolationPacketsToAverage.Value, SyncedObject.interpolationSettings.maxTime);
            _interpolationMaxTime.SettingChanged += (_, __) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(SyncedObject.interpolationSettings.mode, SyncedObject.interpolationSettings.time,
                    SyncedObject.interpolationSettings.packetsToAverage, _interpolationMaxTime.Value);

            CatPartManager.awake += (caller, args) => {
                Cat.CatPartManager partManager = (Cat.CatPartManager)caller;
                
                if(!partManager.GetComponent<PlayerActor>()) return;
                
                _client.catSprite = args.noMetaballsPartTexture;
                _client.playerPartManager = partManager;
            };

            CatControls.awake += (caller, _) => {
                Cat.CatControls controls = (Cat.CatControls)caller;
                
                if(!controls.GetComponent<PlayerActor>()) return;

                catColor = ((Material)_liquidParticleMaterialFieldInfo.GetValue(caller)).color;
                
                GameObject catIcePrefab =
                    (GameObject)AccessTools.Field(typeof(Cat.CatControls), "catIcePrefab").GetValue(caller);
                SpriteRenderer catIceMainRenderer = (SpriteRenderer)AccessTools.Field(typeof(IceBlock), "mainSprite")
                    .GetValue(catIcePrefab.GetComponent<IceBlock>());

                _client.iceSprite = catIceMainRenderer.sprite;
                _client.iceColor = catIceMainRenderer.color;
                _client.playerControls = controls;

                controls.StateSwitchAction += state => {
                    catState = (State)state;
                    catScale = Client.GetScaleFromCatState(catState);
                };

                controls.ControlTargetChangedAction += target => {
                    switch(target) {
                        case Cat.CatControls.ControlTarget.Player:
                            _client.ChangeControllingObject(_client.catId);
                            break;
                        case Cat.CatControls.ControlTarget.Companion:
                            if(_client.companionId == Guid.Empty) break;
                            _client.ChangeControllingObject(_client.companionId);
                            break;
                    }
                };

                controls.CompanionToggeledAction += enabled => {
                    if(enabled) {
                        CompanionSyncedObjectState.companion = (Companion)_companionFieldInfo.GetValue(caller);
                        _client.companionId = Guid.NewGuid();
                        _client.companionState = new CompanionSyncedObjectState { client = _client };
                        _client.companionState.Update();
                        _client.AddSyncedObject(_client.companionId, SyncedObjectType.Companion, _client.companionState,
                            true);
                    }
                    else {
                        CompanionSyncedObjectState.companion = null;
                        _client.companionState = null;
                        _client.RemoveSyncedObject(_client.companionId);
                        _client.companionId = Guid.Empty;
                    }
                };
            };

            CaLAPI.API.PauseScreen.roomInfoUpdated += (_, args) => {
                if(args.newRoom == _client.ownPlayer.room) return;
                _client.ownPlayer.room = args.newRoom;
                _client.UpdateRoom();
            };

            PauseScreen.pauseScreenExitAction += () => {
                _client.ownPlayer.room = null;
                _client.UpdateRoom();
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

                catColor = args.newColor;
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
            _client.catState.Update();
            _client.companionState?.Update();
            if(_update) {
                _client.SendStateDeltaToServer(_client.catId, _client.catState);
                if(_client.companionState != null && _client.companionId != Guid.Empty)
                    _client.SendStateDeltaToServer(_client.companionId, _client.companionState);
            }
            _update = !_update;
        }

        private void OnApplicationQuit() => SetConnected(false);

        private static (string, int) ParseIp(string ip) {
            string[] ipPort = ip.Split(':');
            return (ipPort[0], int.Parse(ipPort[1], CultureInfo.InvariantCulture));
        }
    }
}
