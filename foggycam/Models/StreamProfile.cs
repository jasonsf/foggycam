namespace foggycam.Models
{
    public enum StreamProfile
    {
        AUDIO_AAC = 3,
        AUDIO_SPEEX = 4,
        AUDIO_OPUS = 5,
        AUDIO_OPUS_LIVE = 13,
        VIDEO_H264_50KBIT_L12 = 6,
        VIDEO_H264_530KBIT_L31 = 7,
        VIDEO_H264_100KBIT_L30 = 8,
        VIDEO_H264_2MBIT_L40 = 9,
        VIDEO_H264_50KBIT_L12_THUMBNAIL = 10,
        META = 11,
        DIRECTORS_CUT = 12,
        VIDEO_H264_L31 = 14,
        VIDEO_H264_L40 = 15,
        AVPROFILE_MOBILE_1 = 1,
        AVPROFILE_HD_MAIN_1 = 2
    }
}
