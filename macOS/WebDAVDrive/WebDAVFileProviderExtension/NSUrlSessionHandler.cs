using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Foundation;

namespace WebDAVFileProviderExtension
{
    public class NSUrlSessionHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            NSMutableUrlRequest nsUrlRequest = new NSMutableUrlRequest(new NSUrl(request.RequestUri.AbsoluteUri.ToString()));
            // set http method.
            nsUrlRequest.HttpMethod = request.Method.Method;
            // set headers
            nsUrlRequest.Headers = NSDictionary.FromObjectsAndKeys(request.Headers.Select(p => string.Join(" ", p.Value)).ToArray(),
                                  request.Headers.Select(p => p.Key).ToArray());
            if (request.Content != null)
            {
                // set request body.
                nsUrlRequest.Body = NSData.FromStream(await request.Content.ReadAsStreamAsync());
            }

            NSUrlSessionDataTaskRequest sessionDataTask = await NSUrlSession.SharedSession.CreateDataTaskAsync(nsUrlRequest);
            NSHttpUrlResponse urlResponse = sessionDataTask.Response as NSHttpUrlResponse;

            HttpResponseMessage httpResponseMessage = new HttpResponseMessage((HttpStatusCode)Convert.ToInt32(urlResponse.StatusCode));

            if (sessionDataTask.Response.ExpectedContentLength > 0)
            {
                httpResponseMessage.Content = new StreamContent(sessionDataTask.Data.AsStream());
            }
            else
            {
                // empty response body.
                httpResponseMessage.Content = new ByteArrayContent(new byte[0]);
            }

            // copy response headers
            foreach (KeyValuePair<NSObject, NSObject> header in urlResponse.AllHeaderFields)
            {
                httpResponseMessage.Headers.TryAddWithoutValidation(Convert.ToString(header.Key), Convert.ToString(header.Value));
            }
            httpResponseMessage.RequestMessage = request;

            return httpResponseMessage;
        }
    }
}
