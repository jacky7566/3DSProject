using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenAPI.Classes
{
    public class FennecAVI2InputClass
    {
        public string RW_Wafer_Id { get; set; }
        public IFormFile FormFile { get; set; }
        public string OutputFileName { get; set; }
        public string OutputFilePath { get; set; }
    }
}
