using Handler.Smuggling.Events;
using System.Text.Json;

namespace Handler.Smuggling.ThirdParty
{
    public static class SmugglingEventExtensions
    {
        public static string GenerateLesSmugglingEventJsonMsg(this SmugglingEvent @event)
        {
            var occurenceEvent = @event;

            var lesSmugglingEvent = new LesSmugglingEvent();

            var json = JsonSerializer.Serialize(lesSmugglingEvent);

            return json;
        }
    }
}
