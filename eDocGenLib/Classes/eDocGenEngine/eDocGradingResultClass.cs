using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eDocGradingResultClass
    {
        public string Wafer_ID { get; set; }
        public string Gx { get; set; }
        public string Gy { get; set; }
        public string Device { get; set; }
        public string PRE_AOI_BIN { get; set; }
        public Dictionary<string, string> GradingParameterDic { get; set; } //tMap Spec
        //public string BIN_TEMPORARY { get; set; }
        //public string PRE_AOI_BIN { get; set; }
        //public string PRE_AOI_PF { get; set; }
        //public string PRE_AOI_EMAPCODE { get; set; }
        //public string PARETO_CODE { get; set; }
        //public string NF_tested_die { get; set; }
        ////Below is for new Shasta program usage
        //public eDocSpecialClass eDocSpecialClass { get; set; } //Special from Shasta
    }

    public class eDocSpecialClass
    {
        public string UNIT_SERIAL_NUMBER { get; set; }
        public string APP_SERIAL { get; set; }
        public string WAFER_SN { get; set; }
        public string SUBSTRATEID { get; set; }
        public string SUBSTRATESN { get; set; }
        public string CONFIG { get; set; }
        public string AB_ID_Type { get; set; }
    }
}
