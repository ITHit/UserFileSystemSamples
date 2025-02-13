using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WebDAVDrive.Soap
{
    public class SoapSession
    {
        /// <summary>
        /// HTTP client used to send requests.
        /// </summary>
        public HttpClient Client;
        private HttpClientHandler clientHandler;
        /// <summary>
        /// Root url to remote storage.
        /// </summary>
        private string remoteStorageRootPath;

        private string WebUrl = null;

        /// <summary>
        /// Cookies which will be added to all requests.
        /// </summary>
        /// <remarks>Use this property to add and remove cookies from the collection.</remarks>
        public CookieContainer CookieContainer
        {
            get { return clientHandler.CookieContainer; }
        }

        public SoapSession(HttpClientHandler handler, string remoteStorageRootPath) {
            clientHandler = handler;
            this.remoteStorageRootPath = remoteStorageRootPath;

            LoggingHandler loggingHandler = new LoggingHandler(this, clientHandler);

            Client = new HttpClient(loggingHandler);
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AcmeInc/1.0)");
            Client.DefaultRequestHeaders.Add("Accept", "*/*");
            Client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            Client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        }

        /// <summary>
        /// Do checkout for given file.
        /// </summary>
        /// <param name="remoteStorageUrl">File url on remote storage.</param>
        /// <returns>True if success</returns>
        public async Task<bool> CheckOutAsync(string remoteStorageUrl)
        {
            string url = await GetSiteUrlFromFileUrl(remoteStorageRootPath) + "/_vti_bin/Lists.asmx";

            string xmlSOAP = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <SOAP-ENV:Envelope  xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                <SOAP-ENV:Body>
                    <CheckOutFile xmlns=""http://schemas.microsoft.com/sharepoint/soap/"">
                        <checkoutToLocal>false</checkoutToLocal>
                        <pageUrl>{remoteStorageUrl}</pageUrl>
                    </CheckOutFile>
                </SOAP-ENV:Body>
            </SOAP-ENV:Envelope>";

            string xmlResponse = await PostSOAPRequestAsync(url, xmlSOAP, "CheckOutFile");

            CheckOutFile.Envelope envelope = Deserealize<CheckOutFile.Envelope>(xmlResponse);
            return envelope.Body.CheckOutFileResponse.CheckOutFileResult;
        }

        /// <summary>
        /// Get item information for given file.
        /// </summary>
        /// <param name="remoteStorageUrl">File url on remote storage.</param>
        /// <returns>(True if file is checked out, True if file is checked by current user, user name checked out this file)</returns>
        public async Task<(bool, bool, string)> GetItemInfoAsync(string remoteStorageUrl)
        {
            string url = await GetSiteUrlFromFileUrl(remoteStorageRootPath) + "/_vti_bin/Copy.asmx";

            string xmlSOAP = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <SOAP-ENV:Envelope  xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                <SOAP-ENV:Body>
                    <GetItem xmlns=""http://schemas.microsoft.com/sharepoint/soap/"">
                        <Url>{remoteStorageUrl}</Url>
                    </GetItem>
                </SOAP-ENV:Body>
            </SOAP-ENV:Envelope>";

            string xmlResponse = await PostSOAPRequestAsync(url, xmlSOAP, "GetItem");

            GetItem.Envelope envelope = Deserealize<GetItem.Envelope>(xmlResponse);

            string checkedByUserDisplayName = null;
            string checkedOutUserId = null;
            bool isCheckedOutByMe = false;
            foreach (GetItem.FieldInformation fieldInformation in envelope.Body.GetItemResponse.Fields.FieldInformation)
            {
                switch (fieldInformation.InternalName)
                {
                    case "LinkCheckedOutTitle":
                        checkedByUserDisplayName = fieldInformation.Value;
                        break;
                    case "CheckedOutUserId":
                        string[] split = fieldInformation.Value.Split(";#");
                        checkedOutUserId = split.Length > 1 ? split[1]: "";
                        break;
                    //case "SyncClientId":
                    //    split = Regex.Split(fieldInformation.Value, @";#");
                    //    syncClientId = split[0];
                    //    break;
                    case "MetaInfo":
                        isCheckedOutByMe = fieldInformation.Value.Contains("vti_sourcecontrolmultiuserchkoutby");
                        break;
                }
            }
            return (checkedOutUserId != null && checkedOutUserId.Trim() != "", isCheckedOutByMe, checkedByUserDisplayName);
        }

        /// <summary>
        /// Do checkin for given file.
        /// </summary>
        /// <param name="remoteStorageUrl">File url on remote storage.</param>
        /// <param name="comment">Comment to current version.</param>
        /// <returns>True if success.</returns>
        public async Task<bool> CheckInAsync(string remoteStorageUrl, string comment)
        {
            string url = await GetSiteUrlFromFileUrl(remoteStorageRootPath) + "/_vti_bin/Lists.asmx";

            string xmlSOAP = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <SOAP-ENV:Envelope  xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                <SOAP-ENV:Body>
                    <CheckInFile xmlns=""http://schemas.microsoft.com/sharepoint/soap/"">
                        <CheckinType>1</CheckinType>
                        <comment>{comment}</comment>
                        <pageUrl>{remoteStorageUrl}</pageUrl>
                    </CheckInFile>
                </SOAP-ENV:Body>
            </SOAP-ENV:Envelope>";

            string xmlResponse = await PostSOAPRequestAsync(url, xmlSOAP, "CheckInFile");

            CheckInFile.Envelope envelope = Deserealize<CheckInFile.Envelope>(xmlResponse);
            return envelope.Body.CheckInFileResponse.CheckInFileResult;
        }

        public async Task<string> GetWebUrlFromPageUrl(string pageUrl)
        {
            Uri uri = new Uri(pageUrl);
            string baseUrl = $"{uri.Scheme}://{uri.Host}";
            string url = $"{uri.Scheme}://{uri.Host}" + "/443/_vti_bin/Webs.asmx";

            string xmlSOAP = $@"<?xml version=""1.0""?>
            <SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                <SOAP-ENV:Body>
                    <WebUrlFromPageUrl xmlns=""http://schemas.microsoft.com/sharepoint/soap/"">
                        <pageUrl>{pageUrl}</pageUrl>
                    </WebUrlFromPageUrl>
                </SOAP-ENV:Body>
            </SOAP-ENV:Envelope>";

            string xmlResponse = await PostSOAPRequestAsync(url, xmlSOAP, "WebUrlFromPageUrl");

            WebUrlFromPageUrl.Envelope envelope = Deserealize<WebUrlFromPageUrl.Envelope>(xmlResponse);
            return envelope.Body.WebUrlFromPageUrlResponse.WebUrlFromPageUrlResult;
        }

        private async Task<string> GetSiteUrlFromFileUrl(string fileUrl)
        {
            if (WebUrl == null)
            {
                WebUrl = await GetWebUrlFromPageUrl(fileUrl);
            }
            return WebUrl;

            //Uri uri = new Uri(fileUrl);
            //string[] segments = uri.Segments;
            //string siteUrl = $"{uri.Scheme}://{uri.Host}";

            //// Loop through the segments to construct the site URL
            //for (int i = 0; i < segments.Length; i++)
            //{
            //    if (segments[i].Equals("sites/", StringComparison.OrdinalIgnoreCase) ||
            //        segments[i].Equals("teams/", StringComparison.OrdinalIgnoreCase))
            //    {
            //        // Append the site or team segment
            //        siteUrl += string.Join("", segments, 0, i + 2);
            //        break;
            //    }
            //}

            //return siteUrl.TrimEnd('/');
        }

        private async Task<string> PostSOAPRequestAsync(string url, string xmlSoap, string action)
        {
            using (HttpContent content = new StringContent(xmlSoap, Encoding.UTF8, "text/xml"))
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Add("SOAPAction", $"\"http://schemas.microsoft.com/sharepoint/soap/{action}\"");
                request.Content = content;
                using (HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    //response.EnsureSuccessStatusCode(); // throws an Exception if 404, 500, etc.
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        private T Deserealize<T>(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringReader reader = new StringReader(xml))
            {
                return (T)serializer.Deserialize(reader);
            }
        }


        /// <summary>
        /// Event fired when a new log data available during the request. 
        /// </summary>
        public event SoapEventHandler SoapEventMessage;

        /// <summary>
        /// Rises the <see cref="SoapEventMessage"/> event.
        /// </summary>
        protected virtual void OnSoapMessage(SoapMessageEventArgs e)
        {
            SoapEventHandler handler = SoapEventMessage;
            handler?.Invoke(e);
        }

        /// <summary>
        /// Rises the <see cref="SoapEventMessage"/> event.
        /// </summary>
        /// <param name="message">Message to be passed in <see cref="SoapEventMessage"/> property to <see cref="SoapEventMessage"/> event handler.</param>
        /// <param name="logLevel">Type of information being logged.</param>
        public virtual void LogSoapMessage(string message)
        {
            OnSoapMessage(new SoapMessageEventArgs()
            {
                Message = message,
                //LogLevel = logLevel
            });
        }
    }
}
