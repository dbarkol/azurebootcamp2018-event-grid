using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Twilio.TwiML;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Rest;
using Zohan.Models;

namespace WeWantTheFunc
{
    public static class SessionFeedback
    {
        /// <summary>
        /// This function is invoked by an HTTP request that is
        /// sent from Twilio. It will take the body of the text 
        /// message and retrieve the sentiment analysis score. 
        /// After receiving the score, it will then publish
        /// a message to Azure Event Grid with the details.
        /// </summary>
        /// <returns></returns>
        [FunctionName("SessionFeedback")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req, 
            TraceWriter log)
        {
            log.Info("Session Feedback function triggered.");

            // Read the request content into a string, return
            // a bad request error code if it's empty.
            var data = await req.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(data))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            // Extract the message body from the payload and
            // create a feedback instance.
            var body = GetMessageBody(data);
            var f = new Feedback
            {
                Id = Guid.NewGuid(),
                Message = body,
                Score = GetScore(body)
            };

            // Publish to event grid
            await SendFeedback(f);

            // Format the response 
            var response = new MessagingResponse().Message($"Your score: {f.Score}");
            var twiml = response.ToString();
            twiml = twiml.Replace("utf-16", "utf-8");

            // Send the response
            return new HttpResponseMessage
            {
                Content = new StringContent(twiml, Encoding.UTF8, "application/xml")
            };
        }

        private static async Task SendFeedback(Feedback f)
        {
            var topicHostName = System.Environment.GetEnvironmentVariable("TopicHostName"); 
            var topicKey = System.Environment.GetEnvironmentVariable("TopicKey"); 

            ServiceClientCredentials credentials = new TopicCredentials(topicKey);

            var client = new EventGridClient(credentials);

            var events = new List<EventGridEvent>
            {
                new EventGridEvent()
                {
                    Id = Guid.NewGuid().ToString(),
                    Data = f,
                    EventTime = DateTime.Now,
                    EventType = f.Score > 70 ? "Positive" : "Negative",
                    Subject = "eventgrid/demo/feedback",
                    DataVersion = "1.0"
                }
            };

            await client.PublishEventsAsync(
                topicHostName,
                events);
        }

        private static int GetScore(string message)
        {
            // Create and initialize an instance of the 
            // text analytics API.
            ITextAnalyticsAPI client = new TextAnalyticsAPI
            {
                AzureRegion = AzureRegions.Westus,
                SubscriptionKey = System.Environment.GetEnvironmentVariable("TextAnalyticsApiKey")
            };

            // Get the sentiment
            var results = client.Sentiment(new MultiLanguageBatchInput(
                    new List<MultiLanguageInput>()
                    {
                        new MultiLanguageInput("en", "0", message)
                    }));

            // If nothing comes back, just return 0
            if (results.Documents.Count == 0) return 0;

            // Retreive the result and format it into an integer
            var score = results.Documents[0].Score.GetValueOrDefault();
            return (int)(score * 100);
        }

        private static string GetMessageBody(string data)
        {
            var formValues = data.Split('&')
                .Select(value => value.Split('='))
                .ToDictionary(pair => Uri.UnescapeDataString(pair[0]).Replace("+", " "),
                    pair => Uri.UnescapeDataString(pair[1]).Replace("+", " "));

            return formValues["Body"];
        }
    }
}
