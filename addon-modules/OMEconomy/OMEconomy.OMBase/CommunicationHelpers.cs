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

namespace OMEconomy.OMBase
{
    public class CommunicationHelpers
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static String NormaliseURL(String url)
        {
            url = url.EndsWith("/") ? url : (url + "/");
            url = url.StartsWith("http://") ? url : ("http://" + url);
            return url;
        }

        public static String GetRegionAdress(Scene scene)
        {
            if (scene == null)
                return String.Empty;

            return String.Format("http://{0}:{1}/",
                scene.RegionInfo.ExternalEndPoint.Address.ToString(), scene.RegionInfo.HttpPort.ToString());
        }

        public static String HashParameters(Hashtable parameters, string secret)
        {
            StringBuilder concat = new StringBuilder();

            //Ensure that the parameters are in the correct order
            SortedList<string, string> sortedParameters = new SortedList<string, string>();
            foreach (DictionaryEntry parameter in parameters)
            {
                sortedParameters.Add((string)parameter.Key, (string)parameter.Value);
            }

            foreach (KeyValuePair<string, string> de in sortedParameters)
            {
                concat.Append((string)de.Key + (string)de.Value);
            }
            return HashString(concat.ToString(), secret);
        }

        public static String HashString(string message, string secret)
        {
            SHA1 hashFunction = new SHA1Managed();
            byte[] hashValue = hashFunction.ComputeHash(Encoding.UTF8.GetBytes(message + secret));

            string hashHex = "";
            foreach (byte b in hashValue)
            {
                hashHex += String.Format("{0:x2}", b);
            }

            return hashHex;
        }

        public static String SerializeDictionary(Dictionary<string, string> data)
        {
            string value = String.Empty;
            foreach (KeyValuePair<string, string> pair in data)
            {
                value += pair.Key + "=" + pair.Value + "&";
            }
            return value.Remove(value.Length - 1);
        }

        public static Dictionary<string, string> DoRequest(string url, Dictionary<string, string> postParameters)
        {
            string postData = postParameters == null ? "" : CommunicationHelpers.SerializeDictionary(postParameters);
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] data = encoding.GetBytes(postData);
            String str = String.Empty;

            #region // Debug
#if DEBUG
            m_log.Debug("[OMECONOMY] Request: " + url + "?" + postData);
#endif
            #endregion

            try
            {
#if INSOMNIA
          ServicePointManager.ServerCertificateValidationCallback = delegate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
#endif

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.Timeout = 20000;
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                Stream requestStream = request.GetRequestStream();

                requestStream.Write(data, 0, data.Length);
                requestStream.Close();

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();

                StreamReader reader = new StreamReader(responseStream, Encoding.Default);
                str = reader.ReadToEnd();
                reader.Close();
                responseStream.Flush();
                responseStream.Close();
                response.Close();

                #region // Debug
#if DEBUG
                m_log.DebugFormat("[OMECONOMY] Response: {0}", str);
#endif
                #endregion

                Dictionary<string, string> returnValue = JsonMapper.ToObject<Dictionary<string, string>>(str);
                return returnValue != null ? returnValue : new Dictionary<string, string>();

            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[OMBASE]: Could not parse response Exception: {0} - {1}", e.Message, e.StackTrace);
                return null;
            }
        }

        public static bool ValidateRequest(Hashtable communicationData, Hashtable requestData, string gatewayURL)
        {
            OMBaseModule omBase = new OMBaseModule();

            string hashValue = (string)(communicationData)["hashValue"];
            UUID regionUUID = UUID.Parse((string)(communicationData)["regionUUID"]);
            UInt32 nonce = UInt32.Parse((string)(communicationData)["nonce"]);
            string notificationID = (string)(communicationData)["notificationID"];

            Dictionary<string, string> d = new Dictionary<string, string>();
            d.Add("method", "verifyNotification");
            d.Add("notificationID", notificationID);
            d.Add("regionUUID", regionUUID.ToString());
            d.Add("hashValue", HashString(nonce++.ToString(), omBase.GetRegionSecret(regionUUID)));
            Dictionary<string, string> response = DoRequest(gatewayURL, d);
            string secret = (string)response["secret"];

            if (hashValue == HashParameters(requestData, secret))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GetGatewayURL(string initURL, string name, string moduleVersion, string gatewayEnvironment)
        {

            #region // Debug
#if DEBUG
            m_log.DebugFormat("[OMECONOMY] getGatewayURL({0}, {1}, {2}, {3})",
                initURL, name, moduleVersion, gatewayEnvironment);
#endif
            #endregion

            Dictionary<string, string> d = new Dictionary<string, string>();
            d.Add("moduleName", name);
            d.Add("moduleVersion", moduleVersion);
            d.Add("gatewayEnvironment", gatewayEnvironment);

            Dictionary<string, string> response = CommunicationHelpers.DoRequest(initURL, d);
            string gatewayURL = (string)response["gatewayURL"];

            if (gatewayURL != null)
            {
                m_log.InfoFormat("[{0}]: GatewayURL: {1}", name, gatewayURL);
            }
            else
            {
                m_log.ErrorFormat("[{0}]: Could not set the GatewayURL - Please restart or contact the module vendor", name);
            }
            return gatewayURL;
        }
    }
}
