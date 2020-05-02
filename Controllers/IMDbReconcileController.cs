using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace WebReconcile.Controllers
{
	[ApiController]
	[Route("[controller]/[action]")]
	public class IMDbReconcileController : ControllerBase
	{
		/// <summary>
		/// All possible properties of IMDb.
		/// </summary>
		private readonly JArray allProperties = JArray.Parse("[{id: 'actor', name: 'Actor'},{id: 'birthDate', name: 'Birthdate'},{id: 'contentRating', name: 'Content rating'},{id: 'creator', name: 'Creator'},{id: 'datePublished', name: 'Date published'},{id: 'deathDate', name: 'Deathdate'},{id: 'description', name: 'Description'},{id: 'director', name: 'Director'},{id: 'genre', name: 'Genre'},{id: 'image', name: 'Image'},{id: 'jobTitle', name: 'Jobtitle'},{id: 'keywords', name: 'Keywords'},{id: 'timeRequired', name: 'Duration'}]");

		/// <summary>
		/// String of all temporary in a file stored JSON-LD.
		/// </summary>
		private string JsonCache
		{
			get
			{
				string ldString = "";
				try
				{
                    using StreamReader sr = new StreamReader("cache.json");
                    ldString += sr.ReadToEnd();
                }
				catch (Exception ex)
				{
                    Console.WriteLine(ex.Message);
                }
				return ldString;
			}
			set
			{
				try
				{
                    using StreamWriter outputFile = new StreamWriter("cache.json");
                    outputFile.WriteLine(value);
                }
				catch
				{
				}
			}
		}

		/// <summary>
		/// The standard API, including the Reconciliation Service API, the Data Extension API and the Service Metadata.
		/// </summary>
		/// <returns>Returns the JSON-Result or a callback of it.</returns>
		public ActionResult Api()
		{
			//Reconciliation Service API
			JObject result;
			var queryString = GetQueryString();
			if (queryString.Any(i => i.Key == "queries"))
			{
				JObject queryItems = JObject.Parse(queryString.First(i => i.Key == "queries").Value);
				result =
						new JObject(
						from queryItem in queryItems.Properties()
						select new JProperty(queryItem.Name,
								new JObject(
									new JProperty("result",
										new JArray(
											new JObject(
												new JProperty("type",
													new JArray(
														GetTypeJObject(queryItems[queryItem.Name]["query"].ToString())
													)),
												new JProperty("id", queryItems[queryItem.Name]["query"].ToString()),
												new JProperty("name", GetProperty(queryItems[queryItem.Name]["query"].ToString(), "name")),
												new JProperty("score", 100.0),
												new JProperty("match", true)))))));
			}
			//Data Extension API
			else if (queryString.Any(i => i.Key == "extend"))
			{
				JObject queryItems = JObject.Parse(queryString.First(i => i.Key == "extend").Value);
				var ids = queryItems["ids"];
				var properties = queryItems["properties"];
				result =
					new JObject(
						new JProperty("rows",
							new JObject(
								from id in ids
								select new JProperty(id.ToString(),
									new JObject(
										from property in properties
										select new JProperty(property["id"].ToString(),
											GetContentJArray(id.ToString(), property["id"].ToString())))))),
					new JProperty("meta",
					new JArray(
							from property in properties
							select new JObject(
							new JProperty("id", property["id"].ToString()),
							new JProperty("name", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(property["id"].ToString()))))));
			}
			//Service Metadata
			else
			{
				result = GetConfiguration(Request);
			}

			return CallbackReturn(queryString, result);
		}
		/// <summary>
		/// The Property Proposal API, that proposes possible properties based on the type.
		/// </summary>
		/// <returns>Returns the possible properties as JSON or a callback of it.</returns>
		public ActionResult ProposeProperties()
		{
			var queryString = GetQueryString();
			var type = queryString.FirstOrDefault(i => i.Key == "type").Value.ToString();
			JObject result;
			if (type == "Person")
			{
				result = JObject.Parse("{properties: [{id: 'image',name: 'Image'},{id: 'jobTitle',name: 'Jobtitle'},{id: 'description',name: 'Description'},{id: 'birthDate',name: 'Birthdate'},{id: 'deathDate',name: 'Deathdate'}],type: 'Person'}");
			}
			else if (type == "TVEpisode" || type == "Movie" || type == "TVSeries" || type == "CreativeWork")
			{
				result = JObject.Parse("{properties: [{id: 'actor',name: 'Actor'},{id: 'director',name: 'Director'},{id: 'creator',name: 'Creator'},{id: 'description',name: 'Description'},{id: 'datePublished',name: 'Date published'},{id: 'timeRequired',name: 'Duration'},{id: 'keywords',name: 'Keywords'},{id: 'genre',name: 'Genre'},{id: 'contentRating',name: 'Content rating'},{id: 'image',name: 'Image'}],type: '" + type + "'}");
			}
			else
			{
				result = new JObject(
					new JProperty("properties", allProperties),
					new JProperty("type", type
					));
			}
			return CallbackReturn(queryString, result);
		}
		/// <summary>
		/// The Preview API, that loads a preview of the forwarded id.
		/// </summary>
		/// <returns>Returns a HTML-preview of the item.</returns>
		public ActionResult Preview()
		{
			var queryString = GetQueryString();
			try
			{
				var id = queryString.FirstOrDefault(i => i.Key == "id").Value.ToString();
				var ld = LoadJSON(id);
				var title = ld["name"]?.ToString() ?? "";
				var picture = ld["image"]?.ToString() ?? "";
				var description = ld["description"]?.ToString() ?? "";
				var preview =
				$"<html><head><meta charset='utf-8' /></head><body style='margin: 0px;font: 0.7em sans-serif;'><div style='width: 100px; text-align: center; overflow: hidden; margin-right: 9px; float: left;'><img src='{picture}' alt='{title}' style='height: 100px' /></div><a href='https://www.imdb.com/{FormatIMDbId(id)}/' target='_blank'>{title}</a> <span style='color: #505050;'>({id})</span><p>{Regex.Unescape(description)}</p></body></html>";
				return Content(preview, "text/html");
			}
			catch (Exception)
			{
				return NotFound();
			}
		}

		/// <summary>
		/// The Suggest API, that provides a list of possible properties filtert by the prefix.
		/// </summary>
		/// <returns>Returns the suggested properties or a callback of them.</returns>
		public ActionResult SuggestProperty()
		{
			var queryString = GetQueryString();
			var prefix = queryString.FirstOrDefault(i => i.Key == "prefix").Value.ToString();
			var result = new JObject(
					new JProperty("result",
					String.IsNullOrEmpty(prefix)? allProperties : new JArray(allProperties.Where(i => i["name"].ToString().StartsWith(prefix)))
				));
			return CallbackReturn(queryString, result);
		}

		/// <summary>
		/// Returns with or without a callback based on the querystring.
		/// </summary>
		/// <param name="queryString">String of the query to determine if a callback is needed.</param>
		/// <param name="result">The JSON-object to return as ContentResult.</param>
		/// <returns>Returns the JSON-object with or withour callback.</returns>
		private ContentResult CallbackReturn(IEnumerable<KeyValuePair<string, StringValues>> queryString, JObject result)
		{
			if (queryString.Any(i => i.Key == "callback"))
				return Content(queryString.First(i => i.Key == "callback").Value + '(' + result + ')', "text/javascript");
			else
				return Content(result.ToString(), "application/json");
		}

		#region utils

		/// <summary>
		/// Provides the configuration for the Reconciliation Service API.
		/// </summary>
		/// <param name="request">The HTTP-request to get necessary informations about the host.</param>
		/// <returns>Returns an adapted configuration as JSON-string.</returns>
		private JObject GetConfiguration(HttpRequest request)
		{
			var service_url = request.Scheme + "://" + request.Host + "/" + request.Path.Value.Split("/")[1];
			return JObject.Parse(
			@"{
				name: 'IMDb (en)',
				view: {
					url: 'https://www.imdb.com/Name?{{id}}'
				},
				preview: {
					height: 100,
					url: '" + service_url + @"/preview?id={{id}}',
					width: 400
				},
				defaultTypes: [
					{
						id: 'Person',
						name: 'Person'
					},
					{
						id: 'Movie',
						name: 'Movie'
					},
					{
						id: 'TVSeries',
						name: 'TVSeries'
					},
					{
						id: 'TVEpisode',
						name: 'TVEpisode'
					},
					{
						id: 'CreativeWork',
						name: 'CreativeWork'
					}
				],
				identifierSpace: 'http://www.imdb.com/',
				schemaSpace: 'http://www.imdb.com/',
				suggest: {
					property: {
						service_path: '/suggestproperty',
						service_url: '" + service_url + @"'
					}
				},
				extend: {
					property_settings: [],
					propose_properties: {
						service_path: '/proposeproperties',
						service_url: '" + service_url + @"'
					}
				}
			}");
		}

		/// <summary>
		/// Returns the data of the request based on the request-method.
		/// </summary>
		/// <returns>Returns the data of the request.</returns>
		private IEnumerable<KeyValuePair<string, StringValues>> GetQueryString()
		{
			if (Request.Method == "GET")
				return Request.Query;
			else if (Request.Method == "POST")
				return Request.Form;
			else
				throw new Exception();
		}

		/// <summary>
		/// Loads the JSON-LD from the specific IMDb-Page or if it's already loaded uses the saved one.
		/// </summary>
		/// <param name="id">The IMDb-ID of the IMDb-Page.</param>
		/// <returns>Returns a JSON-object with the JSON-LD.</returns>
		private JObject LoadJSON(string id)
		{
			JArray ldJsonArray = new JArray();
			JObject ldJson = new JObject();
			if (!String.IsNullOrEmpty(JsonCache))
			{
				ldJsonArray = JArray.Parse(JsonCache);
				ldJson = (JObject)ldJsonArray.ToList().Find(j => j["url"].ToString().Trim('/') == FormatIMDbId(id));
			}
			if (ldJson == null || ldJson.Count == 0)
			{
				var web = new HtmlWeb();
				var doc = web.Load("https://www.imdb.com/" + FormatIMDbId(id));
				var match = doc.DocumentNode.SelectSingleNode(@"//script[@type=""application/ld+json""]");
				ldJson = JObject.Parse(match.InnerText);
				ldJsonArray.Add(ldJson);
				JsonCache = ldJsonArray.ToString();
			}
			return ldJson;
		}

		/// <summary>
		/// Gets the specified property of a JSON-LD, that is loaded.
		/// </summary>
		/// <param name="id">The ID of the JSON-LD to load.</param>
		/// <param name="name">The name of the property to load.</param>
		/// <returns>Returns the value of the specified property</returns>
		private string GetProperty(string id, string name)
		{
			var ld = LoadJSON(id);
			if (ld[name] != null)
			{
				return ld[name].ToString();
			}
			else
			{
				return "";
			}
		}

		/// <summary>
		/// Formats the IMDb-ID for the use in an URL.
		/// </summary>
		/// <param name="id">The unformatted IMDb-ID.</param>
		/// <returns>Returns the formatted IMDb-ID.</returns>
		private string FormatIMDbId(string id)
		{
			id = id.Trim('/');
			return (id.StartsWith("tt") ? "title/" : "") + (id.StartsWith("nm") ? "name/" : "") + id;
		}

		/// <summary>
		/// Gets the type of an item with a specific id.
		/// </summary>
		/// <param name="id">The id of the item to load.</param>
		/// <returns>Returns a JSON-object with the type.</returns>
		private JObject GetTypeJObject(string id)
		{
			var ld = LoadJSON(id);
			return new JObject(
				new JProperty("id", ld["@type"].ToString()),
				new JProperty("name", ld["@type"].ToString()
				));
		}

		/// <summary>
		/// Gets the content of the properties of a specific item.
		/// </summary>
		/// <param name="id">The item from which the properties are asked.</param>
		/// <param name="name">The name of the property which is asked for.</param>
		/// <returns>Returns an JSON-array with the content of the properties.</returns>
		private JArray GetContentJArray(string id, string name)
		{
			//IMDb Person property with @type Person, url, name or @type Organization, url
			if (name == "actor" || name == "creator" || name == "director" || name == "writer")
			{
				var property = GetProperty(id, name);
				if (!property.StartsWith("["))
					property = "[" + property + "]";
				return new JArray(
						from prop in JArray.Parse(property).Where(i => i["@type"].ToString() != "Organization")
						select new JObject(
									new JProperty("name", prop["name"].ToString()),
									new JProperty("id", FormatIMDbId(prop["url"].ToString()).Split("/")[1])));
			}
			//string property
			else if (name == "description" || name == "timeRequired" || name == "contentRating" || name == "image")
			{
				return new JArray(
							new JObject(
								new JProperty("str", GetProperty(id, name))));
			}
			//date property
			else if (name == "datePublished" || name == "birthDate" || name == "deathDate")
			{
				return new JArray(
							new JObject(
								new JProperty("date", GetProperty(id, name) + "T00:00:00+00:00")));
			}
			//string property seperated by comma
			else if (name == "keywords")
			{
				return new JArray(
						from prop in GetProperty(id, name).Split(",")
						select new JObject(
									new JProperty("str", prop)));
			}
			//string array property
			else if (name == "genre" || name == "jobTitle")
			{
				return new JArray(
						from prop in JArray.Parse(GetProperty(id, name))
						select new JObject(
									new JProperty("str", prop.ToString())));
			}
			return null;
		}
		#endregion utils

	}
}