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
using System.IO;
using System.Text;
using System.Xml;
using Eddie.Core;

namespace Eddie.Platforms
{
    public class Osx : Platform
    {
		private string m_architecture = "";

		private List<DnsSwitchEntry> m_listDnsSwitch = new List<DnsSwitchEntry>();
		private List<IpV6ModeEntry> m_listIpV6Mode = new List<IpV6ModeEntry>();

        // Override
		public Osx()
		{
			m_architecture = NormalizeArchitecture(ShellPlatformIndipendent("sh", "-c 'uname -m'", "", true, false, true).Trim());
		}

		public override string GetCode()
		{
			return "MacOS";
		}

		public override string GetName()
		{
			return ShellCmd("sw_vers -productVersion");
		}

		public override string GetOsArchitecture()
		{
			return m_architecture;
		}

		public override string GetDefaultDataPath()
		{
			// Only in OSX, always save in 'home' path also with portable edition.
			return "home";
		}

        public override bool IsAdmin()
        {
			// return true; // Uncomment for debugging

			// With root privileges by RootLauncher.cs, Environment.UserName still return the normal username, 'whoami' return 'root'.
			string u = ShellCmd ("whoami").ToLowerInvariant().Trim();
			return (u == "root");
        }

		public override bool IsUnixSystem()
		{
			return true;
		}

		public override string VersionDescription()
        {
			string o = base.VersionDescription();
            o += " - " + ShellCmd("uname -a").Trim();
            return o;
        }

        public override string DirSep
        {
            get
            {
                return "/";
            }
        }

		public override string GetExecutableReport(string path)
		{
			return ShellCmd("otool -L \"" + SystemShell.EscapePath(path) + "\"");
		}

		public override string GetExecutablePathEx()
		{
			string currentPath = System.Reflection.Assembly.GetEntryAssembly().Location;
			if(new FileInfo(currentPath).Directory.Name == "MonoBundle")
			{
				// OSX Bundle detected, use the launcher executable
				currentPath = currentPath.Replace("/MonoBundle/","/MacOS/").Replace(".exe","");
			}
            else if(Process.GetCurrentProcess().ProcessName.StartsWith("mono", StringComparison.InvariantCultureIgnoreCase))
            {
                // mono <app>, Entry Assembly path it's ok
            }
            else
            {
                currentPath = Process.GetCurrentProcess().MainModule.FileName;
            }
			return currentPath;
		}

        public override string GetUserPathEx()
        {
            return Environment.GetEnvironmentVariable("HOME") + DirSep + ".airvpn";
        }

        public override string ShellCmd(string Command, bool noDebugLog)
        {
            return Shell("sh", String.Format("-c '{0}'", Command), "", true, false, noDebugLog);
        }

        public override void FlushDNS()
        {
            Engine.Instance.Logs.Log(LogType.Verbose, Messages.ConnectionFlushDNS);

            // 10.9
            ShellCmd("dscacheutil -flushcache");
			ShellCmd("killall -HUP mDNSResponder");

			// 10.10
			ShellCmd("discoveryutil udnsflushcaches");
            ShellCmd("discoveryutil mdnsflushcache"); // 2.11
        }

        public override bool SearchTool(string name, string relativePath, ref string path, ref string location)
        {
            string pathBin = "/usr/bin/" + name;
            if (Platform.Instance.FileExists(pathBin))
            {
                path = pathBin;
                location = "system";
                return true;
            }

            string pathSBin = "/usr/sbin/" + name;
            if (Platform.Instance.FileExists(pathSBin))
            {
                path = pathSBin;
                location = "system";
                return true;
            }

            // Look in application bundle resources
            string resPath = NormalizePath(relativePath) + "/../Resources/" + name;
            if (File.Exists(resPath))
            {
                path = resPath;
                location = "bundle";
                return true;
            }

            return base.SearchTool(name, relativePath, ref path, ref location);
        }

        // Encounter Mono issue about the .Net method on OS X, similar to Mono issue under Linux. Use shell instead, like Linux
        public override long Ping(string host, int timeoutSec)
        {
			// Note: Linux timeout is -w, OS X timeout is -t
			string cmd = "ping -c 1 -t " + timeoutSec + " -q -n " + SystemShell.EscapeHost(host);
            string result = ShellCmd(cmd);
            
			// Note: Linux have mdev, OS X have stddev
            string sMS = Utils.ExtractBetween(result, "min/avg/max/stddev = ", "/");
            float iMS;
			if (float.TryParse(sMS, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out iMS) == false)
				iMS = -1;

			return (long)iMS;
        }
        
