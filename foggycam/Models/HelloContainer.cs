using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class HelloContainer
    {
        [ProtoMember(1)]
        public int? ProtocolVersion { get; set; }
        [ProtoMember(2)]
        public string? Uuid { get; set; }
        [ProtoMember(3)]
        public bool? RequireConnectedCamera { get; set; }
        [ProtoMember(4)]
        public string? SessionToken { get; set; }
        [ProtoMember(5)]
        public bool? IsCamera { get; set; }
        [ProtoMember(6)]
        public string? DeviceId { get; set; }
        [ProtoMember(7)]
        public string? UserAgent { get; set; }
        [ProtoMember(8)]
        public string? ServiceAccessKey { get; set; }
        [ProtoMember(9)]
        public int? ClientType { get; set; }
        [ProtoMember(10)]
        public string? WwnAccessToken { get; set; }
        [ProtoMember(11)]
        public string? EncryptedDeviceId { get; set; }
        [ProtoMember(12)]
        public byte[]? AuthorizeRequest { get; set; }
        [ProtoMember(13)]
        public string? ClientIpAddress { get; set; }
        [ProtoMember(16)]
        public bool? RequireOwnerServer { get; set; }
    }
}
