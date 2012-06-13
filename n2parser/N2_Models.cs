using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace n2parser
{
    public class ContentRoot
    {
        [XmlElement("zone")]
        public List<Zone> Zones { get; set; }

        [XmlElement("page")]
        public List<ContentRoot> ChildPages { get; set; }

        [XmlAttribute("title")]
        public string Title { get; set; }

        [XmlAttribute("physicalpath")]
        public string cfile { get; set; }

        [XmlAttribute("successfullyparsed")]
        public bool cparsed = false;

        public ContentRoot()
        {
            ChildPages = new List<ContentRoot>();
            Zones = new List<Zone>();
        }

        public override string ToString()
        {
            string s;
            if (!string.IsNullOrEmpty(Title))
                s = Title;
            else if (!string.IsNullOrEmpty(cfile))
                s = cfile;
            else
                s = "ContentPage";
            return "{" + s + "}";
        }
    }

    public class Zone
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("part")]
        public List<Part> Parts { get; set; }

        public Zone()
        {
            Parts = new List<Part>();
        }
    }

    public class Part
    {
        public Part()
        {
            Properties = new List<PartAttribute>();
        }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlElement("property")]
        public List<PartAttribute> Properties { get; set; }
    }

    public class PartAttribute
    {
        [XmlAttribute("key")]
        public string Key { get; set; }

        [XmlElement("value", Type = typeof(CDATA))]
        public CDATA Value { get; set; }

    }

}
