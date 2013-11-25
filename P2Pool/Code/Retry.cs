using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Practices.TransientFaultHandling;
using Microsoft.Practices.EnterpriseLibrary.WindowsAzure.TransientFaultHandling.SqlAzure;

namespace P2Pool
{
    public static class Retry
    {
        private static RetryPolicy _policy;

        static Retry()
        {
            _policy = new RetryPolicy<SqlAzureTransientErrorDetectionStrategy>(3, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
            _policy.Retrying += new EventHandler<RetryingEventArgs>(_policy_Retrying);
        }

        private static void _policy_Retrying(object sender, RetryingEventArgs e)
        {
            try
            {
                Exception ex = new Exception("SQL request failed. Retrying.", e.LastException);
                //Elmah.ErrorLog.GetDefault(HttpContext.Current).Log(new Elmah.Error(ex));
            }
            catch { }
        }

        public static void ExecuteAction(Action action)
        {
            _policy.ExecuteAction(action);
        }

        public static TResult ExecuteAction<TResult>(Func<TResult> func)
        {
            return _policy.ExecuteAction<TResult>(func);
        }

    }
}