using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsbAnalyzer.ViewData;

namespace UsbAnalyzer.Usb.Audio {
    [Fallback("Unknown")]
    public enum JackType {
        Undefined = 0,
        Embedded = 1,
        External = 2,
    }

    [Fallback("Reserved")]
    public enum AudioEpGeneralLockDelayUnits {
        Undefined = 0,
        Milliseconds = 1,

        [Description("PCM Samples")]
        PcmSamples = 2
    }

    public class UsbAudioEndpoint : UsbEndpointDescriptor {
        public byte Refresh { get; }

        public byte SynchAddress { get; }

        public UsbAudioEndpoint(byte[] data) : base(data) {
            try {
                data = data[7..];

                Refresh = data[0];
                SynchAddress = data[1];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Refresh", Refresh, $"{Math.Pow(2, Refresh)}ms"),
                new("Synch Address", SynchAddress),
                ..children
            ]);
        }
    }

    public class UsbClassMidiHeader : UsbClassGeneric {
        public ushort MSC { get; }

        public ushort TotalLength { get; }

        public UsbClassMidiHeader(byte[] data) : base(data) {
            try {
                data = data[3..];
                MSC = (ushort)(data[0] | data[1] << 8);
                TotalLength = (ushort)(data[2] | data[3] << 8);
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Specification Version", MSC, ParseBcd(MSC)),
                new("Total Length", TotalLength),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "Header";
    }

    public class UsbClassMidiJackIn : UsbClassGeneric {
        public JackType JackType { get; }

        public byte JackId { get; }

        public byte JackIdx { get; }

        public UsbClassMidiJackIn(byte[] data) : base(data) {
            try {
                JackType = (JackType)data[3];
                JackId = data[4];
                JackIdx = data[5];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Jack Type", JackType),
                new("Jack ID", JackId),
                new("String Table Indices", [
                    new("Jack", JackIdx)
                ]),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "Jack In";
    }

    public class UsbClassMidiJackOut : UsbClassGeneric {
        public JackType JackType { get; }
        public byte JackId { get; }

        public byte InputPins { get; }

        public List<(byte SourceId, byte SourcePin)> Pins { get; } = new();

        public byte JackIdx { get; }

        public UsbClassMidiJackOut(byte[] data) : base(data) {
            try {
                JackType = (JackType)data[3];
                JackId = data[4];
                InputPins = data[5];

                for (int i = 0; i < InputPins; i++) {
                    var id = data[6 + i * 2];
                    var pin = data[7 + i * 2];

                    Pins.Add((id, pin));
                }

                JackIdx = data[6 + InputPins * 2];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Jack Type", JackType),
                new("Jack ID", JackId),
                new("Number of Input Pins", InputPins),
                new("Pins",
                    Pins.Select((x, i) => new TreeViewEntry($"#{i}", [
                        new("Source ID", x.SourceId),
                        new("Source Pin", x.SourcePin)
                    ])).ToArray()
                ),
                new("String Table Indices", [
                    new("Jack", JackIdx)
                ]),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "Jack Out";
    }

    public class UsbClassMidiElement : UsbClassGeneric {
        public byte ElementId { get; }

        public byte InputPins { get; }

        public List<(byte SourceId, byte SourcePin)> Pins { get; } = new();

        public byte OutputPins { get; }

        public byte InTerminalLink { get; }
        public byte OutTerminalLink { get; }

        public byte CapsSize { get; }

        public List<byte> Caps { get; } = new();

        public byte ElementIdx { get; }

        public UsbClassMidiElement(byte[] data) : base(data) {
            try {
                ElementId = data[3];
                InputPins = data[4];

                for (int i = 0; i < InputPins; i++) {
                    var id = data[5 + i * 2];
                    var pin = data[6 + i * 2];

                    Pins.Add((id, pin));
                }

                data = data[(6 + InputPins * 2)..];
                OutputPins = data[0];
                InTerminalLink = data[1];
                OutTerminalLink = data[2];
                CapsSize = data[3];

                for (int i = 0; i < CapsSize; i++) {
                    Caps.Add(data[4 + i]);
                }

                ElementIdx = data[5 + CapsSize];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Element ID", ElementId),
                new("Number of Input Pins", InputPins),
                new("Pins",
                    Pins.Select((x, i) => new TreeViewEntry($"Pin {i}", [
                        new("Source ID", x.SourceId),
                        new("Source Pin", x.SourcePin)
                    ])).ToArray()
                ),
                new("Number of Output Pins", OutputPins),
                new("Terminal Link", [
                    new("In", InTerminalLink),
                    new("Out", OutTerminalLink)
                ]),
                new("Capabilities Size", CapsSize),
                new("Capabilities", Caps.Aggregate("", (s, b) => $"{s} {b:X2}"), BuildCaps()),
                new("String Table Indices", [
                    new("Element", ElementIdx)
                ]),
                ..children
            ]);
        }

        private TreeViewEntry[] BuildCaps() {
            var list = new List<TreeViewEntry>();

            if (Caps.Count >= 1) {
                if ((Caps[0] & 0x01) != 0) {
                    list.Add(new("Custom Undefined"));
                }

                if ((Caps[0] & 0x02) != 0) {
                    list.Add(new("Midi Clock"));
                }

                if ((Caps[0] & 0x04) != 0) {
                    list.Add(new("Midi Time Code"));
                }

                if ((Caps[0] & 0x08) != 0) {
                    list.Add(new("Midi Machine Control"));
                }

                if ((Caps[0] & 0x10) != 0) {
                    list.Add(new("GM1"));
                }

                if ((Caps[0] & 0x20) != 0) {
                    list.Add(new("GM2"));
                }

                if ((Caps[0] & 0x40) != 0) {
                    list.Add(new("GS"));
                }

                if ((Caps[0] & 0x80) != 0) {
                    list.Add(new("XG"));
                }
            }

            if (Caps.Count >= 2) {
                if ((Caps[1] & 0x01) != 0) {
                    list.Add(new("EFX"));
                }

                if ((Caps[1] & 0x02) != 0) {
                    list.Add(new("Midi Patch Bay"));
                }

                if ((Caps[1] & 0x04) != 0) {
                    list.Add(new("DLS1"));
                }

                if ((Caps[1] & 0x08) != 0) {
                    list.Add(new("DLS2"));
                }
            }

            return list.ToArray();
        }

        protected override string GetSubtypeLabel() => "Element";
    }

    public class UsbClassEpMidiGeneral1 : UsbClassGeneric {
        public byte MidiJacks { get; }

        public List<byte> JackIds { get; } = new();

        public UsbClassEpMidiGeneral1(byte[] data) : base(data) {
            try {
                MidiJacks = data[3];

                for (int i = 0; i < MidiJacks; i++) {
                    JackIds.Add(data[4 + i]);
                }
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Number of Jacks", MidiJacks),
                new("Jacks", JackIds.Select((x,i) => new TreeViewEntry($"#{i}", x)).ToArray()),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "General V1";
    }
}