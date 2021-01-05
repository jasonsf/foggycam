using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class Stream
    {
        [ProtoMember(1)]
        public int channel_id { get; set; }
        [ProtoMember(2)]
        public int codec_type { get; set; }
        [ProtoMember(3)]
        public int sample_rate { get; set; }
        [ProtoMember(4)]
        public byte[] private_data { get; set; }
        [ProtoMember(5)]
        public double start_time { get; set; }
        [ProtoMember(6)]
        public int udp_ssrc { get; set; }
        [ProtoMember(7)]
        public int rtp_start_time { get; set; }
        [ProtoMember(8)]
        public int profile { get; set; }
    }
}
