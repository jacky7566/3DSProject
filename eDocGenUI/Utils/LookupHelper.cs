using System;
using System.Collections.Generic;
using System.Text;

namespace eDocGenUI.Utils
{
    class LookupHelper
    {
        public static string ConvertMaskGroupName(string showName)
        {
            switch (showName)
            {
                case "Shasta":
                    return "JUNO_GROUP";
                case "Non-Shasta":
                    return "OTHER_GROUP";
                case "Turbo":
                    return "OTHER_GROUP";
                default:
                    break;
            }
            return string.Empty;
        }
    }
}
