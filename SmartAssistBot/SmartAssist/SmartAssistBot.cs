
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Connector.DirectLine;
using CoreBot.SmartAssist.Operations;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Net.Http;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using System;
using System.IO;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.AspNetCore.SignalR;
using System.Web.Helpers;
using Newtonsoft.Json.Linq;
using AdaptiveCards;
using Attachment = Microsoft.Bot.Schema.Attachment;
using IMessageActivity = Microsoft.Bot.Schema.IMessageActivity;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using ServiceStack;
using Microsoft.Bot.Builder.Dialogs.Choices;
using SmartBot.SmartAssist.BotConnector;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class ConversationData
    {
        public bool IsEscalated { get; set; }

    }

    public class SmartAssistBot : ActivityHandler
    {

        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        protected readonly ILogger Logger;
        protected readonly KBSearchOperation kBSearchOperation;
        protected readonly AppointmentDetectionOperation appointmentDetectionOperation;
        HttpResponseMessage ResponseFromPVA;
        String ResponseFromPVABody = String.Empty;
        string UserInput = String.Empty;
        string PVAReply = string.Empty;
        string DirectlineConversationidPVA = Microsoft.BotBuilderSamples.DynamicsDataAccessLayer.DirectlineConversationidPVA;
        string DirectlineTokenPVA = Microsoft.BotBuilderSamples.DynamicsDataAccessLayer.DirectlineTokenPVA;
        bool PVAIssue;

        public SmartAssistBot(ConversationState conversationState, UserState userState, IDynamicsDataAccessLayer dynamicsDataAccessLayer, KBSearchOperation kBSearchOperation, AppointmentDetectionOperation appointmentDetectionOperation)
        {


            ConversationState = conversationState;
            UserState = userState;
            this.kBSearchOperation = kBSearchOperation;
            this.appointmentDetectionOperation = appointmentDetectionOperation;

        }


        private async Task setConversationData(ITurnContext turnContext)
        {

            var conversationStateAccessors = ConversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

            conversationData.IsEscalated = true;
            await ConversationState.SaveChangesAsync(turnContext);
            await UserState.SaveChangesAsync(turnContext);
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            var conversationStateAccessors = ConversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationData());

            var activity = turnContext.Activity;
            var actList = new List<Bot.Schema.IActivity>();
            await setConversationData(turnContext);

            if (activity.Type == Bot.Schema.ActivityTypes.ConversationUpdate && conversationData.IsEscalated == false)
            {
                var result = activity.MembersAdded;
                foreach (Bot.Schema.ChannelAccount account in result)
                {
                    if (account.AadObjectId != null)
                    {
                        await setConversationData(turnContext);
                        return;
                    }
                }
            }
            if (activity.Type == Bot.Schema.ActivityTypes.Message && (conversationData.IsEscalated == true))
            {
                await base.OnTurnAsync(turnContext, cancellationToken);
            }

            // Save any state changes that might have occured during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<Bot.Schema.IMessageActivity> turnContext, CancellationToken cancellationToken)
        {




            var cards = new List<Bot.Schema.IMessageActivity>();
            HttpClient client_1 = new HttpClient();
            client_1.DefaultRequestHeaders.Add("Authorization", "Bearer " + DirectlineTokenPVA);



            //post request
            UserInput = turnContext.Activity.Text;
            string jsondata = String.Empty;
            if (UserInput != "endofconversation")
            {
                jsondata = "{'type': 'message','from': {'id': 'user1'},'text': '" + UserInput + "'}";
            }
            else
            {
                jsondata = "{'type': 'endOfConversation','from': {'id': 'user1'}}";
            }
            string post_request_url = "https://directline.botframework.com/v3/directline/conversations/" + DirectlineConversationidPVA + "/activities";
            HttpRequestMessage sendMessageToPVA = new HttpRequestMessage(HttpMethod.Post, post_request_url);
            sendMessageToPVA.Content = new StringContent(jsondata, Encoding.UTF8, "application/json");

            var responseSendMessageToPVA = await client_1.SendAsync(sendMessageToPVA);
            string NewResponseFromPVA = ResponseFromPVABody;


            Thread.Sleep(3000);

            //get request
            ResponseFromPVA = await client_1.GetAsync(post_request_url);
            string responseGetMessageFromPVA = String.Empty;
            if (ResponseFromPVA.IsSuccessStatusCode)
            {
                var body = await ResponseFromPVA.Content.ReadAsStringAsync();
                ResponseFromPVABody = body;
            }
            NewResponseFromPVA = ResponseFromPVABody;

            dynamic newResponsePVA = JsonConvert.DeserializeObject(NewResponseFromPVA);

            JArray NewResponse = (JArray)newResponsePVA["activities"];
            int lengthnewResponse = NewResponse.Count;

            int lengthpreviousResponse;
            if (Microsoft.BotBuilderSamples.DynamicsDataAccessLayer.responsefromPVA == null)
            {
                lengthpreviousResponse = 0;
            }
            else
            {
                dynamic previousResponsePVA = JsonConvert.DeserializeObject(Microsoft.BotBuilderSamples.DynamicsDataAccessLayer.responsefromPVA);
                JArray PreviousResponse = (JArray)previousResponsePVA["activities"];
                lengthpreviousResponse = PreviousResponse.Count;
            }
            bool suggestionfortopics = true;
            try
            {
                var suggestions = newResponsePVA.activities[lengthpreviousResponse+1].suggestedActions.actions[0];

            }
            catch (Exception e)
            {
                suggestionfortopics = false;
            }

            //check if PVA has replied or not
            if (lengthnewResponse-lengthpreviousResponse >= 2 && !suggestionfortopics)
            {
                for (int i = lengthpreviousResponse + 1; i < lengthnewResponse; i++)
                {
                    var element = newResponsePVA.activities[i].text;
                    PVAReply = element;
                    string unescape_PVAreply = Regex.Unescape(PVAReply);
                    bool isanadaptivecard = true;
                    IMessageActivity appointmentCardMessage = null;
                    try
                    {
                        AdaptiveCard pvareplyjsontest = JsonConvert.DeserializeObject<AdaptiveCard>(unescape_PVAreply);
                    }
                    catch (Exception IsNotAdaptiveCard)
                    {
                        isanadaptivecard = false;
                    }
                    if (isanadaptivecard)
                    {
                        AdaptiveCard pvareplyjson = JsonConvert.DeserializeObject<AdaptiveCard>(unescape_PVAreply);
                        var attachmentforadaptivecard = new Attachment()
                        {
                            ContentType = "application/vnd.microsoft.card.adaptive",
                            Content = pvareplyjson,
                        };
                        appointmentCardMessage = MessageFactory.Attachment(attachmentforadaptivecard);

                        cards.Add(appointmentCardMessage);
                    }
                    else
                    {
                        var adaptivecard = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
                        {
                            Body = new List<AdaptiveElement>()
                            {
                                new AdaptiveTextBlock(PVAReply),
                            },
                        };
                        var attachmentforastring = new Attachment()
                        {
                            ContentType = "application/vnd.microsoft.card.adaptive",
                            Content = adaptivecard,
                        };
                        appointmentCardMessage = MessageFactory.Attachment(attachmentforastring);

                        cards.Add(appointmentCardMessage);



                    }
                }
                cards.ForEach((card) =>
                {
                    Dictionary<string, object> channelinfo = new Dictionary<string, object>
                            {
                                { "tags", "smartbot" }
                            };
                    card.ChannelData = channelinfo;
                });
                var temp = cards.ToArray();
                await turnContext.SendActivitiesAsync(cards.ToArray(), cancellationToken);
            }
            
            Microsoft.BotBuilderSamples.DynamicsDataAccessLayer.responsefromPVA = ResponseFromPVABody;





        }
    }
}






