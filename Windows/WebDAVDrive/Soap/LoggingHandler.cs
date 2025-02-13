using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebDAVDrive.Soap
{
    internal class LoggingHandler : DelegatingHandler
    {
        private SoapSession session;
        /// <summary>
        /// Creates instance of this class. 
        /// </summary>
        /// <param name="innerHandler">Inner handler.</param>
        /// <param name="session">WebDavSession object.</param>
        public LoggingHandler(SoapSession session, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            this.session = session;
        }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await LogRequestInfo(request);

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            await LogResponseInfo(request, response);
            return response;
        }

        private async Task LogRequestInfo(HttpRequestMessage requestMessage)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("[{0}] {1} HTTP/{2}\n", requestMessage.Method.Method, requestMessage.RequestUri, requestMessage.Version));
            foreach (KeyValuePair<string, IEnumerable<string>> header in requestMessage.Headers)
            {
                sb.Append(string.Format("{0}: {1}\n", header.Key, string.Join(" ", header.Value)));
            }
            if (requestMessage.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in requestMessage.Content.Headers)
                {
                    sb.Append(string.Format("{0}: {1}\n", header.Key, string.Join(" ", header.Value)));
                }
            }
            session.LogSoapMessage(sb.ToString());

            string reqMethod = requestMessage.Method.Method.ToUpper();
            // Log xml content
            if (requestMessage.Content != null && reqMethod != "GET")
            {
                string xml = await requestMessage.Content.ReadAsStringAsync();
                session.LogSoapMessage(xml);
            }
        }

        private async Task LogResponseInfo(HttpRequestMessage requestMessage, HttpResponseMessage responseMessage)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("\n{0} {1}\n", (int)responseMessage.StatusCode, responseMessage.ReasonPhrase));

            foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Headers)
            {
                sb.Append(string.Format("{0}: {1}\n", header.Key, string.Join(" ", header.Value)));
            }
            if (responseMessage.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in responseMessage.Content.Headers)
                {
                    sb.Append(string.Format("{0}: {1}\n", header.Key, string.Join(" ", header.Value)));
                }
            }
            session.LogSoapMessage(sb.ToString());

            string reqMethod = requestMessage.Method.Method.ToUpper();
            // Log xml content
            if (requestMessage.Content != null && reqMethod != "GET")
            {
                string xml = await responseMessage.Content.ReadAsStringAsync();
                session.LogSoapMessage(xml);
            }
        }
    }
}
