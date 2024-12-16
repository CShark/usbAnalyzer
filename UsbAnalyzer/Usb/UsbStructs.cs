using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
        [Description("Does not apply")]
        None = -1,

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
        [Description("Does not apply")]
        None = -1,
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
        public byte RequestType { get; set; }
        public byte Request { get; set; }
        public ushort Value { get; set; }
        public ushort Index { get; set; }
        public ushort Length { get; set; }

        public DataTransferDirection Direction => (DataTransferDirection)((RequestType & 0b1000_0000) >> 7);
        public DataTransferType Type => (DataTransferType)((RequestType & 0b0111_0000) >> 4);
        public DataTransferTarget Target => (DataTransferTarget)((RequestType & 0b0000_1111));
        public Requests ParsedRequest => (Requests)(RequestType << 8 | Request);

        public TreeViewEntry BuildTree() {
            return new("Setup", [
                new("Request Type", $"0x{RequestType:X2}", Type.ToReadableString(), [
                    new("Direction", "", Direction.ToReadableString()),
                    new("Type", "", Type.ToReadableString()),
                    new("Recipient", "", Target.ToReadableString())
                ]),
                new("Request", Request, ParsedRequest.ToReadableString()),
                new("Value", Value),
                new("Index", Index),
                new("Length", Length)
            ]);
        }
    }

    public class UsbDescriptor {
        public byte Length { get; }
        public DescriptorTypes Type { get; }

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

        public TreeViewEntry BuildTree() {
            return BuildTree([]);
        }

        protected virtual TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return new(Type.ToReadableString(), (byte)Type, [
                new("Length", Length), ..children
            ]);
        }

        protected string ParseBcd(int value) {
            var part1 = (value & 0xFF00) >> 8;
            var part2 = value & 0x00FF;

            var parsed = (part1 >> 4) * 10 + (part1 & 0xF) + (part2 >> 4) / 10f + (part2 & 0xF) / 100f;

            return parsed.ToString("F1", CultureInfo.InvariantCulture);
        }
    }

    public interface IUsbClassProvider {
        public DeviceClass Class { get; }
        public byte Subclass { get; }
        public byte Protocol { get; }
    }

    #region General Descriptors

    public class UsbDeviceDescriptor : UsbDescriptor, IUsbClassProvider {
        public ushort UsbVersion { get; }
        public DeviceClass Class { get; }
        public byte Subclass { get; }
        public byte Protocol { get; }

        public byte MaxPacketSize { get; }
        public ushort Vendor { get; }
        public ushort Product { get; }
        public ushort Revision { get; }

        public byte ManufacturerIdx { get; }
        public byte ProductIdx { get; }
        public byte SerialnumberIdx { get; }

        public byte Configurations { get; }

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

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("USB-Version", UsbVersion, ParseBcd(UsbVersion)),
                new("Device Class", "",
                    UsbDescriptorParser.ParseClass(this), [
                        new("Class", Class),
                        new("Subclass", Subclass, UsbDescriptorParser.ParseSubclass(this)),
                        new("Protocol", Protocol, UsbDescriptorParser.ParseProtocol(this))
                    ]),
                new("Max Packet Size", MaxPacketSize),
                new("Vendor ID", [
                    new("Vendor", Vendor),
                    new("Product", Product),
                    new("Device", Revision)
                ]),
                new("String Table Indices", [
                    new("Manufacturer", ManufacturerIdx),
                    new("Product", ProductIdx),
                    new("Serial number", SerialnumberIdx)
                ]),
                new("Configurations", Configurations),
                ..children
            ]);
        }
    }

    public class UsbConfigurationDescriptor : UsbDescriptor {
        public ushort TotalLength { get; }
        public byte Interfaces { get; }
        public byte ConfigurationId { get; }
        public byte ConfigurationIdx { get; }
        public byte Attributes { get; }
        public byte MaxPower { get; }

        public bool SelfPowered => (Attributes & 0x40) != 0;
        public bool RemoteWakeup => (Attributes & 0x20) != 0;

        public int MaxPowermA => MaxPower * 2;

        public UsbConfigurationDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];

                TotalLength = (ushort)(data[0] | data[1] << 8);
                Interfaces = data[2];
                ConfigurationId = data[3];
                ConfigurationIdx = data[4];
                Attributes = data[5];
                MaxPower = data[6];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Total Length", TotalLength),
                new("Interfaces", Interfaces),
                new("Configuration ID", ConfigurationId),
                new("String Table Indices", [
                    new("Configuration", ConfigurationIdx)
                ]),
                new("Attributes", Attributes, [
                    new("Self Powered", "", $"{SelfPowered}"),
                    new("Remote Wakeup", "", $"{RemoteWakeup}")
                ]),
                new("Max Power", MaxPower, $"{MaxPowermA}mA"),
                ..children
            ]);
        }
    }

    public class UsbInterfaceDescriptor : UsbDescriptor, IUsbClassProvider {
        public byte InterfaceId { get; }
        public byte AlternateId { get; }
        public byte Endpoints { get; }
        public DeviceClass Class { get; }
        public byte Subclass { get; }
        public byte Protocol { get; }

        public byte InterfaceIdx { get; }

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

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Interface ID", InterfaceId),
                new("Alternate ID", AlternateId),
                new("Endpoints", Endpoints),
                new("Interface Class", "",
                    UsbDescriptorParser.ParseClass(this), [
                        new("Class", Class),
                        new("Subclass", Subclass, UsbDescriptorParser.ParseSubclass(this)),
                        new("Protocol", Protocol, UsbDescriptorParser.ParseProtocol(this))
                    ]),
                new("String Table Indices", [
                    new("Interface", InterfaceIdx)
                ]),
                ..children
            ]);
        }
    }

    public class UsbStringDescriptor : UsbDescriptor {
        public string Value { get; }

        public UsbStringDescriptor(byte[] data) : base(data) {
            try {
                data = data[2..];
                Value = Encoding.Unicode.GetString(data);
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new TreeViewEntry("Value", "", Value),
                ..children
            ]);
        }
    }

    public class UsbStringListDescriptor : UsbDescriptor {
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

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree(LangIDs.Select(x => new TreeViewEntry("LCID", x))
                .Concat(children).ToArray());
        }
    }

    public class UsbEndpointDescriptor : UsbDescriptor {
        public byte EndpointAddress { get; }
        public byte Attributes { get; }
        public ushort MaxPacketSize { get; }
        public byte Interval { get; }


        public EpDirection Direction => (EpDirection)((EndpointAddress & 0x80) >> 7);
        public int EndpointNumber => EndpointAddress & 0x0F;
        public EpTransferType TransferType => (EpTransferType)(Attributes & 0x03);

        public EpSync SyncType =>
            TransferType == EpTransferType.Isochronous ? (EpSync)((Attributes & 0x0C) >> 2) : EpSync.None;

        public EpUsage UsageType =>
            TransferType == EpTransferType.Isochronous ? (EpUsage)((Attributes & 0x30) >> 4) : EpUsage.None;

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

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Address", EndpointAddress, [
                    new("Address", "", EndpointNumber.ToString()),
                    new("Direction", "", Direction.ToReadableString())
                ]),
                new("Attributes", Attributes, TransferType == EpTransferType.Isochronous
                    ? [
                        new("Transfer Type", "", TransferType.ToReadableString()),
                        new("Sync Type", "", SyncType.ToReadableString()),
                        new("Usage Type", "", UsageType.ToReadableString())
                    ]
                    : [
                        new("Transfer Type", "", TransferType.ToReadableString())
                    ]),
                new("Max Packet Size", MaxPacketSize),
                new("Interval", Interval),
                ..children
            ]);
        }
    }

    #endregion

    #region Class Specific Descriptors

    public class UsbClassGeneric : UsbDescriptor {
        public byte SubType { get; }

        public UsbClassGeneric(byte[] data) : base(data) {
            try {
                SubType = data[2];
            } catch {
            }
        }

        protected override TreeViewEntry BuildTree(TreeViewEntry[] children) {
            return base.BuildTree([
                new("Subtype", SubType, GetSubtypeLabel()),
                ..children
            ]);
        }

        protected virtual string GetSubtypeLabel() => "Unknown";
    }

    #endregion
}