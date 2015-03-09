using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VhdCompact
{
    public static class StringExtensions
    {
        public static string F(this string str, object arg, params object[] args)
        {
            return string.Format(str, arg, args);
        }
    }
}
