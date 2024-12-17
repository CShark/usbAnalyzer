using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsbAnalyzer.ViewData;

namespace UsbAnalyzer.Usb.Cdc {
    public class UsbClassCdcHeader : UsbClassGeneric {
        [Offset(3)]
        [BcdDisplay]
        public ushort Version { get; }

        public UsbClassCdcHeader(byte[] data) : base(data) {
            try {
                Version = (ushort)(data[3] | data[4] << 8);
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Header";
    }

    public class UsbClassCdcUnion : UsbClassGeneric {
        [Offset(3)]
        public byte ControlInterface { get; }

        [Offset(4)]
        public List<byte> SubInterface { get; } = new();

        public UsbClassCdcUnion(byte[] data) : base(data) {
            try {
                ControlInterface = data[3];

                for (int i = 4; i < data.Length; i++) {
                    SubInterface.Add(data[i]);
                }
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Union";
    }

    public class UsbClassCdcAcm : UsbClassGeneric {
        [Flags]
        public enum ECaps : byte {
            [Description("*CommFeature")]
            CommFeature = 0x01,

            [Description("*LineCoding")]
            LineCoding = 0x02,
            SendBreak = 0x04,
            NetworkConnect = 0x08
        }

        [Offset(3)]
        public ECaps Capabilities { get; }

        public UsbClassCdcAcm(byte[] data) : base(data) {
            try {
                Capabilities = (ECaps)data[3];
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Abstract Control Management";
    }

    public class UsbClassCdcEthernet : UsbClassGeneric {
        [Flags]
        public enum ENetworkStatistics : uint {
            XmitOk = 0x0001,
            RcvOk = 0x0002,
            XmitError = 0x0004,
            RcvError = 0x0008,
            RcvNoBuffer = 0x0010,
            DirectedBytesXmit = 0x0020,
            DirectedFramesXmit = 0x0040,
            MulticastBytesXmit = 0x0080,
            MulticastFramesXmit = 0x0100,
            BroadcastBytesXmit = 0x0200,
            BroadcastFramesXmit = 0x0400,
            DirectedBytesRcv = 0x0800,
            DirectedFramesRcv = 0x1000,
            MulticastBytesRcv = 0x2000,
            MulticastFramesRcv = 0x4000,
            BroadcastBytesRcv = 0x8000,
            BroadcastFramesRcv = 0x10000,
            RcvCrcError = 0x20000,
            TransmitQueueLength = 0x40000,
            RcvErrorAlignment = 0x80000,
            XmitOneCollision = 0x100000,
            XmitMoreCollisions = 0x200000,
            XmitDeferred = 0x400000,
            XmitMaxCollisions = 0x800000,
            RcvOverrun = 0x1000000,
            XmitUnderrun = 0x2000000,
            XmitHeartbeatFailure = 0x4000000,
            XmitTimesCrsLost = 0x8000000,
            XmitLateCollisions = 0x10000000
        }

        [Offset(3)]
        [StringTable]
        [Description("Mac Address")]
        public byte MacAddrIdx { get; }

        [Offset(4)]
        public ENetworkStatistics Statistics { get; }

        [Offset(8)]
        public ushort MaxSegmentSize { get; }

        [Offset(10)]
        public ushort NumberMcFilters { get; }

        [Offset(12)]
        public byte NumberPowerFilters { get; }

        [GroupProperty(nameof(NumberMcFilters))]
        public bool PerfectFilter => (NumberMcFilters & 0x8000) != 0;
        [GroupProperty(nameof(NumberMcFilters))]
        public int NumFilters => (NumberMcFilters & 0x7FFF);

        public UsbClassCdcEthernet(byte[] data) : base(data) {
            try {
                MacAddrIdx = data[3];
                Statistics = (ENetworkStatistics)(data[4] | data[5] << 8 | data[6] << 16 | data[7] << 24);
                MaxSegmentSize = (ushort)(data[8] | data[9] << 8);
                NumberMcFilters = (ushort)(data[10] | data[11] << 8);
                NumberPowerFilters = data[12];
            } catch {
            }
        }

        protected override string GetSubtypeLabel() => "Ethernet Networking";
    }

    public class UsbClassCdcNcm : UsbClassGeneric {
        [Flags]
        public enum ECapabilitiesFlags {
            [Description("Packet Filters")]
            PacketFilters = 0x01,
            [Description("*NetAddress")]
            NetAddress = 0x02,
            [Description("Encapsulated Commands")]
            EncapsulatedCommands = 0x04,
            [Description("*MaxDatagramSize")]
            MaxDatagramSize = 0x08,
            [Description("*CrcMode")]
            CrcMode = 0x10,
            [Description("8-byte NtbInputSize")]
            LargeNtbInputSize = 0x20
        }

        [Offset(3)]
        public ushort Version { get; }
        [Offset(5)]
        public ECapabilitiesFlags Capabilities { get; }

        public UsbClassCdcNcm(byte[] data) : base(data) {
            try {
                Version = (ushort)(data[3] | data[4] << 8);
                Capabilities = (ECapabilitiesFlags)data[5];
            } catch {
            }
        }
        protected override string GetSubtypeLabel() => "NCM";
    }
}