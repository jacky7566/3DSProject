using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eDocAlertClass
    {
        public string Subject { get; set; }
        public string Content { get; set; }
        /// <summary>
        /// 1 = Lite IT, 2 = Lite Engineer, 3 WIN Engineer
        /// </summary>
        public int Level { get; set; }
        public List<string> Attachments { get; set; }
    }
}
