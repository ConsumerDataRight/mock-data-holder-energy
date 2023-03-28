using System;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CDR.DataHolder.API.Infrastructure.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class CheckXVAttribute : ActionFilterAttribute
    {        
        private readonly int _maxVersion;

        public CheckXVAttribute(int minVersion, int maxVersion)
        {            
            _maxVersion = maxVersion;
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            // Set version (x-v) we are responding with in the response header
            context.HttpContext.Response.Headers["x-v"] = _maxVersion.ToString();

            base.OnActionExecuted(context);
        }
    }
}
