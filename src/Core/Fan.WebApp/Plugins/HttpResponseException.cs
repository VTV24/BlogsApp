using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Fan.WebApp.Plugins
{
    public class HttpResponseException : Exception
    {
        public HttpResponseException()
        {
        }
        public HttpResponseException(HttpStatusCode statusCode, object value)
        {
            this.Status = statusCode;
            this.Value = value;
        }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.InternalServerError;
        public object Value { get; set; }
    }
}
