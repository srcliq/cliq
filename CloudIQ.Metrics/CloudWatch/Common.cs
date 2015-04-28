using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CloudWatch
{
    public static class Common
    {
        public static object GetPropertyValue(object src, string propertyName)
        {
            return src.GetType().GetProperty(propertyName).GetValue(src, null);
        }
    }
}
