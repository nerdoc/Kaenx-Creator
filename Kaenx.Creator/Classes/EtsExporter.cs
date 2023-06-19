using Kaenx.Creator.Models;
using Kaenx.Creator.Models.Dynamic;
using Kaenx.Creator.Signing;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Windows;

namespace Kaenx.Creator.Classes
{
    public class EtsExporter
    {
        private ModelGeneral _general;
        List<Models.AppVersionModel> vers = new List<AppVersionModel>();
        List<Models.Hardware> hardware = new List<Hardware>();
        List<Models.Device> devices = new List<Device>();
        List<Models.Application> apps = new List<Models.Application>();
        private string _assemblyPath;
        private string manuId = "";

        public EtsExporter(ModelGeneral gen, AppVersionModel ver, string assemblyPath)
        {
            _general = gen;
            _assemblyPath = assemblyPath;
            vers.Add(ver);
            Models.Application app = _general.Applications.Single(a => a.Versions.Any(v => v.NameText == ver.NameText));
            apps.Add(app);
            Models.Hardware hard = _general.Hardware.First(h => h.Apps.Contains(app));
            hardware.Add(hard);
            Models.Device dev = hard.Devices.First();
            devices.Add(dev);
        } 
    
        public void Export()
        {
            string appId = "";
            if(_general.IsOpenKnx)
            {
                manuId = "M-00FA";
                appId = $"{manuId}_A-{_general.ManufacturerId:X2}{apps[0].Number:X2}-{vers[0].Number:X2}";
            }
            else
            {
                manuId = $"M-{_general.ManufacturerId:X4}";
                appId = $"{manuId}_A-{apps[0].Number:X4}-{vers[0].Number:X2}";
            }
            string path = @"C:\ProgramData\KNX\ETS6\ProjectStore\P-04FF";
            string oldAppId = Directory.GetDirectories(Path.Combine(path, manuId)).Single(d => d.Contains(appId));
            oldAppId = oldAppId.Substring(oldAppId.LastIndexOf('\\')+1);




            if(!Directory.GetDirectories(Path.Combine(path, manuId)).Any(d => d.Contains(appId)))
            {
                MessageBox.Show($"In dem Projekt existiert die AppId {appId} nicht");
                return;
            }
            path = Directory.GetDirectories(Path.Combine(path, manuId)).Single(d => d.Contains(appId));

            string filePath = "temp.knxprod";
            ExportHelper helper = new ExportHelper(_general, hardware, devices, apps, vers, _assemblyPath, filePath);
            ObservableCollection<PublishAction> actions = new ObservableCollection<PublishAction>();
            helper.ExportEts(actions);

            if(actions.Any(a => a.State == PublishState.Fail))
            {
                string errors = string.Join("\r\n", actions.Where(a => a.State == PublishState.Fail));
                MessageBox.Show("Beim Erstellen trat ein Fehler auf:\r\n" + errors);
                return;
            }

            //helper.SignOutput(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "Temp"));
            string appFile = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output", "Temp", manuId)).Single(d => d.Contains(appId));

            string content = File.ReadAllText(appFile);
            content = content.Replace(appId + "-0000", oldAppId);
            File.WriteAllText(appFile, content);

            File.Delete(filePath);

            DoReplace(path, oldAppId,  appFile);
        }
        /*
            M-00FA/AC   Skripte mit Nachrichten
        */

        Dictionary<string, string> map = new Dictionary<string, string>() {
            {"ApplicationPrograms", "APS"},
            {"ApplicationProgram", "AP"},
            {"Static", "St"},
            {"RelativeSegment", "RS"},
            {"ParameterTypes", "PTS"},
            {"ParameterType", "PT"},
            {"TypeRestricted", "TR"},
            {"TypeText", "TTxt"},
            {"TypeNumber", "TNr"},
            {"Parameters", "PS"},
            {"Parameter", "P"},
            {"Memory", "M"},
            {"ParameterRefs", "PRS"},
            {"ParameterRef", "PR"},
            {"ComObjectTable", "COT"},
            {"ComObjectRefs", "CORS"},
            {"AddressTabe", "ADRT"},
            {"AssociationTable", "ASSOT"},
            {"Message", "Ms"},
            {"Languages","LS"},
            {"Language","L"},
            {"TranslationUnit","TU"},
            {"TranslationElement","TE"},
            {"Translation","T"}
        };

        List<string> dynamics = new List<string>()
        {
            "CH",
            "PB",
            "PS",
            "B"
        };

        private void DoReplace(string projectPath, string oldAppId, string newAppPath)
        {
            XDocument xdoc = XDocument.Load(newAppPath);
            string ns = xdoc.Root.Name.NamespaceName;

            foreach(XElement xele in xdoc.Descendants())
            {
                if(map.ContainsKey(xele.Name.LocalName))
                {
                    xele.Name = map[xele.Name.LocalName];
                } else {
                    xele.Name = xele.Name.LocalName;
                }
            }
            xdoc.Root.Attributes().Where((x) => x.IsNamespaceDeclaration).Remove();
            xdoc.Root.Name = xdoc.Root.Name.LocalName;

            XElement xlangs = xdoc.Root.Element(XName.Get("ManufacturerData")).Element(XName.Get("Manufacturer")).Element(XName.Get("LS"));
            xlangs.Remove();

            File.WriteAllText(Path.Combine(projectPath, "A"), xdoc.Root.ToString());
            HashFile(Path.Combine(projectPath, "A"));

            string xeleCopy = xlangs.ToString();
            
            foreach(XElement xele in xlangs.Descendants().ToList())
            {
                if(xele.Attribute("RefId") == null) continue;
                string type = xele.Attribute("RefId").Value;
                if(type.Split('_').Length < 3) continue;
                type = type.Split('_')[2];
                type = type.Substring(0, type.IndexOf('-'));

                if(type == "MD")
                {
                    type = xele.Attribute("RefId").Value.Split('_')[3];
                    type = type.Substring(0, type.IndexOf('-'));
                }

                if(dynamics.Contains(type))
                    xele.Remove();
            }

            File.WriteAllText(Path.Combine(projectPath, "AT"), xlangs.ToString());
            HashFile(Path.Combine(projectPath, "AT"));


            xlangs = XElement.Parse(xeleCopy);
            foreach(XElement xele in xlangs.Descendants().ToList())
            {
                if(xele.Attribute("RefId") == null) continue;
                string type = xele.Attribute("RefId").Value;
                if(type.Split('_').Length < 3) continue;
                type = type.Split('_')[2];
                type = type.Substring(0, type.IndexOf('-'));

                if(type == "MD")
                {
                    type = xele.Attribute("RefId").Value.Split('_')[3];
                    type = type.Substring(0, type.IndexOf('-'));
                }

                if(!dynamics.Contains(type))
                    xele.Remove();
            }

            File.WriteAllText(Path.Combine(projectPath, "ADT"), xlangs.ToString());
            HashFile(Path.Combine(projectPath, "ADT"));


        }

        private void HashFile(string path)
        {
            byte[] file = Hasher.getFileHash(path);
            byte[] data = Hasher.buildEts5Hash(file, 41, 0);
            System.IO.File.WriteAllText(path + ".ets5hash", Convert.ToBase64String(data));
        }
    }
}