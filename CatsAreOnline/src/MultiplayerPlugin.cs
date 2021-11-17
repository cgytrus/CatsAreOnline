using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;

using BepInEx;
using BepInEx.Configuration;

using CalApi.API;

using Cat;

using CatsAreOnline.Shared;
using CatsAreOnline.SyncedObjects;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline;

[BepInPlugin("mod.cgytrus.plugins.calOnline", "Cats are Online", "0.5.2")]
[BepInDependency("mod.cgytrus.plugins.calapi", "0.2.2")]
[DefaultExecutionOrder(int.MaxValue)]
internal class MultiplayerPlugin : BaseUnityPlugin {
    public static CapturedData capturedData { get; private set; } = null!;
    public static ConfigEntry<bool>? connected { get; private set; }
    private ConfigEntry<string> _username;
    private ConfigEntry<string> _displayName;
    private ConfigEntry<string> _address;
    private ConfigEntry<bool> _displayOwnCat;
    private ConfigEntry<bool> _interactions;
    private ConfigEntry<bool> _attachOwnNameTag;
    private ConfigEntry<KeyboardShortcut> _toggleChat;
    private ConfigEntry<int> _chatCapacity;
    private ConfigEntry<int> _historyCapacity;
    private ConfigEntry<KeyboardShortcut> _historyUp;
    private ConfigEntry<KeyboardShortcut> _historyDown;
    private ConfigEntry<float> _messageFadeOutDelay;
    private ConfigEntry<float> _messageFadeOutSpeed;

    private ConfigEntry<float> _remoteUpdateTime;
    private ConfigEntry<float> _interpolationDelay;
    private ConfigEntry<float> _extrapolationTime;
    private ConfigEntry<SyncedObject.InterpolationSettings.MultipleArrivalsHandling>
        _interpolationMultipleArrivalsHandling;

    private readonly Client _client;

    private float _updateTime;
    private float _updateTimer;

    // they're initialized in the methods dumbass
#pragma warning disable CS8618
    public MultiplayerPlugin() {
#pragma warning restore CS8618
        CreateSettings();
        Logger.LogInfo("Creating client");
        _client = new Client(Logger);
        SetupSettings();
    }

    private void Awake() {
        Logger.LogInfo("Initializing commands");
        Commands.Initialize();

        ApplyHooks();

        capturedData = new CapturedData(Logger, _client);
    }

    private void CreateSettings() {
        Logger.LogInfo("Creating settings");

        connected = Config.Bind("General", "Connected", false, "");
        _username = Config.Bind("General", "Username", "", "Your internal name");
        _displayName = Config.Bind("General", "Display Name", "",
            "Your name that will be displayed to other players");
        _address = Config.Bind("General", "Address", "localhost", "");
        _displayOwnCat = Config.Bind("General", "Display Own Cat", false, "");
        _interactions = Config.Bind("General", "Interactions", false, "[EXPERIMENTAL]");
        _attachOwnNameTag = Config.Bind("General", "Attach Own Name Tag", true, "");
        _toggleChat = Config.Bind("General", "Toggle Chat Button", new KeyboardShortcut(KeyCode.T), "");
        _chatCapacity = Config.Bind("Chat", "Chat Capacity", 10,
            "Maximum amount of chat messages that can be displayed at the same time");
        _historyCapacity = Config.Bind("Chat", "History Capacity", 100,
            "Maximum amount of chat message texts that are stored in history to be able to resend them");
        _historyUp = Config.Bind("General", "History Up", new KeyboardShortcut(KeyCode.UpArrow), "");
        _historyDown = Config.Bind("General", "History Down", new KeyboardShortcut(KeyCode.DownArrow), "");
        _messageFadeOutDelay = Config.Bind("Chat", "Message Fade Out Delay", 5f, "");
        _messageFadeOutSpeed = Config.Bind("Chat", "Message Fade Out Speed", 1f, "");

        _remoteUpdateTime = Config.Bind("Advanced", "Remote Update Time", 0.05f, "");
        _interpolationDelay = Config.Bind("Advanced", "Interpolation Delay", 0.1f, "");
        _extrapolationTime = Config.Bind("Advanced", "Extrapolation Time", 0.25f, "");
        _interpolationMultipleArrivalsHandling = Config.Bind("Advanced", "Multiple Arrivals Handling",
            SyncedObject.InterpolationSettings.MultipleArrivalsHandling.Drop,
            "What to do when multiple state change messages arrive at the same time.");
    }

