using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes
{
    public class AVI2_RawDataClass
    {
        public Guid Id { get; set; }
        public Guid Traceability_Id { get; set; }
        public int No { get; set; }
        public string Wafer_Id { get; set; }
        public string RW_Wafer_Id { get; set; }
        public string IGx { get; set; }
        public string IGy { get; set; }
        public string Bin_AOI1 { get; set; }
        public string OGx { get; set; }
        public string OGy { get; set; }        
        public string Bin_AOI2 { get; set; }
        public string Line_Data { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime LastUpdatedDate { get; set; }
        public string LastUpdatedBy { get; set; }
    }
}
