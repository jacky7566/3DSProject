using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eDocBincodeMapClass
    {
        public Guid Id { get; set; }
        public string Mask { get; set; }
        public string EMap_BinCode { get; set; }
        public string AOI_BinCode { get; set; }
        public string BinDescription { get; set; }
        public string BinQuality { get; set; }
        public int BinCount { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int Status { get; set; }
    }
}
