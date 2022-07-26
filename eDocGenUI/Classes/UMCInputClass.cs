using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenUI.Classes
{
    public class UMCInputClass
    {        
        public IFormFile FormFile { get; set; }
        public string Product { get; set; }
        public string ProductType { get; set; }
        public int XShift { get; set; }
        public int YShift { get; set; }
        public Guid EDocSpecId { get; set; }
        public string CreatedBy { get; set; }
    }
}
