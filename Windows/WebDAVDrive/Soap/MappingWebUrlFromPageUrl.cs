using System.Xml.Serialization;

// Use https://xmltocsharp.azurewebsites.net/
namespace WebDAVDrive.Soap.WebUrlFromPageUrl
{
    [XmlRoot(ElementName = "WebUrlFromPageUrlResponse", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
    public class WebUrlFromPageUrlResponse
    {
        [XmlElement(ElementName = "WebUrlFromPageUrlResult", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public string WebUrlFromPageUrlResult { get; set; }
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns { get; set; }
    }

    [XmlRoot(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public class Body
    {
        [XmlElement(ElementName = "WebUrlFromPageUrlResponse", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public WebUrlFromPageUrlResponse WebUrlFromPageUrlResponse { get; set; }
    }

    [XmlRoot(ElementName = "Envelope", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public class Envelope
    {
        [XmlElement(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
        public Body Body { get; set; }
        [XmlAttribute(AttributeName = "soap", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string Soap { get; set; }
        [XmlAttribute(AttributeName = "xsi", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string Xsi { get; set; }
        [XmlAttribute(AttributeName = "xsd", Namespace = "http://www.w3.org/2000/xmlns/")]
        public string Xsd { get; set; }
    }
}
