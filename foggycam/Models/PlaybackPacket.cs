using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class PlaybackPacket
    {
        [ProtoMember(1)]
        public int session_id { get; set; }
        [ProtoMember(2)]
        public int channel_id { get; set; }
        [ProtoMember(3)]
        public long timestamp_delta { get; set; }
        [ProtoMember(4)]
        public byte[] payload { get; set; }
        [ProtoMember(5)]
        public int latency_rtp_sequence { get; set; }
        [ProtoMember(6)]
        public int latency_rtp_ssrc { get; set; }
        [ProtoMember(7)]
        public int[] directors_cut_regions { get; set; }
    }
}
