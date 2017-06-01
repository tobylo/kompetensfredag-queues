using System;
using Infra.Storyteller.Models;

namespace Infra.Storyteller
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new SlackClient(SlackClient.DefaultWebHookUri);
            client.SendSlackMessage(new SlackMessage
            {
                Channel = "infra-test",
                Text = "Hej",
                UserName = "Storyteller"
            });
        }
    }
}