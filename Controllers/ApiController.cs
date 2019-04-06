using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace IMDbWebApi.Controllers
{
    [Route("imdb-reconcile/api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        JObject configuration =
            new JObject(
                new JProperty("name", "IMDb (en)"),
                new JProperty("view",
                    new JObject(
                        new JProperty("url", "https://www.imdb.com/title/{{id}}")
                    )),
                new JProperty("defaultTypes",
                    new JArray(
                        new JObject(
                            new JProperty("id", "/imdb/title"),
                            new JProperty("name", "Title")))));
        // GET api
        [HttpGet]
        public ActionResult Get()
        {
            return ParseKeys(Request.Query);

        }

        // POST api
        [HttpPost]
        public ActionResult Post()
        {
            return ParseKeys(Request.Form);
        }

        //reconcile service
        private ActionResult ParseKeys(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> queryString)
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
                                                    new JArray(
                                                        new JObject(
                                                            new JProperty("id", "/imdb/title"),
                                                            new JProperty("name", "Title")))),
                                                new JProperty("id", wikidataItems[wikidataItem.Name]["query"]),
                                                new JProperty("name", wikidataItems[wikidataItem.Name]["query"]),
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
    }
}
