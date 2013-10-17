using System.Xml;
namespace ColtPlugin
{
    public class COLTRemoteProject
    {
        public string parentIDEProjectPath;

        public string path;
        public string name;

        public string[] sources;
        public string[] libraries;
        public string[] assets;

        public string htmlTemplateDir;

        public string flashPlayerPath;
        public string flexSDKPath;
        public string customConfigPath;

        public string mainClass;

        public string outputFileName;
        public string outputPath;
        public string targetPlayerVersion;
        public string compilerOptions;

        public void Save()
        {
            // <xml projectName="untitled" projectType="AS">
            XmlDocument doc = new XmlDocument();
            XmlElement root = (XmlElement)doc.AppendChild(doc.CreateElement("", "xml", ""));
            root.Attributes.Append(doc.CreateAttribute("projectType")).Value = "AS";
            root.Attributes.Append(doc.CreateAttribute("projectName")).Value = name;

            // <paths> <sources-set>src</sources-set> <libraries-set/> <assets-set/> <html-template/> </paths>
            XmlElement paths = (XmlElement)root.AppendChild(doc.CreateElement("", "paths", ""));
            ((XmlElement)paths.AppendChild(doc.CreateElement("", "sources-set", ""))).InnerText = string.Join(", ", sources);
            ((XmlElement)paths.AppendChild(doc.CreateElement("", "libraries-set", ""))).InnerText = string.Join(", ", libraries);
            ((XmlElement)paths.AppendChild(doc.CreateElement("", "assets-set", ""))).InnerText = string.Join(", ", assets);
            ((XmlElement)paths.AppendChild(doc.CreateElement("", "html-template", ""))).InnerText = htmlTemplateDir;

            // 	<build>
            //	    <sdk>
            //	        <sdk-path>/Applications/COLT/COLT.app/flex_sdk</sdk-path>
            //	        <use-flex>true</use-flex>
            //	        <use-custom>false</use-custom>
            //	        <custom-config/>
            //	    </sdk>
            XmlElement build = (XmlElement)root.AppendChild(doc.CreateElement("", "build", ""));
            XmlElement sdk = (XmlElement)build.AppendChild(doc.CreateElement("", "sdk", ""));
            XmlElement sdkPath = (XmlElement)sdk.AppendChild(doc.CreateElement("", "sdk-path", ""));
            if ((flexSDKPath != null) && (flexSDKPath.Length > 0))
            {
                sdkPath.InnerText = flexSDKPath;
            }
            else
            {
                sdkPath.InnerText = "${colt_home}/flex_sdk";
            }
            ((XmlElement)sdk.AppendChild(doc.CreateElement("", "use-flex", ""))).InnerText = "true";
            XmlElement useCustom = (XmlElement)sdk.AppendChild(doc.CreateElement("", "use-custom", ""));
            XmlElement customConfig = (XmlElement)sdk.AppendChild(doc.CreateElement("", "custom-config", ""));
            if ((customConfigPath != null) && (customConfigPath.Length > 0))
            {
                useCustom.InnerText = "true";
                customConfig.InnerText = customConfigPath;
            }
            else
            {
                useCustom.InnerText = "false";
            }

            //<build>
            //    <main-class>/Users/eliseyev/IdeaProjects/untitled/src/com/codeorchestra/Tree.as</main-class>
            //    <output-name>untitled.swf</output-name>
            //    <output-path>/Users/eliseyev/IdeaProjects/untitled/output</output-path>
            //    <use-max-version>true</use-max-version>
            //    <player-version>11.8</player-version>
            //    <is-rsl>false</is-rsl>
            //    <locale/>
            //    <is-exclude>false</is-exclude>
            //    <is-interrupt>false</is-interrupt>
            //    <interrupt-value>30</interrupt-value>
            //    <compiler-options/>
            //</build>
            XmlElement build2 = (XmlElement)build.AppendChild(doc.CreateElement("", "build", ""));
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "main-class", ""))).InnerText = mainClass;
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "output-name", ""))).InnerText = outputFileName;
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "output-path", ""))).InnerText = outputPath; // FIXME: same path in <production> below - leave it blank here??
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "use-max-version", ""))).InnerText = "true";
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "player-version", ""))).InnerText = targetPlayerVersion;
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "is-rsl", ""))).InnerText = "false";
            build2.AppendChild(doc.CreateElement("", "locale", ""));
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "is-exclude", ""))).InnerText = "false";
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "is-interrupt", ""))).InnerText = "true"; // "false";
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "interrupt-value", ""))).InnerText = "30";
            ((XmlElement)build2.AppendChild(doc.CreateElement("", "compiler-options", ""))).InnerText = compilerOptions;
            
            //<production>
            //    <output-path>/Users/eliseyev/IdeaProjects/untitled/output</output-path>
            //    <compress>false</compress>
            //    <optimize>false</optimize>
            //</production>
            //<run-target>
            //    <run-target>SWF</run-target>
            //</run-target>
            XmlElement production = (XmlElement)build.AppendChild(doc.CreateElement("", "production", ""));
            ((XmlElement)production.AppendChild(doc.CreateElement("", "output-path", ""))).InnerText = outputPath;
            ((XmlElement)production.AppendChild(doc.CreateElement("", "compress", ""))).InnerText = "false";
            ((XmlElement)production.AppendChild(doc.CreateElement("", "optimize", ""))).InnerText = "false";
            XmlElement runTarget = (XmlElement)build.AppendChild(doc.CreateElement("", "run-target", ""));
            ((XmlElement)runTarget.AppendChild(doc.CreateElement("", "run-target", ""))).InnerText = "SWF";

            //<live>
            //    <settings>
            //        <clear-log>false</clear-log>
            //        <disconnect>false</disconnect>
            //    </settings>
            //    <launch>
            //        <launcher>DEFAULT</launcher>
            //        <player-path/>
            //    </launch>
            //    <live>
            //        <live-type>annotated</live-type>
            //        <paused>false</paused>
            //        <make-gs-live>false</make-gs-live>
            //        <max-loop>10000</max-loop>
            //    </live>
            //</live>
            XmlElement live = (XmlElement)root.AppendChild(doc.CreateElement("", "live", ""));
            XmlElement settings = (XmlElement)live.AppendChild(doc.CreateElement("", "settings", ""));
            ((XmlElement)settings.AppendChild(doc.CreateElement("", "clear-log", ""))).InnerText = "true"; // "false";
            ((XmlElement)settings.AppendChild(doc.CreateElement("", "disconnect", ""))).InnerText = "true"; // "false";
            XmlElement launch = (XmlElement)live.AppendChild(doc.CreateElement("", "launch", ""));
            ((XmlElement)launch.AppendChild(doc.CreateElement("", "launcher", ""))).InnerText = "DEFAULT";
            launch.AppendChild(doc.CreateElement("", "player-path", ""));
            XmlElement live2 = (XmlElement)live.AppendChild(doc.CreateElement("", "live", ""));
            ((XmlElement)live2.AppendChild(doc.CreateElement("", "live-type", ""))).InnerText = "annotated";
            ((XmlElement)live2.AppendChild(doc.CreateElement("", "paused", ""))).InnerText = "false";
            ((XmlElement)live2.AppendChild(doc.CreateElement("", "make-gs-live", ""))).InnerText = "false";
            ((XmlElement)live2.AppendChild(doc.CreateElement("", "max-loop", ""))).InnerText = "10000";

            doc.Save(path);
        }
    }
}
