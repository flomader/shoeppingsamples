using Microsoft.PowerBI.Api.V1;
using Microsoft.PowerBI.Security;
using Microsoft.Rest;
using PbiPaasWebApi.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;

namespace PbiPaasWebApi.Controllers
{
    [RoutePrefix("api")]
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class ReportsController : ApiController
    {
        private string workspaceCollectionName;
        private Guid workspaceId;
        private string workspaceCollectionAccessKey;
        private string apiUrl;

        private static string trustedCallerClientId = ConfigurationManager.AppSettings["ad:TrustedCallerClientId"];
        private static string trustedCallerServicePrincipalId = ConfigurationManager.AppSettings["ad:TrustedCallerServicePrincipalId"];

        public ReportsController()
        {
            this.workspaceCollectionName = ConfigurationManager.AppSettings["powerbi:WorkspaceCollectionName"];
            this.workspaceId = Guid.Parse(ConfigurationManager.AppSettings["powerbi:WorkspaceId"]);
            this.workspaceCollectionAccessKey = ConfigurationManager.AppSettings["powerbi:WorkspaceCollectionAccessKey"];
            this.apiUrl = ConfigurationManager.AppSettings["powerbi:ApiUrl"];
        }

        // GET: api/Reports/386818d4-f37f-485f-b750-08f982b0c146
        [HttpGet]
        public async Task<IHttpActionResult> Get(string id)
        {
            CheckCallerId();

            string merchantid;
            if (Request.Headers.Contains("merchantid"))
            {
                merchantid = Request.Headers.GetValues("merchantid").FirstOrDefault();
            } else
            {
                return BadRequest($"Header 'merchantid' is required but not found.");
            }

            var credentials = new TokenCredentials(workspaceCollectionAccessKey, "AppKey");
            using (var client = new PowerBIClient(new Uri(apiUrl), credentials))
            {
                var reportsResponse = await client.Reports.GetReportsAsync(this.workspaceCollectionName, this.workspaceId.ToString());
                var report = reportsResponse.Value.FirstOrDefault(r => r.Id == id);
                if(report == null)
                {
                    return BadRequest($"No reports were found matching the id: {id}");
                }

                var embedToken = PowerBIToken.CreateReportEmbedToken(workspaceCollectionName, workspaceId.ToString(), report.Id, merchantid, new List<string>{ "Merchant" });
                var accessToken = embedToken.Generate(workspaceCollectionAccessKey);
                var reportWithToken = new ReportWithToken(report, accessToken);

                return Ok(reportWithToken);
            }
        }

        private static void CheckCallerId()
        {
            // Uncomment following lines for service principal authentication
            var currentCallerClientId = ClaimsPrincipal.Current.FindFirst("appid").Value;
            var currentCallerServicePrincipalId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
            if (currentCallerClientId != trustedCallerClientId || currentCallerServicePrincipalId != trustedCallerServicePrincipalId)
            {
                throw new HttpResponseException(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized, ReasonPhrase = "The appID or service principal ID is not the expected value." });
            }
        }
    }
}
