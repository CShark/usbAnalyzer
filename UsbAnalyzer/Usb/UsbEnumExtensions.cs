using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UsbAnalyzer.Usb {
    public class FallbackAttribute : Attribute {
        public string FallbackDescription { get; }

        public FallbackAttribute(string fallbackDescription) {
            FallbackDescription = fallbackDescription;
        }
    }

    public static class UsbEnumExtensions {
        public static string ToReadableString(this Enum value) {
            var fieldInfo = value.GetType().GetField(value.ToString());

            if (fieldInfo?.GetCustomAttribute<DescriptionAttribute>() is { } desc) {
                return desc.Description;
            } else if (fieldInfo == null) {
                if (value.GetType().GetCustomAttribute<FallbackAttribute>() is { } fallback) {
                    return fallback.FallbackDescription;
                } else {
                    return "Unknown Value";
                }
            } else {
                return value.ToString();
            }
        }
    }
}