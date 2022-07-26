using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenLib.Classes
{
    public class RWDimensionClass
    {
        public int No { get; set; }
        public int OGx { get; set; }
        public int OGy { get; set; }
        public string Device { get; set; }
        public string Product { get; set; }
        public string ProductType { get; set; }
        public Guid EDocSpecId { get; set; }
    }
}
