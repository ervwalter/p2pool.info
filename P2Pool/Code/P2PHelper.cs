using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace P2Pool
{
    public static class P2PHelper
    {
        public static string ExtractAddress(string value)
        {
            if (!value.Contains(':'))
            {
                return value;
            }
                int pos = value.IndexOf("Address:");
            if (pos >= 0)
            {
                return value.Substring(pos + 9).Trim();
            }
            else
            {
                return null;
            }
        }
    }   
}