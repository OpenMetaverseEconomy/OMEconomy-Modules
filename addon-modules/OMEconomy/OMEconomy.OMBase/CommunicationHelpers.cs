/*
 * Michael E. Steurer, 2011
 * Institute for Information Systems and Computer Media
 * Graz University of Technology
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using OpenMetaverse;
using System;
using System.Collections;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using log4net;
using System.Reflection;
using System.Text;
using System.Net;
using System.IO;
using LitJson;
using System.Security.Cryptography;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Nwc.XmlRpc;

namespace OMEconomy.OMBase
{
    public class CommunicationHelpers
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private string m_gridShortName = String.Empty;
		private string m_gridURL = String.Empty;

		private SceneHandler m_sceneHandler = SceneHandler.getInstance();

		private String m_gatewayURL = String.Empty;
		private String m_initURL = String.Empty;
		private String m_gatewayEnvironment = String.Empty;
		private String m_moduleName = "OMEconomy";

		public static bool ValidateServerCertificate(
			object sender,
			X509Certificate certificate,
			X509Chain chain,
			SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors == SslPolicyErrors.None) {
				return true;
			}
			String moduleName = "OMEconomy";
			if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch) {
				m_log.ErrorFormat("[{0}]: WARNING Server provided a certificate that does not match its hostname", moduleName);
				return false;
			}
			m_log.ErrorFormat("[{0}]: Could not validate server certificate.", moduleName);
			m_log.ErrorFormat("[{0}]: If you are on Linux, try: mozroots --import --ask-remove", moduleName);
			m_log.ErrorFormat("[{0}]: with the user running OpenSim. (Or use --machine).", moduleName);
			return false;
		}

		public CommunicationHelpers(Nini.Config.IConfigSource config, String moduleName, String moduleVersion) {
			try {
				Nini.Config.IConfig startupConfig = config.Configs["OpenMetaverseEconomy"];
				m_gatewayEnvironment = startupConfig.GetString("OMBaseEnvironment", "TEST");
				m_initURL = startupConfig.GetString("OMEconomyInitialize", String.Empty);
				m_gridShortName = startupConfig.GetString("GridShortName", String.Empty);
				m_gridURL = config.Configs["GridService"].GetString("GridServerURI", String.Empty);
				m_moduleName = moduleName;

				if(m_gridShortName == String.Empty || m_initURL == String.Empty) {
					m_log.ErrorFormat("[{0}]: GridShortName or OMEconomyInitialize not set", moduleName);
					return;
				}

				#if DEBUG
				m_log.Debug(String.Format("[{1}] getGatewayURL({0}, {1}, {2}, {3})", m_initURL, moduleName, moduleVersion, m_gatewayEnvironment));
				#endif

				Dictionary<string, string> d = new Dictionary<string, string>();
				d.Add("moduleName", moduleName);
				d.Add("moduleVersion", moduleVersion);
				//d.Add("gridShortName", m_gridShortName);
				d.Add("gatewayEnvironment", m_gatewayEnvironment);

				m_gatewayURL = m_initURL; //use to obtain the real gatewayURL;
				Dictionary<string, string> response = DoRequestDictionary(d);
				if (response != null)
				{
					m_gatewayURL = (string)response["gatewayURL"];

					if(m_gatewayURL != m_initURL && m_gatewayURL != null) 
					{
						m_log.InfoFormat("[{0}]: GatewayURL: {1}", m_moduleName, m_gatewayURL);
					} 
					else 
					{
						m_log.ErrorFormat("[{0}]: Could not set the GatewayURL - Please restart or contact the module vendor", m_moduleName);
					}
				} 
				else 
				{
					m_gatewayURL = null;
					m_log.ErrorFormat("[{0}]: Could not retrieve GatewayURL", m_moduleName);
				}

			} catch(Exception e) {
				m_log.ErrorFormat("[{0}]: " + e, m_moduleName);
			}

		}

		public String GetRegionAdress(Scene scene)
		{
			if (scene == null)
				return String.Empty;

			return String.Format("http://{0}:{1}/",
			                     scene.RegionInfo.ExternalEndPoint.Address.ToString(), scene.RegionInfo.HttpPort.ToString());
		}

        public String NormaliseURL(String url)
        {
            url = url.EndsWith("/") ? url : (url + "/");
            url = url.StartsWith("http://") ? url : ("http://" + url);
            return url;
        }

		public String getGridShortName() {
			return m_gridShortName;
		}

		public String HashParameters(Hashtable parameters, string nonce, UUID regionUUID)
        {
            StringBuilder concat = new StringBuilder();

            //Ensure that the parameters are in the correct order
            SortedList<string, string> sortedParameters = new SortedList<string, string>();
            foreach(DictionaryEntry parameter in parameters)
            {
                sortedParameters.Add((string)parameter.Key, (string)parameter.Value);
            }

            foreach(KeyValuePair<string, string> de in sortedParameters)
            {
                concat.Append((string)de.Key + (string)de.Value);
            }
			String regionSecret = m_sceneHandler.m_regionSecrets[regionUUID];

			String message = concat.ToString() + nonce + regionSecret;
			SHA1 hashFunction = new SHA1Managed();
			byte[] hashValue = hashFunction.ComputeHash(Encoding.UTF8.GetBytes(message));

			string hashHex = "";
			foreach(byte b in hashValue)
			{
				hashHex += String.Format("{0:x2}", b);
			}

#if DEBUG
			m_log.Debug(String.Format("[{0}] SHA1({1}) = {2}", m_moduleName, message, hashHex));
#endif

			return hashHex;
		}

        public String SerializeDictionary(Dictionary<string, string> data)
        {
            string value = String.Empty;
			if(data.Count == 0) 
			{
				return value;
			}

            foreach (KeyValuePair<string, string> pair in data)
            {
                value += pair.Key + "=" + pair.Value + "&";
            }
            return value.Remove(value.Length - 1);
        }

        public Dictionary<string, string> DoRequestDictionary(Dictionary<string, string> postParameters) {
            string str = DoRequestPlain(postParameters);
            if(str != null && str.Length == 0) {
                return new Dictionary<string, string>();
            }
            return str != null ? JsonMapper.ToObject<Dictionary<string, string>> (str) : null;
        }

		private String DoRequestPlain(Dictionary<string, string> postParameters) 
		{
			if (m_gatewayURL == null) {
				m_log.ErrorFormat("[{0}]: Could not access web service. GatewayURL not set.", m_moduleName);
				return null;
			}

			postParameters.Add("gridShortName", m_gridShortName);
			postParameters.Add("gridURL", m_gridURL);

			string postData = postParameters == null ? "" : SerializeDictionary(postParameters);
			ASCIIEncoding encoding = new ASCIIEncoding();
			byte[]  data = encoding.GetBytes(postData);

#if DEBUG
			m_log.DebugFormat("[{0}] Request: {1}?{2}", m_moduleName, m_gatewayURL, postData);
#endif

			try 
			{
				ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;

				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(m_gatewayURL);
				request.Method = "POST";
				request.Timeout = 100000;
				request.ContentType="application/x-www-form-urlencoded";
				request.ContentLength = data.Length;
				Stream requestStream = request.GetRequestStream();

				requestStream.Write(data, 0, data.Length);
				requestStream.Close();

				HttpWebResponse response = (HttpWebResponse)request.GetResponse();
				Stream responseStream = response.GetResponseStream();

				StreamReader reader = new StreamReader(responseStream, Encoding.Default);
				string str = reader.ReadToEnd();
				reader.Close();
				responseStream.Flush();
				responseStream.Close();
				response.Close();

#if DEBUG
				m_log.DebugFormat("[{0}] Response: {1}", m_moduleName, string.Concat(str.Split()));
#endif

				return str;
			} catch (WebException e) {
				m_log.ErrorFormat("[{0}]: Could not access the Web service {1};  {2}", m_moduleName, m_gatewayURL, e.Message);
				return null;
			}
		}

		public Hashtable ValidateRequest(XmlRpcRequest request) {
			Hashtable requestData = (Hashtable)request.Params[0];
			Hashtable communicationData = (Hashtable)request.Params[1];

#if DEBUG
			m_log.DebugFormat("[{0}]: genericNotify(...)", m_moduleName);
			foreach (DictionaryEntry requestDatum in requestData) 
			{
				m_log.DebugFormat("[{0}]: {1} {2}", m_moduleName, requestDatum.Key.ToString(), (string)requestDatum.Value);
			}
			foreach (DictionaryEntry communicationDatum in communicationData) 
			{
				m_log.DebugFormat("[{0}]: {1} {2}", m_moduleName, communicationDatum.Key.ToString(), (string)communicationDatum.Value);
			}
#endif

			Hashtable requestDataHashing = (Hashtable)requestData.Clone();
			requestDataHashing.Remove("method");

			UUID regionUUID  = UUID.Parse((string)(communicationData)["regionUUID"]);
			string nonce  = (string)(communicationData)["nonce"];
			string notificationID = (string)(communicationData)["notificationID"];

			Dictionary<string, string> d = new Dictionary<string, string>();
			d.Add("method", "verifyNotification");
			d.Add("notificationID", notificationID);
			d.Add("regionUUID", regionUUID.ToString());
			d.Add("hashValue", HashParameters(requestDataHashing, nonce, regionUUID));
			Dictionary<string, string> response = DoRequestDictionary(d);
			if (response != null)
			{
				string status = (string)response["status"];
				if (status == "OK") 
				{
					return requestData;
				}
			}
			return null;
		}

		public void RegisterService(string moduleName, string moduleVersion, UUID regionUUID) 
		{
			Dictionary<string, string> d = new Dictionary<string, string>();
			d.Add("method", "registerService");
			d.Add("moduleName", moduleName);
			d.Add("moduleVersion", moduleVersion);
			d.Add("regionUUID", regionUUID.ToString());
			DoRequestDictionary(d);
		}
    }
}
