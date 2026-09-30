using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Esnaf.Tests.Support
{
    /// <summary>
    /// İki düz veri nesnesini (kayıt DTO'ları) özyinelemeli karşılaştırır; ilk farkın yolunu döndürür (eşitse null).
    /// double'lar BİT düzeyinde karşılaştırılır (kayıt gidiş-dönüşü tam olmalı).
    /// </summary>
    public static class DeepCompare
    {
        public static string FirstDifference(object a, object b)
        {
            return Compare(a, b, "$");
        }

        private static string Compare(object a, object b, string path)
        {
            if (a == null || b == null)
            {
                return a == null && b == null ? null : path + ": " + (a ?? "null") + " != " + (b ?? "null");
            }

            Type type = a.GetType();
            if (type != b.GetType())
            {
                return path + ": type " + type.Name + " != " + b.GetType().Name;
            }

            if (a is double)
            {
                return BitConverter.DoubleToInt64Bits((double)a) == BitConverter.DoubleToInt64Bits((double)b) ? null : path + ": " + a + " != " + b;
            }

            if (type.IsPrimitive || a is string || a is decimal || type.IsEnum)
            {
                return a.Equals(b) ? null : path + ": " + a + " != " + b;
            }

            var dictA = a as IDictionary;
            if (dictA != null)
            {
                var dictB = (IDictionary)b;
                if (dictA.Count != dictB.Count)
                {
                    return path + ": dictionary count " + dictA.Count + " != " + dictB.Count;
                }

                foreach (object key in dictA.Keys)
                {
                    if (!dictB.Contains(key))
                    {
                        return path + "[" + key + "]: missing";
                    }

                    string diff = Compare(dictA[key], dictB[key], path + "[" + key + "]");
                    if (diff != null)
                    {
                        return diff;
                    }
                }

                return null;
            }

            var listA = a as IList;
            if (listA != null)
            {
                var listB = (IList)b;
                if (listA.Count != listB.Count)
                {
                    return path + ": count " + listA.Count + " != " + listB.Count;
                }

                for (int i = 0; i < listA.Count; i++)
                {
                    string diff = Compare(listA[i], listB[i], path + "[" + i + "]");
                    if (diff != null)
                    {
                        return diff;
                    }
                }

                return null;
            }

            foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                string diff = Compare(p.GetValue(a), p.GetValue(b), path + "." + p.Name);
                if (diff != null)
                {
                    return diff;
                }
            }

            return null;
        }
    }
}
