using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class TokenContainer
    {
        [ProtoMember(1)]
        public string? session_token { get; set; }
        [ProtoMember(2)]
        public string? wwn_access_token { get; set; }
        [ProtoMember(3)]
        public string? service_access_key { get; set; }
        [ProtoMember(4)]
        public string? olive_token { get; set; }
    }
}
