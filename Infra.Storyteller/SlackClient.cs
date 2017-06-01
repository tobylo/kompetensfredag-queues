using System;
using System.Net;
using Infra.Storyteller.Models;
using Newtonsoft.Json;

namespace Infra.Storyteller
{
    public sealed class SlackClient
    {
        public static readonly Uri DefaultWebHookUri =
            new Uri("");

        private readonly Uri _webHookUri;

        public SlackClient(Uri webHookUri)
        {
            this._webHookUri = webHookUri;
        }

        public void SendSlackMessage(SlackMessage message)
        {
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                byte[] request = System.Text.Encoding.UTF8.GetBytes("payload=" + JsonConvert.SerializeObject(message));
                byte[] response = webClient.UploadData(this._webHookUri, "POST", request);

                // ...handle response...
            }
        }

    }
}