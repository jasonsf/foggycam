using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class PlaybackBegin
    {
        [ProtoMember(1)]
        public int? SessionId { get; set; }
        [ProtoMember(2)]
        public Stream[]? Channels { get; set; }
        [ProtoMember(3)]
        public byte[]? SrtpMasterKey { get; set; }
        [ProtoMember(4)]
        public byte[]? SrtpMasterSalt { get; set; }
        [ProtoMember(5)]
        public int? FacKVal { get; set; }
        [ProtoMember(6)]
        public int? FacNVal { get; set; }
    }
}
