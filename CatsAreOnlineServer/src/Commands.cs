using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using NBrigadier;
using NBrigadier.Arguments;
using NBrigadier.Builder;
using NBrigadier.Context;
using NBrigadier.Tree;

namespace CatsAreOnlineServer {
    public static class Commands {
        public static CommandDispatcher<Player> dispatcher { get; } = new();

        private static readonly Dictionary<CommandNode<Player>, string> descriptions = new();

        // ReSharper disable once ArrangeMethodOrOperatorBody
        public static void Initialize() {
            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("help")
                .Then(RequiredArgumentBuilder<Player, string>.Argument("command", StringArgumentType.Word())
                    .Executes(context => {
                        HelpCommand(StringArgumentType.GetString(context, "command"), context);
                        return 1;
                    }))
                .Executes(context => {
                    HelpCommand(context);
                    return 1;
                })), "Prints this message.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("stop")
                .Executes(context => {
                    if(context.Source is not null) {
                        Server.SendChatMessage(null, context.Source,
                            Server.ServerErrorMessage("Not enough permissions"));
                        return 1;
                    }
                    Server.Stop();
                    return 1;
                })), "Stops the server.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("info")
                .Executes(context => {
                    string commandCount = dispatcher.Root.Children.Count.ToString(CultureInfo.InvariantCulture);
                    string playerCount = Server.players.Count.ToString(CultureInfo.InvariantCulture);
                    string uptime = Server.uptime.ToString("%d\\d\\ %h\\h\\ %m\\m\\ %s\\s", CultureInfo.InvariantCulture);
                    string ping = TimeSpan.FromSeconds(context.Source?.connection.AverageRoundtripTime ?? 0f)
                        .ToString("%f\\m\\s", CultureInfo.InvariantCulture);

                    Server.SendChatMessage(null, context.Source, $"<b>INFO:</b> v{Server.Version}");
                    Server.SendChatMessage(null, context.Source, $"- <b>{commandCount}</b> commands available");
                    Server.SendChatMessage(null, context.Source, $"- <b>{playerCount}</b> players online");
                    Server.SendChatMessage(null, context.Source, $"- Running for {uptime}");
                    Server.SendChatMessage(null, context.Source, $"- Pinging {ping} to you");
                    return 1;
                })), "Prints some info about the server.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("players")
                .Then(RequiredArgumentBuilder<Player, bool>.Argument("printGuid", BoolArgumentType.Bool())
                    .Executes(context => {
                        if(context.Source is not null) {
                            Server.SendChatMessage(null, context.Source,
                                Server.ServerErrorMessage("Not enough permissions"));
                            return 1;
                        }
                        PlayersCommand(BoolArgumentType.GetBool(context, "printGuid"), context);
                        return 1;
                    }))
                .Executes(context => {
                    if(context.Source is not null) {
                        Server.SendChatMessage(null, context.Source,
                            Server.ServerErrorMessage("Not enough permissions"));
                        return 1;
                    }
                    PlayersCommand(false, context);
                    return 1;
                })), "Prints online players.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("locate")
                .Then(RequiredArgumentBuilder<Player, string>.Argument("username", StringArgumentType.String())
                    .Executes(context => {
                        LocateCommand(context, StringArgumentType.GetString(context, "username"));
                        return 1;
                    }))
                .Executes(context => {
                    LocateCommand(context, context.Source);
                    return 1;
                })), "Prints the location of a player.");

            descriptions.Add(dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("say")
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
        }

        private static void HelpCommand(CommandContext<Player> context) {
            IDictionary<CommandNode<Player>, string> usages = dispatcher.GetSmartUsage(dispatcher.Root, context.Source);

            foreach((CommandNode<Player> node, string usage) in usages)
                Server.SendChatMessage(null, context.Source, descriptions.TryGetValue(node, out string description) ?
                    $"{node.Name} - {description} Usage: {usage}" :
                    $"{node.Name} {usage}");
        }

        private static void HelpCommand(string command, CommandContext<Player> context) {
            CommandNode<Player> node = dispatcher.FindNode(new string[] { command });
            IDictionary<CommandNode<Player>, string> usages = dispatcher.GetSmartUsage(node, context.Source);

            foreach((CommandNode<Player> _, string usage) in usages)
                Server.SendChatMessage(null, context.Source, descriptions.TryGetValue(node, out string description) ?
                    $"{command} - {description} Usage: {usage}" : $"{command} {usage}");
        }

        private static void PlayersCommand(bool printGuid, CommandContext<Player> context) {
            foreach((Guid guid, Player player) in Server.players) {
                string displayName = $"{player.username} ";
                string username = printGuid ? $"({guid.ToString()}) " : "";
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
    }
}
