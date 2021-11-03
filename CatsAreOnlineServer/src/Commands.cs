using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using CatsAreOnlineServer.Configuration;

using Lidgren.Network;

using NBrigadier;
using NBrigadier.Arguments;
using NBrigadier.Builder;
using NBrigadier.Context;
using NBrigadier.Tree;

namespace CatsAreOnlineServer {
    public class Commands {
        public CommandDispatcher<Player> dispatcher { get; } = new();

        private readonly Dictionary<CommandNode<Player>, string> _descriptions = new();

        public Commands() {
            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("help")
                .Then(RequiredArgumentBuilder<Player, string>.Argument("command", StringArgumentType.Word())
                    .Executes(context => {
                        HelpCommand(StringArgumentType.GetString(context, "command"), context);
                        return 1;
                    }))
                .Executes(context => {
                    HelpCommand(context);
                    return 1;
                })), "Prints this message.");

            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("stop")
                .Executes(context => {
                    if(!CheckServerPlayer(context)) return 1;
                    Server.Stop();
                    return 1;
                })), "Stops the server.");

            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("restart")
                .Executes(context => {
                    if(!CheckServerPlayer(context)) return 1;
                    Server.Restart();
                    return 1;
                })), "Restarts the server.");

            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("info")
                .Executes(context => {
                    string commandCount = dispatcher.Root.Children.Count.ToString(CultureInfo.InvariantCulture);
                    string playerCount = Server.players.Count.ToString(CultureInfo.InvariantCulture);
                    string uptime = Server.uptime.ToString("%d'd '%h'h '%m'm '%s's'", CultureInfo.InvariantCulture);
                    string ping = ((long)TimeSpan.FromSeconds(context.Source?.latestPing ?? 0f).TotalMilliseconds)
                        .ToString(CultureInfo.InvariantCulture);

                    Server.SendChatMessage(null, context.Source, $"<b>INFO:</b> v{Server.Version}");
                    Server.SendChatMessage(null, context.Source, $"- <b>{commandCount}</b> commands available");
                    Server.SendChatMessage(null, context.Source, $"- <b>{playerCount}</b> players online");
                    Server.SendChatMessage(null, context.Source, $"- Running for <b>{uptime}</b>");
                    Server.SendChatMessage(null, context.Source, $"- Pinging <b>{ping}ms</b> to you");
                    return 1;
                })), "Prints some info about the server.");

            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("players")
                .Then(RequiredArgumentBuilder<Player, bool>.Argument("printIp", BoolArgumentType.Bool())
                    .Executes(context => {
                        PlayersCommand(BoolArgumentType.GetBool(context, "printIp"), context);
                        return 1;
                    }))
                .Executes(context => {
                    PlayersCommand(false, context);
                    return 1;
                })), "Prints online players.");

            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("locate")
                .Then(RequiredArgumentBuilder<Player, string>.Argument("username", StringArgumentType.String())
                    .Executes(context => {
                        LocateCommand(context, StringArgumentType.GetString(context, "username"));
                        return 1;
                    }))
                .Executes(context => {
                    LocateCommand(context, context.Source);
                    return 1;
                })), "Prints the location of a player.");

            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("say")
                .Then(RequiredArgumentBuilder<Player, string>.Argument("message", StringArgumentType.String())
                    .Executes(context => {
                        Server.SendChatMessageToAll(context.Source, StringArgumentType.GetString(context, "message"));
                        return 1;
                    }))
                .Then(RequiredArgumentBuilder<Player, string>.Argument("username", StringArgumentType.String())
                    .Then(RequiredArgumentBuilder<Player, string>.Argument("message", StringArgumentType.String())
                        .Executes(context => {
                            SayCommand(context, StringArgumentType.GetString(context, "username"),
                                StringArgumentType.GetString(context, "message"));
                            return 1;
                        })))), "Sends a message in the chat.");

            _descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("config")
                .Then(LiteralArgumentBuilder<Player>.Literal("get")
                    .Then(RequiredArgumentBuilder<Player, string>.Argument("key", StringArgumentType.Word())
                        .Executes(context => {
                            string key = StringArgumentType.GetString(context, "key");
                            Server.SendChatMessage(null, context.Source,
                                Server.config.TryGetValue(key, out ConfigValueBase value) ?
                                    $"Value of {key} is {value.boxedValue}" : $"{key} not found");
                            return 1;
                        }))
                    .Executes(context => {
                        foreach((string key, ConfigValueBase value) in Server.config.values)
                            Server.SendChatMessage(null, context.Source, $"Value of {key} is {value.boxedValue}");
                        return 1;
                    }))
                .Then(LiteralArgumentBuilder<Player>.Literal("set")
                    .Then(RequiredArgumentBuilder<Player, string>.Argument("key", StringArgumentType.Word())
                        .Then(RequiredArgumentBuilder<Player, string>.Argument("value", StringArgumentType.String())
                            .Executes(context => {
                                ConfigSetCommand(context, StringArgumentType.GetString(context, "key"),
                                    StringArgumentType.GetString(context, "value"));
                                return 1;
                            }))
                        .Executes(context => {
                            ConfigSetCommand(context, StringArgumentType.GetString(context, "key"));
                            return 1;
                        })))
                .Then(LiteralArgumentBuilder<Player>.Literal("save")
                    .Executes(context => {
                        if(!CheckServerPlayer(context)) return 1;
                        Server.config.Save();
                        return 1;
                    }))), "Get/set/reset config values.");
        }

        private static bool CheckServerPlayer(CommandContext<Player> context) {
            if(context.Source is null) return true;
            Server.SendChatMessage(null, context.Source,
                Server.ServerErrorMessage("Not enough permissions"));
            return false;
        }

        private void HelpCommand(CommandContext<Player> context) {
            IDictionary<CommandNode<Player>, string> usages = dispatcher.GetSmartUsage(dispatcher.Root, context.Source);

            foreach((CommandNode<Player> node, string usage) in usages)
                Server.SendChatMessage(null, context.Source, _descriptions.TryGetValue(node, out string description) ?
                    $"{node.Name} - {description} Usage: {usage}" :
                    $"{node.Name} {usage}");
        }

        private void HelpCommand(string command, CommandContext<Player> context) {
            CommandNode<Player> node = dispatcher.FindNode(new string[] { command });
            IDictionary<CommandNode<Player>, string> usages = dispatcher.GetSmartUsage(node, context.Source);

            foreach((CommandNode<Player> _, string usage) in usages)
                Server.SendChatMessage(null, context.Source, _descriptions.TryGetValue(node, out string description) ?
                    $"{command} - {description} Usage: {usage}" : $"{command} {usage}");
        }

        private static void PlayersCommand(bool printIp, CommandContext<Player> context) {
            if(!CheckServerPlayer(context)) return;

            foreach((NetConnection connection, Player player) in Server.players) {
                string displayName = $"{player.username} ";
                string username = printIp ? $"({connection.RemoteEndPoint}) " : "";
                string room = player.IsPlaying() ?
                    $": {player.worldPackName} - {player.worldName} - {player.roomName}" : "";

                Server.SendChatMessage(null, context.Source, $"{displayName}{username}{room}");
            }
        }

        private static void LocateCommand(CommandContext<Player> context, string username) {
            Player player =
                (from ply in Server.players where ply.Value.username == username select ply.Value)
                .FirstOrDefault();
            if(player == null) {
                Server.SendChatMessage(null, context.Source,
                    $"Invalid argument <b>0</b> (player <b>{username}</b> not found)");
                return;
            }

            LocateCommand(context, player);
        }

        private static void LocateCommand(CommandContext<Player> context, Player toLocate) {
            Server.SendChatMessage(null, context.Source,
                $"Worldpack: {toLocate.worldPackName} ({toLocate.worldPackGuid})");
            Server.SendChatMessage(null, context.Source, $"World: {toLocate.worldName} ({toLocate.worldGuid})");
            Server.SendChatMessage(null, context.Source, $"Room: {toLocate.roomName} ({toLocate.roomGuid})");
        }

        private static void SayCommand(CommandContext<Player> context, string username, string message) {
            Player player =
                (from ply in Server.players where ply.Value.username == username select ply.Value)
                .FirstOrDefault();
            if(player == null) {
                Server.SendChatMessage(null, context.Source,
                    $"Invalid argument <b>0</b> (player <b>{username}</b> not found)");
                return;
            }

            Server.SendChatMessage(context.Source, player, message);
        }

        private static void ConfigSetCommand(CommandContext<Player> context, string key, string value) {
            if(!CheckServerPlayer(context)) return;

            try {
                Server.config.SetJsonValue(key, value);
                Server.SendChatMessage(null, context.Source, $"Value of {key} has been set to {value}");
            }
            catch(Exception ex) {
                Server.SendChatMessage(null, context.Source, Server.ServerErrorMessage(ex.Message));
            }
        }

        private static void ConfigSetCommand(CommandContext<Player> context, string key) {
            if(!CheckServerPlayer(context)) return;

            if(!Server.config.TryGetValue(key, out ConfigValueBase value)) {
                Server.SendChatMessage(null, context.Source,
                    Server.ServerErrorMessage($"Invalid argument <b>1</b> ({key} doesn't exist)"));
                return;
            }

            value.boxedValue = value.boxedDefaultValue;
            Server.SendChatMessage(null, context.Source, $"Value of {key} has been reset");
        }
    }
}
