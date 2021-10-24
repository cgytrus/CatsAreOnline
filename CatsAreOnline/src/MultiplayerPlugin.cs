using System;
using System.Globalization;
using System.Reflection;

using BepInEx;
using BepInEx.Configuration;

using CalApi.API;
using CalApi.API.Cat;

using Cat;

using CatsAreOnline.SyncedObjects;

using HarmonyLib;

using Mono.Cecil.Cil;

using MonoMod.Cil;

using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline {
    internal readonly struct ParsedIp {
        public string ip { get; }
        public int port { get; }

        public ParsedIp(string ip, int port) {
            this.ip = ip;
            this.port = port;
        }
    }

    [BepInPlugin("mod.cgytrus.plugins.calOnline", "Cats are Online", "0.4.0")]
    [BepInDependency("mod.cgytrus.plugins.calapi", "0.2.0")]
    internal class MultiplayerPlugin : BaseUnityPlugin {
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

        private bool _update;

        private void Awake() {
            CreateSettings();
            Logger.LogInfo("Creating client");
            _client = new Client();
            CapturedData.catState = State.Normal;
            CapturedData.catScale = CapturedData.catState.GetScale();
            SetupSettings();

            Logger.LogInfo("Initializing commands");
            Commands.Initialize();

            ApplyHooks();
            Logger.LogInfo("Applying patches");
            Util.ApplyAllPatches();
        }

        private void CreateSettings() {
            Logger.LogInfo("Creating settings");

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
        }

        private void SetupSettings() {
            Logger.LogInfo("Setting settings up");
            
            connected.Value = false;
            connected.SettingChanged += (_, _) => {
                if(!SetConnected(connected.Value)) connected.Value = false;
            };

            _client.displayOwnCat = _displayOwnCat.Value;
            _displayOwnCat.SettingChanged += (_, _) => _client.displayOwnCat = _displayOwnCat.Value;

            _client.playerCollisions = _interactions.Value;
            _interactions.SettingChanged += (_, _) => _client.playerCollisions = _interactions.Value;

            Chat.Chat.messagesCapacity = _chatCapacity.Value;
            _chatCapacity.SettingChanged += (_, _) => Chat.Chat.messagesCapacity = _chatCapacity.Value;

            Chat.Chat.historyCapacity = _historyCapacity.Value;
            _historyCapacity.SettingChanged += (_, _) => Chat.Chat.historyCapacity = _historyCapacity.Value;

            Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;
            _messageFadeOutDelay.SettingChanged += (_, _) => Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;

            Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;
            _messageFadeOutSpeed.SettingChanged += (_, _) => Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;

            SyncedObject.interpolationSettings = new SyncedObject.InterpolationSettings(_interpolationMode.Value,
                _interpolationTime.Value, _interpolationPacketsToAverage.Value, _interpolationMaxTime.Value);
            _interpolationMode.SettingChanged += (_, _) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(_interpolationMode.Value, SyncedObject.interpolationSettings.time,
                    SyncedObject.interpolationSettings.packetsToAverage, SyncedObject.interpolationSettings.maxTime);
            _interpolationTime.SettingChanged += (_, _) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(SyncedObject.interpolationSettings.mode, _interpolationTime.Value,
                    SyncedObject.interpolationSettings.packetsToAverage, SyncedObject.interpolationSettings.maxTime);
            _interpolationPacketsToAverage.SettingChanged += (_, _) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(SyncedObject.interpolationSettings.mode,
                    SyncedObject.interpolationSettings.time,
                    _interpolationPacketsToAverage.Value, SyncedObject.interpolationSettings.maxTime);
            _interpolationMaxTime.SettingChanged += (_, _) => SyncedObject.interpolationSettings =
                new SyncedObject.InterpolationSettings(SyncedObject.interpolationSettings.mode,
                    SyncedObject.interpolationSettings.time,
                    SyncedObject.interpolationSettings.packetsToAverage, _interpolationMaxTime.Value);
        }

        private void ApplyHooks() {
            Logger.LogInfo("Applying hooks");

            FieldInfo noMetaballsPartTexture = AccessTools.Field(typeof(Cat.CatPartManager), "noMetaballsPartTexture");
            On.Cat.CatPartManager.Awake += (orig, self) => {
                orig(self);
                if(!self.GetComponent<PlayerActor>()) return;

                CapturedData.catSprite = (Sprite)noMetaballsPartTexture!.GetValue(self);
                CapturedData.catPartManager = self;
            };

            On.Cat.CatControls.Awake += (orig, self) => {
                orig(self);
                if(!self.GetComponent<PlayerActor>()) return;

                CapturedData.catColor = self.GetCurrentConfigurationColor();

                GameObject catIcePrefab =
                    (GameObject)AccessTools.Field(typeof(Cat.CatControls), "catIcePrefab").GetValue(self);
                SpriteRenderer catIceMainRenderer =
                    (SpriteRenderer)AccessTools.Field(typeof(IceBlock), "mainSprite")
                    .GetValue(catIcePrefab.GetComponent<IceBlock>());

                CapturedData.iceSprite = catIceMainRenderer.sprite;
                CapturedData.iceColor = catIceMainRenderer.color;
                CapturedData.catControls = self;

                SubscribeToCatControlsEvents(self);
            };

            // TODO: change to use room guid for actual room check
            FieldInfo roomInfo = AccessTools.Field(typeof(PauseScreen), "roomInfo");
            On.PauseScreen.UpdateRoomInfo += (orig, self, path) => {
                orig(self, path);
                string newRoom = ((Text)roomInfo!.GetValue(self)).text;
                if(newRoom == _client.ownPlayer.room) return;
                _client.ownPlayer.room = newRoom;
                _client.UpdateRoom();
            };

            PauseScreen.pauseScreenExitAction += () => {
                _client.ownPlayer.room = null;
                _client.UpdateRoom();
            };

            On.CanvasManager.Awake += (orig, self) => {
                orig(self);
                _client.InitializeNameTags();
            };

            // ReSharper disable once SuggestBaseTypeForParameter
            void ChangedColor(Cat.CatControls self, Color newColor) {
                if(!self.GetComponent<PlayerActor>()) return;
                CapturedData.catColor = newColor;
            }

            // MM HookGen can't hook properly cuz of the arg being an internal struct so we do il manually
            IL.Cat.CatControls.ApplyConfiguration += il => {
                ILCursor cursor = new(il);
                cursor.GotoNext(code => code.MatchRet());
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<Cat.CatControls, Color>>(CatControlsExtensions.GetCurrentConfigurationColor);
                cursor.EmitDelegate<Action<Cat.CatControls, Color>>(ChangedColor);
            };
            /*On.Cat.CatControls.ApplyConfiguration += (orig, self, configuration) => {
                orig(self, configuration);
                ChangedColor(self, self.GetCurrentConfigurationColor());
            };*/
            On.Cat.CatControls.ApplyColor += (orig, self, color, featureColor) => {
                orig(self, color, featureColor);
                ChangedColor(self, color);
            };

            UI.initialized += (_, _) => { Chat.Chat.Initialize(_client); };
        }

        private void SubscribeToCatControlsEvents(CatControls controls) {
            controls.StateSwitchAction += state => {
                CapturedData.catState = (State)state;
                CapturedData.catScale = CapturedData.catState.GetScale();
            };

            controls.ControlTargetChangedAction += ControlTargetChanged;
            void ControlTargetChanged(CatControls.ControlTarget target) {
                switch(target) {
                    case Cat.CatControls.ControlTarget.Player:
                        _client.ChangeControllingObject(_client.catId);
                        break;
                    case Cat.CatControls.ControlTarget.Companion:
                        if(_client.companionId == Guid.Empty) break;
                        _client.ChangeControllingObject(_client.companionId);
                        break;
                }
            }

            controls.CompanionToggeledAction += enabled => {
                if(enabled) {
                    Companion companion =
                        (Companion)AccessTools.Field(typeof(Cat.CatControls), "companion").GetValue(controls);
                    CapturedData.companionTransform = companion.transform;
                    SpriteRenderer renderer = CapturedData.companionTransform.Find("Companion Sprite")
                        .GetComponent<SpriteRenderer>();
                    CapturedData.companionSprite = renderer.sprite;
                    CapturedData.companionColor = renderer.color;

                    _client.AddCompanion();
                }
                else {
                    CapturedData.companionTransform = null;
                    _client.RemoveCompanion();
                }
            };
        }

        private bool SetConnected(bool connected) {
            if(connected) {
                if(!_client.canConnect) return false;
                ParsedIp ip = ParseIp(_address.Value);
                _client.Connect(ip.ip, ip.port, _username.Value, _displayName.Value);
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

        private static ParsedIp ParseIp(string ip) {
            string[] ipPort = ip.Split(':');
            return new ParsedIp(ipPort[0], int.Parse(ipPort[1], CultureInfo.InvariantCulture));
        }
    }
}
