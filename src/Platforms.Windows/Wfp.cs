﻿// <eddie_source_header>
// This file is part of Eddie/AirVPN software.
// Copyright (C)2014-2016 AirVPN (support@airvpn.org) / https://airvpn.org
//
// Eddie is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Eddie is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Eddie. If not, see <http://www.gnu.org/licenses/>.
// </eddie_source_header>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Management;
using System.Security.Principal;
using System.Xml;
using System.Text;
using System.Threading;
using Eddie.Core;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace Eddie.Platforms
{
    public class Wfp
    {
        private static Dictionary<string, WfpItem> Items = new Dictionary<string, WfpItem>();

        [DllImport("LibPocketFirewall.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void LibPocketFirewallInit(string name);

        [DllImport("LibPocketFirewall.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool LibPocketFirewallStart(string xml);

        [DllImport("LibPocketFirewall.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool LibPocketFirewallStop();

        [DllImport("LibPocketFirewall.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern UInt64 LibPocketFirewallAddRule(string xml);

        [DllImport("LibPocketFirewall.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool LibPocketFirewallRemoveRule(UInt64 id);

        [DllImport("LibPocketFirewall.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr LibPocketFirewallGetLastError();

        public static string LibPocketFirewallGetLastError2()
        {
            IntPtr result = LibPocketFirewallGetLastError();
            string s = Marshal.PtrToStringAnsi(result);
            return s;
        }
        
        public static bool RemoveItem(string code)
        {
            if (Items.ContainsKey(code) == false)
                return false;

            WfpItem item = Items[code];

            foreach (UInt64 id in item.FirewallIds)
            {
                Engine.Instance.Logs.Log(LogType.Verbose, "Clodo: WFP remove rule ID: " + id.ToString());

                bool result = LibPocketFirewallRemoveRule(id);
                if(result == false)
                    throw new Exception(Messages.Format(Messages.WfpRuleRemoveFail, LibPocketFirewallGetLastError2()));
            }

            Items.Remove(code);

            if(Items.Count == 0)
            {
                Engine.Instance.Logs.Log(LogType.Verbose, Messages.WfpStop);
                LibPocketFirewallStop();
            }

            return true;
        }

        public static WfpItem AddItem(string code, XmlElement xml)
        {
            if (Items.ContainsKey(code))
                throw new Exception("Windows WFP, unexpected: Rule '" + code + "' already exists");

            WfpItem item = new WfpItem();

            if(Items.Count == 0)
            {
                Engine.Instance.Logs.Log(LogType.Verbose, Messages.WfpStart);

                // Start firewall
                LibPocketFirewallInit(Constants.Name);

                XmlDocument xmlStart = new XmlDocument();
                XmlElement xmlInfo = xmlStart.CreateElement("firewall");
                xmlInfo.SetAttribute("description", Constants.Name);
                xmlInfo.SetAttribute("weight", "max");

                if (LibPocketFirewallStart(xmlInfo.OuterXml) == false)
                    throw new Exception(Messages.Format(Messages.WfpStartFail, LibPocketFirewallGetLastError2()));
            }

            List<string> layers = new List<string>();

            if (xml.GetAttribute("layer") == "all")
            {
                layers.Add("ale_auth_recv_accept_v4");
                layers.Add("ale_auth_recv_accept_v6");
                layers.Add("ale_auth_connect_v4");
                layers.Add("ale_auth_connect_v6");
                layers.Add("ale_flow_established_v4");
                layers.Add("ale_flow_established_v6");
            }
            else if (xml.GetAttribute("layer") == "ipv4")
            {
                layers.Add("ale_auth_recv_accept_v4");
                layers.Add("ale_auth_connect_v4");
                layers.Add("ale_flow_established_v4");
            }
            else if (xml.GetAttribute("layer") == "ipv6")
            {
                layers.Add("ale_auth_recv_accept_v6");
                layers.Add("ale_auth_connect_v6");
                layers.Add("ale_flow_established_v6");
            }
            else
                layers.Add(xml.GetAttribute("layer"));

            foreach (string layer in layers)
            {
                XmlElement xmlClone = xml.CloneNode(true) as XmlElement;
                xmlClone.SetAttribute("layer", layer);
                string xmlStr = xmlClone.OuterXml;

                Engine.Instance.Logs.Log(LogType.Verbose, "Clodo: WFP Add rule " + xmlStr);

                UInt64 id1 = LibPocketFirewallAddRule(xmlStr);

                if (id1 == 0)
                {
                    throw new Exception(Messages.Format(Messages.WfpRuleAddFail, LibPocketFirewallGetLastError2()));                    
                }
                else
                {
                    Engine.Instance.Logs.Log(LogType.Verbose, "Clodo: WFP Add rule ok, ID: " + id1.ToString() + ", " + xmlStr);
                    item.FirewallIds.Add(id1);
                }
            }

            Items[code] = item;
            return item;
        }
    }
}
