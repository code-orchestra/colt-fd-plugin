﻿namespace ColtPlugin.Rpc
{
    using System;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Web.Services.Protocols;
    using Jayrock.Json;
    using System.Xml;

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

        public JsonRpcClient(string projectPath)
            : base()
        {
            string coltFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") + @"\.colt\";

            XmlDocument storageDescriptor = new XmlDocument();
            storageDescriptor.Load(coltFolder + "storage.xml");

            // <xml>
            //  <storage path='/Users/makc/Desktop/Site/site.colt' subDir='8572a4d3' />
            XmlNodeList storageList = storageDescriptor.SelectNodes("/xml/storage[@path='" + projectPath + "']");
            if (storageList.Count == 1)
            {
                string storage = storageList[0].Attributes["subDir"].Value;
                int port = int.Parse ( File.ReadAllText(coltFolder + @"storage\" + storage + @"\rpc.info").Split(':')[1] );

                Url = "http://127.0.0.1:" + port + "/rpc/coltService"; return;
            }

            Url = "http://127.0.0.1:8092/rpc/coltService";
        }

        public virtual object Invoke(string method, params object[] args)
        {
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
}
