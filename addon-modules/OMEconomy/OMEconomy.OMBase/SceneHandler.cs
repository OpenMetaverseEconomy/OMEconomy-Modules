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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OMEconomy.OMBase
{
    public class SceneHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<ulong, Scene> m_scene = new Dictionary<ulong, Scene>();

        private static volatile SceneHandler instance = null;

        public static SceneHandler Instance
        {
            get
            {
                lock (m_lock)
                {
                    instance = instance == null ? new SceneHandler() : instance;
                    return instance;
                }
            }

        }

        private static object m_lock = new object();

        public List<UUID> GetUniqueRegions()
        {
            List<UUID> uniqueRegions = new List<UUID>();
            lock (m_scene)
            {
                foreach (Scene rs in m_scene.Values)
                {
                    if (!uniqueRegions.Contains(rs.RegionInfo.originRegionID))
                    {
                        uniqueRegions.Add(rs.RegionInfo.originRegionID);
                    }
                }
            }
            return uniqueRegions;
        }

        public void AddScene(Scene scene)
        {
            if (m_scene.ContainsKey(scene.RegionInfo.RegionHandle))
            {
                m_scene[scene.RegionInfo.RegionHandle] = scene;
            }
            else
            {
                m_scene.Add(scene.RegionInfo.RegionHandle, scene);
            }
        }

        /*
                public List<UUID> GetOnlineAvatars()
                {
                    List<UUID> onlineAvatars = new List<UUID>();
                    lock (m_scene)
                    {
                        foreach (Scene s in m_scene.Values)
                        {
                            s.ForEachScenePresence(delegate(ScenePresence sp)
                            {
                                if (!onlineAvatars.Contains(sp.UUID))
                                {
                                    onlineAvatars.Add(sp.UUID);
                                }
                            });
                        }
                    }
                    return onlineAvatars;
                }
        */

        public Scene GetSceneByUUID(UUID regionID)
        {
            lock (m_scene)
            {
                foreach (Scene rs in m_scene.Values)
                {
                    if (rs.RegionInfo.originRegionID == regionID)
                    {
                        return rs;
                    }
                }
            }
            return null;
        }

        public IClientAPI LocateClientObject(UUID agentID)
        {
            lock (m_scene)
            {
                foreach (Scene _scene in m_scene.Values)
                {
                    ScenePresence tPresence = _scene.GetScenePresence(agentID);
                    if (tPresence != null && !tPresence.IsChildAgent && tPresence.ControllingClient != null)
                    {
                        return tPresence.ControllingClient;
                    }
                }
            }
            return null;
        }

        public SceneObjectPart FindPrim(UUID primID)
        {
            lock (m_scene)
            {
                foreach (Scene s in m_scene.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(primID);
                    if (part != null)
                    {
                        return part;
                    }
                }
            }
            return null;
        }

        public Scene LocateSceneClientIn(UUID agentID)
        {
            lock (m_scene)
            {
                foreach (Scene _scene in m_scene.Values)
                {
                    ScenePresence tPresence = _scene.GetScenePresence(agentID);
                    if (tPresence != null && !tPresence.IsChildAgent)
                    {
                        return _scene;
                    }
                }
            }
            return null;
        }

        public Scene GetRandomScene()
        {
            lock (m_scene)
            {
                foreach (Scene rs in m_scene.Values)
                    return rs;
            }
            return null;
        }

        public string ResolveAgentName(UUID agentID)
        {
            Scene scene = GetRandomScene();
            string agentName = String.Empty;
            IUserManagement userManager = scene.RequestModuleInterface<IUserManagement>();
            if (userManager != null)
            {
                agentName = userManager.GetUserName(agentID);
                agentName = agentName == "(hippos)" ? String.Empty : agentName;
            }
            return agentName;
        }

        public string ResolveGroupName(UUID groupID)
        {
            Scene scene = GetRandomScene();
            IGroupsModule gm = scene.RequestModuleInterface<IGroupsModule>();
            try
            {
                string @group = gm.GetGroupRecord(groupID).GroupName;
                if (@group != null)
                {
                    m_log.DebugFormat("[OMBASE]: Resolved group {0} to {1}", groupID, @group);
                    return @group;
                }
            }
            catch (Exception)
            {
                m_log.ErrorFormat("[OMBASE]: Could not resolve group {0}", groupID);
            }

            return String.Empty;
        }

        public String GetObjectLocation(SceneObjectPart part)
        {
            int x = Convert.ToInt32(part.AbsolutePosition.X);
            int y = Convert.ToInt32(part.AbsolutePosition.Y);
            int z = Convert.ToInt32(part.AbsolutePosition.Z);

            return "<" + x + "/" + y + "/" + z + ">";
        }

    }
}
