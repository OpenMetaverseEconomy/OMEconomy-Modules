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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using LitJson;
using Mono.Addins;
using OpenMetaverse;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Scenes;

[assembly: Addin("OMBaseModule", OMEconomy.OMBase.OMBaseModule.MODULE_VERSION)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OMEconomy.OMBase
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class OMBaseModule : ISharedRegionModule
    {
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private string MODULE_NAME = "OMBase";
		public const string MODULE_VERSION = "4.0.4";

        private bool Enabled = false;

		private SceneHandler m_sceneHandler = SceneHandler.getInstance();
		CommunicationHelpers m_communication = null;

       	private delegate void delegateAsynchronousClaimUser(Dictionary<string, string> data);

		public OMBaseModule() {}

        #region ISharedRegion implementation


        public string Name
        {
            get { return MODULE_NAME; }
        }

        public void Initialise(IConfigSource config)
        {

			m_communication = new CommunicationHelpers(config, MODULE_NAME, MODULE_VERSION);

            IConfig cfg = config.Configs["OpenMetaverseEconomy"];

            if (null == cfg)
                return;

            Enabled = cfg.GetBoolean("enabled", false);

            if (!Enabled)
                return;

            MainServer.Instance.AddXmlRPCHandler("OMBaseNotification", GenericNotify, false);
        }

        public void PostInitialise() { }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            m_sceneHandler.AddScene(scene);

            InitializeRegion(
				m_sceneHandler.GetRegionIP(scene), scene.RegionInfo.RegionName, scene.RegionInfo.originRegionID);

            scene.AddCommand(this, "OMBaseTest", "Test Open Metaverse Economy Connection", "Test Open Metaverse Economy Connection", TestConnection);
            scene.AddCommand(this, "OMRegister", "Registers the Metaverse Economy Module", "Registers the Metaverse Economy Module", RegisterModule);

        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;

            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnClientClosed += OnClientClosed;
        }

		private string UUIDToString(UUID item) {
			return item.ToString();
		}

        public void RemoveRegion(Scene scene)
		{
			m_log.Debug ("Close Region");
		    if (!Enabled)
		        return;

		    scene.EventManager.OnMakeRootAgent -= OnMakeRootAgent;
		    scene.EventManager.OnClientClosed -= OnClientClosed;
						
			List<string> regions = new List<string>();
			regions.Add (scene.RegionInfo.originRegionID.ToString ());

     		//List<string> regions = m_sceneHandler.GetUniqueRegions().ConvertAll<string>(UUIDToString);
			System.Threading.ThreadPool.QueueUserWorkItem(delegate {
				Dictionary<string, string> d = new Dictionary<string, string>();

				d.Add("method", "closeRegion");
				d.Add("regions", JsonMapper.ToJson(regions));
				m_communication.DoRequestDictionary(d);
			}, null);
        }

        public void Close()
        {
        }

        #endregion

        #region // Events
        private void OnMakeRootAgent(ScenePresence sp)
        {
            IClientAPI client = m_sceneHandler.LocateClientObject(sp.UUID);
            Scene currentScene = m_sceneHandler.LocateSceneClientIn(sp.UUID);

            if (sp.PresenceType == PresenceType.Npc) return;

            Dictionary<string, string> dd = new Dictionary<string, string>();
            dd.Add("method", "claimUser");
            dd.Add("avatarUUID", sp.UUID.ToString());
            dd.Add("avatarName", sp.Name);
            dd.Add("language", "ENG");
            dd.Add("viewer", sp.Viewer);
            dd.Add("clientIP", "http://" + client.RemoteEndPoint.ToString() + "/");
            dd.Add("regionUUID", m_sceneHandler.LocateSceneClientIn(sp.UUID).RegionInfo.RegionID.ToString());
            dd.Add("regionIP", m_communication.GetRegionAdress(currentScene));

            delegateAsynchronousClaimUser a = new delegateAsynchronousClaimUser(asynchronousClaimUser);
            a.BeginInvoke(dd, null, null);
        }

        private void OnClientClosed(UUID clientID, Scene scene)
        {
            Scene sc = m_sceneHandler.LocateSceneClientIn(clientID);
            if (sc == null)
                return;
			System.Threading.ThreadPool.QueueUserWorkItem(delegate {
	            try
	            {
	                Dictionary<string, string> d = new Dictionary<string, string>();
	                d.Add("method", "leaveUser");
	                d.Add("avatarUUID", clientID.ToString());
	                d.Add("regionUUID", sc.RegionInfo.RegionID.ToString());
	                m_communication.DoRequestDictionary(d);
	            }
	            catch (Exception e)
	            {
	                m_log.DebugFormat("[OMBASE]: LeaveAvatar(): {0}", e.Message);
	            }
			}, null);
        }
        #endregion

        internal void InitializeRegion(string regionAdress, string regionName, UUID regionUUID)
        {
			System.Threading.ThreadPool.QueueUserWorkItem(delegate {
	            Dictionary<string, string> d = new Dictionary<string, string>();
	            d.Add("method", "initializeRegion");
	            d.Add("regionIP", regionAdress);
	            d.Add("regionName", regionName);
	            d.Add("regionUUID", regionUUID.ToString());
	            d.Add("simulatorVersion", VersionInfo.Version);
	            d.Add("moduleVersion", MODULE_VERSION);
	            Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
	
				if (response != null)
	            {
					bool secret_updated = m_sceneHandler.SetRegionSecret(regionUUID, (string)response["regionSecret"]);
					if (secret_updated)
					{
						m_log.InfoFormat("[{0}]: Updated secret for region {1}.", Name, regionUUID);
					}
					m_log.InfoFormat("[{0}]: The Service is Available.", Name);
	            }
	            else
	            {
					m_log.ErrorFormat("[{0}]: The Service is not Available", Name);
	            }
			}, null);
        }


        private void RegisterModule(string module, string[] args)
        {
            m_log.Info("[OMECONOMY]: +-");
            m_log.Info("[OMECONOMY]: | Your grid identifier is \"" + m_communication.getGridShortName() + "\"");
            string longName = MainConsole.Instance.CmdPrompt("           [OMECONOMY]: | Please enter the grid's full name");
			string adminUUID = MainConsole.Instance.CmdPrompt("           [OMECONOMY]: | Please enter the admins Avatar UUID");

            Dictionary<string, string> d = new Dictionary<string, string>();
            d.Add("method", "registerScript");
			d.Add("gridLongName", longName);
			d.Add("adminUUID", adminUUID);
            d.Add("gridDescription", "");

            Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
			if (response != null && response.ContainsKey("success") && response["success"] == "TRUE")
            {
                m_log.Info("[OMECONOMY]: +-");
                m_log.Info("[OMECONOMY]: | Please visit");
                m_log.Info("[OMECONOMY]: |   " + response["scriptURL"]);
                m_log.Info("[OMECONOMY]: | to get the Terminal's script");
                m_log.Info("[OMECONOMY]: +-");
            }
            else
            {
                m_log.Error("Could not active the grid. Please check the parameters and try again");
            }
        }

        private void TestConnection(string module, string[] args)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            d.Add("method", "checkStatus");
            bool status = false;

			Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
			if (response != null && response.ContainsKey("status") && response["status"] == "INSOMNIA")
            {
               status = true;
            }

            m_log.Info("[OMECONOMY]: +---------------------------------------");
            m_log.Info("[OMECONOMY]: | gridID: " + m_communication.getGridShortName());
            m_log.Info("[OMECONOMY]: | connectionStatus: " + status);
            m_log.Info("[OMECONOMY]: +---------------------------------------");
        }

        private void asynchronousClaimUser(Dictionary<string, string> data)
        {
            if (m_communication.DoRequestDictionary(data) == null)
            {
                ServiceNotAvailable(new UUID(data["avatarUUID"]));
            }
        }

        private void ServiceNotAvailable(UUID agentID)
        {
            string message = "The currency service is not available. Please try again later.";
            m_sceneHandler.LocateClientObject(agentID).SendBlueBoxMessage(UUID.Zero, String.Empty, message);
        }

        public XmlRpcResponse GenericNotify(XmlRpcRequest request, IPEndPoint ep)
        {
            XmlRpcResponse r = new XmlRpcResponse();
            try
            {
                
				Hashtable requestData = m_communication.ValidateRequest(request);

                if (requestData != null)
                {
					string method = (string)requestData["method"];
                    switch (method)
                    {
                        case "notifyUser": r.Value = UserInteract(requestData);
                            break;
                        case "writeLog": r.Value = WriteLog(requestData);
                            break;
                        case "notifyIsAlive": r.Value = IsAlive(requestData);
                            break;
                        default: m_log.ErrorFormat("[{0}]: Method {1} is not supported.", Name, method);
                            break;
                    }
                }
                else
                {
					r.SetFault(1, "Could not validate the request");
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: genericNotify() Exception: {1} - {2}", Name, e.Message, e.StackTrace);
                r.SetFault(1, "Could not parse the requested method");
            }
            return r;
        }

        private Hashtable UserInteract(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            try
            {
                UUID receiverUUID = UUID.Parse((string)requestData["receiverUUID"]);
                Int32 type = Int32.Parse((string)requestData["type"]);
                string payloadID = (string)requestData["payloadID"];

                Dictionary<string, string> d = new Dictionary<string, string>();
                d.Add("method", "getNotificationMessage");
                d.Add("payloadID", payloadID);

                Dictionary<string, string> messageItems = m_communication.DoRequestDictionary(d);
                if (messageItems == null)
                {
                    throw new Exception("Could not fetch payload with ID " + payloadID);
                }

#if DEBUG
                foreach (KeyValuePair<string, string> pair in messageItems)
                {
                    m_log.Error(pair.Key + "  " + pair.Value);
                }
#endif

                IClientAPI client = m_sceneHandler.LocateClientObject(receiverUUID);
                if (client == null)
                {
                    throw new Exception("Could not locate the specified avatar");
                }

                Scene userScene = m_sceneHandler.GetSceneByUUID(client.Scene.RegionInfo.originRegionID);
                if (userScene == null)
                {
                    throw new Exception("Could not locate the specified scene");
                }

                string message = messageItems["message"];

                UUID senderUUID = UUID.Zero;
                string senderName = String.Empty;
                IDialogModule dm = null;
                IClientAPI sender = null;

                IUserManagement userManager = m_sceneHandler.GetRandomScene().RequestModuleInterface<IUserManagement>();
                if (userManager == null)
                {
                    throw new Exception("Could not locate UserMangement Interface");
                }

                switch (type)
                {
                    case (int)NotificationType.LOAD_URL:
                        string url = messageItems["url"];

                        dm = userScene.RequestModuleInterface<IDialogModule>();
                        dm.SendUrlToUser(receiverUUID, "OMEconomy", UUID.Zero, UUID.Zero, false, message, url);
                        break;

                    case (int)NotificationType.CHAT_MESSAGE:
                        senderUUID = UUID.Parse(messageItems["senderUUID"]);
                        senderName = userManager.GetUserName(senderUUID);


                        client.SendChatMessage(
                            message, (byte)ChatTypeEnum.Say, Vector3.Zero, senderName,
                            senderUUID, senderUUID, (byte)ChatSourceType.Agent, (byte)ChatAudibleLevel.Fully);

                        sender = m_sceneHandler.LocateClientObject(senderUUID);
                        if (sender != null)
                        {
                            sender.SendChatMessage(
                                message, (byte)ChatTypeEnum.Say, Vector3.Zero, senderName,
                                senderUUID, senderUUID, (byte)ChatSourceType.Agent, (byte)ChatAudibleLevel.Fully);
                        }
                        break;

                    case (int)NotificationType.ALERT:
                        dm = userScene.RequestModuleInterface<IDialogModule>();
                        dm.SendAlertToUser(receiverUUID, message);
                        break;

                    case (int)NotificationType.DIALOG:
                        client.SendBlueBoxMessage(UUID.Zero, "", message);
                        break;

                    case (int)NotificationType.GIVE_NOTECARD:
                        break;

                    case (int)NotificationType.INSTANT_MESSAGE:
                        senderUUID = UUID.Parse(messageItems["senderUUID"]);
                        UUID sessionUUID = UUID.Parse(messageItems["sessionUUID"]);
                        if (messageItems.ContainsKey("senderName"))
                        {
                            senderName = messageItems["senderName"];
                        }
                        else
                        {
                            senderName = userManager.GetUserName(UUID.Parse((string)messageItems["senderUUID"]));

                        }

                        GridInstantMessage msg = new GridInstantMessage();
                        msg.fromAgentID = senderUUID.Guid;
                        msg.toAgentID = receiverUUID.Guid;
                        msg.imSessionID = sessionUUID.Guid;
                        msg.fromAgentName = senderName;
                        msg.message = (message != null && message.Length > 1024) ? msg.message = message.Substring(0, 1024) : message;
                        msg.dialog = (byte)InstantMessageDialog.MessageFromAgent;
                        msg.fromGroup = false;
                        msg.offline = (byte)0;
                        msg.ParentEstateID = 0;
                        msg.Position = Vector3.Zero;
                        msg.RegionID = userScene.RegionInfo.RegionID.Guid;


                        client.SendInstantMessage(msg);

                        sender = m_sceneHandler.LocateClientObject(senderUUID);
                        if (sender != null)
                        {
                            sender.SendInstantMessage(msg);
                        }
                        break;

                    default:
                        break;
                }

                rparms["success"] = true;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: userInteract() Exception: {1} - {2}", Name, e.Message, e.StackTrace);
                rparms["success"] = false;
            }
            return rparms;
        }

        private Hashtable WriteLog(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            try
            {
                m_log.ErrorFormat("[{0}]: {1}", requestData["message"]);
                rparms["success"] = true;
            }
            catch (Exception)
            {
                rparms["success"] = false;
            }
            return rparms;
        }

        private Hashtable IsAlive(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            rparms["success"] = false;
            if (requestData.ContainsKey("avatarUUID"))
            {
                UUID avatarUUID = UUID.Parse((string)requestData["avatarUUID"]);
                if (m_sceneHandler.LocateClientObject(avatarUUID) != null)
                {
                    rparms["success"] = true;
                }
            }
            else
            {
                rparms["success"] = true;
                rparms["version"] = MODULE_VERSION;
            }
            return rparms;
        }
    }

    public enum NotificationType : int
    {
        LOAD_URL = 1,
        INSTANT_MESSAGE = 2,
        ALERT = 3,
        DIALOG = 4,
        GIVE_NOTECARD = 5,
        CHAT_MESSAGE = 6,
    }
}
