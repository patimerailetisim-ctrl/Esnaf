using System;
using Newtonsoft.Json;

namespace Esnaf.Domain.Content
{
    /// <summary>
    /// Newtonsoft varsayılan olarak sessizce dönüştürür (10000.5 -> long, "10000" -> sayı, 5 -> "5"). İçerikte bu, fiyat gibi
    /// alanlarda gizli hataya yol açar. Bu dönüştürücü yalnızca tam tip eşleşmesine izin verir:
    /// tam sayı alanı = JSON tam sayısı, ondalık alan = JSON sayısı, bool = true/false, string = JSON metni.
    /// </summary>
    internal sealed class StrictScalarConverter : JsonConverter
    {
        public override bool CanWrite
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            Type target = Nullable.GetUnderlyingType(objectType) ?? objectType;
            return target == typeof(string)
                || target == typeof(int)
                || target == typeof(long)
                || target == typeof(double)
                || target == typeof(bool);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type nullable = Nullable.GetUnderlyingType(objectType);
            Type target = nullable ?? objectType;
            bool allowsNull = !objectType.IsValueType || nullable != null;

            if (reader.TokenType == JsonToken.Null)
            {
                if (!allowsNull)
                {
                    throw Error(reader, "A null value is not allowed here.");
                }

                return null;
            }

            if (target == typeof(string))
            {
                if (reader.TokenType == JsonToken.String)
                {
                    return (string)reader.Value;
                }

                throw Error(reader, "Expected a JSON string.");
            }

            if (target == typeof(bool))
            {
                if (reader.TokenType == JsonToken.Boolean)
                {
                    return (bool)reader.Value;
                }

                throw Error(reader, "Expected true or false.");
            }

            if (target == typeof(double))
            {
                if (reader.TokenType == JsonToken.Float)
                {
                    return (double)reader.Value;
                }

                if (reader.TokenType == JsonToken.Integer && reader.Value is long)
                {
                    return (double)(long)reader.Value;
                }

                throw Error(reader, "Expected a JSON number.");
            }

            // int / long: yalnızca tam sayı belirteci (10000.0 bile kabul edilmez).
            if (reader.TokenType != JsonToken.Integer || !(reader.Value is long))
            {
                throw Error(reader, "Expected a whole number (no decimals, no quotes).");
            }

            long value = (long)reader.Value;
            if (target == typeof(int))
            {
                if (value < int.MinValue || value > int.MaxValue)
                {
                    throw Error(reader, "Number " + value + " is out of range.");
                }

                return (int)value;
            }

            return value;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("StrictScalarConverter is read-only.");
        }

        private static JsonReaderException Error(JsonReader reader, string message)
        {
            var lineInfo = reader as IJsonLineInfo;
            int line = lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0;
            int position = lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0;
            string full = message + " Path '" + reader.Path + "', line " + line + ", position " + position + ".";
            return new JsonReaderException(full, reader.Path, line, position, null);
        }
    }
}
