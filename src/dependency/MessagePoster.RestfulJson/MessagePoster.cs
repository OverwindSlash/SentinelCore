using SentinelCore.Domain.Abstractions.MessagePoster;
using System.Text;

namespace MessagePoster.RestfulJson
{
    public class MessagePoster : IJsonMessagePoster
    {
        private string _url;
        private readonly Dictionary<string, string> _preferences;

        public MessagePoster(string url, Dictionary<string, string> preferences)
        {
            _url = url;
            _preferences = preferences;
        }

        public void PostRestfulJsonMessage(string jsonMsg)
        {
            var content = new StringContent(jsonMsg, Encoding.UTF8, "application/json");

            Task.Run(async () =>
            {
                using var client = new HttpClient();
                HttpResponseMessage response = await client.PostAsync(_url, content);

                if (response.IsSuccessStatusCode)
                {
                    //Console.WriteLine("Send message successful！");
                    string result = await response.Content.ReadAsStringAsync();
                    //Console.WriteLine("response：" + result);
                }
                else
                {
                    //Console.WriteLine("Send message failed！" + response.StatusCode);
                }
            });
        }
    }
}
