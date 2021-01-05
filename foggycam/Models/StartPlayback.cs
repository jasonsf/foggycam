using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class StartPlayback
    {
        [ProtoMember(1)]
        public int? session_id { get; set; }
        [ProtoMember(2)]
        public int? profile { get; set; }
        [ProtoMember(3)]
        public double? start_time { get; set; }
        [ProtoMember(4)]
        public byte[]? external_ip { get; set; }
        [ProtoMember(5)]
        public int? intexternal_port { get; set; }
        [ProtoMember(6)]
        public int[]? other_profiles { get; set; }
        [ProtoMember(7)]
        public int? profile_not_found_action { get; set; }
    }
}
