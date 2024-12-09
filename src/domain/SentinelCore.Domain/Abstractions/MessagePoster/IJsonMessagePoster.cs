namespace SentinelCore.Domain.Abstractions.MessagePoster
{
    public interface IJsonMessagePoster
    {
        void PostRestfulJsonMessage(string jsonMsg);
    }
}
