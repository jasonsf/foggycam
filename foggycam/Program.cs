using foggycam.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using WebSocket4Net;

namespace foggycam
{
    class Program
    {
        static string ISSUE_TOKEN = "";
        static string COOKIE = "";
        static string API_KEY = "";
        static string USER_AGENT = "";
        static string NEST_API_HOSTNAME = "";
        static string CAMERA_API_HOSTNAME = "";
        static string CAMERA_AUTH_COOKIE = "";

        static WebSocket ws;

        static bool authorized = false;

        static int videoChannelId = -1;
        static int audioChannelId = -1;

        static string NEXUS_HOST = "";
        static string CAMERA_UUID = "";
        static string HOMEBOX_CAMERA_ID = "";
        static string TOKEN = "";

        static dynamic CAMERA = null;
        static dynamic CONFIG = null;

        static List<byte[]> videoStream = new List<byte[]>();
        static List<byte[]> audioStream = new List<byte[]>();

        static AutoResetEvent autoEvent = new AutoResetEvent(false);
        static Random random = new Random();

        static async Task Main(string[] args)
        {
            Console.WriteLine("[log] Reading config...");
            try
            {
                CONFIG = JsonConvert.DeserializeObject(File.ReadAllText("camera_config.json"));
                ISSUE_TOKEN = CONFIG.issue_token;
                COOKIE = CONFIG.cookie;
                API_KEY = CONFIG.api_key;
                USER_AGENT = CONFIG.user_agent;
                NEST_API_HOSTNAME = CONFIG.nest_api_hostname;
                CAMERA_API_HOSTNAME = CONFIG.camera_api_hostname;
                CAMERA_AUTH_COOKIE = CONFIG.camera_auth_cookie;

                Console.WriteLine("[log] Config loaded.");
            }
            catch
            {
                Console.WriteLine("[error] Could not read config.");
                Environment.Exit(1);
            }

            TOKEN = await GetGoogleToken(ISSUE_TOKEN, COOKIE);

            if (!string.IsNullOrEmpty(TOKEN))
            {
                Console.WriteLine($"[log] Token succesfully obtained.");

                var data = await GetCameras(TOKEN);
                CAMERA = (dynamic)data;

                NEXUS_HOST = (string)CAMERA.items[0].direct_nexustalk_host;
                CAMERA_UUID = (string)CAMERA.items[0].uuid;

                ThreadPool.QueueUserWorkItem(new WaitCallback(StartWork), autoEvent);
                autoEvent.WaitOne();
            }
            else
            {
                Console.WriteLine("[error] Could not get the token.");
            }
        }

        private async static void StartWork(object state)
        {
            SetupConnection(NEXUS_HOST + ":80/nexustalk", CAMERA_UUID, HOMEBOX_CAMERA_ID, TOKEN);

            while (true)
            {
                await Task.Delay(15000);
                var pingBuffer = PreformatData(PacketType.PING, new byte[0]);
                ws.Send(pingBuffer, 0, pingBuffer.Length);
                Console.WriteLine("[log] Sent ping.");
            }
        }

        private static void StartPlayback(dynamic cameraInfo)
        {
            var primaryProfile = StreamProfile.VIDEO_H264_2MBIT_L40;

            string[] capabilities = ((JArray)cameraInfo.capabilities).ToObject<string[]>();
            var matchingCapabilities = from c in capabilities where c.StartsWith("streaming.cameraprofile") select c;

            List<int> otherProfiles = new List<int>();
            foreach (var capability in matchingCapabilities)
            {
                var cleanCapability = capability.Replace("streaming.cameraprofile.", "");
                var successParsingEnum = Enum.TryParse(cleanCapability, out StreamProfile targetProfile);

                if (successParsingEnum)
                {
                    otherProfiles.Add((int)targetProfile);
                }
            }

            if ((bool)cameraInfo.properties["audio.enabled"])
            {
                otherProfiles.Add((int)StreamProfile.AUDIO_AAC);
            }

            StartPlayback sp = new StartPlayback();
            sp.session_id = random.Next(0, 100);
            sp.profile = (int)primaryProfile;
            sp.other_profiles = otherProfiles.ToArray<int>();

            using (MemoryStream spStream = new MemoryStream())
            {
                Serializer.Serialize(spStream, sp);
                var formattedSPOutput = PreformatData(PacketType.START_PLAYBACK, spStream.ToArray());
                ws.Send(formattedSPOutput, 0, formattedSPOutput.Length);
            }
        }

