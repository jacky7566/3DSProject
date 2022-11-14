using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes
{
    public class TraceabilityStatusClass
    {
        public string Wafer_Id { get; set; }
        public string RW_Wafer_Id { get; set; }
        public string FilePath { get; set; }
        public string IngestionStatus { get; set; }
        public DateTime Ingestion_CreatedDate { get; set; }
        public DateTime Ingestion_LastUpdatedDate { get; set; }
        public string Good_Die_Qty { get; set; }
        public string eDocStatus { get; set; }
        public string Error_Message { get; set; }
        public string Grade_Spec_Version { get; set; }
        public string eDoc_Spec_Version { get; set; }
        public string Latest_AVI2_FileName { get; set; }
        public DateTime eDoc_CreatedDate { get; set; }
        public DateTime eDoc_LastUpdatedDate { get; set; }
    }
}
