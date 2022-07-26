using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Classes.eDocGenEngine
{
    public class eDocConfigClass
    {
        public Guid Id { get; set; }
        public string ProductType { get; set; }
        public string ServerName { get; set; }
        public string ConfigType { get; set; }
        public string ConfigKey { get; set; }
        public string ConfigValue { get; set; }
        public int Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
    }
}