        public override void EnsureExecutablePermissions(string path)
		{
			if ((path == "") || (Platform.Instance.FileExists(path) == false))
				return;

			ShellCmd("chmod +x \"" + SystemShell.EscapePath(path) + "\"");
		}

		public override string GetSystemFont()
		{
			// Crash with Xamarin 6.1.2
			return "";
		}

		public override string GetSystemFontMonospace()
		{
			// Crash with Xamarin 6.1.2
			return "";
		}

		public override string GetDriverAvailable()
		{
			return "Expected";
		}

		public override bool CanInstallDriver()
		{
			return false;
		}

		public override bool CanUnInstallDriver()
		{
			return false;
		}

		public override void InstallDriver()
		{
		}

		public override void UnInstallDriver()
		{
		}

		public override void RouteAdd(RouteEntry r)
		{
			base.RouteAdd (r);
		}

		public override void RouteRemove(RouteEntry r)
		{
			base.RouteRemove (r);
		}

        public override void ResolveWithoutAnswer(string host)
        {
            // Base method with Dns.GetHostEntry have cache issue, for example on Fedora. OS X it's based on Mono.
            if (Platform.Instance.FileExists("/usr/bin/host"))
                ShellCmd("host -W 5 -t A " + SystemShell.EscapeHost(host));
            else
                base.ResolveWithoutAnswer(host);
        }

        public override List<RouteEntry> RouteList()
		{	
			List<RouteEntry> entryList = new List<RouteEntry>();

			string result = ShellCmd("route -n -ee");

			string[] lines = result.Split('\n');
			foreach (string line in lines)
			{
				string[] fields = Utils.StringCleanSpace(line).Split(' ');

				if (fields.Length == 11)
				{
					RouteEntry e = new RouteEntry();
					e.Address = fields[0];
					e.Gateway = fields[1];
					e.Mask = fields[2];
					e.Flags = fields[3].ToUpperInvariant();
					e.Metrics = fields[4];
					// ref
					// use
					e.Interface = fields[7];
					e.Mss = fields[8];
					e.Window = fields[9];
					e.Irtt = fields[10];

					if (e.Address.Valid == false)
						continue;
					if (e.Gateway.Valid == false)
						continue;
					if (e.Mask.Valid == false)
						continue;

					entryList.Add(e);
				}
			}

			return entryList;
		}

        public override string GenerateSystemReport()
        {
            string t = base.GenerateSystemReport();

            t += "\n\n-- OS X\n";

            t += "\n-- ifconfig\n";
            t += ShellCmd("ifconfig");

            return t;
        }

        public override Dictionary<int, string> GetProcessesList()
		{
			// We experience some crash under OSX with the base method.
			
			Dictionary<int, string> result = new Dictionary<int,string>();

			String resultS = ShellCmd("top -b -n 1 | awk '{print $1,$12}'");

			string[] resultA = resultS.Split('\n');
			foreach (string pS in resultA)
			{
				int posS = pS.IndexOf(' ');
				if (posS != -1)
				{
					int pid = Conversions.ToInt32(pS.Substring(0, posS).Trim());
					string name = pS.Substring(posS).Trim().ToLowerInvariant();

					result[pid] = name;
				}
			}

			return result;
		}

		public override bool OnCheckEnvironment()
		{
			return true;
		}

		public override void OnNetworkLockManagerInit()
		{
			base.OnNetworkLockManagerInit();

			Engine.Instance.NetworkLockManager.AddPlugin(new NetworkLockOsxPf());
		}

        public override string OnNetworkLockRecommendedMode()
        {
            return "osx_pf";
        }

        public override void OnRecoveryLoad(XmlElement root)
		{
			XmlElement nodeDns = Utils.XmlGetFirstElementByTagName(root, "DnsSwitch");
			if (nodeDns != null)
			{
				foreach (XmlElement nodeEntry in nodeDns.ChildNodes)
				{
					DnsSwitchEntry entry = new DnsSwitchEntry();
					entry.ReadXML(nodeEntry);
					m_listDnsSwitch.Add(entry);
				}
			}

			XmlElement nodeIpV6 = Utils.XmlGetFirstElementByTagName(root, "IpV6");
			if (nodeIpV6 != null)
			{
				foreach (XmlElement nodeEntry in nodeIpV6.ChildNodes)
				{
					IpV6ModeEntry entry = new IpV6ModeEntry();
					entry.ReadXML(nodeEntry);
					m_listIpV6Mode.Add(entry);
				}
			}

			base.OnRecoveryLoad(root);
		}

