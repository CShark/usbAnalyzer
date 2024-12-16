﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

            RawEntries = entries.ToArray();
        }
    }

    public class UsbLogPacketGroup {
        public PacketTypes Type { get; protected set; }
        public byte Address { get; protected set; }
        public byte Endpoint { get; protected set; }

        public UsbLogPacket[] RawPackets { get; protected set; }

        protected UsbLogPacketGroup() {
        }
    }

    public class UsbLogPacketGroupSetup : UsbLogPacketGroup {
        public UsbSetup SetupData { get; }

        public List<UsbDescriptor> Descriptors { get; } = new();

        public UsbLogPacketGroupSetup(List<UsbLogPacket> packets) {
            Type = packets.First().Type;
            Address = packets.First().Address;
            Endpoint = packets.First().Endpoint;

            var data = packets.SelectMany(x => x.Data).ToArray();

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            SetupData = Marshal.PtrToStructure<UsbSetup>(handle.AddrOfPinnedObject());
            handle.Free();

            UsbDescriptorParser parser = new UsbDescriptorParser();

            if (SetupData.ParsedRequest == Requests.SetAddress) {
                Address = (byte)SetupData.Value;
            }

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

            RawPackets = packets.ToArray();
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