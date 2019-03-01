using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebApi.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        string configuration = @"{""name"":""IMDb (en)"", ""view"": {""url"": ""https://www.imdb.com/title/{{id}}""}, ""defaultTypes"": [{""id"": ""/imdb/title"", ""name"": ""Title""}]}";

        // GET api
        [HttpGet]
        public ActionResult Get()
        {
            var queryString = Request.Query;
            if (queryString["queries"].Any())
            {
                var result = "{";
                JObject qList = JObject.Parse(queryString["queries"]);
                foreach (JProperty property in qList.Properties())
                {
                    Console.WriteLine("query: " + qList[property.Name]["query"]);
                    Console.WriteLine("type: " + qList[property.Name]["type"]);
                    result += property.Name + @": {""result"": [{""score"": 100.0, ""type"": [{""name"": ""Title"", ""id"": ""/imdb/title""}], ""id"": " + qList[property.Name]["query"] + @", ""name"" : " + qList[property.Name]["query"] + @", ""match"": true}]}";
                }
                result += "}";
                if (queryString["callback"].Any())
                {
                    var send = Content(queryString["callback"] + '(' + JsonConvert.SerializeObject(result) + ')', "text/javascript");
                    return send;
                }
                else
                    return Content(JsonConvert.SerializeObject(result), "application/json");
            }
            else if (queryString["callback"].Any())
                return Content(queryString["callback"] + '(' + configuration + ')', "text/javascript");
            else
                return Content(configuration, "application/json");
        }
        // POST api
        [HttpPost]
        public ActionResult Post()
        {
            var queryString = Request.Form;
            if (queryString["queries"].Any())
            {
                var result =@"{";
                JObject qList = JObject.Parse(queryString["queries"]);
                foreach (JProperty property in qList.Properties())
                {
                    Console.WriteLine("query: " + qList[property.Name]["query"]);
                    Console.WriteLine("type: " + qList[property.Name]["type"]);
                    result += property.Name + @": {""result"": [{""score"": 100.0, ""type"": [{""name"": ""Title"", ""id"": ""/imdb/title""}], ""id"": " + qList[property.Name]["query"] + @", ""name"": " + qList[property.Name]["query"] + @", ""match"": true}]},";
                }
                result += @"}";
                if (queryString["callback"].Any())
                    return Content(queryString["callback"] + '(' + result + ')', "text/javascript");
                else
                    return Content(result, "application/json");
            }
            else if (queryString["callback"].Any())
                return Content(queryString["callback"] + '(' + configuration + ')', "text/javascript");
            else
                return Content(configuration, "application/json");
        }
    }
}
