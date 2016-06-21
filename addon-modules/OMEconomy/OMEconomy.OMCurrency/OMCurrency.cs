/*
 * Copyright (c) Contributors, OpenCurrency Team
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
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
 *
 * Major changes.
 *   Michael E. Steurer, 2011
 *   Institute for Information Systems and Computer Media
 *   Graz University of Technology
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Xml;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using LitJson;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OMEconomy.OMBase;

[assembly: Addin("OMCurrencyModule", OMEconomy.OMBase.OMBaseModule.MODULE_VERSION)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]

namespace OMEconomy.OMCurrency
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "OMCurrencyModule")]

    public class OMCurrencyModule : IMoneyModule, ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string MODULE_VERSION = OMBase.OMBaseModule.MODULE_VERSION;
        private string MODULE_NAME = "OMCURRENCY";

        Boolean Enabled = false;

        CommunicationHelpers m_communication = null;
        public SceneHandler m_sceneHandler = SceneHandler.getInstance();
        public Dictionary<UUID, int> m_KnownClientFunds = new Dictionary<UUID, int>();
        public OMBaseModule omBase = new OMBaseModule();


        public event ObjectPaid OnObjectPaid;

        #region ISharedRegion implementation
        public string Name { get { return "OMCURRENCY"; } }

        public void Initialise(IConfigSource config)
        {
            IConfig cfg = config.Configs["OpenMetaverseEconomy"];
            if (cfg == null)
                return;

            Enabled = cfg.GetBoolean("enabled", Enabled);

            if (!Enabled)
                return;
            m_communication = new CommunicationHelpers(config, MODULE_NAME, OMBase.OMBaseModule.MODULE_VERSION);
            MainServer.Instance.AddXmlRPCHandler("OMCurrencyNotification", currencyNotify, false);

            MainServer.Instance.AddXmlRPCHandler("getCurrencyQuote", getCurrencyQuote, false);
            MainServer.Instance.AddXmlRPCHandler("buyCurrency", buyCurrency, false);

            MainServer.Instance.AddXmlRPCHandler("preflightBuyLandPrep", preBuyLand);
            MainServer.Instance.AddXmlRPCHandler("buyLandPrep", buyLand);
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            scene.RegisterModuleInterface<IMoneyModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnValidateLandBuy += OnValidateLandBuy;
            scene.EventManager.OnLandBuy += OnLandBuy;
            m_communication.RegisterService(MODULE_NAME, OMBase.OMBaseModule.MODULE_VERSION, scene.RegionInfo.RegionID);
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RemoveRegion(Scene scene)
        {
            if (!Enabled)
                return;
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClientClosed -= OnClientClosed;
            scene.EventManager.OnValidateLandBuy -= OnValidateLandBuy;
            scene.EventManager.OnLandBuy -= OnLandBuy;

            scene.UnregisterModuleInterface<IMoneyModule>(this);
        }

        public void Close() { }
        #endregion

        #region // Not implemented

        public bool GroupCreationCovered(IClientAPI client) { return true; }
        public bool AmountCovered(UUID agentID, int amount) { return true; }
        public bool UploadCovered(UUID agentID, int amount) { return true; }
        public int UploadCharge { get { return 13; } }
        public int GroupCreationCharge { get { return 12; } }
        public void ApplyUploadCharge(UUID agentID, int second, string third) { }
        public void ApplyGroupCreationCharge(UUID agentID) { }

        public void MoveMoney(UUID fromAgentID, UUID toAgentID, int amount, string text)
        {
        }


        //prior 0.7.6
        //public void ApplyCharge(UUID agentID, int amount, string text) { }

        //new since 0.7.6
        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type) { }
        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type, string extraData) { }


        public void updateClientFunds(UUID clientUUID)
        {
        }

        #endregion

        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount, UUID txn, out string reason)
        {
            try
            {
                SceneObjectPart part = m_sceneHandler.FindPrim(objectID);
                if (part == null)
                {
                    throw new Exception("Could not find prim " + objectID);
                }

                Dictionary<string, string> additionalParameters = new Dictionary<string, string>();
                additionalParameters.Add("primUUID", part.UUID.ToString());
                additionalParameters.Add("primName", part.Name);
                additionalParameters.Add("primDescription", part.Description);

                additionalParameters.Add("primLocation", m_sceneHandler.GetObjectLocation(part));
                additionalParameters.Add("parentUUID", part.OwnerID.ToString());

                DoMoneyTransfer(fromID, toID, amount, (int)TransactionType.OBJECT_PAYS, additionalParameters);
                reason = String.Empty;
                return true;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: ObjectGiveMoney Exception: {1} - {2}", Name, e.Message, e.StackTrace);
                reason = e.Message;
                return false;
            }
        }


        public int GetBalance(UUID clientUUID)
        {
            lock (m_KnownClientFunds)
            {
                return m_KnownClientFunds.ContainsKey(clientUUID) ? m_KnownClientFunds[clientUUID] : 0;
            }
        }

        #region // Events
        private void OnNewClient(IClientAPI client)
        {
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnMoneyTransferRequest += OnMoneyTransferRequest;
            client.OnRequestPayPrice += requestPayPrice;
            client.OnObjectBuy += ObjectBuy;
            client.OnLogout += OnLogout;
            client.OnScriptAnswer += OnScriptAnswer;
        }

        private void OnClientClosed(UUID agentUUID, Scene scene)
        {
            lock (m_KnownClientFunds)
            {
                if (m_KnownClientFunds.ContainsKey(agentUUID))
                {
                    m_KnownClientFunds.Remove(agentUUID);
                }
            }

            IClientAPI client = m_sceneHandler.LocateClientObject(agentUUID);

            if (client != null)
            {
                client.OnMoneyBalanceRequest -= SendMoneyBalance;
                client.OnRequestPayPrice -= requestPayPrice;
                client.OnScriptAnswer -= OnScriptAnswer;
                client.OnObjectBuy -= ObjectBuy;
                client.OnMoneyTransferRequest -= OnMoneyTransferRequest;
            }
        }

        private void OnLogout(IClientAPI client)
        {
            OnClientClosed(client.AgentId, null);
        }

        private void OnScriptAnswer(IClientAPI remoteClient, UUID objectID, UUID itemID, int answer)
        {
            try
            {
                SceneObjectPart part = m_sceneHandler.FindPrim(objectID);
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("primUUID", part.UUID.ToString());
                parameters.Add("primName", part.Name);
                parameters.Add("primDescription", part.Description);

                parameters.Add("primLocation", m_sceneHandler.GetObjectLocation(part));
                parameters.Add("parentUUID", part.OwnerID.ToString());
                parameters.Add("regionUUID", part.RegionID.ToString());
                //parameters.Add("m_gridURL", m_gridURL);

                if ((answer & 0x2) == 2)
                {
                    parameters.Add("method", "allowPrimDebit");
                }
                else
                {
                    parameters.Add("method", "removePrimDebit");
                }

                TaskInventoryItem item = null;
                part.TaskInventory.TryGetValue(itemID, out item);

                Dictionary<string, string[]> inventoryItems = new Dictionary<string, string[]>();
                inventoryItems.Add(item.ItemID.ToString(), new string[] { answer.ToString(), item.Name });
                parameters.Add("inventoryItems", JsonMapper.ToJson(inventoryItems));

                m_communication.DoRequestDictionary(parameters);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: ScriptChangedEvent Exception: {1} - {2}", Name, e.Message, e.StackTrace);
            }
        }

        private void OnMoneyTransferRequest(UUID sourceID, UUID destID, int amount, int transactionType, string description)
        {
            IClientAPI sender = m_sceneHandler.LocateClientObject(sourceID);
            if (sender == null)
            {
                m_log.ErrorFormat("[{0}]: MoneyTransferRequest(): Could not find Avatar {1}:({2})",
                    Name, sourceID.ToString(), sender.Name);

                return;
            }

            switch (transactionType)
            {
                case (int)TransactionType.PAY_OBJECT:
                    SceneObjectPart part = m_sceneHandler.FindPrim(destID);
                    if (part == null)
                    {
                        return;
                    }

                    string name = m_sceneHandler.ResolveAgentName(part.OwnerID);
                    if (String.IsNullOrEmpty(name))
                    {
                        name = m_sceneHandler.ResolveGroupName(part.OwnerID);
                    }

                    Dictionary<string, string> additionalParameters = new Dictionary<string, string>();
                    additionalParameters.Add("primUUID", part.UUID.ToString());
                    additionalParameters.Add("primName", part.Name);
                    additionalParameters.Add("primDescription", part.Description);
                    additionalParameters.Add("primLocation", m_sceneHandler.GetObjectLocation(part));

                    DoMoneyTransfer(sourceID, part.OwnerID, amount, transactionType, additionalParameters);
                    break;

                case (int)TransactionType.GIFT:
                    DoMoneyTransfer(sourceID, destID, amount, transactionType);
                    break;

                default:
                    m_log.ErrorFormat("[{0}]: TransactionType {1}  not specified.", Name, transactionType);
                    break;
            }
        }

        private void OnValidateLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            e.economyValidated = false;
        }

        private void OnLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            Scene s = m_sceneHandler.LocateSceneClientIn(e.agentId);
            if (e.economyValidated == false)
            {
                if (e.parcelPrice == 0)
                {
                    e.economyValidated = true;
                    s.EventManager.TriggerLandBuy(osender, e);
                }
                else
                {
                    ILandObject parcel = s.LandChannel.GetLandObject(e.parcelLocalID);

                    Dictionary<string, string> additionalParameters = new Dictionary<string, string>();
                    additionalParameters.Add("final", e.final == true ? "1" : "0");
                    additionalParameters.Add("removeContribution", e.removeContribution == true ? "1" : "0");
                    additionalParameters.Add("parcelLocalID", e.parcelLocalID.ToString());
                    additionalParameters.Add("parcelName", parcel.LandData.Name);
                    additionalParameters.Add("transactionID", e.transactionID.ToString());
                    additionalParameters.Add("amountDebited", e.amountDebited.ToString());
                    additionalParameters.Add("authenticated", e.authenticated == true ? "1" : "0");

                    DoMoneyTransfer(e.agentId, e.parcelOwnerID, e.parcelPrice, (int)TransactionType.BUY_LAND, additionalParameters);
                }
            }
        }

        #endregion Event Handler

        public void SendMoneyBalance(IClientAPI client, UUID agentID, UUID SessionID, UUID TransactionID)
        {
            if (client.AgentId == agentID && client.SessionId == SessionID)
            {
                int returnfunds = GetBalance(agentID);


                //new since 0.7.6
                client.SendMoneyBalance(
                TransactionID, true, new byte[0], returnfunds, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);

                //prior 0.7.6
                //client.SendMoneyBalance(TransactionID, true, new byte[0], returnfunds);

            }
        }

        public void DoMoneyTransfer(UUID sourceId, UUID destId, int amount, int transactiontype)
        {
            DoMoneyTransfer(sourceId, destId, amount, transactiontype, null);
        }

        public void DoMoneyTransfer(UUID sourceId, UUID destId, int amount,
            int transactiontype, Dictionary<string, string> additionalParameters)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                IClientAPI recipient = m_sceneHandler.LocateClientObject(destId);
                string recipientName = recipient == null ? destId.ToString() : recipient.FirstName + " " + recipient.LastName;

                IClientAPI sender = m_sceneHandler.LocateClientObject(sourceId);
                string senderName = sender == null ? sourceId.ToString() : sender.FirstName + " " + sender.LastName;

                Dictionary<string, string> d = new Dictionary<string, string>();
                d.Add("method", "transferMoney");
                d.Add("senderUUID", sourceId.ToString());
                d.Add("senderName", senderName);
                d.Add("recipientUUID", destId.ToString());
                d.Add("recipientName", recipientName);
                d.Add("amount", amount.ToString());
                d.Add("transactionType", transactiontype.ToString());
                if (transactiontype == (int)TransactionType.OBJECT_PAYS)
                {
                    d.Add("regionUUID", m_sceneHandler.LocateSceneClientIn(destId).RegionInfo.RegionID.ToString());
                }
                else
                {
                    d.Add("regionUUID", m_sceneHandler.LocateSceneClientIn(sourceId).RegionInfo.RegionID.ToString());
                }
                //d.Add("m_gridURL", m_gridURL);

                if (additionalParameters != null)
                {
                    foreach (KeyValuePair<string, string> pair in additionalParameters)
                    {
                        d.Add(pair.Key, pair.Value);
                    }
                }

                if (m_communication.DoRequestDictionary(d) == null)
                {
                    serviceNotAvailable(sourceId);
                }
            }, null);
        }

        #region Local Fund Management

        private void SetBalance(UUID AgentID, Int32 balance)
        {
            lock (m_KnownClientFunds)
            {
                if (m_KnownClientFunds.ContainsKey(AgentID))
                {
                    m_KnownClientFunds[AgentID] = balance;
                }
                else
                {
                    m_KnownClientFunds.Add(AgentID, balance);
                }
            }
        }

        #endregion Local Fund Management

        public void requestPayPrice(IClientAPI client, UUID objectID)
        {
            SceneObjectPart prim = m_sceneHandler.FindPrim(objectID);
            if (prim != null)
            {
                SceneObjectPart root = prim.ParentGroup.RootPart;
                client.SendPayPrice(objectID, root.PayPrice);
            }
        }

        public void ObjectBuy(IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID groupID, UUID categoryID, uint localID, byte saleType, int salePrice)
        {
            m_log.DebugFormat("User {0} buys object {1} for {2} OMC", agentID, localID, salePrice);
            Scene s = m_sceneHandler.LocateSceneClientIn(remoteClient.AgentId);
            SceneObjectPart part = s.GetSceneObjectPart(localID);
            if (part == null)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. The object can not be found.", false);
                return;
            }

            if (salePrice == 0)
            {
                m_log.Debug ("Sale Price is 0");
                IBuySellModule buyModule = s.RequestModuleInterface<IBuySellModule>();
                if (buyModule != null)
                {
                    m_log.Debug ("Call BuyObject if sale price is 0");
                    buyModule.BuyObject(remoteClient, categoryID, localID, saleType, salePrice);
                }
                else
                {
                    throw new Exception("Could not find IBuySellModule");
                }
            }
            else
            {
                Dictionary<string, string> buyObject = new Dictionary<string, string>();
                buyObject.Add("categoryID", categoryID.ToString());
                buyObject.Add("localID", Convert.ToString(localID));
                buyObject.Add("saleType", saleType.ToString());
                buyObject.Add("objectUUID", part.UUID.ToString());
                buyObject.Add("objectName", part.Name);
                buyObject.Add("objectDescription", part.Description);
                buyObject.Add("objectLocation", m_sceneHandler.GetObjectLocation(part));

                DoMoneyTransfer(remoteClient.AgentId, part.OwnerID, salePrice, (int)TransactionType.BUY_OBJECT, buyObject);
            }
        }

        private void serviceNotAvailable(UUID avatarUUID)
        {
            string message = "The currency service is not available. Please try again later.";
            m_sceneHandler.LocateClientObject(avatarUUID).SendBlueBoxMessage(UUID.Zero, String.Empty, message);
        }


        #region XML NOTIFICATIONS

        public XmlRpcResponse currencyNotify(XmlRpcRequest request, IPEndPoint ep)
        {

            XmlRpcResponse r = new XmlRpcResponse ();
            Hashtable requestData = m_communication.ValidateRequest(request);
            if(requestData != null) {
                string method = (string)requestData["method"];
                switch (method)
                {
                    case "notifyDeliverObject": r.Value = deliverObject(requestData);
                        break;
                    case "notifyOnObjectPaid": r.Value = onObjectPaid(requestData);
                        break;
                    case "notifyLandBuy": r.Value = landBuy(requestData);
                        break;
                    case "notifyChangePrimPermission": r.Value = changePrimPermissions(requestData);
                        break;
                    case "notifyBalanceUpdate": r.Value = balanceUpdate(requestData);
                        break;
                    case "notifyGetVersion": r.Value = GetVersion(requestData);
                        break;
                    default: m_log.ErrorFormat("[{0}]: Method {1} is not supported", Name, method);
                        break;
                }
            } else {
                r.SetFault(-1, "Could not validate the request");
            }
            return r;
        }


        //   12 => {8, 4}     25 => {16, 8, 1}    127 => {64, 32, 16, 8, 4, 2, 1}
        private List<int> sliceBits(int permissions)
        {
            string binaryString = Convert.ToString(permissions, 2);
            List<int> returnValue = new List<int>();
            for (int i = binaryString.Length; i > 0; i--)
            {
                if (binaryString[i - 1] == '1')
                {
                    returnValue.Add((int)Math.Pow(2, binaryString.Length - i));
                }
            }
            return returnValue;
        }

        private Hashtable changePrimPermissions(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();

            try
            {
                UUID primUUID = UUID.Parse((string)requestData["primUUID"]);
                SceneObjectPart part = m_sceneHandler.FindPrim(primUUID);
                if (part == null)
                {
                    throw new Exception("Could not find the requested prim");
                }

                string inventoryItemsString = (string)requestData["inventoryItems"];
                Dictionary<string, string> inventoryItems = JsonMapper.ToObject<Dictionary<string, string>>(inventoryItemsString);
                foreach (KeyValuePair<string, string> inventoryItem in inventoryItems)
                {
                    if (inventoryItem.Value == "0")
                    {
                        part.RemoveScriptEvents(UUID.Parse(inventoryItem.Key));
                    }
                    else
                    {
                        part.SetScriptEvents(UUID.Parse(inventoryItem.Key), Convert.ToInt32(inventoryItem.Value));
                    }
                }

                rparms["success"] = true;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: changePrimPermissions() Exception: {1} - {2}", Name, e.Message, e.StackTrace);
                rparms["success"] = false;
            }
            return rparms;
        }

        private Hashtable balanceUpdate(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            try
            {
                UUID avatarUUID = UUID.Parse((string)requestData["avatarUUID"]);
                Int32 balance = Int32.Parse((string)requestData["balance"]);

                IClientAPI client = m_sceneHandler.LocateClientObject(avatarUUID);
                if (client == null)
                {
                    throw new Exception("Avatar " + avatarUUID.ToString() + " does not reside in this region");
                }

                SetBalance(client.AgentId, balance);
                SendMoneyBalance(client, client.AgentId, client.SessionId, UUID.Zero);
                rparms["success"] = true;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: balanceUpdate() Exception: {1} - {2}", Name, e.Message, e.StackTrace);
                rparms["success"] = false;
            }
            return rparms;
        }

        private Hashtable landBuy(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            rparms["success"] = false;
            try
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                d.Add("method", "buyLand");
                d.Add("id", (string)requestData["id"]);
                Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
                if (response != null) 
                {
                    UUID agentID = UUID.Parse((string)response["senderUUID"]);
                    int parcelLocalID = int.Parse((string)response["parcelLocalID"]);
                    int transactionID = int.Parse((string)response["transactionID"]);
                    int amountDebited = int.Parse((string)response["amountDebited"]);
                    bool final = (string)response["final"] == "1" ? true : false;
                    bool authenticated = (string)response["authenticated"] == "1" ? true : false;
                    bool removeContribution = (string)response["removeContribution"] == "1" ? true : false;

                    UUID regionUUID = UUID.Parse(response["regionUUID"]);
                    Scene s = m_sceneHandler.GetSceneByUUID(regionUUID);
                    ILandObject parcel = s.LandChannel.GetLandObject(parcelLocalID);

                    UUID groupID = parcel.LandData.GroupID;
                    int parcelArea = parcel.LandData.Area;
                    int parcelPrice = parcel.LandData.SalePrice;
                    bool groupOwned = parcel.LandData.IsGroupOwned;
                    UUID parcelOwnerUUID = parcel.LandData.OwnerID;

                    EventManager.LandBuyArgs landbuyArguments =
                        new EventManager.LandBuyArgs(agentID, groupID, final, groupOwned, removeContribution,
                                                     parcelLocalID, parcelArea, parcelPrice, authenticated);

                    IClientAPI sender = m_sceneHandler.LocateClientObject(agentID);
                    if (sender != null)
                    {
                        landbuyArguments.amountDebited = amountDebited;
                        landbuyArguments.parcelOwnerID = parcelOwnerUUID;
                        landbuyArguments.transactionID = transactionID;

                        s.EventManager.TriggerValidateLandBuy(sender, landbuyArguments);
                        landbuyArguments.economyValidated = true;

                        s.EventManager.TriggerLandBuy(sender, landbuyArguments);

                        rparms["success"] = true;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: landBuy(...) Exception: {1} - {2}", Name, e.Message, e.StackTrace);
            }

            return rparms;
        }


        private Hashtable deliverObject(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            rparms["success"] = false;
            try
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                d.Add("method", "deliverObject");
                d.Add("id", (string)requestData["id"]);

                Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
                if (response != null && response["success"] == "TRUE" || response["success"] == "1")
                {
                    UInt32 localID = UInt32.Parse(response["localID"]);
                    UUID receiverUUID = UUID.Parse(response["receiverUUID"]);
                    UUID categoryID = UUID.Parse(response["categoryID"]);
                    byte saleType = byte.Parse(response["saleType"]);
                    int salePrice = response.ContainsKey("salePrice") ? Int32.Parse(response["salePrice"]) : 0;

                    IClientAPI sender = m_sceneHandler.LocateClientObject(receiverUUID);
                    if (sender == null)
                    {
                        throw new Exception("Avatar " + receiverUUID.ToString() + " does not reside in this region");
                    }

                    Scene s = m_sceneHandler.LocateSceneClientIn(receiverUUID);
                    if (s == null)
                    {
                        throw new Exception("Could not find the receiver's current scene");
                    }

                    IBuySellModule buyModule = s.RequestModuleInterface<IBuySellModule>();
                    if (buyModule != null)
                    {
                        m_log.Debug("Call BuyObject from delicerObject");
                        buyModule.BuyObject(sender, categoryID, localID, saleType, salePrice);
                    }
                    else
                    {
                        throw new Exception("Could not find IBuySellModule");
                    }
                    rparms["success"] = true;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: deliverObject() Exception: {1} - {2}", Name, e.Message, e.StackTrace);
            }
            return rparms;
        }

        private Hashtable GetVersion(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            rparms["version"] = MODULE_VERSION;
            return rparms;
        }

        private Hashtable onObjectPaid(Hashtable requestData)
        {
            Hashtable rparms = new Hashtable();
            rparms["success"] = false;
            try
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                d.Add("method", "objectPaid");
                d.Add("id", (string)requestData["id"]);

                Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
                if (response != null) 
                {
                    UUID primUUID = UUID.Parse(response["primUUID"]);
                    UUID senderUUID = UUID.Parse(response["senderUUID"]);
                    Int32 amount = Int32.Parse(response["amount"]);

                    IClientAPI depositor = m_sceneHandler.LocateClientObject(senderUUID);
                    if (depositor == null)
                    {
                        throw new Exception("Avatar " + senderUUID.ToString() + " does not reside in this Region");
                    }


                    ObjectPaid HandlerOnObjectPaid = OnObjectPaid;
                    if (HandlerOnObjectPaid != null) 
                    {
                        m_log.Debug("Trigger Object Payed");
                        HandlerOnObjectPaid(primUUID, senderUUID, amount);
                    }
                    else
                    {
                        m_log.Debug("No Trigger Object Payed");
                    }

                    rparms["success"] = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error("[OMCURRENCY]: onObjectPaid() " + e.Message);
            }
            return rparms;
        }

        public XmlRpcResponse buyCurrency(XmlRpcRequest request, IPEndPoint ep)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable returnresp = new Hashtable();

            try
            {
                UUID avatarUUID = UUID.Parse((string)requestData["agentId"]);
                int amount = (Int32)requestData["currencyBuy"];

                Dictionary<string, string> d = new Dictionary<string, string>();
                d.Add("method", "buyCurrency");
                d.Add("avatarUUID", avatarUUID.ToString());
                d.Add("amount", amount.ToString());

                Dictionary<string, string> response = m_communication.DoRequestDictionary(d);

                if (response != null && response["success"] == "TRUE" || response["success"] == "1")
                {
                    returnresp.Add("success", true);
                }
                else
                {
                    throw new Exception();
                }

            }
            catch (Exception)
            {
                returnresp.Add("success", false);
                returnresp.Add("errorMessage", "Please visit virwox.com to transfer money");
                returnresp.Add("errorURI", "http://www.virwox.com");
            }

            XmlRpcResponse returnval = new XmlRpcResponse();
            returnval.Value = returnresp;
            return returnval;
        }


        public XmlRpcResponse getCurrencyQuote(XmlRpcRequest request, IPEndPoint ep)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            UUID agentId = UUID.Zero;
            int amount = 0;
            Hashtable quoteResponse = new Hashtable();

            try
            {
                UUID.TryParse((string)requestData["agentId"], out agentId);
                amount = (Int32)requestData["currencyBuy"];

                int realAmount = amount / getExchangeRate() + 1;
                amount = realAmount * getExchangeRate();

                Hashtable currencyResponse = new Hashtable();
                currencyResponse.Add("estimatedCost", realAmount * 100);
                currencyResponse.Add("currencyBuy", amount);

                quoteResponse.Add("success", true);
                quoteResponse.Add("currency", currencyResponse);
                quoteResponse.Add("confirm", "");

            }
            catch (Exception)
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                d.Add("method", "getString");
                d.Add("type", "chargeAccount");
                d.Add("avatarUUID", (string)requestData["agentId"]);

                Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
                if (response != null) 
                {
                    quoteResponse.Add ("success", false);
                    quoteResponse.Add ("errorMessage", response ["errorMessage"]);
                    quoteResponse.Add ("errorURI", response ["errorURI"]);
                } 
                else 
                {
                    quoteResponse.Add ("success", false);
                }
            }

            XmlRpcResponse returnval = new XmlRpcResponse();
            returnval.Value = quoteResponse;
            return returnval;
        }

        public XmlRpcResponse preBuyLand(XmlRpcRequest request, IPEndPoint ep)
        {
            m_log.Error("preBuyLand(XmlRpcRequest request, IPEndPoint ep)");
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            Hashtable membershiplevels = new Hashtable();
            ArrayList levels = new ArrayList();
            Hashtable level = new Hashtable();
            level.Add("id", "00000000-0000-0000-0000-000000000000");
            level.Add("description", "");
            levels.Add(level);

            Hashtable landuse = new Hashtable();
            landuse.Add("upgrade", true);
            landuse.Add("action", "");

            Hashtable currency = new Hashtable();
            currency.Add("estimatedCost", 0);

            Hashtable membership = new Hashtable();
            membershiplevels.Add("upgrade", true);
            membershiplevels.Add("action", "");
            membershiplevels.Add("levels", membershiplevels);

            retparam.Add("success", true); // Cannot buy now - overall message.
            retparam.Add("currency", currency);
            retparam.Add("membership", membership);
            retparam.Add("landuse", landuse);
            retparam.Add("confirm", "");

            ret.Value = retparam;
            return ret;
        }

        public XmlRpcResponse buyLand(XmlRpcRequest request, IPEndPoint ep)
        {
            m_log.Error("buyLand(XmlRpcRequest request, IPEndPoint ep)");
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            retparam.Add("success", true);
            ret.Value = retparam;
            return ret;
        }

        private int getExchangeRate()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            d.Add("method", "getExchangeRate");
            Dictionary<string, string> response = m_communication.DoRequestDictionary(d);
            if (response != null)
            {
                return int.Parse((string)response["currentExchangeRate"]);
            }
            else
            {
                m_log.Error("buyLand(XmlRpcRequest request, IPEndPoint ep)");
            }
            return 0;
        }

        #endregion XML NOTIFICATIONS
    }

    public enum TransactionType : int
    {
        BUY_OBJECT = 5000,
        GIFT = 5001,
        PAY_OBJECT = 5008,
        OBJECT_PAYS = 5009,
        BUY_LAND = 5013,
    }
}
