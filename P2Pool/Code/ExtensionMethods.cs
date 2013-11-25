using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;
using System.Security.Cryptography;
using System.Web.Caching;

namespace P2Pool
{
    public static class ExtensionMethods
    {
        private const string CssBasePath = "~/content/";
        private const string JavaScriptBasePath = "~/scripts/";

        public static MvcHtmlString Include(this HttpServerUtilityBase server, string path)
        {
            string filePath = server.MapPath(path);
            return MvcHtmlString.Create(File.ReadAllText(filePath));
        }

        public static string Absolute(this UrlHelper helper, string url)
        {
            string siteUrl = "http://btcstats.net";
            return new Uri(new Uri(siteUrl), url).ToString();
        }

        public static MvcHtmlString ToJson(this HtmlHelper helper, object value)
        {
            string json = JsonConvert.SerializeObject(value);
            return MvcHtmlString.Create(json);
        }

        /// <summary>    
        /// Compares to DateTimes and converts the result to an easy human readable format.    
        /// </summary>    
        /// <param name="time">A past or future DateTime.</param>    
        /// <param name="relativeTo">Relative to this time.</param>    
        /// <returns></returns>    
        public static string ToRelativeTime(this DateTime nullableTime)
        {
            DateTime time = (DateTime)nullableTime;
            DateTime relativeTo = DateTime.Now;
            TimeSpan ts = relativeTo.Subtract(time).Duration();
            string DateFormat = "MMMM d";
            string dir = (relativeTo > time) ? "ago" : "to go";

            if (relativeTo.Year != time.Year)
            {
                DateFormat += ", yyyy";
            }

            if (ts.Days < 360)
            {
                //Days   
                if (ts.Days > 0)
                {
                    return string.Format("{0}d {1}", ts.Days, dir);
                }

                //hours   
                if (ts.Hours > 0)
                {
                    return string.Format("{0}h {1}", ts.Hours, dir);
                }

                //minutes   
                if (ts.Minutes > 0)
                {
                    return string.Format("{0}min {1}", ts.Minutes, dir);
                }

                if (ts.Seconds > 0)
                {
                    return string.Format("{0}s {1}", ts.Seconds, dir);
                }

                return "just now";
            }
            return time.ToString(DateFormat);
        }

        public static string SafeToString(this decimal? value, int decimalPlaces = 2, string nullDisplay = "-")
        {
            if (value.HasValue)
            {
                return value.Value.ToString("F" + decimalPlaces.ToString());
            }
            else
            {
                return nullDisplay;
            }
        }

        public static string ToBtcString(this decimal value)
        {
            return value.ToString("0.00######");
        }


        public static string JavaScript(this UrlHelper helper, string filename)
        {
            string hash = CalculateHash(helper, JavaScriptBasePath);
            string path = JavaScriptBasePath + filename;
            return helper.Content(path) + "?" + hash;
        }

        public static string StyleSheet(this UrlHelper helper, string filename)
        {
            string hash = CalculateHash(helper, CssBasePath);
            string path = CssBasePath + filename;
            return helper.Content(path) + "?" + hash;
        }

        private static string CalculateHash(UrlHelper helper, string basePath)
        {
            string hash = helper.RequestContext.HttpContext.Cache["__Hash:" + basePath] as string;
            if (hash == null)
            {
                string scriptPath = helper.RequestContext.HttpContext.Request.MapPath(basePath);
                using (MemoryStream stream = new MemoryStream())
                {
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        foreach (string file in Directory.GetFiles(scriptPath))
                        {
                            writer.WriteLine(File.ReadAllText(file));
                        }

                        writer.Flush();
                        SHA1Managed sha1 = new SHA1Managed();
                        hash = HttpServerUtility.UrlTokenEncode(sha1.ComputeHash(stream.ToArray())).Substring(0, 8).ToLower();
                    }
                }
                helper.RequestContext.HttpContext.Cache.Insert("__Hash:" + basePath, hash, new CacheDependency(scriptPath));
            }
            return hash;
        }
        public static DateTime FromUnixTime(this int unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }

        public static int ToUnixTime(this DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt32((date - epoch).TotalSeconds);
        }
    }
}