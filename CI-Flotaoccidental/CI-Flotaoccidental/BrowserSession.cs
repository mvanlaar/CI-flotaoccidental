using HtmlAgilityPack;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace CI_Flotaoccidental
{
	public class BrowserSession
	{
		private bool _isPost;

		private HtmlDocument _htmlDoc;

		public CookieCollection Cookies
		{
			get;
			set;
		}

		public FormElementCollection FormElements
		{
			get;
			set;
		}

		public string Get(string url)
		{
			this._isPost = false;
			this.CreateWebRequestObject().Load(url);
			return this._htmlDoc.DocumentNode.InnerHtml;
		}

		public string Post(string url)
		{
			this._isPost = true;
			this.CreateWebRequestObject().Load(url, "POST");
			return this._htmlDoc.DocumentNode.InnerHtml;
		}

		private HtmlWeb CreateWebRequestObject()
		{
			return new HtmlWeb
			{
				UseCookies = true,
				PreRequest = new HtmlWeb.PreRequestHandler(this.OnPreRequest),
				PostResponse = new HtmlWeb.PostResponseHandler(this.OnAfterResponse),
				PreHandleDocument = new HtmlWeb.PreHandleDocumentHandler(this.OnPreHandleDocument)
			};
		}

		protected bool OnPreRequest(HttpWebRequest request)
		{
			this.AddCookiesTo(request);
			if (this._isPost)
			{
				this.AddPostDataTo(request);
			}
			return true;
		}

		protected void OnAfterResponse(HttpWebRequest request, HttpWebResponse response)
		{
			this.SaveCookiesFrom(response);
		}

		protected void OnPreHandleDocument(HtmlDocument document)
		{
			this.SaveHtmlDocument(document);
		}

		private void AddPostDataTo(HttpWebRequest request)
		{
			string payload = this.FormElements.AssemblePostPayload();
			byte[] buff = Encoding.UTF8.GetBytes(payload.ToCharArray());
			request.ContentLength = (long)buff.Length;
			request.ContentType = "application/x-www-form-urlencoded";
			Stream reqStream = request.GetRequestStream();
			reqStream.Write(buff, 0, buff.Length);
		}

		private void AddCookiesTo(HttpWebRequest request)
		{
			if (this.Cookies != null && this.Cookies.Count > 0)
			{
				request.CookieContainer.Add(this.Cookies);
			}
		}

		private void SaveCookiesFrom(HttpWebResponse response)
		{
			if (response.Cookies.Count > 0)
			{
				if (this.Cookies == null)
				{
					this.Cookies = new CookieCollection();
				}
				this.Cookies.Add(response.Cookies);
			}
		}

		private void SaveHtmlDocument(HtmlDocument document)
		{
			this._htmlDoc = document;
			this.FormElements = new FormElementCollection(this._htmlDoc);
		}
	}
}
