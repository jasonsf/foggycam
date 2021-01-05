using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class HelloContainer
    {
        [ProtoMember(1)]
        public int? protocol_version { get; set; }
        [ProtoMember(2)]
        public string? uuid { get; set; }
        [ProtoMember(3)]
        public bool? require_connected_camera { get; set; }
        [ProtoMember(4)]
        public string? session_token { get; set; }
        [ProtoMember(5)]
        public bool? is_camera { get; set; }
        [ProtoMember(6)]
        public string? device_id { get; set; }
        [ProtoMember(7)]
        public string? user_agent { get; set; }
        [ProtoMember(8)]
        public string? service_access_key { get; set; }
        [ProtoMember(9)]
        public int? client_type { get; set; }
        [ProtoMember(10)]
        public string? wwn_access_token { get; set; }
        [ProtoMember(11)]
        public string? encrypted_device_id { get; set; }
        [ProtoMember(12)]
        public byte[]? authorize_request { get; set; }
        [ProtoMember(13)]
        public string? client_ip_address { get; set; }
        [ProtoMember(16)]
        public bool? require_owner_server { get; set; }
    }
}