        private static void ProcessBuffers(List<byte[]> videoStream, List<byte[]> audioStream)
        {
            List<byte[]> videoBuffer = new List<byte[]>();
            List<byte[]> audioBuffer = new List<byte[]>();

            for (int i = 0; i < videoStream.Count; i++)
            {
                videoBuffer.Add(videoStream[i]);
            }
            videoStream.Clear();

            // Ideally, this needs to match the batch of video frames, so we're snapping to the video
            // buffer length as the baseline. I am not yet certain this is a good assumption, but time will tell.
            for (int i = 0; i < videoBuffer.Count; i++)
            {
                try
                {
                    audioBuffer.Add(audioStream[i]);
                }
                catch
                {
                    // There is a chance there are not enough audio packets
                    // so it's worth to pre-emptively catch this scenario.
                }
            }
            audioStream.Clear();

            DumpToFile(videoBuffer, audioBuffer, DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".mp4");
        }

        static void DumpToFile(List<byte[]> videoBuffer, List<byte[]> audioBuffer, string filename)
        {
            var startInfo = new ProcessStartInfo(CONFIG.ffmpeg_path.ToString());
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;

            var argumentBuilder = new List<string>();
            argumentBuilder.Add("-loglevel panic");
            argumentBuilder.Add("-f h264");
            argumentBuilder.Add("-i pipe:");
            argumentBuilder.Add("-c:v libx264");
            argumentBuilder.Add("-bf 0");
            argumentBuilder.Add("-pix_fmt yuv420p");
            argumentBuilder.Add("-an");
            argumentBuilder.Add(filename);

            startInfo.Arguments = string.Join(" ", argumentBuilder.ToArray());

            var _ffMpegProcess = new Process();
            _ffMpegProcess.EnableRaisingEvents = true;
            _ffMpegProcess.OutputDataReceived += (s, e) => { Debug.WriteLine(e.Data); };
            _ffMpegProcess.ErrorDataReceived += (s, e) => { Debug.WriteLine(e.Data); };

            _ffMpegProcess.StartInfo = startInfo;

            Console.WriteLine($"[log] Starting write to {filename}...");

            _ffMpegProcess.Start();
            _ffMpegProcess.BeginOutputReadLine();
            _ffMpegProcess.BeginErrorReadLine();

            for (int i = 0; i < videoBuffer.Count; i++)
            {
                _ffMpegProcess.StandardInput.BaseStream.Write(videoBuffer[i], 0, videoBuffer[i].Length);
            }

            _ffMpegProcess.StandardInput.BaseStream.Close();

            Console.WriteLine($"[log] Writing of {filename} completed.");
        }

        static void SetupConnection(string host, string cameraUuid, string deviceId, string token)
        {
            var tc = new TokenContainer();
            tc.olive_token = token;

            using (var mStream = new MemoryStream())
            {
                Serializer.Serialize(mStream, tc);

                var helloRequestBuffer = new HelloContainer();
                helloRequestBuffer.protocol_version = 3;
                helloRequestBuffer.uuid = cameraUuid;
                helloRequestBuffer.device_id = deviceId;
                helloRequestBuffer.require_connected_camera = false;
                helloRequestBuffer.user_agent = USER_AGENT;
                helloRequestBuffer.client_type = 3;
                helloRequestBuffer.authorize_request = mStream.GetBuffer();

                using (var finalMStream = new MemoryStream())
                {
                    Serializer.Serialize(finalMStream, helloRequestBuffer);

                    var dataBuffer = PreformatData(PacketType.HELLO, finalMStream.ToArray());
                    var target = $"wss://{host}";
                    Console.WriteLine($"[log] Setting up connection to {target}...");

                    ws = new WebSocket(target, sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls)
                    {
                        EnableAutoSendPing = true,
                        AutoSendPingInterval = 5
                    };
                    ws.Security.AllowNameMismatchCertificate = true;
                    ws.Security.AllowUnstrustedCertificate = true;
                    ws.DataReceived += Ws_DataReceived;
                    ws.Error += Ws_Error;

                    ws.Opened += (s, e) =>
                    {
                        ws.Send(dataBuffer, 0, dataBuffer.Length);
                    };
                    ws.Open();
                }
            }
        }

