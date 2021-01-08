using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class Stream
    {
        [ProtoMember(1)]
        public int ChannelId { get; set; }
        [ProtoMember(2)]
        public int CodecType { get; set; }
        [ProtoMember(3)]
        public int SampleRate { get; set; }
        [ProtoMember(4)]
        public byte[] PrivateData { get; set; }
        [ProtoMember(5)]
        public double StartTime { get; set; }
        [ProtoMember(6)]
        public int UdpSsrc { get; set; }
        [ProtoMember(7)]
        public int RtpStartTime { get; set; }
        [ProtoMember(8)]
        public int Profile { get; set; }
    }
}
