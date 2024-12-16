using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsbAnalyzer.Usb.Audio;
using UsbAnalyzer.Usb.Cdc;

namespace UsbAnalyzer.Usb {
    public class UsbDescriptorParser {
        public virtual (UsbDescriptor, UsbDescriptorParser) Parse(byte[] data, int index) {
            try {
                if (data.Length >= 2) {
                    var type = (DescriptorTypes)data[1];
                    UsbDescriptor desc;

                    if (type == DescriptorTypes.ClassInterface || type == DescriptorTypes.ClassEndpoint) {
                        return ParseSpecificDescriptor(type, data[2], data);
                    } else {
                        desc = type switch {
                            DescriptorTypes.Device => new UsbDeviceDescriptor(data),
                            DescriptorTypes.Configuration => new UsbConfigurationDescriptor(data),
                            DescriptorTypes.Interface => new UsbInterfaceDescriptor(data),
                            DescriptorTypes.String => index == 0
                                ? new UsbStringListDescriptor(data)
                                : new UsbStringDescriptor(data),
                            DescriptorTypes.Endpoint => new UsbEndpointDescriptor(data),
                            _ => new UsbDescriptor(data)
                        };
                    }

                    if (desc is IUsbClassProvider classId) {
                        return (desc, GetClassSpecificParser(classId));
                    } else {
                        return (desc, this);
                    }
                }
            } catch {
            }

            return (new UsbDescriptor(data), this);
        }

        protected virtual (UsbDescriptor, UsbDescriptorParser) ParseSpecificDescriptor(DescriptorTypes type,
            byte subtype, byte[] data) {
            return (new UsbClassGeneric(data), this);
        }

        protected UsbDescriptorParser GetClassSpecificParser(IUsbClassProvider classId) {
            if (classId.Class == DeviceClass.Audio) {
                if (classId is { Subclass: 0x03, Protocol: 0x00 }) {
                    return new MidiParser();
                }
            } else if (classId.Class == DeviceClass.Communications) {
                return new CdcParser();
            }

            return new UsbDescriptorParser();
        }

        public static string ParseClass(IUsbClassProvider classId) {
            switch (classId.Class) {
                case DeviceClass.Audio:
                    return ParseSubclass(classId);
                    break;
                case DeviceClass.Communications:
                    return ParseSubclass(classId);
                    break;
            }

            return "";
        }

        public static string ParseSubclass(IUsbClassProvider classId) {
            switch (classId.Class) {
                case DeviceClass.Audio:
                    switch (classId.Subclass) {
                        case 1:
                            return "Audio Control";
                        case 2:
                            return "Audio Streaming";
                        case 3:
                            return "MIDI Streaming";
                    }

                    break;
                case DeviceClass.Communications:
                    switch (classId.Subclass) {
                        case 0:
                            return "Reserved";
                        case 1:
                            return "Direct Line Control Mode";
                        case 2:
                            return "Abstract Control Model";
                        case 3:
                            return "Telephone Control Model";
                        case 4:
                            return "Multi-Channel Control Model";
                        case 5:
                            return "CAPI Control Model";
                        case 6:
                            return "Ethernet Networking Control Model";
                        case 7:
                            return "ATM Networking Control Model";
                        case 8:
                            return "Wireless Handset Control Model";
                        case 9:
                            return "Device Management";
                        case 10:
                            return "Mobile Direct Line Model";
                        case 11:
                            return "OBEX";
                        case 12:
                            return "Ethernet Emulation Model";
                        case 13:
                            return "Network Control Model";
                        case > 0x80 and <= 0xFE:
                            return "Vendor Specific";
                    }

                    break;
            }

            return "";
        }

        public static string ParseProtocol(IUsbClassProvider classId) {
            switch (classId.Class) {
                case DeviceClass.Communications:
                    switch (classId.Protocol) {
                        case 1:
                            return "AT: V.250";
                        case 2:
                            return "AT: PCCA-101";
                        case 3:
                            return "AT: PCCA-101, Annex O";
                        case 4:
                            return "AT: GSM 07.07";
                        case 5:
                            return "AT: 3GPP 23.007";
                        case 6:
                            return "AT: TIA CDMA";
                        case 7:
                            return "Ethernet Emulation Model";
                        case 0xFE:
                            return "External";
                        case 0xFF:
                            return "Vendor specific";
                    }

                    break;
            }

            return "";
        }
    }
}