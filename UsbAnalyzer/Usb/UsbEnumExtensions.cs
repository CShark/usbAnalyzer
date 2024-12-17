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

    public class OffsetAttribute : Attribute {
        public int Order { get; }
        public OffsetAttribute(int order) {
            Order = order;
        }
    }

    public class StringTableAttribute : Attribute;

    public class BcdDisplayAttribute : Attribute;

    public class HideValueAttribute : Attribute;

    public class HelperAttribute : Attribute;

    public class ReadableAccessorAttribute : Attribute {
        public string PropertyName { get; }

        public ReadableAccessorAttribute(string propertyName) {
            PropertyName = propertyName;
        }
    }

    public class GroupPropertyAttribute : Attribute {
        public string ParentProperty { get; }
        public GroupPropertyAttribute(string parentProperty) {
            ParentProperty = parentProperty;
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