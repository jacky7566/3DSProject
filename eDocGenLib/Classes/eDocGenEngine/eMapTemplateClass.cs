using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eMapTemplateClass
    {
        public string Orientation { get; set; }
        public string Wafersize { get; set; }
        public string DeviceSizeX { get; set; }
        public string DeviceSizeY { get; set; }
        public string StepSizeX { get; set; }
        public string StepSizeY { get; set; }
        public List<string[]> FiducialList { get; set; }
    }
}
