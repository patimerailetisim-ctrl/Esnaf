using System;
using System.Globalization;

namespace Esnaf.Domain.Products
{
    public enum AttributeKind
    {
        Number = 0,
        Text = 1,
        Flag = 2
    }

    /// <summary>
    /// Ürün örneğindeki tek bir niteliğin değeri: sayı (pil %78), metin (ekran: "replaced_aftermarket") veya bayrak (kutu: false).
    /// Sektör bağımsız nitelik torbası için ortak değer tipi (GDD 15.1).
    /// </summary>
    public readonly struct AttributeValue : IEquatable<AttributeValue>
    {
        private readonly long _number;
        private readonly string _text;
        private readonly bool _flag;

        public AttributeKind Kind { get; }

        private AttributeValue(AttributeKind kind, long number, string text, bool flag)
        {
            Kind = kind;
            _number = number;
            _text = text;
            _flag = flag;
        }

        public static AttributeValue FromNumber(long number)
        {
            return new AttributeValue(AttributeKind.Number, number, null, false);
        }

        public static AttributeValue FromText(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new AttributeValue(AttributeKind.Text, 0L, text, false);
        }

        public static AttributeValue FromFlag(bool flag)
        {
            return new AttributeValue(AttributeKind.Flag, 0L, null, flag);
        }

        /// <summary>Değer sayı değilse InvalidOperationException.</summary>
        public long Number
        {
            get
            {
                Require(AttributeKind.Number);
                return _number;
            }
        }

        /// <summary>Değer metin değilse InvalidOperationException.</summary>
        public string Text
        {
            get
            {
                Require(AttributeKind.Text);
                return _text;
            }
        }

        /// <summary>Değer bayrak değilse InvalidOperationException.</summary>
        public bool Flag
        {
            get
            {
                Require(AttributeKind.Flag);
                return _flag;
            }
        }

        private void Require(AttributeKind expected)
        {
            if (Kind != expected)
            {
                throw new InvalidOperationException("Attribute is a " + Kind + ", not a " + expected + ".");
            }
        }

        public bool Equals(AttributeValue other)
        {
            return Kind == other.Kind
                && _number == other._number
                && string.Equals(_text, other._text, StringComparison.Ordinal)
                && _flag == other._flag;
        }

        public override bool Equals(object obj)
        {
            return obj is AttributeValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 31 + _number.GetHashCode();
                hash = hash * 31 + (_text == null ? 0 : StringComparer.Ordinal.GetHashCode(_text));
                hash = hash * 31 + (_flag ? 1 : 0);
                return hash;
            }
        }

        public static bool operator ==(AttributeValue a, AttributeValue b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(AttributeValue a, AttributeValue b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case AttributeKind.Number:
                    return _number.ToString(CultureInfo.InvariantCulture);
                case AttributeKind.Text:
                    return _text;
                default:
                    return _flag ? "true" : "false";
            }
        }
    }
}