        static byte[] PreformatData(PacketType packetType, byte[] buffer)
        {
            byte[] finalBuffer;
            if (packetType == PacketType.LONG_PLAYBACK_PACKET)
            {
                var requestBuffer = new byte[5];
                requestBuffer[0] = (byte)packetType;
                var byteData = BitConverter.GetBytes((uint)buffer.Length);
                Array.Reverse(byteData);

                Buffer.BlockCopy(byteData, 0, requestBuffer, 1, byteData.Length);
                finalBuffer = new byte[requestBuffer.Length + buffer.Length];
                requestBuffer.CopyTo(finalBuffer, 0);
                buffer.CopyTo(finalBuffer, requestBuffer.Length);
            }
            else
            {
                var requestBuffer = new byte[3];
                requestBuffer[0] = (byte)packetType;
                var byteData = BitConverter.GetBytes((ushort)buffer.Length);
                Array.Reverse(byteData);

                Buffer.BlockCopy(byteData, 0, requestBuffer, 1, byteData.Length);
                finalBuffer = new byte[requestBuffer.Length + buffer.Length];
                requestBuffer.CopyTo(finalBuffer, 0);
                buffer.CopyTo(finalBuffer, requestBuffer.Length);
            }

            return finalBuffer;
        }

        public static byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        private static void Ws_DataReceived(object sender, WebSocket4Net.DataReceivedEventArgs e)
        {
            ProcessReceivedData(e.Data);
        }

        private static void ProcessReceivedData(byte[] buffer)
        {
            int type = buffer[0];
            try
            {
                Debug.WriteLine("Received packed type: " + (PacketType)type);

                int headerLength;
                uint length;
                if ((PacketType)type == PacketType.LONG_PLAYBACK_PACKET)
                {
                    headerLength = 5;
                    var lengthBytes = new byte[4];
                    Buffer.BlockCopy(buffer, 1, lengthBytes, 0, lengthBytes.Length);
                    Array.Reverse(lengthBytes);
                    length = BitConverter.ToUInt32(lengthBytes);
                    Console.WriteLine("[log] Declared long playback packet length: " + length);
                }
                else
                {
                    headerLength = 3;
                    var lengthBytes = new byte[2];
                    Buffer.BlockCopy(buffer, 1, lengthBytes, 0, lengthBytes.Length);
                    Array.Reverse(lengthBytes);
                    length = BitConverter.ToUInt16(lengthBytes);
                    Console.WriteLine("[log] Declared playback packet length: " + length);
                }

                var payloadEndPosition = length + headerLength;

                Index top = headerLength;
                Index bottom = (Index)payloadEndPosition;

                var rawPayload = buffer[top..bottom];
                using (var dStream = new MemoryStream(rawPayload))
                {
                    HandlePacketData((PacketType)type, rawPayload);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("[error] Error with packet capture.");
                Console.WriteLine(ex.Message);
            }

        }

        private static void HandlePacketData(PacketType type, byte[] rawPayload)
        {
            switch (type)
            {
                case PacketType.OK:
                    authorized = true;
                    StartPlayback(CAMERA.items[0]);
                    break;
                case PacketType.PING:
                    Console.WriteLine("[log] Ping.");
                    break;
                case PacketType.PLAYBACK_BEGIN:
                    HandlePlaybackBegin(rawPayload);
                    break;
                case PacketType.PLAYBACK_PACKET:
                    HandlePlayback(rawPayload);
                    break;
                case PacketType.REDIRECT:
                    authorized = false;
                    HandleRedirect(rawPayload);
                    break;
                case PacketType.ERROR:
                    authorized = false;
                    ws.Close();
                    HandleError(rawPayload);
                    break;
                default:
                    Console.WriteLine(type);
                    Console.WriteLine("[streamer] Unknown type.");
                    break;
            }
        }

        private static void HandleRedirect(byte[] rawPayload)
        {
            ws.Close();

            using (MemoryStream stream = new MemoryStream(rawPayload))
            {
                var packet = Serializer.Deserialize<Redirect>(stream);
                SetupConnection(packet.new_host, CAMERA_UUID, HOMEBOX_CAMERA_ID, TOKEN);
            }
        }

        private static void HandleError(byte[] rawPayload)
        {
            using (MemoryStream stream = new MemoryStream(rawPayload))
            {
                var packet = Serializer.Deserialize<PlaybackError>(stream);

                Console.WriteLine($"[error] The capture errored out for the following reason: {packet.reason}");
            }
        }

        private static void HandlePlayback(byte[] rawPayload)
        {
            using (MemoryStream stream = new MemoryStream(rawPayload))
            {
                var packet = Serializer.Deserialize<PlaybackPacket>(stream);

                if (packet.channel_id == videoChannelId)
                {
                    Console.WriteLine("[log] Video packet received.");
                    byte[] h264Header = { 0x00, 0x00, 0x00, 0x01 };
                    var writingBlock = new byte[h264Header.Length + packet.payload.Length];
                    h264Header.CopyTo(writingBlock, 0);
                    packet.payload.CopyTo(writingBlock, h264Header.Length);

                    videoStream.Add(writingBlock);
                }
                else if (packet.channel_id == audioChannelId)
                {
                    Console.WriteLine("[log] Audio packet received.");
                    audioStream.Add(packet.payload);
                }
                else
                {
                    Console.WriteLine("[log] Unknown channel: " + packet.channel_id);
                }
            }

            Console.WriteLine($"[log] Video buffer length: {videoStream.Count}");
            Console.WriteLine($"[log] Socket state: {ws.State}");
            // Once we reach a certain threshold, let's make sure that we flush the buffer.
            if (videoStream.Count > 1000)
            {
                ProcessBuffers(videoStream, audioStream);
            }
        }

        private static void HandlePlaybackBegin(byte[] rawPayload)
        {
            using (MemoryStream stream = new MemoryStream(rawPayload))
            {
                var packet = Serializer.Deserialize<PlaybackBegin>(stream);

                foreach (var registeredStream in packet.channels)
                {
                    if ((CodecType)registeredStream.codec_type == CodecType.H264)
                    {
                        videoChannelId = registeredStream.channel_id;
                    }
                    else if ((CodecType)registeredStream.codec_type == CodecType.AAC)
                    {
                        audioChannelId = registeredStream.channel_id;
                    }
                }
            }
        }

        private async static void Ws_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            Console.WriteLine("[log] Socket errored out.");
            Console.WriteLine(e.Exception.Message);
            Console.WriteLine(e.Exception.InnerException);
            Console.WriteLine(e.Exception.GetType());

            authorized = false; 
        }


