using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Navigation;
using UsbAnalyzer.ViewData;

namespace UsbAnalyzer.Usb {
    #region Descriptor Enums

    public enum DataTransferDirection : byte {
        [Description("Host to Device")]
        HostToDevice,

        [Description("Device to Host")]
        DeviceToHost
    }

    [Fallback("Reserved")]
    public enum DataTransferType : byte {
        Standard,
        Class,
        Vendor,
        Reserved
    }

    [Fallback("Reserved")]
    public enum DataTransferTarget : byte {
        Device,
        Interface,
        Endpoint,
        Other
    }

    [Fallback("Unknown Descriptor")]
    public enum DescriptorTypes : byte {
        [Description("Device Descriptor")]
        Device = 0x01,

        [Description("Configuration Descriptor")]
        Configuration = 0x02,

        [Description("String Descriptor")]
        String = 0x03,

        [Description("Interface Descriptor")]
        Interface = 0x04,

        [Description("Endpoint Descriptor")]
        Endpoint = 0x05,

        [Description("Class Specific Interface Descriptor")]
        ClassInterface = 0x24,

        [Description("Class Specific Endpoint Descriptor")]
        ClassEndpoint = 0x25
    }

    [Fallback("Unknown Request")]
    public enum Requests {
        [Description("Get Status")]
        GetStatusD = 0x8000,

        [Description("Clear Feature")]
        ClearFeatureD = 0x0001,

        [Description("Set Feature")]
        SetFeatureD = 0x0003,

        [Description("Set Address")]
        SetAddress = 0x0005,

        [Description("Get Descriptor")]
        GetDescriptor = 0x8006,

        [Description("Set Descriptor")]
        SetDescriptor = 0x0007,

        [Description("Get Configuration")]
        GetConfiguration = 0x8008,

        [Description("Set Configuration")]
        SetConfiguration = 0x0009,

        [Description("Get Status")]
        GetStatusI = 0x8100,

        [Description("Clear Feature")]
        ClearFeatureI = 0x0101,

        [Description("Set Feature")]
        SetFeatureI = 0x0103,

        [Description("Get Interface")]
        GetInterface = 0x810A,

        [Description("Set Interface")]
        SetInterface = 0x800B,

        [Description("Get Status")]
        GetStatusE = 0x8200,

        [Description("Clear Feature")]
        ClearFeatureE = 0x0201,

        [Description("Set Feature")]
        SetFeatureE = 0x0203,

        [Description("Synch Frame")]
        SynchFrame = 0x8212
    }

    public enum EpTransferType {
        Control = 0,
        Isochronous = 1,
        Bulk = 2,
        Interrupt = 3,
    }

    public enum EpDirection {
        Out = 0,
        In = 1
    }

    public enum EpSync {
        [Description("No synchronisation")]
        NoSync = 0,

        [Description("Asynchronous")]
        Async = 1,
        Adaptive = 2,

        [Description("Synchronous")]
        Sync = 3
    }

    [Fallback("Reserved")]
    public enum EpUsage {
        Data = 0,
        Feedback = 1,

        [Description("Explicit Feedback Data")]
        ExplicitFeedback = 2,
        Reserved = 3
    }

    public enum DeviceClass {
        Undefined = 0x00,
        Audio = 0x01,
        Communications = 0x02,
        HID = 0x03,
        Physical = 0x05,
        Image = 0x06,
        Printer = 0x07,

        [Description("Mass Storage")]
        MassStorage = 0x08,
        Hub = 0x09,

        [Description("CDC Data")]
        CDCData = 0x0A,

        [Description("Smart Card")]
        SmartCard = 0x0B,

        [Description("Content Security")]
        ContentSecurity = 0x0D,
        Video = 0x0E,

        [Description("Personal Healthcare")]
        PersonalHealthcare = 0x0F,

        [Description("Audio/Video")]
        AudioVideo = 0x10,

        [Description("Diagnostic Device")]
        DiagnosticDevice = 0xDC,
        Miscellaneous = 0xEF,

        [Description("Application-Specific")]
        ApplicationSpecific = 0xFE,

        [Description("Vendor-Specific")]
        VendorSpecific = 0xFF
    }

    #endregion

    [StructLayout(LayoutKind.Sequential)]
    public struct UsbSetup {
        [Offset(0)]
        public byte RequestType { get; set; }
        [Offset(1)]
        [ReadableAccessor(nameof(RequestHelper))]
        public byte Request { get; set; }
        [Offset(2)]
        public ushort Value { get; set; }
        [Offset(4)]
        public ushort Index { get; set; }
        [Offset(6)]
        public ushort Length { get; set; }

