namespace foggycam.Models
{
    public enum PacketType
    {
        PING = 1,
        HELLO = 100,
        PING_CAMERA = 101,
        AUDIO_PAYLOAD = 102,
        START_PLAYBACK = 103,
        STOP_PLAYBACK = 104,
        CLOCK_SYNC_ECHO = 105,
        LATENCY_MEASURE = 106,
        TALKBACK_LATENCY = 107,
        METADATA_REQUEST = 108,
        OK = 200,
        ERROR = 201,
        PLAYBACK_BEGIN = 202,
        PLAYBACK_END = 203,
        PLAYBACK_PACKET = 204,
        LONG_PLAYBACK_PACKET = 205,
        CLOCK_SYNC = 206,
        REDIRECT = 207,
        TALKBACK_BEGIN = 208,
        TALKBACK_END = 209,
        METADATA = 210,
        METADATA_ERROR = 211,
        AUTHORIZE_REQUEST = 212
    }
}
