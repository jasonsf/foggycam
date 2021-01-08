using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class TokenContainer
    {
        [ProtoMember(1)]
        public string? SessionToken { get; set; }
        [ProtoMember(2)]
        public string? WwnAccessToken { get; set; }
        [ProtoMember(3)]
        public string? ServiceAccessKey { get; set; }
        [ProtoMember(4)]
        public string? OliveToken { get; set; }
    }
}
