using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VitaPackageHelper
{
    public class VitaPackage
    {
        public string fileName;
        public string appId;
        public string region;
        public Dictionary<String, String> sfoData;
        public Byte[] icon;
    }
    public class SFOFFSET
    {
        public Int16 key_offset;
        public Int16 data_format;
        public Int32 data_lenght;
        public Int32 data_max_lenght;
        public Int32 data_offset;
    }
}
