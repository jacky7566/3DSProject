using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes
{
    public class Traceability_InfoClass
    {
        public Guid Id { get; set; }
        public string Wafer_Id { get; set; }
        public string RW_Wafer_Id { get; set; }
        public string FileType { get; set; }
        public string FilePath { get; set; }
        public int Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime LastUpdatedDate { get; set; }
        public string LastUpdatedBy { get; set; }
        public string Recipe { get; set; }
        public DateTime Sort_Date { get; set; }
        public int RetryCount { get; set; }
    }
}
