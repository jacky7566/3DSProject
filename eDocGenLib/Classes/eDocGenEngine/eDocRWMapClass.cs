using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eDocRWMapClass
    {
        [DisplayName("DIE_NUM")]
        public int No { get; set; }
        [DisplayName("FAB_WF_ID")]
        public string Wafer_Id { get; set; }
        [DisplayName("RW_WF_ID")]
        public string RW_Wafer_Id { get; set; }
        [DisplayName("FAB_WF_X"), Description("G_X")]
        public string IGx { get; set; }
        [DisplayName("FAB_WF_Y"), Description("G_Y")]
        public string IGy { get; set; }
        [DisplayName("BIN_AOI1")]
        public string Bin_AOI1 { get; set; }
        [DisplayName("RW_WF_X")]
        public string OGx { get; set; }
        [DisplayName("RW_WF_Y")]
        public string OGy { get; set; }
        [DisplayName("PS_AVI_PF")]
        public string Bin_AOI2 { get; set; }
        public string EMap_BinCode { get; set; }
        public string Device { get; set; }
        public string Line_Data { get; set; }
    }
}
