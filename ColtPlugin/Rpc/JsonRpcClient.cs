namespace ColtPlugin.Rpc
{
    using System;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Web.Services.Protocols;
    using Jayrock.Json;
    using System.Xml;
    using System.Timers;
    using System.Diagnostics;

    public class JsonRpcException : Exception
    {
        public String TypeName;
        public JsonRpcException(String typeName, String message) : base((typeName == null) ? "" : ("[" + typeName + "] ") + message)
        {
            TypeName = typeName;
        }
    }

    /// <summary>
    /// Based on sample client from Jayrock author
    /// http://markmail.org/message/xwlaeb3nfanv2kgm
    /// </summary>
    [System.ComponentModel.DesignerCategory("")]
    public class JsonRpcClient : HttpWebClientProtocol
    {
        private int _id;
        private string _projectPath;

        public JsonRpcClient(string projectPath)
            : base()
        {
            _projectPath = projectPath;

            string coltFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") + @"\.colt\";

            XmlDocument storageDescriptor = new XmlDocument();
            storageDescriptor.Load(coltFolder + "storage.xml");

            // <xml>
            //  <storage path='/Users/makc/Desktop/Site/site.colt' subDir='8572a4d3' />
            XmlNodeList storageList = storageDescriptor.SelectNodes("/xml/storage[@path='" + _projectPath + "']");
            if (storageList.Count == 1)
            {
                string storage = storageList[0].Attributes["subDir"].Value;
                int port = int.Parse ( File.ReadAllText(coltFolder + @"storage\" + storage + @"\rpc.info").Split(':')[1] );

                Url = "http://127.0.0.1:" + port + "/rpc/coltService"; return;
            }

            Url = "http://127.0.0.1:8092/rpc/coltService";
        }

        public void PingOrRunCOLT(string executable)
        {
            try
            {
                Invoke("ping", new Object[] { });
            }

            catch (Exception)
            {
                // put it on recent files list
                string coltFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") + @"\.colt\";

                XmlDocument workingSet = new XmlDocument();
                workingSet.Load(coltFolder + "workingset.xml");
                XmlElement root = (XmlElement)workingSet.SelectSingleNode("/workingset");
                XmlElement project = (XmlElement)root.PrependChild(workingSet.CreateElement("", "project", ""));
                project.Attributes.Append(workingSet.CreateAttribute("path")).Value = _projectPath;
                workingSet.Save(coltFolder + "workingset.xml");

                // open COLT exe
                Process.Start(executable);
            }
        }

        public virtual void InvokeAsync(string method, Callback callback, params object[] args)
        {
            JsonRpcClientAsyncHelper helper = new JsonRpcClientAsyncHelper(this, method, callback, args);
        }

        public virtual object Invoke(string method, params object[] args)
        {
            Console.WriteLine("Invoke: method = " + method);

            WebRequest request = GetWebRequest(new Uri(Url));
            request.Method = "POST";

            using (Stream stream = request.GetRequestStream())
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
            {
                JsonObject call = new JsonObject();
                call["id"] = ++_id;
                call["method"] = method;
                call["params"] = args;
                call.Export(new JsonTextWriter(writer));
            }

            using (WebResponse response = GetWebResponse(request))
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
// String s = reader.ReadToEnd();
// return s;
                JsonObject answer = new JsonObject();
                answer.Import(new JsonTextReader(reader));

                object errorObject = answer["error"];

                if (errorObject != null) OnError(errorObject);

                return answer["result"];
            }
        }

        protected virtual void OnError(object errorObject)
        {
            JsonObject error = errorObject as JsonObject;

            if (error != null)
            {
                string message = error["message"] as string;
                if (message == null) message = "";

                string exceptionType = null;
                JsonObject data = error["data"] as JsonObject;
                if (data != null)
                {
                    exceptionType = data["exceptionTypeName"] as string;
                }

                throw new JsonRpcException(exceptionType, message);
            }

            throw new Exception(errorObject as string);
        }
    }

    public delegate void Callback (object result);

    class JsonRpcClientAsyncHelper
    {
        JsonRpcClient client;
        string method;
        object[] args;
        Callback callback;
        Timer timer;
        int count;

        public JsonRpcClientAsyncHelper(JsonRpcClient client, string method, Callback callback, params object[] args)
        {
            this.client = client;
            this.method = method;
            this.args = args;
            this.callback = callback;

            timer = new Timer();
            timer.SynchronizingObject = (System.Windows.Forms.Form)PluginCore.PluginBase.MainForm;
            timer.Interval = 1000;
            timer.Elapsed += OnTimer;
            count = 0;
            OnTimer();
        }

        private void OnTimer(Object sender = null, EventArgs e = null)
        {
            timer.Stop();

            Console.WriteLine("JsonRpcClientAsyncHelper's OnTimer(), method = " + method);

            if (COLTIsRunning())
            {
                // we are good to go
                try
                {
                    object result = client.Invoke(method, args);
                    if (callback != null) callback(result);
                }

                catch (Exception error)
                {
                    if (callback != null) callback(error);
                }

                return;
            }

            if (count++ > 13)
            {
                PluginCore.Managers.TraceManager.Add(Resources.LocaleHelper.GetString("Error.StartingCOLTTimedOut"), -1);
                return;
            }

            timer.Start();
        }

        private Boolean COLTIsRunning()
        {
            try
            {
                client.Invoke("ping", new Object[] { });
                return true;
            }

            catch (Exception)
            {
            }

            return false;
        }
    }
}
