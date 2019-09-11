using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace IMDbReconcile.Controllers
{
	[ApiController]
	[Route("[controller]/[action]")]
	public class IMDbReconcileController : ControllerBase
	{
		//configuration
		private string GetConfiguration(HttpRequest request)
		{
			return JObject.Parse(
			@"{
				name: 'IMDb (en)',
				view: {
					url: 'https://www.imdb.com/Name?{{id}}'
				},
				preview: {
					height: 100,
					url: '" + request.Scheme + @"://" + request.Host + @"/" + request.Path.Value.Split("/")[1] + @"/preview?id={{id}}',
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
				schemaSpace: 'http://www.imdb.com/',
				extend: {
					property_settings: [],
					propose_properties: {
						service_path: '/proposeproperties',
						service_url: '" + request.Scheme + @"://" + request.Host + @"/" + request.Path.Value.Split("/")[1] + @"'
					}
				}
			}").ToString();
		}

		public ActionResult Api()
		{
			var queryString = GetQueryString();
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
			if (queryString.Any(i => i.Key == "extend"))
			{
				JObject wikidataItems = JObject.Parse(queryString.First(i => i.Key == "extend").Value);
				var result = JObject.Parse(@"{rows:{tt0511646:{actor:[{name:'Omar Gooding',id:'nm0328954'}]}},meta:[{id:'actor',name:'Actor',type:{id:'name/',name:'Name'}}]}");
				return Content(result.ToString(), "application/json");
			}
			else if (queryString.Any(i => i.Key == "callback"))
				return Content(queryString.First(i => i.Key == "callback").Value + '(' + GetConfiguration(Request) + ')', "text/javascript");
			else
				return Content(GetConfiguration(Request), "application/json");
		}

		public ActionResult Preview()
		{
			var queryString = GetQueryString();
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

		public ActionResult ProposeProperties()
		{
			var queryString = GetQueryString();
			var type = queryString.FirstOrDefault(i => i.Key == "type").Value.ToString();
			string result;
			if (type == "name/")
			{
				result = JObject.Parse(@"{properties: [{id: 'name',name: 'Name'},{id: 'image',name: 'Image'},{id: 'jobTitle',name: 'Name'},{id: 'jobTitle',name: 'Jobtitle'},{id: 'description',name: 'Description'},{id: 'birthDate',name: 'Birthdate'}],type: 'name/',limit: 6}").ToString();
			}
			else if (type == "title/")
			{
				result = JObject.Parse(@"{properties: [{id: 'actor',name: 'Actor'},{id: 'director',name: 'Director'},{id: 'creator',name: 'Creator'},{id: 'description',name: 'Description'},{id: 'datePublished',name: 'Date published'},{id: 'timeRequired',name: 'Time required'}],type: 'title/',limit: 6}").ToString();
			}
			else
			{
				throw new Exception();
			}
			if (queryString.Any(i => i.Key == "callback"))
				return Content(queryString.First(i => i.Key == "callback").Value + '(' + result + ')', "text/javascript");
			else
				return Content(result.ToString(), "application/json");
		}

		#region utils
		private IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> GetQueryString()
		{
			if (Request.Method == "GET")
				return Request.Query;
			else if (Request.Method == "POST")
				return Request.Form;
			else
				throw new Exception();
		}
		private JObject LoadJSON(string id)
		{
			var web = new HtmlWeb();
			var doc = web.Load("https://www.imdb.com/" + FormatIMDbId(id));
			var match = doc.DocumentNode.SelectSingleNode(@"//script[@type=""application/ld+json""]");
			return JObject.Parse(match.InnerText);
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
			if (FormatIMDbId(id).StartsWith("title"))
			{
				return new JArray(
					new JObject(
						new JProperty("id", "title/"),
						new JProperty("name", "Title")));
			}
			else if (FormatIMDbId(id).StartsWith("name"))
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
