using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;

namespace CDR.DataHolder.API.Infrastructure.Models
{
    public class ErrorResponseVersion : DefaultErrorResponseProvider
    {
        public override IActionResult CreateResponse(ErrorResponseContext context)
        {            
            // Get x-v from request header
            var versionHeaderValue = context.Request.Headers["x-v"];
            
            // The version was not specified.
            if (string.IsNullOrEmpty(versionHeaderValue))
            {
                return new ObjectResult(new ResponseErrorList(Error.MissingRequiredHeader()))
                {
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            var invalid_XV_Version = true;

            // If the x-v is set, check that it is a postive integer.
            if (int.TryParse(versionHeaderValue, out int version))
            {
                invalid_XV_Version = version < 1;
            }

            if (invalid_XV_Version)
            {
                return new ObjectResult(new ResponseErrorList(Error.InvalidXVVersion()))
                {
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            if (context.ErrorCode == "InvalidApiVersion")
            {
                return new ObjectResult(new ResponseErrorList(Error.InvalidVersion()))
                {
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            if (context.ErrorCode == "UnsupportedApiVersion")
            {
                return new ObjectResult(new ResponseErrorList(Error.UnsupportedVersion()))
                {
                    StatusCode = (int)HttpStatusCode.NotAcceptable
                };
            }

            return base.CreateResponse(context);
        }
    }
}
