using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eDocSpecParameterClass
    {
        public string Type { get; set; }
        public string Parameter_name { get; set; }
        public string Display_Paramenter_Name { get; set; }
        public string Test_Parameter_Name { get; set; }
        public string Default_Value { get; set; }
        public string Note { get; set; }
        public string LSL { get; set; }
        public string USL { get; set; }
        public string Bin_Code { get; set; }
        public string IsValidate { get; set; }
        public string ValLSL { get; set; }
        public string ValUSL { get; set; }
        public string ValMessage { get; set; }
    }
}