        static async Task<object> GetCameras(string token)
        {
            var httpClient = new HttpClient();
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{CAMERA_API_HOSTNAME}/api/cameras.get_owned_and_member_of_with_properties"),
                Method = HttpMethod.Get,
                Headers =
                {
                    { "Cookie", $"user_token={token}" },
                    { "User-Agent", USER_AGENT },
                    { "Referer", NEST_API_HOSTNAME }
                }
            };

            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var rawResponse = await response.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject(rawResponse);
            }

            return null;
        }

        static async Task<string> GetGoogleToken(string issueToken, string cookie)
        {
            var tokenUri = new Uri(issueToken);
            string referrerDomain;
            try
            {
                referrerDomain = HttpUtility.ParseQueryString(tokenUri.Query).Get("ss_domain");
            }
            catch (Exception ex)
            {
                throw new ArgumentException("[error] Could not parse the referrer domain out of the token.");
            }

            try
            {
                var httpClient = new HttpClient();
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(issueToken),
                    Method = HttpMethod.Get,
                    Headers =
                    {
                        { "Sec-Fetch-Mode", "cors" },
                        { "User-Agent", USER_AGENT },
                        { "X-Requested-With", "XmlHttpRequest" },
                        { "Referer", "https://accounts.google.com/o/oauth2/iframe" },
                        { "cookie", cookie }
                    }
                };

                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    dynamic rawResponse = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                    var accessToken = rawResponse.access_token;

                    var parameters = new Dictionary<string, string> { { "embed_google_oauth_access_token", "true" }, { "expire_after", "3600s" }, { "google_oauth_access_token", $"{ accessToken}" }, { "policy_id", "authproxy-oauth-policy" } };
                    var encodedContent = new FormUrlEncodedContent(parameters);

                    request = new HttpRequestMessage
                    {
                        RequestUri = new Uri("https://nestauthproxyservice-pa.googleapis.com/v1/issue_jwt"),
                        Method = HttpMethod.Post,
                        Content = encodedContent,
                        Headers =
                        {
                            { "Authorization", $"Bearer {accessToken}" },
                            { "User-Agent", USER_AGENT },
                            { "x-goog-api-key", API_KEY },
                            { "Referer", referrerDomain }
                        }
                    };

                    response = await httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        rawResponse = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                        return rawResponse.jwt;
                    }
                    else
                    {
                        Console.WriteLine(response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Could not perform Google authentication. {ex.Message}");
            }

            return null;
        }
    }
}
