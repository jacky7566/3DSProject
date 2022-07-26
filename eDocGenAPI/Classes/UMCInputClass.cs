using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenAPI.Classes
{
    public class UMCInputClass
    {
        public string FileName { get; set; }
        public byte[] FormFile { get; set; }
        public string Product { get; set; }
        public string ProductType { get; set; }
        public int XShift { get; set; }
        public int YShift { get; set; }
        public string Id { get; set; }
        public string EMapVersion { get; set; }
        public string CreatedBy { get; set; }
    }
}
