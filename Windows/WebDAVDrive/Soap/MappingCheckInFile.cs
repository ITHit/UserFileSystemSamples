using System.Xml.Serialization;

namespace WebDAVDrive.Soap.CheckInFile
{
    [XmlRoot(ElementName = "CheckInFileResponse", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
    public class CheckInFileResponse
    {
        [XmlElement(ElementName = "CheckInFileResult", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public bool CheckInFileResult { get; set; }
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns { get; set; }
    }

    [XmlRoot(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public class Body
    {
        [XmlElement(ElementName = "CheckInFileResponse", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public CheckInFileResponse CheckInFileResponse { get; set; }
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
