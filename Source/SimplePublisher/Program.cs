using System;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using Zohan.Models;

namespace SimplePublisher
{
    class Program
    {
        private const string _textAnalyticsApi = "<text-analysis-api-key>";
        private const string _topicHostName = "<topic-name>.eastus2euap-1.eventgrid.azure.net";
        private const string _topicEndpoint = "https://<topic-name>.eastus2euap-1.eventgrid.azure.net/api/events";
        private const string _topicKey = "<topic-key>";

        private static void Main(string[] args)
        {
            const string message = "This is a very useful service";
            var score = GetScore(message);

            Console.WriteLine("Score: {0}", score);
            Console.WriteLine("Sending message...");

            var f = new Feedback
            {
                Id = Guid.NewGuid(),
                Score = score,
                Message = message
            };

            // Send using SDK
            SendSdk(f).Wait();
            
            // Send using HTTP 
            //SendHttp(f).Wait();

            Console.WriteLine("Message sent");
            Console.ReadLine();
        }

        private static async Task SendSdk(Feedback f)
        {
            var topicHostName = _topicHostName;
            var topicKey = _topicKey;

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

        private static async Task SendHttp(Feedback f)
        {
            var sas = CreateEventGridSasToken(_topicEndpoint, DateTime.Now.AddDays(1), _topicKey);

            // Instantiate an instance of the HTTP client with the 
            // event grid topic endpoint.
            var client = new HttpClient { BaseAddress = new Uri(_topicEndpoint) };

            // Configure the request headers with the content type
            // and SAS token needed to make the request.
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // Comment this out to use the SAS token
            client.DefaultRequestHeaders.Add("aeg-sas-token", sas);

            //Comment this out to use the topic key
            //client.DefaultRequestHeaders.Add("aeg-sas-key", _topicKey);

            var events = new List<GridEvent<Feedback>>
            {
                new GridEvent<Feedback>()
                {
                    Data = f,
                    Subject = "eventgrid/demo/feedback",
                    EventType = f.Score > 70 ? "Positive" : "Negative",
                    EventTime = DateTime.UtcNow,
                    Id = Guid.NewGuid().ToString()
                }
            };

            // Serialize the data
            var json = JsonConvert.SerializeObject(events);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

            // Publish grid event
            var response = await client.PostAsync(string.Empty, stringContent);
        }

        private static int GetScore(string message)
        {
            ITextAnalyticsAPI client = new TextAnalyticsAPI();
            client.AzureRegion = AzureRegions.Westus;
            client.SubscriptionKey = _textAnalyticsApi;

            var results = client.Sentiment(
                new MultiLanguageBatchInput(
                    new List<MultiLanguageInput>()
                    {
                        new MultiLanguageInput("en", "0", message)
                    }));

            if (results.Documents.Count == 0) return 0;
            var score = results.Documents[0].Score.GetValueOrDefault();
            var fixedScore = (int)(score * 100);

            return fixedScore;
        }

        private static string CreateEventGridSasToken(string resourcePath, DateTime expirationUtc, string topicKey)
        {
            const char resource = 'r';
            const char expiration = 'e';
            const char signature = 's';

            // Encode the topic resource path and expiration parameters
            var encodedResource = HttpUtility.UrlEncode(resourcePath);
            var encodedExpirationUtc = HttpUtility.UrlEncode(expirationUtc.ToString(CultureInfo.InvariantCulture));

            // Format the unsigned SAS token
            var unsignedSas = $"{resource}={encodedResource}&{expiration}={encodedExpirationUtc}";

            // Create an HMCASHA256 policy with the topic key
            using (var hmac = new HMACSHA256(Convert.FromBase64String(topicKey)))
            {
                // Encode the signature and create the fully signed URL with the
                // appropriate parameters.
                var bytes = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedSas)));
                var encodedSignature = HttpUtility.UrlEncode(bytes);
                var signedSas = $"{unsignedSas}&{signature}={encodedSignature}";

                return signedSas;
            }
        }
    }
}
