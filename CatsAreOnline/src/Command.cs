using System;

namespace CatsAreOnline {
    public struct Command {
        public Action<string[]> action { get; set; }
        public string description { get; set; }

        public Command(Action<string[]> action, string description) {
            this.action = action;
            this.description = description;
        }

        public void Execute(params string[] args) => action(args);
    }
}
