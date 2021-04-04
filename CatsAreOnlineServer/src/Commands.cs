using System.Globalization;

namespace CatsAreOnlineServer {
    public static class Commands {
        public static void Initialize() => Server.commandRegistry.Add("info", InfoCommand);

        private static string[] InfoCommand(params string[] args) => new[] {
            Server.ServerMessage($"<b>INFO:</b> v{Server.Version}"),
            Server.ServerMessage(
                $"- <b>{Server.commandRegistry.Count.ToString(CultureInfo.InvariantCulture)}</b> commands available"),
            Server.ServerMessage(
                $"- <b>{Server.playerRegistry.Count.ToString(CultureInfo.InvariantCulture)}</b> players online")
        };
    }
}
