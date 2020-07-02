// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CoreBot.Models;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.BotBuilderSamples
{

    /// <summary>
    /// Connects to CDS and searches for relevant knowledge articles
    /// </summary>
    public class DynamicsDataAccessLayer: IDynamicsDataAccessLayer
    {
        private Guid applicationId;
        private readonly string clientSecret;
        private readonly string aadInstanceUrl;
        private readonly string organizationUrl;
        private readonly Guid tenantId;
        private readonly string accessToken;
        public static string DirectlineConversationidPVA = String.Empty;
        public static string DirectlineTokenPVA = String.Empty;
        public static string responsefromPVA;

        public DynamicsDataAccessLayer(IConfiguration configuration){
            applicationId = Guid.Parse(configuration["DynamicsAppId"]);
            clientSecret = configuration["DynamicsAppSecret"];
            aadInstanceUrl = "https://login.microsoftonline.com";
            organizationUrl = configuration["DynamicsOrgUrl"];
            tenantId = Guid.Parse(configuration["TenantId"]);
            accessToken = GetAccessToken().Result;
            
        }

        protected async Task<string> GetAccessToken()
        {
            string botId = ConfigurationManager.AppSettings["BotId"] ?? string.Empty;
            string tenantId = ConfigurationManager.AppSettings["BotTenantId"] ?? string.Empty;
            string botTokenEndpoint = ConfigurationManager.AppSettings["BotTokenEndpoint"] ?? string.Empty;
            string botName = ConfigurationManager.AppSettings["BotName"] ?? string.Empty;
            SmartBot.SmartAssist.BotConnector.BotService s_botService = new SmartBot.SmartAssist.BotConnector.BotService()
            {
                BotName = botName,
                BotId = botId,
                TenantId = tenantId,
                TokenEndPoint = botTokenEndpoint,
            };


            //returns the token which is used for returning conversation id.
            var tokenPVA = await s_botService.GetTokenAsync();

            using (var directLineClient = new DirectLineClient(tokenPVA))
            {
                var conversation = await directLineClient.Conversations.StartConversationAsync();
                DirectlineConversationidPVA = conversation.ConversationId;
                //DirectlineConversationidPVA = conversationtId;
                DirectlineTokenPVA = conversation.Token;
            }
            Trace.WriteLine("Getting Token");
            var clientcred = new ClientCredential(applicationId.ToString(), clientSecret);
            var authenticationContext = new AuthenticationContext($"{aadInstanceUrl}/{tenantId}");
            var authenticationResult = await authenticationContext.AcquireTokenAsync(organizationUrl, clientcred);
            return authenticationResult.AccessToken;
        }

        /// <summary>
        /// Executes FullTextSearchKnowledgeArticle action in the CDS org to look for Knowledge articles
        /// </summary>
        /// <param name="searchString"> Search text </param>
        /// <returns> Knowledge article results as string </returns>
        public async Task<string> FullTextKBSearchAsync(string searchString) {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var fullTextKBSearchUrl = organizationUrl + "/api/data/v9.1/FullTextSearchKnowledgeArticle";

            var fullTextSearchBody = new FullTextKBSearchRequest
            {
                RemoveDuplicates = "true",
                UseInflection = "false",
                StateCode = 3,
                SearchText = searchString,
                QueryExpression = new QueryExpression {
                    Type = "Microsoft.Dynamics.CRM.QueryExpression",
                    EntityName = "knowledgearticle",
                    ColumnSet = new ColumnSet {
                        Type = "Microsoft.Dynamics.CRM.ColumnSet",
                        Columns = new List<string>() {
                            "title", "isinternal", "description", "articlepublicnumber", "modifiedon", "statecode", "keywords"
                        },
                    },
                    Orders = new List<Order>() {
                        new Order {
                            AttributeName = "modifiedon",
                            OrderType = "Descending"
                        }
                    },
                    PageInfo = new PageInfo {
                        ReturnTotalRecordCount = true,
                        Count = 10
                    }
                }
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(fullTextSearchBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(fullTextKBSearchUrl, jsonContent);
            var kbResults = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode) {
                ///  return kbResults;
                return "Hi I am kunal";
            }
            else {
                return null;
            }
        }
    }
}
