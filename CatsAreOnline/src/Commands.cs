using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using CaLAPI.API.Cat;

using UnityEngine;

namespace CatsAreOnline {
    public static class Commands {
        public static void Initialize() {
            Client.commandRegistry.Add("help", new Command(HelpCommand, "Prints this message. [INT] - page"));
            
            Client.commandRegistry.Add("debug", new Command(DebugCommand, "Prints some server and client debug info"));
            
            Client.commandRegistry.Add("players", new Command(PlayersCommand, "Prints online players. [INT] - page"));
            Client.commandRegistry.Add("list", Client.commandRegistry["players"]);
            
            Client.commandRegistry.Add("servers", new Command(ServersCommand, "nvm"));
            Client.commandRegistry.Add("discover", Client.commandRegistry["servers"]);
            
            Client.commandRegistry.Add("info", new Command(InfoCommand, "Prints some info about the server"));
            Client.commandRegistry.Add("server", Client.commandRegistry["info"]);
            
            Client.commandRegistry.Add("teleport",
                new Command(TeleportCommand,
                    "Teleports you to a specified location. FLOAT - x coord, FLOAT - y coord | STRING - player's username to teleport to"));
            Client.commandRegistry.Add("tp", Client.commandRegistry["teleport"]);
            
            Client.commandRegistry.Add("spectate",
                new Command(SpectateCommand,
                    "Spectate a player in the same room. STRING - player's username to spectate"));
            Client.commandRegistry.Add("spec", Client.commandRegistry["spectate"]);
        }

        private static void HelpCommand(params string[] args) {
            Dictionary<Command, string> actualCommands = new Dictionary<Command, string>();
            Dictionary<string, string> commandAliases = new Dictionary<string, string>();
            foreach(KeyValuePair<string, Command> command in Client.commandRegistry) {
                if(actualCommands.TryGetValue(command.Value, out string name)) commandAliases.Add(command.Key, name);
                else actualCommands.Add(command.Value, command.Key);
            }
            
            int pageCapacity = Chat.Chat.messagesCapacity - 2;
            int totalPages = actualCommands.Count / pageCapacity + 1;
            
            int page = 1;
            if(args.Length >= 1 && !int.TryParse(args[0], out page)) {
                Chat.Chat.AddErrorMessage("Invalid argument <b>0</b>");
                return;
            }

            page = Mathf.Clamp(page, 1, totalPages);
            
            Chat.Chat.AddMessage($"Page {page.ToString()}/{totalPages.ToString()}");
            int i = 0;
            foreach(KeyValuePair<Command, string> command in actualCommands) {
                if(i < (page - 1) * pageCapacity) {
                    ++i;
                    continue;
                }
                if(i >= Mathf.Min(actualCommands.Count, pageCapacity)) break;

                string commandName = command.Value;
                string aliases = string.Join(", ",
                    from alias in commandAliases where alias.Value == commandName select alias.Key);
                string description = command.Key.description;

                Chat.Chat.AddMessage($"{commandName} [{aliases}] - {description}");

                ++i;
            }
        }

        private static void DebugCommand(params string[] args) {
            if(args.Length == 0) Client.debug.enabled = !Client.debug.enabled;
            else Client.debug.enabled = true;

            ClientDebug.DataTypeFlag ToggleFlags(ClientDebug.DataTypeFlag current) =>
                current == ClientDebug.DataTypeFlag.All ? ClientDebug.DataTypeFlag.None : ClientDebug.DataTypeFlag.All;

            foreach(string arg in args) {
                string[] splitArg = arg.Split('_');
                if(splitArg.Length < 1 || splitArg.Length > 3) {
                    Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                    continue;
                }

                string argType = splitArg[0].ToLowerInvariant();
                if(argType != "client" && argType != "server") {
                    Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                    continue;
                }

                string argAction = splitArg.Length >= 3 ? splitArg[2].ToLowerInvariant() : null;
                if(argAction != null && argAction != "state") {
                    Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                    continue;
                }
                
                switch(splitArg.Length) {
                    case 3:
                    case 2:
                        string dataType = splitArg[1];
                        if(!Enum.TryParse(dataType, true, out ClientDebug.DataTypeFlag flag)) {
                            Chat.Chat.AddWarningMessage($"Unknown data type '{arg}'");
                            continue;
                        }

                        switch(splitArg.Length) {
                            case 3:
                                Chat.Chat.AddDebugMessage(
                                    argType == "client" ? $"[CLIENT] {Client.debug.client.HasFlag(flag).ToString()}" :
                                        $"[SERVER] {Client.debug.server.HasFlag(flag).ToString()}");
                                break;
                            case 2:
                                if(argType == "client") Client.debug.client ^= flag;
                                else Client.debug.server ^= flag;
                                break;
                        }
                        break;
                    case 1 when argType == "client": Client.debug.client = ToggleFlags(Client.debug.client);
                        break;
                    case 1: Client.debug.server = ToggleFlags(Client.debug.server);
                        break;
                }
            }
        }

