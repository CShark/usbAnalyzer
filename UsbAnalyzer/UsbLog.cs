using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UsbAnalyzer.Parser;
using UsbAnalyzer.Usb;

namespace UsbAnalyzer {
    public enum PIDTypes : byte {
        Error,
        SOF,
        Setup,
        Data0,
        Data1,
        ACK,
        In,
        Out,
        NAK,
        STALL,
    }

    public enum PacketTypes : byte {
        SOF,
        Setup,
        In,
        Out,
    }

    public enum PacketResponses : byte {
        Unknown,
        ACK,
        NAK,
        STALL,
    }

    public enum PipeType : byte {
        Control,
        Isochronous,
        Bulk,
        Interrupt,
    }

    public record UsbLogEntry(
        TimeSpan Timestamp,
        PIDTypes Pid,
        byte Address,
        byte Endpoint,
        ushort Frame,
        byte[] Data,
        ushort CRC) {
        public UsbLogPacket? Packet { get; set; }

        public string DataText => Data != null && Data.Length > 0
            ? string.Join(" ", Data.Select(b => b.ToString("X2")))
            : string.Empty;
    }

    public class UsbLogPacket {
        public PacketTypes Type { get; }
        public PacketResponses Response { get; }
        public byte Address { get; }
        public byte Endpoint { get; }
        public ushort? Frame { get; }

        public TimeSpan Timestamp { get; }

        public byte[] Data { get; }

        public UsbLogEntry[] RawEntries { get; }

        public UsbLogPacketGroup? PacketGroup { get; set; }

        public UsbLogPacket(List<UsbLogEntry> entries) {
            Type = entries.First().Pid switch {
                PIDTypes.Setup => PacketTypes.Setup,
                PIDTypes.In => PacketTypes.In,
                PIDTypes.Out => PacketTypes.Out,
                PIDTypes.SOF => PacketTypes.SOF,
                _ => throw new ArgumentException("The first transfer has to be a Setup, In, Out or SOF")
            };

            Response = entries.Last().Pid switch {
                PIDTypes.ACK => PacketResponses.ACK,
                PIDTypes.NAK => PacketResponses.NAK,
                PIDTypes.STALL => PacketResponses.STALL,
                _ => PacketResponses.Unknown
            };

            Address = entries.First().Address;
            Endpoint = entries.First().Endpoint;

            if (Type == PacketTypes.SOF) {
                Frame = entries.First().Frame;
                Data = [];
            } else {
                Data = entries.FirstOrDefault(x => x.Pid == PIDTypes.Data0 || x.Pid == PIDTypes.Data1)?.Data ?? [];
            }

            Timestamp = entries.First().Timestamp;
            RawEntries = entries.ToArray();
        }
    }

    public class UsbLogPacketGroup {
        public PacketTypes Type { get; protected set; }

        public PipeType PipeType { get; protected set; }

        public byte Address { get; protected set; }
        public byte Endpoint { get; protected set; }

        public TimeSpan Timestamp { get; protected set; }

        public UsbLogPacket[] RawPackets { get; protected set; }

        public UsbLogPacketGroup(List<UsbLogPacket> packets) {
            RawPackets = packets.ToArray();
            Timestamp = packets.First().Timestamp;
            Address = packets.First().Address;
            Endpoint = packets.First().Endpoint;
            Type = packets.First().Type;

            if (packets.Last().Response == PacketResponses.Unknown) {
                PipeType = PipeType.Isochronous;
            } else {
                PipeType = packets.Count > 1 ? PipeType.Bulk : PipeType.Interrupt;
            }
        }
    }

    public class UsbLogPacketGroupSetup : UsbLogPacketGroup {
        public UsbSetup SetupData { get; }

        public byte[] SetupBytes { get; }

        public List<UsbDescriptor> Descriptors { get; } = new();

        public UsbLogPacketGroupSetup(List<UsbLogPacket> packets) : base(packets) {
            var data = packets.SelectMany(x => x.Data).ToArray();

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            SetupData = Marshal.PtrToStructure<UsbSetup>(handle.AddrOfPinnedObject());
            handle.Free();

            UsbDescriptorParser parser = new UsbDescriptorParser();

            if (SetupData.ParsedRequest == Requests.SetAddress) {
                Address = (byte)SetupData.Value;
            }

            SetupBytes = data[..8];

            if (data.Length > 8) {
                data = data[8..];

                try {
                    while (data.Length > 0) {
                        var len = Math.Min(data[0], data.Length);
                        var packet = data[..len];

                        (var desc, parser) = parser.Parse(packet, SetupData.Index);
                        Descriptors.Add(desc);

                        data = data[len..];
                    }
                } catch (ArgumentOutOfRangeException) {
                }
            }

            PipeType = PipeType.Control;
        }
    }

    public static class UsbLogDecoder {
        public static List<UsbLogEntry>? Decode(string log) {
            ParserBase? parser = null;

            if (log.StartsWith("Time [s],PID,Address,Endpoint,Frame #,Data,CRC")) {
                parser = new KingstParser();
            }

            return parser?.Parse(log);
        }
    }
}