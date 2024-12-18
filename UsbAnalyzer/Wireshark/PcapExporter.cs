using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using UsbAnalyzer.Usb;

namespace UsbAnalyzer.Wireshark {
    internal class PcapExporter {
        private enum EState : uint {
            Success,
            Pending,
            Error,
        }

        private enum ECtrlStage : byte {
            Setup,
            Data,
            Status,
            Complete
        }

        private enum ETransfer : byte {
            Isochronous = 0,
            Interrupt = 1,
            Control = 2,
            Bulk = 3
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PcapPacket {
            public uint TimestampSeconds;
            public uint TimestampMicroseconds;
            public uint CaptureLength;
            public uint OriginalLength;

            public PcapPacket(TimeSpan timestamp) {
                TimestampSeconds = (uint)timestamp.TotalSeconds;
                TimestampMicroseconds = (uint)(timestamp.TotalMicroseconds % 1000000);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct UsbPcapHeaderPacket {
            public ushort Len;
            public ulong IrpId;
            public EState State;
            public ushort Function;
            public byte Info;
            public ushort Bus;
            public ushort Device;
            public byte Endpoint;
            public ETransfer Transfer;
            public uint DataLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct UsbPcapHeaderCtrl {
            public UsbPcapHeaderPacket Header;
            public ECtrlStage Stage;
        }

        public void Export(Stream target, IEnumerable<UsbLogEntry> entries) {
            var packets = entries.Select(x => x.Packet).Distinct().Where(x => x != null).Select(x => x.PacketGroup)
                .Distinct().Where(x => x != null).ToList();

            using var writer = new BinaryWriter(target);

            WritePcapHeader(writer);

            foreach (var packet in packets) {
                WritePacket(writer, packet);
            }
        }

        private void WritePcapHeader(BinaryWriter writer) {
            writer.Write(0xA1B2C3D4); // Magic Number (Timestamp in Microseconds)
            writer.Write((ushort)2); // Major Version
            writer.Write((ushort)4); // Minor Version
            writer.Write(0); // Reserved 1
            writer.Write(0); // Reserved 2
            writer.Write(65535); // Snap length (max bytes captured per packet)
            writer.Write(0xF9); // LINKTYPE_USBPCAP (249)
        }

        private void WritePacket(BinaryWriter writer, UsbLogPacketGroup packet) {
            if (packet is UsbLogPacketGroupSetup setup) {
                writer.Write(BuildControlPacket(setup));
            } else if (packet.RawPackets[0].Response == PacketResponses.Unknown) {
                // Iso packet
            } else {
                // Bulk / Interrupt
                writer.Write(BuildBulkIntPacket(packet));
            }
        }

        private byte[] BuildBulkIntPacket(UsbLogPacketGroup data) {
            var bytes = data.RawPackets.SelectMany(x => x.Data).ToArray();
            var header = new PcapPacket(data.Timestamp);
            var usbHeader = new UsbPcapHeaderPacket {
                DataLength = (uint)bytes.Length,
                IrpId = (ulong)data.GetHashCode(),
                Info = (byte)(data.Type == PacketTypes.Out ? 0 : 1),
                Bus = 0,
                Device = data.Address,
                Endpoint = data.Endpoint,
                Transfer = data.PipeType == PipeType.Interrupt ? ETransfer.Interrupt : ETransfer.Bulk,
                Function = 9
            };

            usbHeader.Len = (ushort)Marshal.SizeOf(usbHeader);
            header.CaptureLength = header.OriginalLength = usbHeader.Len + usbHeader.DataLength;

            var result = new byte[header.CaptureLength + Marshal.SizeOf(header)];
            var hData = StructToBytes(header);
            var uData = StructToBytes(usbHeader);
            hData.CopyTo(result, 0);
            uData.CopyTo(result, hData.Length);
            bytes.CopyTo(result, hData.Length + uData.Length);
            return result;
        }

        private byte[] BuildControlPacket(UsbLogPacketGroupSetup setup) {
            byte[] startPacket;
            byte[] endPacket;

            {
                var outData = setup.RawPackets.Where(x => x.Type == PacketTypes.Out || x.Type == PacketTypes.Setup)
                    .ToList();
                var header = new PcapPacket(outData[0].Timestamp);
                var usbHeader = new UsbPcapHeaderCtrl {
                    Header = new() {
                        DataLength = (uint)outData.Aggregate(0, (l, x) => l + x.Data.Length),
                        IrpId = (ulong)setup.GetHashCode(),
                        State = EState.Success,
                        Info = 0,
                        Bus = 0,
                        Device = setup.Address,
                        Endpoint = setup.Endpoint,
                        Transfer = ETransfer.Control,

                        Function = setup.SetupData.ParsedRequest switch {
                            Requests.GetDescriptor => 0x0B,
                            _ => 0x08
                        }
                    },
                    Stage = ECtrlStage.Setup
                };

                usbHeader.Header.Len = (ushort)Marshal.SizeOf(usbHeader);
                header.CaptureLength = header.OriginalLength = usbHeader.Header.Len + usbHeader.Header.DataLength;

                startPacket = new byte[header.CaptureLength + Marshal.SizeOf(header)];
                var hData = StructToBytes(header);
                var uData = StructToBytes(usbHeader);
                hData.CopyTo(startPacket, 0);
                uData.CopyTo(startPacket, hData.Length);
                outData.SelectMany(x => x.Data).ToArray().CopyTo(startPacket, hData.Length + uData.Length);
            }

            {
                var inData = setup.RawPackets.Where(x => x.Type == PacketTypes.In).ToList();
                var header = new PcapPacket(inData.FirstOrDefault()?.Timestamp ?? setup.RawPackets[0].Timestamp);
                var usbHeader = new UsbPcapHeaderCtrl {
                    Header = new() {
                        DataLength = (uint)inData.Aggregate(0, (l, x) => l + x.Data.Length),
                        IrpId = (ulong)setup.GetHashCode(),
                        State = inData.LastOrDefault()?.Response == PacketResponses.NAK ? EState.Error : EState.Success,
                        Info = 1,
                        Bus = 0,
                        Device = setup.Address,
                        Endpoint = setup.Endpoint,
                        Transfer = ETransfer.Control,
                        Function = 0x08
                    },
                    Stage = ECtrlStage.Complete
                };

                usbHeader.Header.Len = (ushort)Marshal.SizeOf(usbHeader);
                header.CaptureLength = header.OriginalLength = usbHeader.Header.Len + usbHeader.Header.DataLength;

                endPacket = new byte[header.CaptureLength + Marshal.SizeOf(header)];
                var hData = StructToBytes(header);
                var uData = StructToBytes(usbHeader);
                hData.CopyTo(endPacket, 0);
                uData.CopyTo(endPacket, hData.Length);
                inData.SelectMany(x => x.Data).ToArray().CopyTo(endPacket, hData.Length + uData.Length);
            }

            return startPacket.Concat(endPacket).ToArray();
        }

        private byte[] StructToBytes<T>(T value) where T : struct {
            var size = Marshal.SizeOf(value);
            var data = new byte[size];
            var ptr = IntPtr.Zero;
            try {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(value, ptr, true);
                Marshal.Copy(ptr, data, 0, size);
            } finally {
                Marshal.FreeHGlobal(ptr);
            }

            return data;
        }
    }
}