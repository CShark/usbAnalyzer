using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbAnalyzer.Parser {
    internal abstract class ParserBase {
        public abstract List<UsbLogEntry> Parse(string data);
    }
}
