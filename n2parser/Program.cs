using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Xml.Serialization;

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
            List<ContentRoot> docFiles = TraverseDirectory(dir);
            Console.WriteLine("Found {0} documentation files", docFiles.Count);

            docFiles.ForEach(a => ProcessContentItem(a));

            XmlSerializer xss = new XmlSerializer(typeof(List<ContentRoot>));
            using (var txt = File.CreateText("n2data.xml"))
            {
                xss.Serialize(txt, docFiles);
                txt.Close();
            }

//            Console.ReadKey();
        }

        static void ProcessContentItem(ContentRoot root)
        {
            // depth-first
            root.ChildPages.ForEach(a => ProcessContentItem(a));

            string filename = root.cfile;
            if (String.IsNullOrWhiteSpace(filename))
                return; // skip

            if (Directory.Exists(filename))
                return; // is a default contentitem

            if (!File.Exists(filename))
                return;


            Console.WriteLine("Processing {0}", Path.GetFileName(filename));
            XDocument doc = ReadXML(filename);
            if (doc != null)
            {
                ParseOptions po = ReadOptions(doc);
                root.Zones.AddRange(ReadZones(doc, po));

                var titles = doc.Descendants("title").ToList();
                if (titles.Count > 0)
                    root.Title = titles.First().InnerXml();
            }

            root.cparsed = true;
        }

        private static ParseOptions ReadOptions(XDocument doc)
        {
            ParseOptions po = new ParseOptions();

            var n2properties = from x in doc.Descendants("meta")
                               let nameAttr = x.Attribute("name")
                               where nameAttr != null && nameAttr.Value.StartsWith("N2:", StringComparison.OrdinalIgnoreCase)
                               select new { name = nameAttr.Value, value = x.Attribute("content").Value };

            foreach (var pi in n2properties)
            {
                Console.WriteLine("    name = {0}, value = {1}", pi.name, pi.value);

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
            return po;
        }

        private static IEnumerable<Zone> ReadZones(XDocument doc, ParseOptions po)
        {
            // find zones 
            List<Zone> zones = new List<Zone>();
            List<XElement> xmlZones = doc.Descendants("div").Where(Jquery2Linq(po.ZoneClass)).ToList();

            foreach (var zoneElement in xmlZones)
            {
                Zone z = new Zone();
                z.Name = zoneElement.Attribute("id").Value;
                if (zones.Any(a => a.Name.Equals(z.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("Duplicate zone");
                zones.Add(z);
                
                // find parts within each zone 
                List<XElement> xmlParts = zoneElement.Descendants("div").Where(Jquery2Linq(po.PartClass)).ToList();
                foreach (var xmlPart in xmlParts)
                {
                    // find out the type of the part (second CSS class)
                    var possibleTypes = GetLeftoverClasses(xmlPart.Attribute("class").Value, po.PartClass);
                    if (possibleTypes.Count != 1)
                        throw new Exception(string.Format("invalid css class: {0}", xmlPart.Attribute("class").Value));

                    Part ppart = new Part();

                        // find out each attribute of each part 
                        foreach (var attr in xmlPart.Descendants("div").Where(Jquery2Linq(po.AttributeClass)))
                        {
                            var possibleAttrs = GetLeftoverClasses(attr.Attribute("class").Value, po.AttributeClass);
                            if (possibleAttrs.Count != 1)
                                throw new Exception(string.Format("invalid css class: {0}", xmlPart.Attribute("class").Value));

                            String attr_name = possibleAttrs[0];
                            String attr_value = attr.InnerXml();

                            // remove HTML comments
                            attr_value = System.Text.RegularExpressions.Regex.Replace(attr_value, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
                        
                        
                            ppart.Properties.Add(new PartAttribute()
                            {
                                Key = attr_name,
                                Value = new CDATA(attr_value)
                            });
                            Console.WriteLine("    =====> {0}.{1} = {2} ", possibleTypes[0], attr_name, (attr_value.Length));
                        }

                    z.Parts.Add(ppart);

                }
            }

            return zones;
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
        private static List<ContentRoot> TraverseDirectory(string dir)
        {
            var docFiles = new List<ContentRoot>();

            // for this directory -- 

            string[] files = Directory.GetFiles(dir);
            ContentRoot page;

            string[] rootPages = files.Where(a=> 
                                                a.EndsWith("index.htm", StringComparison.OrdinalIgnoreCase) ||
                                                a.EndsWith("index.html", StringComparison.OrdinalIgnoreCase) ||
                                                a.EndsWith("@root.htm", StringComparison.OrdinalIgnoreCase) || 
                                                a.EndsWith("@root.html", StringComparison.OrdinalIgnoreCase)
                                            ).ToArray();
            if (rootPages.Length > 1)
                throw new Exception(String.Format("Ambiguous root pages in directory '{0}' : [{1}]", dir, 
                    String.Join(", ", rootPages)));

            string[] subdirectories = Directory.GetDirectories(dir);

            if (rootPages.Length > 0)
            {
                // this is a root content-item (ignore sibiling items and assign any child dirs to this item)

                ContentRoot pg = new ContentRoot() { cfile = rootPages[0] };
                docFiles.Add(pg);

                foreach (var subdirectory in subdirectories)
                    pg.ChildPages.AddRange(TraverseDirectory(subdirectory));
            }
            else
            {
                // each page is a non-root content-item; need to generate a dummy root for them

                if (subdirectories.Length > 0)
                    throw new Exception(String.Format("Directory {0} can't contain subdirectories because it doesn't have a root page", dir));

                ContentRoot dummy = new ContentRoot() { cfile = dir  };
                docFiles.Add(dummy);

                foreach (var file in files.Where(a=> 
                    a.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) || 
                    a.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    )

                    dummy.ChildPages.Add(new ContentRoot() { cfile = file });

                foreach (var subdirectory in subdirectories)
                    dummy.ChildPages.AddRange(TraverseDirectory(subdirectory));

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
                    var c = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("-> warning: fixing &nbsp; in xml");
                    Console.ForegroundColor = c;
                    text = text.Replace("&nbsp;", "&#160;");
                }
                return XDocument.Load(new StringReader(text));
            }
            catch (Exception x)
            {
                var c = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("-> failed to read file {1}: {0}", x.Message, filename);
                Console.ForegroundColor = c;
                return null;
            }
        }
    }
}
