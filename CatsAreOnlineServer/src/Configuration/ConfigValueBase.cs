using System;

namespace CatsAreOnlineServer.Configuration {
    public abstract class ConfigValueBase {
        protected object _boxedValue;

        public object boxedValue {
            get => _boxedValue;
            set {
                _boxedValue = value;
                ForceUpdateValue();
            }
        }

        public object boxedDefaultValue { get; }

        public event EventHandler valueChanged;

        protected ConfigValueBase(object boxedDefaultValue) {
            boxedValue = boxedDefaultValue;
            this.boxedDefaultValue = boxedDefaultValue;
        }

        public void ForceUpdateValue() => valueChanged?.Invoke(this, EventArgs.Empty);
    }
}
