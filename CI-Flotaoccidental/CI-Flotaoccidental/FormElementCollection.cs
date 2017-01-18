using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace CI_Flotaoccidental
{
	public class FormElementCollection : Dictionary<string, string>
	{
		public FormElementCollection(HtmlDocument htmlDoc)
		{
			IEnumerable<HtmlNode> inputs = htmlDoc.DocumentNode.Descendants("input");
			foreach (HtmlNode element in inputs)
			{
				this.AddInputElement(element);
			}
			IEnumerable<HtmlNode> menus = htmlDoc.DocumentNode.Descendants("select");
			foreach (HtmlNode element2 in menus)
			{
				this.AddMenuElement(element2);
			}
			IEnumerable<HtmlNode> textareas = htmlDoc.DocumentNode.Descendants("textarea");
			foreach (HtmlNode element3 in textareas)
			{
				this.AddTextareaElement(element3);
			}
		}

		public string AssemblePostPayload()
		{
			StringBuilder sb = new StringBuilder();
			foreach (KeyValuePair<string, string> element in this)
			{
				string value = HttpUtility.UrlEncode(element.Value);
				sb.Append("&" + element.Key + "=" + value);
			}
			return sb.ToString().Substring(1);
		}

		private void AddInputElement(HtmlNode element)
		{
			string name = element.GetAttributeValue("name", "");
			string value = element.GetAttributeValue("value", "");
			string type = element.GetAttributeValue("type", "");
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			string a;
			if ((a = type.ToLower()) != null && (a == "checkbox" || a == "radio"))
			{
				if (!base.ContainsKey(name))
				{
					base.Add(name, "");
				}
				string isChecked = element.GetAttributeValue("checked", "unchecked");
				if (!isChecked.Equals("unchecked"))
				{
					base[name] = value;
					return;
				}
			}
			else
			{
				base.Add(name, value);
			}
		}

		private void AddMenuElement(HtmlNode element)
		{
			string name = element.GetAttributeValue("name", "");
			IEnumerable<HtmlNode> options = element.Descendants("option");
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			HtmlNode firstOp = options.First<HtmlNode>();
			string defaultValue = firstOp.GetAttributeValue("value", firstOp.NextSibling.InnerText);
			base.Add(name, defaultValue);
			foreach (HtmlNode option in options)
			{
				string selected = option.GetAttributeValue("selected", "notSelected");
				if (!selected.Equals("notSelected"))
				{
					string selectedValue = option.GetAttributeValue("value", option.NextSibling.InnerText);
					base[name] = selectedValue;
				}
			}
		}

		private void AddTextareaElement(HtmlNode element)
		{
			string name = element.GetAttributeValue("name", "");
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			base.Add(name, element.InnerText);
		}
	}
}
