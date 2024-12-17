using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.XPath;
using UsbAnalyzer.Usb;

namespace UsbAnalyzer.ViewData {
    public class TreeViewEntry {
        public ObservableCollection<TreeViewEntry> Children { get; set; } = new();

        public string Key { get; } = "";

        public string OriginalValue { get; } = "";
        public string ReadableValue { get; private set; } = "";

        public bool IsRoot { get; set; } = false;

        public bool IsInvalid { get; init; } = false;

        public static TreeViewEntry BuildTree(UsbSetup setup) {
            return ParseObjValue(setup, "Setup");
        }

        public static TreeViewEntry BuildTree(UsbDescriptor descriptor) {
            return ParseObjValue(descriptor, descriptor.Type.ToReadableString());
        }

        private static TreeViewEntry ParseObjValue(object descriptor, string label) {
            var members = descriptor.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance);

            var list = new Dictionary<string, TreeViewEntry>();
            var groups = new Dictionary<TreeViewEntry, string>();
            var root = new List<TreeViewEntry>();
            var orderList = new Dictionary<TreeViewEntry, int>();
            foreach (var member in members) {
                if (member.GetCustomAttribute<OffsetAttribute>() == null &&
                    member.GetCustomAttribute<GroupPropertyAttribute>() == null &&
                    member.GetCustomAttribute<HelperAttribute>() == null) continue;

                var name = member.Name;
                var value = member.GetValue(descriptor);
                var order = member.GetCustomAttribute<OffsetAttribute>()?.Order ?? int.MaxValue;
                var groupAttr = member.GetCustomAttribute<GroupPropertyAttribute>();
                string? group = null;

                if (groupAttr != null) {
                    group = groupAttr.ParentProperty;
                } else if (member.GetCustomAttribute<StringTableAttribute>() != null) {
                    group = "String Table Indices";
                }

                var item = ParseValue(member, descriptor, value);
                list.Add(name, item);
                orderList.Add(item, order);

                if (group != null) {
                    groups.Add(item, group);
                }
            }

            var groupedItems = groups.GroupBy(x => x.Value);

            foreach (var group in groupedItems) {
                var items = group.Select(x => (x.Key, orderList[x.Key])).OrderBy(x => x.Item2).Select(x => x.Key)
                    .ToList();

                if (list.TryGetValue(group.Key, out var parent)) {
                    parent.Children = new(items);
                    if (orderList[parent] == int.MaxValue) {
                        orderList[parent] = orderList[items.First()];
                    }
                } else {
                    var item = new TreeViewEntry(group.Key, items);
                    var order = orderList[items.First()];

                    orderList.Add(item, order);
                    list.Add(group.Key, item);
                }
            }

            foreach (var item in list) {
                if (!groups.ContainsKey(item.Value)) {
                    root.Add(item.Value);
                }
            }

            return new TreeViewEntry(label, root.Select(x => (x, orderList[x])).OrderBy(x => x.Item2).Select(x => x.x));
        }

        private static TreeViewEntry ParseValue(PropertyInfo type, object desc, object? value) {
            var name = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? type.Name;
            var isHelper = type.GetCustomAttribute<HelperAttribute>() != null;

            if (value == null) {
                return new(name, "", "") { IsInvalid = true };
            }

            var readAccessor = type.GetCustomAttribute<ReadableAccessorAttribute>()?.PropertyName;
            string? readable = null;

            if (readAccessor != null) {
                readable = desc.GetType().GetProperty(readAccessor,
                    BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.Public)?.GetValue(desc) as string;
            }

            if (type.GetCustomAttribute<HideValueAttribute>() != null) {
                readable ??= "";
            }

            var raw = type.GetCustomAttribute<OffsetAttribute>() != null ? null : "";

            var parsedData = ParseValue(type, value);

            var root = new TreeViewEntry(name, isHelper ? "" : raw ?? parsedData.raw, readable ?? parsedData.readable);

            if (value is Enum e && e.GetType().GetCustomAttribute<FlagsAttribute>() != null) {
                var values = Enum.GetValues(e.GetType()).Cast<Enum>().OrderBy(x =>
                    Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType())));

                foreach (var item in values) {
                    root.Children.Add(new(item.ToReadableString(), "", $"{e.HasFlag(item)}"));
                }

                root.ReadableValue = "";
            } else if (value is IEnumerable enumerable) {
                var idx = 0;
                foreach (var elem in enumerable) {
                    if (IsBaseType(elem)) {
                        parsedData = ParseValue(type, elem);
                        root.Children.Add(new($"#{idx}", parsedData.raw, parsedData.readable));
                    } else {
                        var container = new TreeViewEntry($"#{idx}");

                        var properties = elem.GetType()
                            .GetProperties(BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance);

                        foreach (var prop in properties) {
                            container.Children.Add(ParseValue(prop, desc, prop.GetValue(elem)));
                        }

                        root.Children.Add(container);
                    }

                    idx++;
                }
            }

            return root;
        }

        private static (string raw, string readable) ParseValue(PropertyInfo type, object? value) {
            switch (value) {
                case string s:
                    return new("", s);
                case byte b:
                    return new($"0x{b:X2}", $"{b}");
                case ushort s:
                    if (type.GetCustomAttribute<BcdDisplayAttribute>() != null) {
                        return new($"0x{s:X4}", ParseBcd(s));
                    } else {
                        return new($"0x{s:X4}", $"{s}");
                    }
                case uint i:
                    return new($"0x{i:X8}", $"{i}");
                case Enum e:
                    var rawValue = Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()));
                    return new($"0x{rawValue:X2}", e.ToReadableString());
                case IEnumerable:
                    return new("", "");
            }

            return new(value.ToString().Split(".").Last(), "unsupported type");
        }

        private static bool IsBaseType(object value) => value is string or byte or ushort or uint or Enum;

        private static string ParseBcd(int value) {
            var part1 = (value & 0xFF00) >> 8;
            var part2 = value & 0x00FF;

            var parsed = (part1 >> 4) * 10 + (part1 & 0xF) + (part2 >> 4) / 10f + (part2 & 0xF) / 100f;

            return parsed.ToString("F1", CultureInfo.InvariantCulture);
        }

        public TreeViewEntry(string key) {
            Key = key;
        }

        public TreeViewEntry(string key, IEnumerable<TreeViewEntry> children) {
            Key = key;
            Children = new(children);
        }

        public TreeViewEntry(string key, string originalValue, string readableValue) {
            Key = key;
            OriginalValue = originalValue;
            ReadableValue = readableValue;
        }

        public TreeViewEntry(string key, string originalValue, string readableValue, TreeViewEntry[] children) {
            Key = key;
            OriginalValue = originalValue;
            ReadableValue = readableValue;
            Children = new(children);
        }

        public TreeViewEntry(string key, byte value, string? readable = null) {
            Key = key;
            OriginalValue = $"0x{value:X2}";
            ReadableValue = readable ?? $"{value}";
        }


        public TreeViewEntry(string key, ushort value, string? readable = null) {
            Key = key;
            OriginalValue = $"0x{value:X4}";
            ReadableValue = readable ?? $"{value}";
        }
    }
}