		public override void OnRecoverySave(XmlElement root)
		{
			XmlDocument doc = root.OwnerDocument;

			if (m_listDnsSwitch.Count != 0)
			{
				XmlElement nodeDns = (XmlElement)root.AppendChild(doc.CreateElement("DnsSwitch"));
				foreach (DnsSwitchEntry entry in m_listDnsSwitch)
				{
					XmlElement nodeEntry = nodeDns.AppendChild(doc.CreateElement("entry")) as XmlElement;
					entry.WriteXML(nodeEntry);
				}
			}

			if (m_listIpV6Mode.Count != 0)
			{
				XmlElement nodeDns = (XmlElement)root.AppendChild(doc.CreateElement("IpV6"));
				foreach (IpV6ModeEntry entry in m_listIpV6Mode)
				{
					XmlElement nodeEntry = nodeDns.AppendChild(doc.CreateElement("entry")) as XmlElement;
					entry.WriteXML(nodeEntry);
				}
			}
		}

		public override bool OnIpV6Do()
		{
			if (Engine.Instance.Storage.GetLower("ipv6.mode") == "disable")
			{
				string[] interfaces = GetInterfaces();
				foreach (string i in interfaces)
				{
					string getInfo = ShellCmd("networksetup -getinfo \"" + SystemShell.EscapeInsideQuote(i) + "\"");

                    string mode = Utils.RegExMatchOne(getInfo, "^IPv6: (.*?)$");
					string address = Utils.RegExMatchOne(getInfo, "^IPv6 IP address: (.*?)$");
					
					if( (mode == "") && (address != "") )
						mode = "LinkLocal";

					if (mode != "Off")
					{
						Engine.Instance.Logs.Log(LogType.Verbose, MessagesFormatter.Format(Messages.NetworkAdapterIpV6Disabled, i));

						IpV6ModeEntry entry = new IpV6ModeEntry();
						entry.Interface = i;
						entry.Mode = mode;
						entry.Address = address;
						if (mode == "Manual")
						{
							entry.Router = Utils.RegExMatchOne(getInfo, "^IPv6 IP Router: (.*?)$");
							entry.PrefixLength = Utils.RegExMatchOne(getInfo, "^IPv6 Prefix Length: (.*?)$");
						}
						m_listIpV6Mode.Add(entry);

						ShellCmd("networksetup -setv6off \"" + SystemShell.EscapeInsideQuote(i) + "\""); 
                    }					
				}

				Recovery.Save();				
			}

			base.OnIpV6Do();

			return true;
		}

		public override bool OnIpV6Restore()
		{
			foreach (IpV6ModeEntry entry in m_listIpV6Mode)
			{                
                if (entry.Mode == "Off")
				{
					ShellCmd("networksetup -setv6off \"" + SystemShell.EscapeInsideQuote(entry.Interface) + "\"");
                }
				else if (entry.Mode == "Automatic")
				{
					ShellCmd("networksetup -setv6automatic \"" + SystemShell.EscapeInsideQuote(entry.Interface) + "\"");
                }
				else if (entry.Mode == "LinkLocal")
				{
					ShellCmd("networksetup -setv6LinkLocal \"" + SystemShell.EscapeInsideQuote(entry.Interface) + "\""); 
				}
				else if (entry.Mode == "Manual")
				{
					ShellCmd("networksetup -setv6manual \"" + SystemShell.EscapeInsideQuote(entry.Interface) + "\" " + entry.Address + " " + entry.PrefixLength + " " + entry.Router); // IJTF2 // TOCHECK
                }

				Engine.Instance.Logs.Log(LogType.Verbose, MessagesFormatter.Format(Messages.NetworkAdapterIpV6Restored, entry.Interface));
			}

			m_listIpV6Mode.Clear();
			
			Recovery.Save();

			base.OnIpV6Restore();

			return true;
		}

