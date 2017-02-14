﻿using CommonNote.PluginInterface;
using MSHC.Math.Encryption;
using MSHC.Util.Helper;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Xml.Linq;

namespace CommonNote.Settings
{
	class SettingAttribute : Attribute
	{
		public bool Encrypted { get; set; }

		public SettingAttribute()
		{
			Encrypted = false;
		}

		public void Serialize(SettingType ptype, PropertyInfo prop, AppSettings obj, XElement xroot)
		{
			string data;

			switch (ptype)
			{
				case SettingType.Integer:
					data = Convert.ToString((int)prop.GetValue(obj));
					break;
				case SettingType.NullableInteger:
					var nint = (int?) prop.GetValue(obj);
					data = nint == null ? string.Empty : nint.ToString();
					break;
				case SettingType.Boolean:
					data = Convert.ToString((bool)prop.GetValue(obj));
					break;
				case SettingType.Guid:
					data = ((Guid)prop.GetValue(obj)).ToString("B");
					break;
				case SettingType.EncryptedString:
					data = Encrypt((string)prop.GetValue(obj));
					break;
				case SettingType.String:
					data = (string)prop.GetValue(obj);
					break;
				case SettingType.Enum:
					data = Convert.ToString(prop.GetValue(obj));
					break;
				case SettingType.FontFamily:
					data = ((FontFamily)prop.GetValue(obj)).Source;
					break;
				case SettingType.RemoteProvider:
					data = ((IRemoteProvider)prop.GetValue(obj)).GetUniqueID().ToString("B");
					break;
				default:
					throw new ArgumentOutOfRangeException("ptype", ptype, null);
			}

			var x = new XElement(prop.Name, data, new XAttribute("type", ptype));

			xroot.Add(x);
		}

		public void Deserialize(SettingType ptype, PropertyInfo prop, AppSettings data, XElement xroot)
		{
			switch (ptype)
			{
				case SettingType.Integer:
					prop.SetValue(data, XHelper.GetChildValue(xroot, prop.Name, (int)prop.GetValue(data)));
					return;
				case SettingType.NullableInteger:
					prop.SetValue(data, XHelper.GetChildValue(xroot, prop.Name, (int?)prop.GetValue(data)));
					return;
				case SettingType.Boolean:
					prop.SetValue(data, XHelper.GetChildValue(xroot, prop.Name, (bool)prop.GetValue(data)));
					return;
				case SettingType.Guid:
					prop.SetValue(data, XHelper.GetChildValue(xroot, prop.Name, (Guid)prop.GetValue(data)));
					return;
				case SettingType.EncryptedString:
					prop.SetValue(data, Decrypt(XHelper.GetChildValue(xroot, prop.Name, Encrypt((string)prop.GetValue(data)))));
					return;
				case SettingType.String:
					prop.SetValue(data, XHelper.GetChildValue(xroot, prop.Name, (string)prop.GetValue(data)));
					return;
				case SettingType.Enum:
					prop.SetValue(data, XHelper.GetChildValue(xroot, prop.Name, prop.GetValue(data), prop.PropertyType));
					break;
				case SettingType.FontFamily:
					prop.SetValue(data, GetFontByNameOrDefault(XHelper.GetChildValue(xroot, prop.Name, ((FontFamily)prop.GetValue(data)).Source), (FontFamily)prop.GetValue(data)));
					break;
				case SettingType.RemoteProvider:
					prop.SetValue(data, PluginManager.GetPlugin(XHelper.GetChildValue(xroot, prop.Name, PluginManager.GetDefaultPlugin().GetUniqueID())));
					break;
				default:
					throw new ArgumentOutOfRangeException("ptype", ptype, null);
			}
		}

		public bool TestEquality(SettingType ptype, PropertyInfo prop, AppSettings da, AppSettings db)
		{
			var va = prop.GetValue(da);
			var vb = prop.GetValue(db);

			switch (ptype)
			{
				case SettingType.Integer:
					return (int)va == (int)vb;
				case SettingType.NullableInteger:
					return (int?)va == (int?)vb;
				case SettingType.Boolean:
					return (bool)va == (bool)vb;
				case SettingType.Guid:
					return (Guid)va == (Guid)vb;
				case SettingType.EncryptedString:
					return (string)va == (string)vb;
				case SettingType.String:
					return (string)va == (string)vb;
				case SettingType.Enum:
					return (int)va == (int)vb;
				case SettingType.FontFamily:
					return ((FontFamily)va).Source == ((FontFamily)vb).Source;
				case SettingType.RemoteProvider:
					return ((IRemoteProvider)va).GetUniqueID() == ((IRemoteProvider)vb).GetUniqueID();
				default:
					throw new ArgumentOutOfRangeException("ptype", ptype, null);
			}
		}

		private static FontFamily GetFontByNameOrDefault(string name, FontFamily defaultFamily)
		{
			return Fonts.SystemFontFamilies.FirstOrDefault(p => p.Source == name) ?? defaultFamily;
		}

		private static string Encrypt(string data)
		{
			if (string.IsNullOrWhiteSpace(data)) return string.Empty;

			return Convert.ToBase64String(AESThenHMAC.SimpleEncryptWithPassword(Encoding.UTF32.GetBytes(data), AppSettings.ENCRYPTION_KEY));
		}

		private static string Decrypt(string data)
		{
			if (string.IsNullOrWhiteSpace(data)) return string.Empty;

			return Encoding.UTF32.GetString(AESThenHMAC.SimpleDecryptWithPassword(Convert.FromBase64String(data), AppSettings.ENCRYPTION_KEY));
		}
	}
}
