using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace getpidinfo
{
    public class XMLConfig
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        public XMLConfig(string file)
        {

            if (!File.Exists(file)) throw new FileNotFoundException("config xml not does not exist");
            var xml = XDocument.Load(file);
            var configuration = xml.Element("configuration");

            // load all values from xml config
            foreach (var e in configuration.Elements())
            {
                values[e.Name.LocalName] = e.Value;
            }

            // in all values, replace [varName] with its actual value
            for (int i = 0; i < 5; i++) // few iterations to propagate [varName]
            {
                var valuesCopy = new Dictionary<string, string>(values);
                foreach (var kvp_replaceInThis in valuesCopy.Keys)
                {
                    foreach (var kvp_replaceBy in valuesCopy)
                    {
                        values[kvp_replaceInThis] = values[kvp_replaceInThis].Replace("[" + kvp_replaceBy.Key + "]", kvp_replaceBy.Value);
                    }
                }
            }


            //var modFoldersPriority = values["modFoldersPriority"].Split('\n').Select(x => x.Trim()).Reverse().ToList();
            //values["modFolders"] = string.Join(";", modFolders.OrderByDescending(x => modFoldersPriority.IndexOf(x)));
            //values["port"] = port.ToString();

        }

        public string this[string key]
        {
            get
            {
                return values[key];
            }
        }


    }
}
