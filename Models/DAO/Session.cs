namespace Sample.Models.DAO
{
    // Redis용 객체
    public class Session
    {
        public long Uid { get; set; }
        public int SessionKey { get; set; }
        public sbyte ShardIndex { get; set; }
        public sbyte State { get; set; }
        public int StateValue { get; set; }
    }
}
