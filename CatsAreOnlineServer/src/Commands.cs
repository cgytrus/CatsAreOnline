using System.Globalization;

using NBrigadier;
using NBrigadier.Builder;

namespace CatsAreOnlineServer {
    public static class Commands {
        public static CommandDispatcher<Player> dispatcher { get; } = new();
        
        // ReSharper disable once ArrangeMethodOrOperatorBody
        public static void Initialize() {
            dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("stop")
                .Executes(context => {
                    if(context.Source != null) {
                        Server.SendChatMessage(context.Source, Server.ServerErrorMessage($"Not enough permissions"));
                        return 1;
                    }
                    Server.Stop();
                    return 1;
                }));
            dispatcher.Register(LiteralArgumentBuilder<Player>.Literal("info")
                .Executes(context => {
                    Server.SendChatMessage(context.Source, Server.ServerMessage($"<b>INFO:</b> v{Server.Version}"));
                    Server.SendChatMessage(context.Source, Server.ServerMessage(
                        $"- <b>{dispatcher.Root.Children.Count.ToString(CultureInfo.InvariantCulture)}</b> commands available"));
                    Server.SendChatMessage(context.Source, Server.ServerMessage(
                        $"- <b>{Server.playerCount.ToString(CultureInfo.InvariantCulture)}</b> players online"));
                    return 1;
                }));
        }
    }
}
