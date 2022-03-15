using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Encoders;
using WebSocketSharp.Server;

namespace WebRtcLearn
{
    class Program
    {
        private const int WEBSOCKET_PORT = 8081;
        private const string TESTE_FILENAME = "G:\\nicolas.maia\\Downloads\\teste.WAV";
        private static byte[] completeAudioBuffer = File.ReadAllBytes(TESTE_FILENAME);

        private static int numberOfChunks = 25;
        private static int chunkLength = completeAudioBuffer.Length / numberOfChunks;

        private static byte[] miniBuffer1 = new byte[chunkLength];
        private static byte[] miniBuffer2 = new byte[chunkLength];

        private static bool flipStream = false;

        static void Main()
        {
            Console.WriteLine("WebRTC Get Started");

            // Start web socket.
            Console.WriteLine("Starting web socket server...");
            var webSocketServer = new WebSocketServer(IPAddress.Any, WEBSOCKET_PORT);
            webSocketServer.AddWebSocketService<WebRTCWebSocketPeer>("/", (peer) => peer.CreatePeerConnection = () => CreatePeerConnection());
            webSocketServer.Start();

            Console.WriteLine($"Waiting for web socket connections on {webSocketServer.Address}:{webSocketServer.Port}...");

            Console.WriteLine("Press any key exit.");
            Console.ReadLine();

        }

        private static void FillBuffer(int bufferPosition, int bufferSize, byte[] destBuffer)
        {
            Buffer.BlockCopy(completeAudioBuffer, bufferPosition, destBuffer, 0, bufferSize);
        }

        private static void onConnected(AudioExtrasSource audioSource)
        {
            int i = 0;

            Stream audioStream = new MemoryStream(miniBuffer1);

            audioSource.OnSendFromAudioStreamComplete += () =>
            {
                audioStream = new MemoryStream(flipStream == true ? miniBuffer2 : miniBuffer1);
                audioSource.SendAudioFromStream(audioStream, SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum.Rate16KHz);

                FillBuffer(i * chunkLength, chunkLength, flipStream == true ? miniBuffer1 : miniBuffer2);
                flipStream = !flipStream;
                i++;
            };

            audioSource.SendAudioFromStream(audioStream, SIPSorceryMedia.Abstractions.AudioSamplingRatesEnum.Rate16KHz);
            flipStream = !flipStream;

        }
        private static Task<RTCPeerConnection> CreatePeerConnection()
        {
            var pc = new RTCPeerConnection(null);

            var audioSource = new AudioExtrasSource(new AudioEncoder());
            var audioFormat = audioSource.GetAudioSourceFormats()[0];
            audioSource.SetAudioSourceFormat(audioFormat);
            audioSource.AudioSamplePeriodMilliseconds = 20;
            MediaStreamTrack audioTrack = new MediaStreamTrack(audioSource.GetAudioSourceFormats(), MediaStreamStatusEnum.SendRecv);
            pc.addTrack(audioTrack);

            audioSource.OnAudioSourceEncodedSample += pc.SendAudio;

            pc.onconnectionstatechange += async (state) =>
            {
                Console.WriteLine($"Peer connection state change to {state}.");

                switch (state)
                {
                    case RTCPeerConnectionState.connected:
                        onConnected(audioSource);
                        break;
                    case RTCPeerConnectionState.failed:
                        pc.Close("ice disconnection");
                        break;
                    case RTCPeerConnectionState.closed:
                        await audioSource.CloseAudio();
                        break;
                }
            };

            return Task.FromResult(pc);
        }
    }
}