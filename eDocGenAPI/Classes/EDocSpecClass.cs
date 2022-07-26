using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenAPI.Classes
{
    [Table("TBL_eDoc_Spec")]
    public class EDocSpecClass
    {
        [ExplicitKey]
        public Guid Id { get; set; }
        public string Mask { get; set; }
        public string ProductType { get; set; }
        public string UMCFileName { get; set; }
        public byte[] UMCFile { get; set; }
        public string EMapVersion { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime LastUpdatedDate { get; set; }
        public string LastUpdatedBy { get; set; }
        public int Status { get; set; }
    }
}
