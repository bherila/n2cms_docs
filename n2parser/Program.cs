using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;

namespace n2parser
{
    public static class Extensions
    {
        public static string OuterXml(this XElement thiz)
        {
            var xReader = thiz.CreateReader();
            xReader.MoveToContent();
            return xReader.ReadOuterXml();
        }
        public static string InnerXml(this XElement thiz)
        {
            var xReader = thiz.CreateReader();
            xReader.MoveToContent();
            return xReader.ReadInnerXml();
        }
    }

    class Program
    {
        class ParseOptions
        {
            public ParseOptions()
            {
                ZoneClass = (".N2_Zone");
                PartClass = (".N2_Part");
                AttributeClass = (".N2_Attribute");
                IgnoreClass = (".N2_Ignore");
                ShowNav = true;
                ShowTitle = true;
                Type = "TextPage";
            }

            public string ZoneClass;
            public string PartClass;
            public string AttributeClass;
            public string IgnoreClass;
            public bool ShowNav;
            public bool ShowTitle;
            public string Type;
        }

        static Func<XElement, bool> Jquery2Linq(string jquerySelector)
        {
            string xpath = jquerySelector;
            string[] words = jquerySelector.Trim().Split(' ');
            if (words.Length == 1)
            {
                string sel = words[0];
                if (sel[0] == '.')
                {
                    // select by class
                    //xpath = string.Format(@"//div[contains(concat(' ',normalize-space(@class),' '),' {0} ')]", words[0].Trim('.'));
                    return (XElement e) =>
                        e.Name.LocalName.ToLower() == "div" &&
                        e.Attribute("class").Value.Split(' ').Any(
                            anyClass => anyClass.Trim().Equals(words[0].Trim('.', ' '), StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    throw new NotSupportedException("only css-class based selectors are supported at this time");
                }
            }
            else
            {
                throw new NotSupportedException("compound selectors are not supported");
            }
            return (XElement e) => true;
        }

        static void Main(string[] args)
        {
            string dir = Environment.CurrentDirectory;
            {
                string rootFileName = "@N2_SiteSettings.xml";
                // look up until we see rootFile
                try
                {
                    while (!Directory.GetFiles(dir, "*.xml").Any(a => Path.GetFileName(a).Equals(rootFileName, StringComparison.OrdinalIgnoreCase)))
                        dir = Directory.GetParent(dir).FullName;
                }
                catch
                {
                    Console.WriteLine("Failed to find the N2 documentation root.");
                    return;
                }
                dir = Path.Combine(dir, "Documentation");
            }

            Console.WriteLine("Found N2 documentation root at {0}", dir);
            List<string> docFiles = GetFiles(dir);
            Console.WriteLine("Found {0} documentation files", docFiles.Count);

            //XPathExpression PropertyExpression = XPathExpression.Compile(@"//meta[starts-with(@name, 'N2:')]");
            while (docFiles.Count > 0)
            {
                string filename = docFiles[0];
                docFiles.RemoveAt(0);
                Console.WriteLine("Processing {0}", Path.GetFileName(filename));
                XDocument doc = ReadXML(filename);
                if (doc != null)
                {
                    XPathNavigator docNavigator = doc.CreateNavigator();
                    ParseOptions po = new ParseOptions();

                    var n2properties = from x in doc.Descendants("meta")
                                       let nameAttr = x.Attribute("name")
                                       where nameAttr != null && nameAttr.Value.StartsWith("N2:", StringComparison.OrdinalIgnoreCase)
                                       select new { name = nameAttr.Value, value = x.Attribute("content").Value };

                    foreach (var pi in n2properties)
                    {
                        Console.WriteLine("name = {0}, value = {1}", pi.name, pi.value);

                        if (pi.name.Equals("N2:ZoneSelector", StringComparison.OrdinalIgnoreCase))
                            po.ZoneClass = (pi.value);
                        else if (pi.name.Equals("N2:PartSelector", StringComparison.OrdinalIgnoreCase))
                            po.PartClass = (pi.value);
                        else if (pi.name.Equals("N2:AttributeSelector", StringComparison.OrdinalIgnoreCase))
                            po.AttributeClass = (pi.value);
                        else if (pi.name.Equals("N2:IgnoreSelector", StringComparison.OrdinalIgnoreCase))
                            po.IgnoreClass = (pi.value);
                        else if (pi.name.Equals("N2:ShowNav", StringComparison.OrdinalIgnoreCase))
                            po.ShowNav = bool.Parse(pi.value);
                        else if (pi.name.Equals("N2:ShowTitle", StringComparison.OrdinalIgnoreCase))
                            po.ShowTitle = bool.Parse(pi.value);
                        else if (pi.name.Equals("N2:Type", StringComparison.OrdinalIgnoreCase))
                            po.Type = pi.value;

                        //TODO: Handle N2:MapProperty

                    }

                    // find zones 
                    List<XElement> Zones = doc.Descendants("div").Where(Jquery2Linq(po.ZoneClass)).ToList();
                    foreach (var zone in Zones)
                    {
                        // find parts within each zone 
                        List<XElement> Parts = zone.Descendants("div").Where(Jquery2Linq(po.PartClass)).ToList();
                        foreach (var part in Parts)
                        {
                            // find out the type of the part (second CSS class)
                            var possibleTypes = GetLeftoverClasses(part.Attribute("class").Value, po.PartClass);
                            if (possibleTypes.Count != 1)
                                throw new Exception(string.Format("invalid css class: {0}", part.Attribute("class").Value));

                            // find out each attribute of each part 
                            List<XElement> Attrs = part.Descendants("div").Where(Jquery2Linq(po.AttributeClass)).ToList();
                            foreach (var attr in Attrs)
                            {
                                var possibleAttrs = GetLeftoverClasses(attr.Attribute("class").Value, po.AttributeClass);
                                if (possibleAttrs.Count != 1)
                                    throw new Exception(string.Format("invalid css class: {0}", part.Attribute("class").Value));

                                String attr_name = possibleAttrs[0];
                                String attr_value = attr.InnerXml();

                                // remove HTML comments
                                attr_value = System.Text.RegularExpressions.Regex.Replace(attr_value, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

                                Console.WriteLine("=====> {0}.{1} = {2} ", possibleTypes[0], attr_name, attr_value);
                            }
                        }
                    }
                }
            }
            Console.ReadKey();
        }

        private static List<string> GetLeftoverClasses(string input, string targetItem)
        {
            return (from x in input.Split(' ')
                    let y = x.Trim(' ')
                    where !y.Equals(targetItem.TrimStart('.'), StringComparison.OrdinalIgnoreCase)
                    select y).ToList();
        }

        /// <summary>
        /// Recursively gets a list of the *.htm* files in a folder.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private static List<string> GetFiles(string dir)
        {
            var docFiles = new List<string>();
            List<string> docDirs = new List<string>() { dir };
            while (docDirs.Count > 0)
            {
                dir = docDirs[0];
                docDirs.RemoveAt(0);
                docFiles.AddRange(Directory.GetFiles(dir, "*.htm*"));
                docDirs.AddRange(Directory.GetDirectories(dir));
            }
            return docFiles;
        }

        /// <summary>
        /// Reads an XML file from disk and returns the XML file or null if the XML file
        /// failed to load successfully. 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        static XDocument ReadXML(string filename)
        {
            try
            {
                var text = File.ReadAllText(filename);
                if (text.Contains("&nbsp;"))
                {
                    Console.WriteLine("-> warning: fixing &nbsp; in xml");
                    text = text.Replace("&nbsp;", "&#160;");
                }
                return XDocument.Load(new StringReader(text));
            }
            catch (Exception x)
            {
                Console.WriteLine("-> failed to read file {1}: {0}", x.Message, filename);
                return null;
            }
        }
    }
}
