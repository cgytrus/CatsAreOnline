using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatsAreOnlineServer.Configuration {
    public class ConfigValueBaseJsonConverter : JsonConverter<ConfigValueBase> {
        public override ConfigValueBase Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options) {
            object value = JsonSerializer.Deserialize<object>(ref reader, options);
            if(value is JsonElement element) value = JsonElementToObject(element);
            return new ConfigValue<object>(value);
        }

        // the fuck
        private static object JsonElementToObject(JsonElement element) {
            switch(element.ValueKind) {
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.False: return false;
                case JsonValueKind.True: return true;
                case JsonValueKind.String:
                    return StringToObject(element);
                case JsonValueKind.Number: {
                    object value = NumberToObject(element);
                    if(value is not null) return value;
                    break;
                }
                case JsonValueKind.Object: {
                    object value = ObjectToObject(element);
                    if(value is not null) return value;
                    break;
                }
            }

            throw new JsonException("Unsupported type.");
        }

        private static object StringToObject(JsonElement element) {
            if(element.TryGetGuid(out Guid guidValue)) return guidValue;
            if(element.TryGetDateTime(out DateTime dateTimeValue)) return dateTimeValue;
            if(element.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffsetValue)) return dateTimeOffsetValue;
            return element.GetString();
        }

        private static object NumberToObject(JsonElement element) {
            if(element.TryGetByte(out byte byteValue)) return byteValue;
            if(element.TryGetInt16(out short shortValue)) return shortValue;
            if(element.TryGetInt32(out int intValue)) return intValue;
            if(element.TryGetInt64(out long longValue)) return longValue;
            if(element.TryGetSByte(out sbyte sbyteValue)) return sbyteValue;
            if(element.TryGetUInt16(out ushort ushortValue)) return ushortValue;
            if(element.TryGetUInt32(out uint uintValue)) return uintValue;
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if(element.TryGetUInt64(out ulong ulongValue)) return ulongValue;
            return DecimalNumberToObject(element);
        }

        private static object DecimalNumberToObject(JsonElement element) {
            if(element.TryGetDecimal(out decimal decimalValue)) return decimalValue;
            if(element.TryGetDouble(out double doubleValue)) return doubleValue;
            if(element.TryGetSingle(out float floatValue)) return floatValue;
            return null;
        }

        private static object ObjectToObject(JsonElement element) {
            if(element.TryGetProperty("value", out JsonElement valueValue)) return JsonElementToObject(valueValue);
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if(element.TryGetProperty("boxedValue", out JsonElement boxedValueValue))
                return JsonElementToObject(boxedValueValue);
            return null;
        }

        public override void Write(Utf8JsonWriter writer, ConfigValueBase value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.boxedValue, options);
    }
}
