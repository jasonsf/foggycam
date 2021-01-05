using ProtoBuf;

namespace foggycam.Models
{
    [ProtoContract]
    public class Redirect
    {
        [ProtoMember(1)]
        public string new_host { get; set; }
        [ProtoMember(2)]
        public bool is_transcode { get; set; }
    }
}
