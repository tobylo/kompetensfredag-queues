using Newtonsoft.Json;

namespace Infra.Storyteller.Models
{
    public sealed class SlackMessage
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("username")]
        public string UserName { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("icon_emoji")]
        public string Icon
        {
            get { return ":computer:"; }
        }
    }
}