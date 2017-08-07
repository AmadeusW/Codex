using Codex.API.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Codex.API.Controllers
{
    public class UploadController : ApiController
    {
        // GET: api/Upload
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Upload/5
        public string Get(string name, string path, string script)
        {
            using (var upload = new UploadAction())
            {
                upload.MakeLocalCopy(name, path);
                upload.ExecuteScript(script);
                upload.ImportToCodex(name);
                System.Diagnostics.Debug.WriteLine($"Upload successful");
                return "OK";
            }
        }

        // POST: api/Upload
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/Upload/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Upload/5
        public void Delete(int id)
        {
        }
    }
}
