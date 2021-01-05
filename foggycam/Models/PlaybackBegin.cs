using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class PlaybackBegin
    {
        [ProtoMember(1)]
        public int? session_id { get; set; }
        [ProtoMember(2)]
        public Stream[]? channels { get; set; }
        [ProtoMember(3)]
        public byte[]? srtp_master_key { get; set; }
        [ProtoMember(4)]
        public byte[]? srtp_master_salt { get; set; }
        [ProtoMember(5)]
        public int? fac_k_val { get; set; }
        [ProtoMember(6)]
        public int? fac_n_val { get; set; }
    }
}
