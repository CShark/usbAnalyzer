using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbAnalyzer.Usb.Audio {
    internal class MidiParser : AudioParser {
        protected override (UsbDescriptor, UsbDescriptorParser) ParseSpecificDescriptor(DescriptorTypes type,
            byte subtype, byte[] data) {
            if (type == DescriptorTypes.ClassInterface) {
                switch (subtype) {
                    case 0x01: // Header
                        var desc = new UsbClassMidiHeader(data);
                        if ((desc.MSC & 0xFF00) == 0x0200) {
                            return (desc, new MidiParserV2());
                        } else {
                            return (desc, new MidiParserV1());
                        }
                    case 0x02: // Jack In
                        return (new UsbClassMidiJackIn(data), this);
                    case 0x03: // Jack Out
                        return (new UsbClassMidiJackOut(data), this);
                    case 0x04: // Element
                        return (new UsbClassMidiElement(data), this);
                        break;
                }
            } else if (type == DescriptorTypes.ClassEndpoint) {
                switch (subtype) {
                    case 0x01: // MS General V1
                        return (new UsbClassEpMidiGeneral1(data), this);
                    case 0x02: // MS General V2
                        break;
                }
            }

            return base.ParseSpecificDescriptor(type, subtype, data);
        }


        public class MidiParserV1 : MidiParser {
            public override (UsbDescriptor, UsbDescriptorParser) Parse(byte[] data, int index) {
                var type = (DescriptorTypes)data[1];

                if (type == DescriptorTypes.Endpoint) {
                    return (new UsbAudioEndpoint(data), this);
                }

                return base.Parse(data, index);
            }
        }

        public class MidiParserV2 : MidiParser {
            // TODO: Implement Terminal Blocks
        }
    }
}