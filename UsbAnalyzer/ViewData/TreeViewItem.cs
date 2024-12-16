using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsbAnalyzer.Usb;

namespace UsbAnalyzer.ViewData {
    public class TreeViewEntry {
        public ObservableCollection<TreeViewEntry> Children { get; set; } = new();

        public string Key { get; set; } = "";

        public string OriginalValue { get; set; } = "";
        public string ReadableValue { get; set; } = "";

        public bool IsRoot { get; set; } = false;

        public TreeViewEntry(string key) {
            Key = key;
        }

        public TreeViewEntry(string key, TreeViewEntry[] children) {
            Key = key;
            Children = new(children);
        }

        public TreeViewEntry(string key, string originalValue) {
            Key = key;
            OriginalValue = originalValue;
        }

        public TreeViewEntry(string key, string originalValue, TreeViewEntry[] children) {
            Key = key;
            OriginalValue = originalValue;
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

        public TreeViewEntry(string key, byte value, TreeViewEntry[] children) {
            Key = key;
            OriginalValue = $"0x{value:X2}";
            Children = new(children);
        }

        public TreeViewEntry(string key, ushort value, string? readable = null) {
            Key = key;
            OriginalValue = $"0x{value:X4}";
            ReadableValue = readable ?? $"{value}";
        }

        public TreeViewEntry(string key, ushort value, TreeViewEntry[] children) {
            Key = key;
            OriginalValue = $"0x{value:X4}";
            Children = new(children);
        }

        public TreeViewEntry(string key, Enum value, string? readable = null) {
            Key = key;
            var rawValue = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
            OriginalValue = $"0x{rawValue:X2}";
            ReadableValue = readable ?? value.ToReadableString();
        }

        public TreeViewEntry(string key, uint value, TreeViewEntry[] children) {
            Key = key;
            OriginalValue = $"0x{value:X8}";
            Children = new(children);
        }
    }
}