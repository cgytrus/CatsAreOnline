using System.Globalization;

namespace CatsAreOnlineServer.Configuration;

public class ConfigValue<T> : ConfigValueBase {
    public T value {
        get {
            // the type may be wrong after c#->json->c# conversion so we fix it
            if(boxedValue is not T)
                boxedValueBacking = System.Convert.ChangeType(boxedValue, typeof(T), CultureInfo.InvariantCulture);
            return (T)boxedValue;
        }
        set => boxedValue = value;
    }

    public T defaultValue => (T)boxedDefaultValue;

    public ConfigValue() : this(default) { }
    public ConfigValue(T defaultValue) : this((object)defaultValue) { }
    public ConfigValue(object boxedDefaultValue) : base(boxedDefaultValue) { }
}