        [GroupProperty(nameof(RequestType))]
        public DataTransferDirection Direction => (DataTransferDirection)((RequestType & 0b1000_0000) >> 7);
        [GroupProperty(nameof(RequestType))]
        public DataTransferType Type => (DataTransferType)((RequestType & 0b0111_0000) >> 4);
        [GroupProperty(nameof(RequestType))]
        public DataTransferTarget Target => (DataTransferTarget)((RequestType & 0b0000_1111));
        
        public Requests ParsedRequest => (Requests)(RequestType << 8 | Request);

        public string RequestHelper => $"{ParsedRequest}";
    }

    public class UsbDescriptor {
        [Offset(0)]
        public byte? Length { get; }

        [Offset(1)]
        public DescriptorTypes? Type { get; }

        public byte[] Data { get; }

        public UsbDescriptor(byte[] data) {
            Data = [];
            try {
                Length = data[0];
                Type = (DescriptorTypes)data[1];
                Data = data[2..];
            } catch (ArgumentOutOfRangeException) {
            }
        }
    }

    public interface IUsbClassProvider {
        public DeviceClass? Class { get; }
        public byte? Subclass { get; }
        public byte? Protocol { get; }
    }

    #region General Descriptors

    public class UsbDeviceDescriptor : UsbDescriptor, IUsbClassProvider {
        [BcdDisplay]
        [Offset(3)]
        public ushort? UsbVersion { get; }

        [Helper]
        [Description("Device Class")]
        public string DeviceClass => UsbDescriptorParser.ParseClass(this);

        [Offset(4)]
        [GroupProperty(nameof(DeviceClass))]
        public DeviceClass? Class { get; }

        [Offset(5)]
        [GroupProperty(nameof(DeviceClass))]
        [ReadableAccessor(nameof(SubclassHelper))]
        public byte? Subclass { get; }

        [Offset(6)]
        [GroupProperty(nameof(DeviceClass))]
        [ReadableAccessor(nameof(ProtocolHelper))]
        public byte? Protocol { get; }

        [Offset(7)]
        public byte? MaxPacketSize { get; }

        [GroupProperty("Vendor ID")]
        [Offset(8)]
        public ushort? Vendor { get; }

        [GroupProperty("Vendor ID")]
        [Offset(10)]
        public ushort? Product { get; }

        [GroupProperty("Vendor ID")]
        [Offset(12)]
        public ushort? Revision { get; }

        [StringTable]
        [Offset(14)]
        public byte? ManufacturerIdx { get; }

        [StringTable]
        [Offset(15)]
        public byte? ProductIdx { get; }

        [StringTable]
        [Offset(16)]
        public byte? SerialnumberIdx { get; }

        [Offset(17)]
        public byte? Configurations { get; }

        public string SubclassHelper => UsbDescriptorParser.ParseSubclass(this);
        public string ProtocolHelper => UsbDescriptorParser.ParseProtocol(this);

        public UsbDeviceDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];