    private void SetupSettings() {
        Logger.LogInfo("Setting settings up");

        connected!.Value = false;
        connected.SettingChanged += (_, _) => {
            if(!SetConnected(connected.Value)) connected.Value = false;
        };

        _client.displayOwnCat = _displayOwnCat.Value;
        _displayOwnCat.SettingChanged += (_, _) => _client.displayOwnCat = _displayOwnCat.Value;

        _client.interactions = _interactions.Value;
        _interactions.SettingChanged += (_, _) => _client.interactions = _interactions.Value;

        _client.attachOwnNameTag = _displayOwnCat.Value;
        _attachOwnNameTag.SettingChanged += (_, _) => _client.attachOwnNameTag = _attachOwnNameTag.Value;

        Chat.Chat.messagesCapacity = _chatCapacity.Value;
        _chatCapacity.SettingChanged += (_, _) => Chat.Chat.messagesCapacity = _chatCapacity.Value;

        Chat.Chat.historyCapacity = _historyCapacity.Value;
        _historyCapacity.SettingChanged += (_, _) => Chat.Chat.historyCapacity = _historyCapacity.Value;

        Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;
        _messageFadeOutDelay.SettingChanged += (_, _) => Chat.Chat.fadeOutDelay = _messageFadeOutDelay.Value;

        Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;
        _messageFadeOutSpeed.SettingChanged += (_, _) => Chat.Chat.fadeOutSpeed = _messageFadeOutSpeed.Value;

        _updateTime = _remoteUpdateTime.Value;
        _remoteUpdateTime.SettingChanged +=
            (_, _) => _updateTime = _remoteUpdateTime.Value;

        SyncedObject.interpolationSettings.delay = _interpolationDelay.Value;
        _interpolationDelay.SettingChanged +=
            (_, _) => SyncedObject.interpolationSettings.delay = _interpolationDelay.Value;

        SyncedObject.interpolationSettings.extrapolationTime = _extrapolationTime.Value;
        _extrapolationTime.SettingChanged +=
            (_, _) => SyncedObject.interpolationSettings.extrapolationTime = _extrapolationTime.Value;

        SyncedObject.interpolationSettings.multipleArrivalsHandling = _interpolationMultipleArrivalsHandling.Value;
        _interpolationMultipleArrivalsHandling.SettingChanged +=
            (_, _) => SyncedObject.interpolationSettings.multipleArrivalsHandling =
                _interpolationMultipleArrivalsHandling.Value;
    }

    private void ApplyHooks() {
        Logger.LogInfo("Applying hooks");

        On.Cat.CatControls.Awake += (orig, self) => {
            orig(self);
            if(!self.GetComponent<PlayerActor>()) return;

            SubscribeToCatControlsEvents(self);
        };

        SubscribeToLocationUpdates();

        UI.initialized += (_, _) => {
            Chat.Chat.Initialize(_client);
            _client.InitializeNameTags();
        };
    }

    private void SubscribeToLocationUpdates() {
        void UpdateWorldPack() {
            if(!_client.ownPlayer.UpdateWorldPack()) return;
            _client.UpdateWorldPack();
            _client.AddCat();
        }
        void UpdateWorld() {
            UpdateWorldPack();
            if(!_client.ownPlayer.UpdateWorld()) return;
            _client.UpdateWorld();
            _client.AddCat();
        }
        void UpdateRoom() {
            if(!_client.ownPlayer.UpdateRoom()) return;
            _client.UpdateRoom();
            _client.AddCat();
        }
        void UpdateLocation() {
            UpdateWorld();
            UpdateRoom();
        }
        WorldPackSettings.WorldPackSettingsLoadedAction += UpdateWorldPack;
        // lqd why you haaardcooode aaaaaaaa
        On.WorldPackSettings.LoadOfficialPackSettings += orig => {
            orig();
            UpdateWorldPack();
        };
        WorldSettings.WorldSettingsLoadedAction += UpdateWorld;
        RSSystem.RoomSettings.RoomSettingsLoadedAction += UpdateRoom;
        RoomEditor.PlayModeActivatedAction += UpdateLocation;

        void RoomUnloaded() {
            if(!_client.ownPlayer.ResetLocation()) return;
            _client.UpdateWorldPack();
            _client.UpdateWorld();
            _client.UpdateRoom();
        }
        PauseScreen.pauseScreenExitAction += RoomUnloaded;
        RoomEditor.EditModeActivatedAction += RoomUnloaded;
    }

    private void SubscribeToCatControlsEvents(CatControls controls) {
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
    }

    private bool SetConnected(bool connected) {
        if(connected) {
            if(!_client.canConnect) return false;
            IPEndPoint ip = ParseIp(_address.Value);
            _client.Connect(ip, _username.Value, _displayName.Value);
        }
        else _client.Disconnect();

        return true;
    }

    private void Update() {
        _client.Update();

        _client.catState.Update();
        _client.companionState?.Update();
        if(_updateTimer >= _updateTime) {
            _updateTimer -= _updateTime;
            _client.SendStateDeltaToServer(_client.catId, _client.catState);
            if(_client.companionState is not null && _client.companionId != Guid.Empty)
                _client.SendStateDeltaToServer(_client.companionId, _client.companionState);
        }
        _updateTimer += Time.unscaledDeltaTime;

        if(_toggleChat.Value.IsDown()) Chat.Chat.chatFocused = true;
        Chat.Chat.UpdateMessagesFadeOut();
        Chat.Chat.UpdateMessageHistory(_historyUp.Value.IsDown(), _historyDown.Value.IsDown());
    }

