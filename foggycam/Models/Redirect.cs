using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class Redirect
    {
        [ProtoMember(1)]
        public string NewHost { get; set; }
        [ProtoMember(2)]
        public bool IsTranscode { get; set; }
    }
}