		public override bool OnDnsSwitchDo(string dns)
		{
			string mode = Engine.Instance.Storage.GetLower("dns.mode");

			if (mode == "auto")
			{
				string[] interfaces = GetInterfaces();
				foreach (string i in interfaces)
				{
					string i2 = i.Trim();
					
					string current = ShellCmd("networksetup -getdnsservers \"" + SystemShell.EscapeInsideQuote(i2) + "\""); 

                    // v2
                    List<string> ips = new List<string>();
                    foreach(string line in current.Split('\n'))
                    {
                        string ip = line.Trim();
                        if (IpAddress.IsIP(ip))
                            ips.Add(ip);
                    }

                    if (ips.Count != 0)
                        current = String.Join(",", ips.ToArray());
                    else
                        current = "";
                    if (current != dns)
                    {
                        // Switch
                        Engine.Instance.Logs.Log(LogType.Verbose, MessagesFormatter.Format(Messages.NetworkAdapterDnsDone, i2, ((current == "") ? "Automatic" : current), dns));

                        DnsSwitchEntry e = new DnsSwitchEntry();
                        e.Name = i2;
                        e.Dns = current;
                        m_listDnsSwitch.Add(e);

                        string dns2 = dns.Replace(",", "\" \"");
                        ShellCmd("networksetup -setdnsservers \"" + SystemShell.EscapeInsideQuote(i2) + "\" \"" + dns2 + "\""); // IJTF2 eh?
                    }                    
				}

				Recovery.Save ();
			}

			base.OnDnsSwitchDo(dns);

			return true;
		}

		public override bool OnDnsSwitchRestore()
		{
			foreach (DnsSwitchEntry e in m_listDnsSwitch)
			{
				string v = e.Dns;
                if (v == "")
                    v = "empty";
				v = v.Replace (",", "\" \"");

				Engine.Instance.Logs.Log(LogType.Verbose, MessagesFormatter.Format(Messages.NetworkAdapterDnsRestored, e.Name, ((e.Dns == "") ? "Automatic" : e.Dns)));
				ShellCmd("networksetup -setdnsservers \"" + e.Name + "\" \"" + v + "\""); // IJTF2
            }

			m_listDnsSwitch.Clear();

			Recovery.Save ();

			base.OnDnsSwitchRestore();

			return true;
		}

		public override string GetTunStatsMode()
		{
			// Mono NetworkInterface::GetIPv4Statistics().BytesReceived always return 0 under OSX.
			return "OpenVpnManagement";
		}

		public override string GetGitDeployPath()
		{
			// Under OSX, binary is inside a bundle AirVPN.app/Contents/MacOS/
			return GetApplicationPath() + "/../../../../../../../deploy/" + Platform.Instance.GetSystemCode () + "/";
		}

		public string[] GetInterfaces()
		{
			string[] interfaces = ShellCmd("networksetup -listallnetworkservices | grep -v denotes").Split('\n');
            return interfaces;
		}
    }

	public class DnsSwitchEntry
	{
		public string Name;
		public string Dns;

		public void ReadXML(XmlElement node)
		{
			Name = Utils.XmlGetAttributeString(node, "name", "");
			Dns = Utils.XmlGetAttributeString(node, "dns", "");
		}

		public void WriteXML(XmlElement node)
		{
			Utils.XmlSetAttributeString(node, "name", Name);
			Utils.XmlSetAttributeString(node, "dns", Dns);
		}
	}

	public class IpV6ModeEntry
	{
		public string Interface;
		public string Mode;
		public string Address;
		public string Router;
		public string PrefixLength;

		public void ReadXML(XmlElement node)
		{
			Interface = Utils.XmlGetAttributeString(node, "interface", "");
			Mode = Utils.XmlGetAttributeString(node, "mode", "");
			Address = Utils.XmlGetAttributeString(node, "address", "");
			Router = Utils.XmlGetAttributeString(node, "router", "");
			PrefixLength = Utils.XmlGetAttributeString(node, "prefix_length", "");
		}

		public void WriteXML(XmlElement node)
		{
			Utils.XmlSetAttributeString(node, "interface", Interface);
			Utils.XmlSetAttributeString(node, "mode", Mode);
			Utils.XmlSetAttributeString(node, "address", Address);
			Utils.XmlSetAttributeString(node, "router", Router);
			Utils.XmlSetAttributeString(node, "prefix_length", PrefixLength);
		}
	}
}

