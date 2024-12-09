using Handler.MultiOccurrence.Events;
using System.Text.Json;

namespace Handler.MultiOccurrence.ThirdParty
{
    public static class MultiOccurenceEventExtensions
    {
        public static string GenerateLesCastingNetJsonMsg(this MultiOccurenceEvent @event)
        {
            var occurenceEvent = @event;

            var lesCastingNetImage = new LesCastingNetImage()
            {
                name = occurenceEvent.EventScenePath.Replace('\\', '/')
            };

            var lesCastingNetEvent = new LesCastingNetEvent()
            {
                cameraid = occurenceEvent.DeviceName,
                warntime = DateTime.Now.ToString("s") + "z",
                piclist = new List<LesCastingNetImage>()
                {
                    lesCastingNetImage
                }
            };

            var json = JsonSerializer.Serialize(lesCastingNetEvent);

            return json;
        }
    }
}
