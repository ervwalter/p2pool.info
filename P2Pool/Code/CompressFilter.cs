using System.Web;
using System.Web.Mvc;
using System.IO.Compression;

namespace P2Pool
{
    public class Compress : ActionFilterAttribute
    {
		public override void OnActionExecuted(ActionExecutedContext filterContext)
		{
			if (filterContext.Exception != null)
			{
				return;
			}

			HttpRequestBase request = filterContext.HttpContext.Request;

			string acceptEncoding = request.Headers["Accept-Encoding"];

			if (string.IsNullOrEmpty(acceptEncoding)) return;

			acceptEncoding = acceptEncoding.ToUpperInvariant();

			HttpResponseBase response = filterContext.HttpContext.Response;

			if (acceptEncoding.Contains("GZIP"))
			{
				response.AppendHeader("Content-encoding", "gzip");
				response.Filter = new GZipStream(response.Filter, CompressionMode.Compress);
			}
			else if (acceptEncoding.Contains("DEFLATE"))
			{
				response.AppendHeader("Content-encoding", "deflate");
				response.Filter = new DeflateStream(response.Filter, CompressionMode.Compress);
			}
		}


    }
}