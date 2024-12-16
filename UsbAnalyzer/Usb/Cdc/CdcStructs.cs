using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsbAnalyzer.ViewData;

namespace UsbAnalyzer.Usb.Cdc {
    public class UsbClassCdcHeader : UsbClassGeneric {
        public ushort Version { get; }

        public UsbClassCdcHeader(byte[] data) : base(data) {
            try {
                Version = (ushort)(data[3] | data[4] << 8);
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Version", Version, ParseBcd(Version)),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "Header";
    }

    public class UsbClassCdcUnion : UsbClassGeneric {
        public byte ControlInterface { get; }

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

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Control Interface", ControlInterface),
                new("Sub Interfaces", SubInterface.Select((x, i) => new TreeViewEntry($"#{i}", x)).ToArray()),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "Union";
    }

    public class UsbClassCdcAcm : UsbClassGeneric {
        public byte Capabilities { get; }

        public UsbClassCdcAcm(byte[] data) : base(data) {
            try {
                Capabilities = data[3];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Capabilities", Capabilities, [
                    new("*CommFeature", "", $"{(Capabilities & 0x01) != 0}"),
                    new("*LineCoding", "", $"{(Capabilities & 0x02) != 0}"),
                    new("SendBreak", "", $"{(Capabilities & 0x04) != 0}"),
                    new("NetworkConnect", "", $"{(Capabilities & 0x08) != 0}")
                ]),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "Abstract Control Management";
    }

    public class UsbClassCdcEthernet : UsbClassGeneric {
        public byte MacAddrIdx { get; }
        public uint Statistics { get; }
        public ushort MaxSegmentSize { get; }
        public ushort NumberMcFilters { get; }
        public byte NumberPowerFilters { get; }

        public UsbClassCdcEthernet(byte[] data) : base(data) {
            try {
                MacAddrIdx = data[3];
                Statistics = (uint)(data[4] | data[5] << 8 | data[6] << 16 | data[7] << 24);
                MaxSegmentSize = (ushort)(data[8] | data[9] << 8);
                NumberMcFilters = (ushort)(data[9] | data[10] << 8);
                NumberPowerFilters = data[11];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("String Table Indices", [
                    new("Mac Address", MacAddrIdx)
                ]),
                new("Statistics", Statistics, [
                    new("XMIT_OK", "", $"{(Statistics & 0x0001) != 0}"),
                    new("RVC_OK", "", $"{(Statistics & 0x0002) != 0}"),
                    new("XMIT_ERROR", "", $"{(Statistics & 0x0004) != 0}"),
                    new("RCV_ERROR", "", $"{(Statistics & 0x0008) != 0}"),
                    new("RCV_NO_BUFFER", "", $"{(Statistics & 0x0010) != 0}"),
                    new("DIRECTED_BYTES_XMIT", "", $"{(Statistics & 0x0020) != 0}"),
                    new("DIRECTED_FRAMES_XMIT", "", $"{(Statistics & 0x0040) != 0}"),
                    new("MULTICAST_BYTES_XMIT", "", $"{(Statistics & 0x0080) != 0}"),
                    new("MULTICAST_FRAMES_XMIT", "", $"{(Statistics & 0x0100) != 0}"),
                    new("BROADCAST_BYTES_XMIT", "", $"{(Statistics & 0x0200) != 0}"),
                    new("BROADCAST_FRAMES_XMIT", "", $"{(Statistics & 0x0400) != 0}"),
                    new("DIRECTED_BYTES_RCV", "", $"{(Statistics & 0x0800) != 0}"),
                    new("DIRECTED_FRAMES_RCV", "", $"{(Statistics & 0x1000) != 0}"),
                    new("MULTICAST_BYTES_RCV", "", $"{(Statistics & 0x2000) != 0}"),
                    new("MULTICAST_FRAMES_RCV", "", $"{(Statistics & 0x4000) != 0}"),
                    new("BROADCAST_BYTES_RCV", "", $"{(Statistics & 0x8000) != 0}"),
                    new("BROADCAST_FRAMES_RCV", "", $"{(Statistics & 0x10000) != 0}"),
                    new("RCV_CRC_ERROR", "", $"{(Statistics & 0x20000) != 0}"),
                    new("TRANSMIT_QUEUE_LENGTH", "", $"{(Statistics & 0x40000) != 0}"),
                    new("RCV_ERROR_ALIGNMENT", "", $"{(Statistics & 0x80000) != 0}"),
                    new("XMIT_ONE_COLLISION", "", $"{(Statistics & 0x100000) != 0}"),
                    new("XMIT_MORE_COLLISIONS", "", $"{(Statistics & 0x200000) != 0}"),
                    new("XMIT_DEFERRED", "", $"{(Statistics & 0x400000) != 0}"),
                    new("XMIT_MAX_COLLISIONS", "", $"{(Statistics & 0x800000) != 0}"),
                    new("RCV_OVERRUN", "", $"{(Statistics & 0x1000000) != 0}"),
                    new("XMIT_UNDERRUN", "", $"{(Statistics & 0x2000000) != 0}"),
                    new("XMIT_HEARTBEAT_FAILURE", "", $"{(Statistics & 0x4000000) != 0}"),
                    new("XMIT_TIMES_CRS_LOST", "", $"{(Statistics & 0x8000000) != 0}"),
                    new("XMIT_LATE_COLLISIONS", "", $"{(Statistics & 0x10000000) != 0}")
                ]),
                new("Max Segment Size", MaxSegmentSize),
                new("Multicast Filters", NumberMcFilters, [
                    new("Perfect Filtering", "", $"{(NumberMcFilters & 0x8000) != 0}"),
                    new("Number of Filters", "", $"{NumberMcFilters & 0x7FFF}")
                ]),
                new("Power Filters", NumberPowerFilters),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "Ethernet Networking";
    }

    public class UsbClassCdcNcm : UsbClassGeneric {
        public ushort Version { get; }
        public byte Capabilities { get; }

        public UsbClassCdcNcm(byte[] data) : base(data) {
            try {
                Version = (ushort)(data[3] | data[4] << 8);
                Capabilities = data[5];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Version", Version, ParseBcd(Version)),
                new("Capabilities", Capabilities, [
                    new("Packet Filters", "", $"{(Capabilities & 0x01) != 0}"),
                    new("*NetAddress", "", $"{(Capabilities & 0x02) != 0}"),
                    new("Encapsulated Commands", "", $"{(Capabilities & 0x04) != 0}"),
                    new("*MaxDatagramSize", "", $"{(Capabilities & 0x08) != 0}"),
                    new("*CrcMode", "", $"{(Capabilities & 0x10) != 0}"),
                    new("8-byte NtbInputSize", "", $"{(Capabilities & 0x20) != 0}")
                ]),
                ..children
            ]);
        }

        protected override string GetSubtypeLabel() => "NCM";
    }
}