using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eDocGenParaClass
    {
        public Traceability_InfoClass HeaderInfo { get; set; }
        public  List<eDocConfigClass> EDocConfigList { get; set; }
        public List<eDocRWMapClass> RWMapList { get; set; }
        public eDocWaferTestHeaderClass WaferTestHeader { get; set; }
        public List<dynamic> MCOAthenaList { get; set; }
        public List<string> GradingFileList { get; set; }
        public List<List<Dictionary<string, string>>> GradingResultList { get; set; }
        public string AVI2FilePath { get; set; }
        public string EDocResultPath { get; set; }
        public string EMapVersion { get; set; }
        public int GoodDieQty { get; set; }
        public string EPIReactor { get; set; }
        public string POR_Version { get; set; }
        public eDocAlertClass MailInfo { get; set; }
        public DateTime CreationStartTime { get; set; }
        public string GradingSpecFilePath { get; set; }
    }
}
