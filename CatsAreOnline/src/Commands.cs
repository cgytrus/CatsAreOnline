﻿using System;
using System.Collections.Generic;
using System.Linq;

using CalApi.API.Cat;

using NBrigadier;
using NBrigadier.Arguments;
using NBrigadier.Builder;
using NBrigadier.Context;
using NBrigadier.Tree;

using UnityEngine;

using Object = UnityEngine.Object;

namespace CatsAreOnline {
    public static class Commands {
        public static CommandDispatcher<Client> dispatcher { get; } = new();

        private static readonly Dictionary<CommandNode<Client>, string> descriptions = new();
        
        public static void Initialize() {
            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("help")
                .Then(RequiredArgumentBuilder<Client, int>.Argument("page", IntegerArgumentType.Integer(1))
                    .Executes(context => {
                    HelpCommand(IntegerArgumentType.GetInteger(context, "page") - 1, context.Source);
                    return 1;
                }))
                .Then(RequiredArgumentBuilder<Client, string>.Argument("command", StringArgumentType.Word())
                    .Executes(context => {
                        HelpCommand(StringArgumentType.GetString(context, "command"), context.Source);
                        return 1;
                    }))
                .Executes(context => {
                HelpCommand(0, context.Source);
                return 1;
            })), "Prints this message.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("debug")
                .Then(RequiredArgumentBuilder<Client, string>.Argument("flags", StringArgumentType.String())
                    .Executes(context => {
                        context.Source.debug.enabled = true;

                        foreach(string arg in StringArgumentType.GetString(context, "flags").Split(' '))
                            DebugCommand(arg, context.Source);
                        return 1;
                    }))
                .Executes(context => {
                    context.Source.debug.enabled = !context.Source.debug.enabled;
                    return 1;
                })), "Prints some server and client debug info");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("players")
                .Then(RequiredArgumentBuilder<Client, int>.Argument("page", IntegerArgumentType.Integer(1))
                    .Then(RequiredArgumentBuilder<Client, bool>.Argument("printUsername", BoolArgumentType.Bool())
                        .Executes(context => {
                            PlayersCommand(IntegerArgumentType.GetInteger(context, "page") - 1,
                                BoolArgumentType.GetBool(context, "printUsername"), context.Source);
                            return 1;
                        }))
                    .Executes(context => {
                        PlayersCommand(IntegerArgumentType.GetInteger(context, "page") - 1, false, context.Source);
                        return 1;
                    }))
                .Executes(context => {
                    PlayersCommand(0, false, context.Source);
                    return 1;
                })), "Prints online players.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("info")
                    .Executes(RedirectToServer)), "Prints some info about the server");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("teleport")
                .Then(RequiredArgumentBuilder<Client, string>.Argument("username", StringArgumentType.String())
                    .Executes(context => {
                        TeleportCommand(context, StringArgumentType.GetString(context, "username"));
                        return 1;
                    }))
                .Then(RequiredArgumentBuilder<Client, float>.Argument("x", FloatArgumentType.FloatArg())
                    .Then(RequiredArgumentBuilder<Client, float>.Argument("y", FloatArgumentType.FloatArg())
                        .Executes(context => {
                            TeleportCommand(context,
                                new Vector2(FloatArgumentType.GetFloat(context, "x"),
                                    FloatArgumentType.GetFloat(context, "y")));
                            return 1;
                        })))
            ), "Teleports you to the specified position.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("spectate")
                .Then(RequiredArgumentBuilder<Client, string>.Argument("username", StringArgumentType.String())
                    .Executes(context => {
                        SpectateCommand(context, StringArgumentType.GetString(context, "username"));
                        return 1;
                    }))
                .Executes(context => {
                    SpectateCommand(context);
                    return 1;
                })), "Spectate a player in the same room as you.");
            
            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("enter")
                .Then(RequiredArgumentBuilder<Client, string>
                    .Argument("username", StringArgumentType.String())
                    .Executes(context => {
                        EnterCommand(context, StringArgumentType.GetString(context, "username"));
                        return 1;
                    }))
                .Then(RequiredArgumentBuilder<Client, string>.Argument("worldPackGuid", StringArgumentType.String())
                    .Then(RequiredArgumentBuilder<Client, string>.Argument("worldGuid", StringArgumentType.String())
                        .Then(RequiredArgumentBuilder<Client, string>.Argument("roomGuid", StringArgumentType.String())
                            .Executes(context => {
                                EnterCommand(context, StringArgumentType.GetString(context, "worldPackGuid"),
                                    StringArgumentType.GetString(context, "worldGuid"),
                                    StringArgumentType.GetString(context, "roomGuid"));
                                return 1;
                            }))))), "Enter the specified room.");
        }

        private static int RedirectToServer(CommandContext<Client> context) {
            context.Source.SendServerCommand(context.Input);
            return 1;
        }

        private static void PrintCollection<T>(ICollection<T> collection, int page, Func<T, string> selector) {
            int pageCapacity = Chat.Chat.messagesCapacity - 2;
            int totalPages = collection.Count / pageCapacity;
            
            page = Mathf.Clamp(page, 0, totalPages);
            
            Chat.Chat.AddMessage($"Page {(page + 1).ToString()}/{(totalPages + 1).ToString()}");
            int i = 0;
            foreach(T item in collection) {
                if(i < page * pageCapacity) {
                    i++;
                    continue;
                }
                if(i >= Mathf.Min(collection.Count, pageCapacity)) break;

                Chat.Chat.AddMessage(selector(item));

                i++;
            }
        }

        private static void HelpCommand(int page, Client source) => PrintCollection(
            dispatcher.GetSmartUsage(dispatcher.Root, source), page,
            command => descriptions.TryGetValue(command.Key, out string description) ?
                $"{command.Key.Name} - {description} Usage: {command.Value}" :
                $"{command.Key.Name} {command.Value}");

        private static void HelpCommand(string command, Client source) {
            CommandNode<Client> node = dispatcher.FindNode(new string[] { command });
            IDictionary<CommandNode<Client>, string> usages = dispatcher.GetSmartUsage(node, source);
            
            foreach((CommandNode<Client> _, string usage) in usages)
                Chat.Chat.AddMessage(descriptions.TryGetValue(node, out string description) ?
                    $"{command} - {description} Usage: {usage}" : $"{command} {usage}");
        }

        private static void DebugCommand(string arg, Client source) {
            ClientDebug.DataTypeFlag ToggleFlags(ClientDebug.DataTypeFlag current) =>
                current == ClientDebug.DataTypeFlag.All ? ClientDebug.DataTypeFlag.None : ClientDebug.DataTypeFlag.All;

            if(!DebugCommandArgValid(arg, out string[] splitArg, out string argType)) return;

            switch(splitArg.Length) {
                case 3 or 2: {
                    string dataType = splitArg[1];
                    if(!Enum.TryParse(dataType, true, out ClientDebug.DataTypeFlag flag)) {
                        Chat.Chat.AddWarningMessage($"Unknown data type '{arg}'");
                        return;
                    }

                    DebugCommandParse3Or2Args(flag, splitArg, argType, source);

                    break;
                }
                case 1 when argType == "client": source.debug.client = ToggleFlags(source.debug.client);
                    break;
                case 1: source.debug.server = ToggleFlags(source.debug.server);
                    break;
            }
        }
        private static void DebugCommandParse3Or2Args(ClientDebug.DataTypeFlag dataTypeFlag,
            IReadOnlyCollection<string> splitArg, string argType, Client source) {
            switch(splitArg.Count) {
                case 3:
                    Chat.Chat.AddDebugMessage(argType == "client" ?
                        $"[CLIENT] {source.debug.client.HasFlag(dataTypeFlag).ToString()}" :
                        $"[SERVER] {source.debug.server.HasFlag(dataTypeFlag).ToString()}");
                    break;
                case 2 when argType == "client":
                    source.debug.client ^= dataTypeFlag;
                    break;
                case 2:
                    source.debug.server ^= dataTypeFlag;
                    break;
            }
        }

        private static bool DebugCommandArgValid(string arg, out string[] splitArg, out string argType) {
            splitArg = arg.Split('_');
            if(splitArg.Length is < 1 or > 3) {
                Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                argType = null;
                return false;
            }

            argType = splitArg[0].ToLowerInvariant();
            if(argType != "client" && argType != "server") {
                Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                return false;
            }

            string argAction = splitArg.Length >= 3 ? splitArg[2].ToLowerInvariant() : null;
            // ReSharper disable once InvertIf
            if(argAction != null && argAction != "state") {
                Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                return false;
            }

            return true;
        }

        private static void PlayersCommand(int page, bool printUsername, Client source) =>
            PrintCollection(source.players, page, player => {
            string displayName = $"{player.displayName} ";
            string username = printUsername ? $"({player.username}) " : "";
            string room = player.IsPlaying() ? $": {player.worldPackName} - {player.worldName} - {player.roomName}" : "";
                
            return $"{displayName}{username}{room}";
        });

        private static void TeleportCommand(CommandContext<Client> context, string username) {
            Player player =
                (from ply in context.Source.playerRegistry where ply.Value.username == username select ply.Value)
                .FirstOrDefault();
            if(player == null) {
                Chat.Chat.AddErrorMessage($"Invalid argument <b>0</b> (player <b>{username}</b> not found)");
                return;
            }

            if(!player.LocationEqual(context.Source.ownPlayer)) {
                Chat.Chat.AddErrorMessage(
                    $"Invalid argument <b>0</b> (player <b>{username}</b> is in a different room)");
                return;
            }
            
            CapturedData.catPartManager.MoveCat(context.Source.syncedObjectRegistry[player.controlling].transform
                .position);
            Chat.Chat.AddMessage(
                $"Teleported <b>{context.Source.ownPlayer.displayName}</b> to <b>{player.displayName}</b>");
        }
        
        private static void TeleportCommand(CommandContext<Client> context, Vector2 position) {
            CapturedData.catPartManager.MoveCat(position);
            Chat.Chat.AddMessage(
                $"Teleported <b>{context.Source.ownPlayer.displayName}</b> to <b>{position.ToString()}</b>");
        }

        private static void SpectateCommand(CommandContext<Client> context) {
            Player player = context.Source.spectating;
            if(player == null) return;

            context.Source.spectating = null;
            Chat.Chat.AddMessage($"Stopped spectating <b>{player.username}</b>");
        }
        
        private static void SpectateCommand(CommandContext<Client> context, string username) {
            Player player = (from ply in context.Source.playerRegistry where ply.Value.username == username
                             select ply.Value)
                .FirstOrDefault();
            if(player == null) {
                Chat.Chat.AddErrorMessage($"Invalid argument <b>0</b> (player <b>{username}</b> not found)");
                return;
            }

            if(player.username == context.Source.ownPlayer.username) {
                Chat.Chat.AddErrorMessage("Invalid argument <b>0</b> (can't spectate yourself)");
                return;
            }

            if(!player.LocationEqual(context.Source.ownPlayer)) {
                Chat.Chat.AddErrorMessage(
                    $"Invalid argument <b>0</b> (player <b>{username}</b> is in a different location)");
                return;
            }

            context.Source.spectating = player;
            Chat.Chat.AddMessage($"Spectating <b>{player.displayName}</b>");
        }

        private static void EnterCommand(CommandContext<Client> context, string username) {
            Player player = (from ply in context.Source.playerRegistry where ply.Value.username == username
                             select ply.Value)
                .FirstOrDefault();
            if(player == null) {
                Chat.Chat.AddErrorMessage($"Invalid argument <b>0</b> (player <b>{username}</b> not found)");
                return;
            }

            if(player.username == context.Source.ownPlayer.username) {
                Chat.Chat.AddErrorMessage("Invalid argument <b>0</b> (can't join yourself)");
                return;
            }

            if(player.LocationEqual(context.Source.ownPlayer)) {
                Chat.Chat.AddErrorMessage(
                    $"Invalid argument <b>0</b> (player <b>{username}</b> is in the same location)");
                return;
            }
            
            EnterCommand(context, player.worldPackGuid, player.worldGuid, player.roomGuid);
        }

        private static void EnterCommand(CommandContext<Client> context, string worldPackGuid, string worldGuid,
            string roomGuid) {
            if(!context.Source.ownPlayer.IsPlaying()) {
                Chat.Chat.AddErrorMessage("You're not currently in some other location");
                return;
            }

            try {
                string path =
                    MultiplayerPlugin.FindLocationPath(worldPackGuid, worldGuid, roomGuid, out bool isOfficial);
                if(isOfficial) {
                    LevelLoadManager.LoadLevel(path);
                    WorldPackSettings.LoadOfficialPackSettings();
                }
                else LevelLoadManager.LoadCommunityLevel(path);
            }
            catch(Exception ex) {
                Chat.Chat.AddErrorMessage(ex.Message);
            }
        }
    }
}
