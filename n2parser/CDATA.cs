using System.Xml.Schema;
using System.Xml.Serialization;
using System.Xml;

namespace n2parser
{
    public class CDATA : IXmlSerializable
    {

        private string text;

        public CDATA()
        { }

        public CDATA(string text)
        {
            this.text = text;
        }

        public string Text
        {
            get { return text; }
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            this.text = reader.ReadString();
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            writer.WriteCData(this.text);
        }

    }

}
