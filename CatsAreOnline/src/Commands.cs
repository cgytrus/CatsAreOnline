using System;
using System.Collections.Generic;
using System.Linq;

using CaLAPI.API.Cat;

using NBrigadier;
using NBrigadier.Arguments;
using NBrigadier.Builder;
using NBrigadier.Context;
using NBrigadier.Tree;

using UnityEngine;

namespace CatsAreOnline {
    public static class Commands {
        public static CommandDispatcher<Client> dispatcher { get; } = new CommandDispatcher<Client>();

        private static readonly Dictionary<CommandNode<Client>, string> descriptions =
            new Dictionary<CommandNode<Client>, string>();
        
        public static void Initialize() {
            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Client>.Literal("help")
                .Then(RequiredArgumentBuilder<Client, int>.Argument("page", IntegerArgumentType.Integer(1)).Executes(
                context => {
                    HelpCommand(IntegerArgumentType.GetInteger(context, "page") - 1, context.Source);
                    return 1;
                }))
                .Then(RequiredArgumentBuilder<Client, string>.Argument("command", StringArgumentType.Word()).Executes(
                    context => {
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
            ), "Teleports you to the specified location.");

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
            
            foreach(KeyValuePair<CommandNode<Client>, string> usage in usages)
                Chat.Chat.AddMessage(descriptions.TryGetValue(node, out string description) ?
                    $"{command} - {description} Usage: {usage.Value}" : $"{command} {usage.Value}");
        }

        private static void DebugCommand(string arg, Client source) {
            ClientDebug.DataTypeFlag ToggleFlags(ClientDebug.DataTypeFlag current) =>
                current == ClientDebug.DataTypeFlag.All ? ClientDebug.DataTypeFlag.None : ClientDebug.DataTypeFlag.All;

            string[] splitArg = arg.Split('_');
            if(splitArg.Length < 1 || splitArg.Length > 3) {
                Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                return;
            }

            string argType = splitArg[0].ToLowerInvariant();
            if(argType != "client" && argType != "server") {
                Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                return;
            }

            string argAction = splitArg.Length >= 3 ? splitArg[2].ToLowerInvariant() : null;
            if(argAction != null && argAction != "state") {
                Chat.Chat.AddWarningMessage($"Invalid argument syntax ('{arg}')");
                return;
            }
            
            switch(splitArg.Length) {
                case 3:
                case 2:
                    string dataType = splitArg[1];
                    if(!Enum.TryParse(dataType, true, out ClientDebug.DataTypeFlag flag)) {
                        Chat.Chat.AddWarningMessage($"Unknown data type '{arg}'");
                        return;
                    }

                    switch(splitArg.Length) {
                        case 3:
                            Chat.Chat.AddDebugMessage(
                                argType == "client" ? $"[CLIENT] {source.debug.client.HasFlag(flag).ToString()}" :
                                    $"[SERVER] {source.debug.server.HasFlag(flag).ToString()}");
                            break;
                        case 2:
                            if(argType == "client") source.debug.client ^= flag;
                            else source.debug.server ^= flag;
                            break;
                    }
                    break;
                case 1 when argType == "client": source.debug.client = ToggleFlags(source.debug.client);
                    break;
                case 1: source.debug.server = ToggleFlags(source.debug.server);
                    break;
            }
        }

        private static void PlayersCommand(int page, bool printUsername, Client source) =>
            PrintCollection(source.playerRegistry.Values, page, player => {
            string displayName = $"{player.displayName} ";
            string username = printUsername ? $"({player.username}) " : "";
            string room = string.IsNullOrWhiteSpace(player.state.room) ? "" : $": {player.state.room}";
                
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

            if(player.state.room != context.Source.state.room) {
                Chat.Chat.AddErrorMessage(
                    $"Invalid argument <b>0</b> (player <b>{username}</b> is in a different room)");
                return;
            }
            
            context.Source.playerPartManager.MoveCat(player.transform.position);
            Chat.Chat.AddMessage($"Teleported <b>{context.Source.displayName}</b> to <b>{player.displayName}</b>");
        }
        
        private static void TeleportCommand(CommandContext<Client> context, Vector2 position) {
            context.Source.playerPartManager.MoveCat(position);
            Chat.Chat.AddMessage($"Teleported <b>{context.Source.displayName}</b> to <b>{position.ToString()}</b>");
        }

        private static void SpectateCommand(CommandContext<Client> context) {
            Player player = context.Source.spectating;
            if(player == null) return;

            FollowPlayer.followPlayerHead = player.restoreFollowPlayerHead;
            FollowPlayer.customFollowTarget = player.restoreFollowTarget;
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

            if(player.username == context.Source.username) {
                Chat.Chat.AddErrorMessage("Invalid argument <b>0</b> (can't spectate yourself)");
                return;
            }

            if(player.state.room != context.Source.state.room) {
                Chat.Chat.AddErrorMessage(
                    $"Invalid argument <b>0</b> (player <b>{username}</b> is in a different room)");
                return;
            }

            context.Source.spectating = player;
            player.restoreFollowPlayerHead = FollowPlayer.followPlayerHead;
            player.restoreFollowTarget = FollowPlayer.customFollowTarget;
            FollowPlayer.followPlayerHead = false;
            FollowPlayer.customFollowTarget = player.transform;
            Chat.Chat.AddMessage($"Spectating <b>{player.displayName}</b>");
        }
    }
}
