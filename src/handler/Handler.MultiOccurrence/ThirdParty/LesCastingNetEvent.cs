namespace Handler.MultiOccurrence.ThirdParty
{
    public class LesCastingNetImage
    {
        public string name { get; set; }
    }

    public class LesCastingNetEvent
    {
        public string cameraid { get; set; }
        public string warntime { get; set; }

        public List<LesCastingNetImage> piclist { get; set; }
    }
}
