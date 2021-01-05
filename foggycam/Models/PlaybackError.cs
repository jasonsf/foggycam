using ProtoBuf;

namespace foggycam.Models
{
    public enum ErrorReason
    {
        ERROR_TIME_NOT_AVAILABLE = 1,
        ERROR_PROFILE_NOT_AVAILABLE = 2,
        ERROR_TRANSCODE_NOT_AVAILABLE = 3,
        PLAY_END_SESSION_COMPLETE = 128
    }

    [ProtoContract]
    public class PlaybackError
    {
        [ProtoMember(1)]
        public int session_id { get; set; }
        [ProtoMember(2)]
        public string reason { get; set; }
    }
}
