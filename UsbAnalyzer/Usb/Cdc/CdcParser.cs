using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbAnalyzer.Usb.Cdc {
    internal class CdcParser : UsbDescriptorParser {
        protected override (UsbDescriptor, UsbDescriptorParser) ParseSpecificDescriptor(DescriptorTypes type, byte subtype, byte[] data) {
            if (type == DescriptorTypes.ClassInterface) {
                switch (subtype) {
                    case 0:
                        return (new UsbClassCdcHeader(data), this);
                    case 2:
                        return (new UsbClassCdcAcm(data), this);
                    case 6:
                        return (new UsbClassCdcUnion(data), this);
                    case 0x0F:
                        return (new UsbClassCdcEthernet(data), this);
                    case 0x1A:
                        return (new UsbClassCdcNcm(data), this);
                }
            }

            return base.ParseSpecificDescriptor(type, subtype, data);
        }
    }
}