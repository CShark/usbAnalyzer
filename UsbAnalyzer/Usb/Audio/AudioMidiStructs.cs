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

    public class PinInfo {
        [Offset(0)]
        [Description("Source ID")]
        public byte SourceId { get; }

        [Offset(1)]
        [Description("Source Pin")]
        public byte SourcePin { get; }

        public PinInfo(byte sourceId, byte sourcePin) {
            SourceId = sourceId;
            SourcePin = sourcePin;
        }
    }

    public class UsbAudioEndpoint : UsbEndpointDescriptor {
        [Offset(7)]
        [ReadableAccessor(nameof(RefreshHelper))]
        public byte Refresh { get; }

        [Offset(8)]
        public byte SynchAddress { get; }

        public string RefreshHelper => $"{Math.Pow(2, Refresh)}ms";

        public UsbAudioEndpoint(byte[] data) : base(data) {
            try {
                data = data[7..];

                Refresh = data[0];
                SynchAddress = data[1];
            } catch {
            }
        }
    }

    public class UsbClassMidiHeader : UsbClassGeneric {
        [Offset(3)]
        [BcdDisplay]
        public ushort MSC { get; }

        [Offset(5)]
        public ushort TotalLength { get; }

        public UsbClassMidiHeader(byte[] data) : base(data) {
            try {
                data = data[3..];
                MSC = (ushort)(data[0] | data[1] << 8);
                TotalLength = (ushort)(data[2] | data[3] << 8);
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Header";
    }

    public class UsbClassMidiJackIn : UsbClassGeneric {
        [Offset(3)]
        public JackType JackType { get; }

        [Offset(4)]
        public byte JackId { get; }

        [Offset(5)]
        public byte JackIdx { get; }

        public UsbClassMidiJackIn(byte[] data) : base(data) {
            try {
                JackType = (JackType)data[3];
                JackId = data[4];
                JackIdx = data[5];
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Jack In";
    }

    public class UsbClassMidiJackOut : UsbClassGeneric {
        [Offset(3)]
        public JackType JackType { get; }

        [Offset(4)]
        public byte JackId { get; }

        [Offset(5)]
        public byte InputPins { get; }

        [Offset(6)]
        public List<PinInfo> Pins { get; } = new();

        [Offset(7)]
        public byte JackIdx { get; }

        public UsbClassMidiJackOut(byte[] data) : base(data) {
            try {
                JackType = (JackType)data[3];
                JackId = data[4];
                InputPins = data[5];

                for (int i = 0; i < InputPins; i++) {
                    var id = data[6 + i * 2];
                    var pin = data[7 + i * 2];

                    Pins.Add(new(id, pin));
                }

                JackIdx = data[6 + InputPins * 2];
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Jack Out";
    }

    public class UsbClassMidiElement : UsbClassGeneric {
        [Flags]
        public enum MidiCapabilities {
            None = 0,
            CustomUndefined = 0x01,
            MidiClock = 0x02,
            MidiTimeCode = 0x04,
            MidiMachineControl = 0x08,
            GM1 = 0x10,
            GM2 = 0x20,
            GS = 0x40,
            XG = 0x80,
            EFX = 0x0100, // Second byte starts here
            MidiPatchBay = 0x0200,
            DLS1 = 0x0400,
            DLS2 = 0x0800,
        }

        [Offset(3)]
        public byte ElementId { get; }

        [Offset(4)]
        public byte InputPins { get; }

        [Offset(5)]
        public List<PinInfo> Pins { get; } = new();

        [Offset(6)]
        public byte OutputPins { get; }

        [Offset(7)]
        public byte InTerminalLink { get; }

        [Offset(8)]
        public byte OutTerminalLink { get; }

        [Offset(9)]
        public byte CapsSize { get; }

        [Offset(10)]
        public MidiCapabilities Caps { get; }

        [Offset(11)]
        public byte ElementIdx { get; }

        public UsbClassMidiElement(byte[] data) : base(data) {
            try {
                ElementId = data[3];
                InputPins = data[4];

                for (int i = 0; i < InputPins; i++) {
                    var id = data[5 + i * 2];
                    var pin = data[6 + i * 2];

                    Pins.Add(new(id, pin));
                }

                data = data[(6 + InputPins * 2)..];
                OutputPins = data[0];
                InTerminalLink = data[1];
                OutTerminalLink = data[2];
                CapsSize = data[3];

                uint caps = 0;
                for (int i = 0; i < CapsSize; i++) {
                    caps |= (uint)(data[4 + i] << (i * 8));
                }

                Caps = (MidiCapabilities)caps;

                ElementIdx = data[5 + CapsSize];
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Element";
    }

    public class UsbClassEpMidiGeneral1 : UsbClassGeneric {
        [Offset(3)]
        public byte MidiJacks { get; }

        [Offset(4)]
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

        protected override string GetSubtypeLabel() => "General V1";
    }
}