using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenLib.Classes
{
    public class GradingSpecClass
    {
        public string Spec_Id { get; set; }
        public string Mask { get; set; }
        public string Spec_FileName { get; set; }
        public string Spec_Version { get; set; }
        public string EMapVersion { get; set; }
        public string UMCFileName { get; set; }
        public Guid Id { get; set; }
    }
}
