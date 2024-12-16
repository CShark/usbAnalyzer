using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbAnalyzer.Parser {
    internal class KingstParser : ParserBase {
        public override List<UsbLogEntry> Parse(string log) {
            var lines = log.Split("\r\n");
            var entries = new List<UsbLogEntry>();

            int runningPacketId = 0;
            int firstPacketIdx = 0;

            foreach (var line in lines.Skip(1)) {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = line.Split(",");

                // Parse each column into the appropriate type
                var timestamp = TimeSpan.FromSeconds(double.Parse(columns[0], CultureInfo.InvariantCulture));
                var pid = Enum.TryParse<PIDTypes>(columns[1], true, out var parsedPid)
                    ? parsedPid
                    : PIDTypes.Error;
                var address = TryParseHexByte(columns[2]);
                var endpoint = TryParseHexByte(columns[3]);
                var frame = TryParseHexUShort(columns[4]);
                var data = ParseDataField(columns[5]);
                var crc = TryParseHexUShort(columns[6]);

                // Create a new UsbLogEntry
                entries.Add(new UsbLogEntry(timestamp, pid, address, endpoint, frame, data, crc));
            }

            var packetStack = new List<UsbLogEntry>();
            var packetBuckets = new Dictionary<int, List<UsbLogPacket>>();

            foreach (var entry in entries) {
                if (entry.Pid == PIDTypes.Setup || entry.Pid == PIDTypes.In || entry.Pid == PIDTypes.Out) {
                    ComposePacket();
                    packetStack.Add(entry);
                } else if (entry.Pid == PIDTypes.NAK || entry.Pid == PIDTypes.ACK || entry.Pid == PIDTypes.STALL) {
                    packetStack.Add(entry);
                    ComposePacket();
                } else if (entry.Pid != PIDTypes.SOF && entry.Pid != PIDTypes.Error) {
                    packetStack.Add(entry);
                }
            }

            var groupStack = new List<UsbLogPacket>();
            foreach (var bucket in packetBuckets) {
                if ((bucket.Key & 0x00FF) == 0x00) {
                    foreach (var packet in bucket.Value) {
                        if (packet.Type == PacketTypes.Setup) {
                            ComposeSetupGroup();
                            groupStack.Add(packet);
                        } else {
                            groupStack.Add(packet);
                        }
                    }

                    ComposeSetupGroup();
                }
            }


            return entries;

            void ComposeSetupGroup() {
                if (groupStack.Any()) {
                    var group = new UsbLogPacketGroupSetup(groupStack);
                    groupStack.ForEach(x => x.PacketGroup = group);
                    groupStack.Clear();
                }
            }

            void ComposePacket() {
                if (packetStack.Any()) {
                    var packet = new UsbLogPacket(packetStack);
                    packetStack.ForEach(x => x.Packet = packet);
                    packetStack.Clear();

                    var id = packet.Address << 8 | packet.Endpoint;

                    if (!packetBuckets.ContainsKey(id)) {
                        packetBuckets[id] = new();
                    }

                    packetBuckets[id].Add(packet);
                }
            }
        }

        private static byte TryParseHexByte(string value) {
            value = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;

            return string.IsNullOrWhiteSpace(value)
                ? (byte)0
                : byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static ushort TryParseHexUShort(string value) {
            value = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;

            return string.IsNullOrWhiteSpace(value)
                ? (ushort)0
                : ushort.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static byte[] ParseDataField(string dataField) {
            if (string.IsNullOrWhiteSpace(dataField)) return Array.Empty<byte>();
            return dataField.Split(' ')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? d[2..] : d)
                .Select(d => byte.Parse(d, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToArray();
        }
    }
}