        private static void PlayersCommand(params string[] args) {
            int pageCapacity = Chat.Chat.messagesCapacity - 2;
            int totalPages = Client.playerRegistry.Count / pageCapacity + 1;
            
            int page = 1;
            bool printUsername = false;
            if(args.Length >= 1 && !int.TryParse(args[0], out page)) {
                Chat.Chat.AddErrorMessage("Invalid argument <b>0</b>");
                return;
            }
            if(args.Length >= 2 && !bool.TryParse(args[1], out printUsername)) {
                Chat.Chat.AddErrorMessage("Invalid argument <b>1</b>");
                return;
            }

            page = Mathf.Clamp(page, 1, totalPages);
            
            Chat.Chat.AddMessage($"Page {page.ToString()}/{totalPages.ToString()}");
            int i = 0;
            foreach(Player player in Client.playerRegistry.Values) {
                if(i < (page - 1) * pageCapacity) {
                    ++i;
                    continue;
                }
                if(i >= Mathf.Min(Client.playerRegistry.Count, pageCapacity)) break;
                
                string displayName = $"{player.displayName} ";
                string username = printUsername ? $"({player.username}) " : "";
                string room = string.IsNullOrWhiteSpace(player.state.room) ? "" : $": {player.state.room}";
                
                Chat.Chat.AddMessage($"{displayName}{username}{room}");

                ++i;
            }
        }

        private static void ServersCommand(params string[] args) =>
            Chat.Chat.AddErrorMessage("This command is not yet implemented, sry :(");

        private static void InfoCommand(params string[] args) => Client.SendServerCommand("info");

        private static void TeleportCommand(params string[] args) {
            if(args.Length != 1 && args.Length != 2) {
                string length = args.Length.ToString(CultureInfo.InvariantCulture);
                Chat.Chat.AddErrorMessage($"Invalid argument count (<b>{length}</b>), should be <b>1</b> or <b>2</b>");
                return;
            }

            switch(args.Length) {
                case 1:
                    Player player =
                        (from ply in Client.playerRegistry where ply.Value.username == args[0] select ply.Value)
                        .FirstOrDefault();
                    if(player == null) {
                        Chat.Chat.AddErrorMessage($"Invalid argument <b>0</b> (player <b>{args[0]}</b> not found)");
                        return;
                    }

                    if(player.state.room != Client.state.room) {
                        Chat.Chat.AddErrorMessage(
                            $"Invalid argument <b>0</b> (player <b>{args[0]}</b> is in a different room)");
                        return;
                    }
                    
                    Client.playerPartManager.MoveCat(player.transform.position);
                    Chat.Chat.AddMessage($"Teleported <b>{Client.displayName}</b> to <b>{player.displayName}</b>");
                    break;
                case 2:
                    if(!float.TryParse(args[0], out float x) || !float.TryParse(args[1], out float y)) {
                        Chat.Chat.AddErrorMessage("Invalid argument");
                        return;
                    }
                    Vector2 newPosition = new Vector2(x, y);
                    Client.playerPartManager.MoveCat(newPosition);
                    Chat.Chat.AddMessage($"Teleported <b>{Client.displayName}</b> to <b>{newPosition.ToString()}</b>");
                    break;
            }
        }
        
        private static void SpectateCommand(params string[] args) {
            Player player;
            switch(args.Length) {
                case 0:
                    player = Client.spectating;
                    if(player == null) return;

                    FollowPlayer.followPlayerHead = player.restoreFollowPlayerHead;
                    FollowPlayer.customFollowTarget = player.restoreFollowTarget;
                    Client.spectating = null;
                    Chat.Chat.AddMessage($"Stopped spectating <b>{player.username}</b>");

                    break;
                case 1:
                    player =
                        (from ply in Client.playerRegistry where ply.Value.username == args[0] select ply.Value)
                        .FirstOrDefault();
                    if(player == null) {
                        Chat.Chat.AddErrorMessage($"Invalid argument <b>0</b> (player <b>{args[0]}</b> not found)");
                        return;
                    }

                    if(player.username == Client.username) {
                        Chat.Chat.AddErrorMessage("Invalid argument <b>0</b> (can't spectate yourself)");
                        return;
                    }

                    if(player.state.room != Client.state.room) {
                        Chat.Chat.AddErrorMessage(
                            $"Invalid argument <b>0</b> (player <b>{args[0]}</b> is in a different room)");
                        return;
                    }

                    Client.spectating = player;
                    player.restoreFollowPlayerHead = FollowPlayer.followPlayerHead;
                    player.restoreFollowTarget = FollowPlayer.customFollowTarget;
                    FollowPlayer.followPlayerHead = false;
                    FollowPlayer.customFollowTarget = player.transform;
                    Chat.Chat.AddMessage($"Spectating <b>{player.displayName}</b>");
                    break;
            }
        }
    }
}
