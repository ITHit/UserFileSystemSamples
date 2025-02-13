using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WebDAVDrive.Soap.GetItem
{
    [XmlRoot(ElementName = "FieldInformation", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
    public class FieldInformation
    {
        [XmlAttribute(AttributeName = "Type")]
        public string Type { get; set; }
        [XmlAttribute(AttributeName = "DisplayName")]
        public string DisplayName { get; set; }
        [XmlAttribute(AttributeName = "InternalName")]
        public string InternalName { get; set; }
        [XmlAttribute(AttributeName = "Id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }
    }

    [XmlRoot(ElementName = "Fields", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
    public class Fields
    {
        [XmlElement(ElementName = "FieldInformation", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public List<FieldInformation> FieldInformation { get; set; }
    }

    [XmlRoot(ElementName = "GetItemResponse", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
    public class GetItemResponse
    {
        [XmlElement(ElementName = "GetItemResult", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public string GetItemResult { get; set; }
        [XmlElement(ElementName = "Fields", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public Fields Fields { get; set; }
        [XmlElement(ElementName = "Stream", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public string Stream { get; set; }
        [XmlAttribute(AttributeName = "xmlns")]
        public string Xmlns { get; set; }
    }

    [XmlRoot(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public class Body
    {
        [XmlElement(ElementName = "GetItemResponse", Namespace = "http://schemas.microsoft.com/sharepoint/soap/")]
        public GetItemResponse GetItemResponse { get; set; }
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
