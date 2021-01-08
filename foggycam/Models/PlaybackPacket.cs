using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class PlaybackPacket
    {
        [ProtoMember(1)]
        public int SessionId { get; set; }
        [ProtoMember(2)]
        public int ChannelId { get; set; }
        [ProtoMember(3)]
        public long TimestampDelta { get; set; }
        [ProtoMember(4)]
        public byte[] Payload { get; set; }
        [ProtoMember(5)]
        public int LatecyRtpSequence { get; set; }
        [ProtoMember(6)]
        public int LatencyRtpSsrc { get; set; }
        [ProtoMember(7)]
        public int[] DirectorsCutRegions { get; set; }
    }
}
