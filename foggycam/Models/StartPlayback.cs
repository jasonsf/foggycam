using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class StartPlayback
    {
        [ProtoMember(1)]
        public int? SessionId { get; set; }
        [ProtoMember(2)]
        public int? Profile { get; set; }
        [ProtoMember(3)]
        public double? StartTime { get; set; }
        [ProtoMember(4)]
        public byte[]? ExternalIp { get; set; }
        [ProtoMember(5)]
        public int? ExternalPort { get; set; }
        [ProtoMember(6)]
        public int[]? OtherProfiles { get; set; }
        [ProtoMember(7)]
        public int? ProfileNotFoundAction { get; set; }
    }
}
