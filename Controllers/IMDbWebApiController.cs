using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace IMDbWebApi.Controllers
{
	[ApiController]
	public class IMDbWebApiController : ControllerBase
	{
		//configuration
		private readonly JObject configuration = JObject.Parse(
			@"{
				name: 'IMDb (en)',
				view: {
					url: 'https://www.imdb.com/{{id}}'
				},
				preview: {
					height: 100,
					url: 'http://localhost:5000/imdb-reconcile/preview?id={{id}}',
					width: 400
				},
				defaultTypes: [
					{
						id: 'title/',
						name: 'Title'
					},
					{
						id: 'name/',
						name: 'Name'
					}
				],
				identifierSpace: 'http://www.imdb.com/',
				schemaSpace: 'http://www.imdb.com/'
			}");
		private JObject ldjson;

		//reconcile service
		[HttpGet]
		[Route("imdb-reconcile/api")]
		public ActionResult GetApi()
		{
			return ParseApi(Request.Query);
		}

		[HttpPost]
		[Route("imdb-reconcile/api")]
		public ActionResult PostApi()
		{
			return ParseApi(Request.Form);
		}

		private ActionResult ParseApi(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> queryString)
		{
			if (queryString.Any(i => i.Key == "queries"))
			{
				JObject wikidataItems = JObject.Parse(queryString.First(i => i.Key == "queries").Value);
				JObject result =
						new JObject(
						from wikidataItem in wikidataItems.Properties()
						select new JProperty(wikidataItem.Name,
								new JObject(
									new JProperty("result",
										new JArray(
											new JObject(
												new JProperty("type",
													GetTypeArray(wikidataItems[wikidataItem.Name]["query"].ToString())
													),
												new JProperty("id", wikidataItems[wikidataItem.Name]["query"].ToString()),
												new JProperty("name", GetTitle(wikidataItems[wikidataItem.Name]["query"].ToString())),
												new JProperty("score", 100.0),
												new JProperty("match", true)))))));
				if (queryString.Any(i => i.Key == "callback"))
					return Content(queryString.First(i => i.Key == "callback").Value + '(' + result + ')', "text/javascript");
				else
					return Content(result.ToString(), "application/json");
			}
			else if (queryString.Any(i => i.Key == "callback"))
				return Content(queryString.First(i => i.Key == "callback").Value + '(' + configuration + ')', "text/javascript");
			else
				return Content(configuration.ToString(), "application/json");
		}

		//preview service

		[HttpPost]
		[Route("imdb-reconcile/preview")]
		public ActionResult PostPreview()
		{
			return ParsePreview(Request.Form);
		}

		[HttpGet]
		[Route("imdb-reconcile/preview")]
		public ActionResult GetPreview()
		{
			return ParsePreview(Request.Query);
		}

		private ActionResult ParsePreview(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> queryString)
		{
			try
			{
				var id = queryString.FirstOrDefault(i => i.Key == "id").Value.ToString();
				var ld = LoadJSON(id);
				var title = ld["name"].ToString();
				var picture = ld["image"].ToString();
				var description = ld["description"].ToString();
				var preview =
				@"<html><head><meta charset='utf-8' /></head><body style='margin: 0px; font-family: Arial; sans-serif'><div style='height: 100px; width: 400px; overflow: hidden; font-size: 0.7em'><div style='width: 100px; text-align: center; overflow: hidden; margin-right: 9px; float: left'><img src='"
				+ picture
				+ @"' alt='"
				+ title
				+ @"' style='height: 100px' /></div><div style='margin-left: 3px;'><a href='https://www.imdb.com/"
				+ FormatIMDbId(id)
				+ @"/' target='_blank' style='text-decoration: none;'>"
				+ title
				+ @"</a> <span style='color: #505050;'>("
				+ id
				+ @")</span><p>"
				+ Regex.Unescape(description)
				+ @"</p></div></div></body></html>";
				return Content(preview, "text/html");
			}
			catch (Exception)
			{
				return NotFound();
			}
		}
		#region utils
		private JObject LoadJSON(string id)
		{
			if (ldjson == null || ldjson["url"].ToString() != $"/{FormatIMDbId(id)}/")
			{
				var web = new HtmlWeb();
				var doc = web.Load("https://www.imdb.com/" + FormatIMDbId(id));
				var match = doc.DocumentNode.SelectSingleNode(@"//script[@type=""application/ld+json""]");
				ldjson = JObject.Parse(match.InnerText);
			}
			return ldjson;
		}
		private string FormatIMDbId(string id)
		{
			id = id.Trim('/');
			return (id.StartsWith("tt") ? "title/" : "") + (id.StartsWith("nm") ? "name/" : "") + id;
		}

		private string GetTitle(string id)
		{
			return LoadJSON(id)["name"].ToString();
		}

		private JArray GetTypeArray(string id)
		{
			if (FormatIMDbId(id).StartsWith("tt"))
			{
				return new JArray(
					new JObject(
						new JProperty("id", "title/"),
						new JProperty("name", "Title")));
			}
			else if (FormatIMDbId(id).StartsWith("nm"))
			{
				return new JArray(
					new JObject(
						new JProperty("id", "name/"),
						new JProperty("name", "Name")));
			}
			else
			{
				throw new Exception("Unknown Type");
			}
		}

		#endregion utils

	}
}