                UsbVersion = (ushort)(data[0] | data[1] << 8);
                Class = (DeviceClass)data[2];
                Subclass = data[3];
                Protocol = data[4];
                MaxPacketSize = data[5];
                Vendor = (ushort)(data[6] | data[7] << 8);
                Product = (ushort)(data[8] | data[9] << 8);
                Revision = (ushort)(data[10] | data[11] << 8);
                ManufacturerIdx = data[12];
                ProductIdx = data[13];
                SerialnumberIdx = data[14];
                Configurations = data[15];
            } catch {
            }
        }
    }

    public class UsbConfigurationDescriptor : UsbDescriptor {
        [Flags]
        public enum EAttributes : byte {
            [Description("Self Powered")]
            SelfPowered = 0x40,

            [Description("Remote Wakeup")]
            RemoteWakeup = 0x02,
        }

        [Offset(2)]
        [Description("Total Length")]
        public ushort TotalLength { get; }

        [Offset(4)]
        public byte Interfaces { get; }

        [Offset(5)]
        [Description("Configuration ID")]
        public byte ConfigurationId { get; }

        [Offset(6)]
        [StringTable]
        public byte ConfigurationIdx { get; }

        [Offset(7)]
        public EAttributes Attributes { get; }

        [Offset(8)]
        [Description("Max Power")]
        [ReadableAccessor(nameof(MaxPowerReadable))]
        public byte MaxPower { get; }

        public string MaxPowerReadable => $"{MaxPower * 2}mA";

        public UsbConfigurationDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];

                TotalLength = (ushort)(data[0] | data[1] << 8);
                Interfaces = data[2];
                ConfigurationId = data[3];
                ConfigurationIdx = data[4];
                Attributes = (EAttributes)data[5];
                MaxPower = data[6];
            } catch {
            }
        }
    }

    public class UsbInterfaceDescriptor : UsbDescriptor, IUsbClassProvider {
        [Offset(2)]
        public byte InterfaceId { get; }

        [Offset(3)]
        public byte AlternateId { get; }

        [Offset(4)]
        public byte Endpoints { get; }

        [Helper]
        [Description("Interface Class")]
        public string InterfaceClass => UsbDescriptorParser.ParseClass(this);

        [Offset(5)]
        [GroupProperty(nameof(InterfaceClass))]
        public DeviceClass? Class { get; }

        [Offset(6)]
        [GroupProperty(nameof(InterfaceClass))]
        [ReadableAccessor(nameof(SubclassHelper))]
        public byte? Subclass { get; }

        [Offset(7)]
        [GroupProperty(nameof(InterfaceClass))]
        [ReadableAccessor(nameof(ProtocolHelper))]
        public byte? Protocol { get; }

        [Offset(8)]
        [StringTable]
        public byte InterfaceIdx { get; }

        public string SubclassHelper => UsbDescriptorParser.ParseSubclass(this);
        public string ProtocolHelper => UsbDescriptorParser.ParseProtocol(this);

        public UsbInterfaceDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];
                InterfaceId = data[0];
                AlternateId = data[1];
                Endpoints = data[2];
                Class = (DeviceClass)data[3];
                Subclass = data[4];
                Protocol = data[5];
                InterfaceIdx = data[6];
            } catch {
            }
        }
    }

    public class UsbStringDescriptor : UsbDescriptor {
        [Offset(2)]
        public string Value { get; }

        public UsbStringDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];
                Value = Encoding.Unicode.GetString(data);
            } catch {
            }
        }
    }

    public class UsbStringListDescriptor : UsbDescriptor {
        [Offset(2)]
        public List<ushort> LangIDs { get; } = new();

        public UsbStringListDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];
                while (data.Length >= 2) {
                    LangIDs.Add((ushort)(data[0] | data[1] << 8));
                    data = data[2..];
                }
            } catch {
            }
        }
    }

    public class UsbEndpointDescriptor : UsbDescriptor {
        [Offset(2)]
        [HideValue]
        public byte EndpointAddress { get; }

        [Offset(3)]
        [HideValue]
        public byte Attributes { get; }

        [Offset(4)]
        public ushort MaxPacketSize { get; }

        [Offset(6)]
        public byte Interval { get; }

        [GroupProperty(nameof(EndpointAddress))]
        public EpDirection Direction => (EpDirection)((EndpointAddress & 0x80) >> 7);

        [GroupProperty(nameof(EndpointAddress))]
        public byte EndpointNumber => (byte)(EndpointAddress & 0x0F);

        [GroupProperty(nameof(Attributes))]
        public EpTransferType TransferType => (EpTransferType)(Attributes & 0x03);

        [GroupProperty(nameof(Attributes))]
        public EpSync SyncType => (EpSync)((Attributes & 0x0C) >> 2);

        [GroupProperty(nameof(Attributes))]
        public EpUsage UsageType => (EpUsage)((Attributes & 0x30) >> 4);

        public UsbEndpointDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];
                EndpointAddress = data[0];
                Attributes = data[1];
                MaxPacketSize = (ushort)(data[2] | data[3] << 8);
                Interval = data[4];
            } catch {
            }
        }
    }

    #endregion

    #region Class Specific Descriptors

    public class UsbClassGeneric : UsbDescriptor {
        [Offset(2)]
        [ReadableAccessor(nameof(SubTypeReadable))]
        public byte SubType { get; }

        public string SubTypeReadable => GetSubtypeLabel();

        public UsbClassGeneric(byte[] data) : base(data) {
            try {
                SubType = data[2];
            } catch {
            }
        }

        protected virtual string GetSubtypeLabel() => "Unknown";
    }

    #endregion
}