    private void LateUpdate() => _client.UpdateAllNameTagsPositions();

    private void OnApplicationQuit() => SetConnected(false);

    private static IPEndPoint ParseIp(string ip) {
        string[] ipPort = ip.Split(':');
        if(ipPort.Length == 7) Chat.Chat.AddMessage($"{ipPort[3]} (why)");
        return ipPort.Length <= 1 ? NetUtility.Resolve(ipPort[0], SharedConfig.Port) :
            NetUtility.Resolve(ipPort[0], int.Parse(ipPort[1], CultureInfo.InvariantCulture));
    }

    public static string FindLocationPath(string worldPackGuid, string worldGuid, string roomGuid,
        out bool isOfficial) {
        isOfficial = worldPackGuid == "8651f68f-f757-4108-a6f9-28afd861a110";
        return isOfficial ? FindOfficialLocationPath(worldGuid, roomGuid) :
            FindCommunityLocationPath(worldPackGuid, worldGuid, roomGuid);
    }

    private static string FindOfficialLocationPath(string worldGuid, string roomGuid) {
        string packPath = Path.Combine(Application.temporaryCachePath, "Official Maps");
        string worldPath = FindWorldPathInPack(packPath, worldGuid);
        string[] roomPath = FindRoomPathInWorld(worldPath, roomGuid).Split(Path.DirectorySeparatorChar);
        int startIndex = Array.IndexOf(roomPath, "Official Maps");
        string[] officialRoomPath = new string[roomPath.Length - startIndex];
        Array.Copy(roomPath, startIndex, officialRoomPath, 0, officialRoomPath.Length);
        return Path.Combine(officialRoomPath);
    }

    private static string FindCommunityLocationPath(string packGuid, string worldGuid, string roomGuid) {
        string packPath = FindCommunityPack(packGuid);
        string worldPath = FindWorldPathInPack(packPath, worldGuid);
        string roomPath = FindRoomPathInWorld(worldPath, roomGuid);
        return roomPath;
    }

    private static string FindCommunityPack(string packGuid) {
        string[] packsPaths = {
            Path.Combine(Application.persistentDataPath, "Packs", "Imported", packGuid),
            Path.Combine(Application.persistentDataPath, "Custom")
        };
        foreach(string packsPath in packsPaths) {
            string? findCommunityPack = FindCommunityPack(packsPath, packGuid);
            if(findCommunityPack != null) return findCommunityPack;
        }

        throw new InvalidDataException($"Pack {packGuid} was not found.");
    }

    private static string? FindCommunityPack(string packsPath, string packGuid) {
        if(!Directory.Exists(packsPath)) throw new InvalidDataException($"{packsPath} doesn't exist.");
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach(string directory in Directory.GetDirectories(packsPath)) {
            string settingsPath = Path.Combine(directory, "settings.data");
            if(!File.Exists(settingsPath)) continue;
            string currentGuid = JsonUtility.FromJson<WorldPackSettings.Settings>(File.ReadAllText(settingsPath))
                .worldPackGUID;
            if(currentGuid != packGuid) continue;
            return directory;
        }

        return null;
    }

    private static string FindWorldPathInPack(string packPath, string worldGuid) {
        if(!Directory.Exists(packPath))
            throw new InvalidDataException($"{packPath} doesn't exist.");
        foreach(string directory in Directory.GetDirectories(packPath)) {
            string settingsPath = Path.Combine(directory, "settings.data");
            if(!File.Exists(settingsPath)) continue;
            string currentGuid = JsonUtility.FromJson<WorldSettings.Settings>(File.ReadAllText(settingsPath))
                .worldGUID;
            if(currentGuid != worldGuid) continue;
            return directory;
        }

        throw new InvalidDataException($"World {worldGuid} was not found in {packPath}.");
    }

    private static string FindRoomPathInWorld(string worldPath, string roomGuid) {
        if(!Directory.Exists(worldPath))
            throw new InvalidDataException($"{worldPath} doesn't exist.");
        foreach(string directory in Directory.GetDirectories(worldPath)) {
            string settingsPath = Path.Combine(directory, "settings.data");
            if(!File.Exists(settingsPath)) continue;
            string currentGuid = JsonUtility
                .FromJson<RSSystem.RoomSettings.Settings>(File.ReadAllText(settingsPath)).roomGUID;
            if(currentGuid != roomGuid) continue;
            return directory;
        }

        throw new InvalidDataException($"Room {roomGuid} was not found in {worldPath}.");
    }
}
