using Media.Rtp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RtspDemo
{
    public partial class Form1 : Form
    {
        public Media.Rtsp.RtspClient RtspClient;
        System.IO.FileInfo rtspLog = new System.IO.FileInfo("rtpFrame" + DateTime.UtcNow.ToFileTimeUtc() + ".txt");
        Media.Common.Loggers.FileLogger logWriter;
        private Thread RtspThread;
        FileStream WriteData;
        bool IsIFrame = true;

        public Form1()
        {
            InitializeComponent();
            this.Load += new EventHandler(Form1_Load);
            logWriter = new Media.Common.Loggers.FileLogger(rtspLog);
        }

        void Form1_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RtspClient.StopPlaying();
            RtspThread.Abort();
            WriteData.Close();
            textBox1.AppendText("Rtsp Server Stopping...\r\n");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
            string path = AppDomain.CurrentDomain.BaseDirectory + "video.h264";
            WriteData = new FileStream(path, FileMode.Append, FileAccess.Write);
            RtspThread = new Thread(new ThreadStart(RtspClientTests));
            RtspThread.Start();
            textBox1.AppendText("Rtsp Server Starting...\r\n");
        }

        public void RtspClientTests()
        {
            foreach (var TestObject in new[] 
            {
                 new
                {
                    //Uri = "rtsp://218.204.223.237:554/live/1/66251FC11353191F/e7ooqwcfbqjoo80j.sdp",
                    Uri="rtsp://218.204.223.237:554/live/1/0547424F573B085C/gsfp90ef4k0a6iap.sdp",
                    Creds = default(System.Net.NetworkCredential),
                    Proto = (Media.Rtsp.RtspClient.ClientProtocolType?)null,
                }
            })
            {
                Media.Rtsp.RtspClient.ClientProtocolType? proto = TestObject.Proto;
                try
                {
                    TestRtspClient(TestObject.Uri, TestObject.Creds, proto);
                }
                catch (Exception ex)
                {
                    //writeError(ex);
                }
                ConsoleKey next = Console.ReadKey(true).Key;
            }
        }

        private string Bytes10To16(byte[] bytes)
        {
            string value = "";
            int i = 0;
            foreach (byte b in bytes)
            {
                if (i > 6)
                {
                    return value;
                }
                value += b.ToString("x8") + "\t";
                i++;
            }
            return value;
        }

        protected void Client_RtpFrameChanged(object sender, RtpFrame rtpFrame)
        {
            if (rtpFrame.PayloadTypeByte == 96)
            {
                Media.Rtsp.Server.MediaTypes.RFC6184Media.RFC6184Frame h264 =
                    new Media.Rtsp.Server.MediaTypes.RFC6184Media.RFC6184Frame(rtpFrame);
                h264.Depacketize(rtpFrame);
                System.IO.MemoryStream memory = h264.Buffer;
                byte[] data = memory.ToArray();
            //    if (data == null || data.Length <= 0)
            //    {
            //        return;
            //    }
            //    if (IsIFrame)
            //    {
            //        if (data[4] == 103)
            //        {
            //            IsIFrame = false;
            //            WriteData.Write(data, 0, data.Length);
            //            return;
            //        }
            //    }
            //    WriteData.Write(data, 0, data.Length);
            //    WriteData.Flush(true);
                string str = Bytes10To16(data);
                if (string.IsNullOrEmpty(str))
                {
                    return;
                }
                textBox1.AppendText(str + "\n");
            }
        }

        private void TestRtspClient(string location, System.Net.NetworkCredential cred = null, Media.Rtsp.RtspClient.ClientProtocolType? protocol = null)
        {
            //For display

            int rtspInterleaved = 0;
            //For allowing the test to run in an automated manner or otherwise some output is disabled.
            bool shouldStop = false;
            //Define a RtspClient
            Media.Rtsp.RtspClient client = null;
            //If There was a location given

            int bufferSize = Media.Rtsp.RtspClient.DefaultBufferSize;
            //Using a new Media.RtspClient optionally with a specified buffer size (0 indicates use the MTU if possible)
            using (client = new Media.Rtsp.RtspClient(location, protocol, bufferSize))
            {
                //Use the credential specified
                if (cred != null) client.Credential = cred;
                //FileInfo to represent the log
                System.IO.FileInfo rtspLog = new System.IO.FileInfo("rtspLog" + DateTime.UtcNow.ToFileTimeUtc() + ".log.txt");
                //Create a log to write the responses to.
                using (Media.Common.Loggers.FileLogger logWriter = new Media.Common.Loggers.FileLogger(rtspLog))
                {
                    //Attach the logger to the client
                    client.Logger = logWriter;
                    //Attach the Rtp logger, should possibly have IsShared on ILogger.
                    using (Media.Common.Loggers.ConsoleLogger consoleLogger = new Media.Common.Loggers.ConsoleLogger())
                    {
                        client.Client.Logger = consoleLogger;
                        //Define a connection eventHandler
                        Media.Rtsp.RtspClient.RtspClientAction connectHandler = null;
                        connectHandler = (sender, args) =>
                        {
                            if (client == null || client.IsDisposed) return;
                            //Increase ReadTimeout here if required
                            //client.SocketReadTimeout 
                            try
                            {
                                Console.WriteLine("\t*****************\nConnected to :" + client.CurrentLocation);
                                Console.WriteLine("\t*****************\nConnectionTime:" + client.ConnectionTime);
                                //If the client is not already playing, and the client hasn't received any messages yet then start playing
                                if (false == client.IsPlaying && client.MessagesReceived == 0)
                                {
                                    Console.WriteLine("\t*****************\nStarting Playback of :" + client.CurrentLocation);
                                    //Try to start listening
                                    client.StartPlaying();
                                    Console.WriteLine("\t*****************\nStartedListening to :" + client.CurrentLocation);
                                }
                            }
                            catch (Exception ex)
                            {
                                //writeError(ex); 
                                shouldStop = true;
                            }
                        };
                        //Attach it
                        client.OnConnect += connectHandler;
                        //Define an event to handle 'InterleavedData'
                        //This is usually not required.
                        //You can use this event for large or incomplete packet data or othewise as required from the RtspClient.
                        //Under Rtp Transport this event is used propegate data which does not belong to Rtp from the RtpClient to the RtspClient.
                        Media.Rtp.RtpClient.InterleaveHandler rtpInterleave = (sender, data, offset, count) =>
                        {
                            ++rtspInterleaved;
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("\tInterleaved=>" + count + " Bytes");
                            Console.WriteLine("\tInterleaved=>" + Encoding.ASCII.GetString(data, offset, count));
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                        };

                        //Define an event to handle Rtsp Request events
                        client.OnRequest += (sender, message) =>
                        {
                            if (message != null)
                            {
                                string output = "Client Sent " + message.MessageType + " :" + message.ToString();
                                logWriter.Log(output);
                                Console.ForegroundColor = ConsoleColor.DarkCyan;
                                Console.WriteLine(output);
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                            }
                            else
                            {
                                string output = "Null Response";
                                logWriter.Log(output);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(output);
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                            }
                        };

                        //Define an event to handle Disconnection from the RtspClient.
                        client.OnDisconnect += (sender, args) => Console.WriteLine("\t*****************Disconnected from :" + client.CurrentLocation);

                        //Define an event to handle Rtsp Response events
                        //Note that this event is also used to handle `pushed` responses which the server sent to the RtspClient without a request.
                        //This can be determined when request is null OR response.MessageType == Request
                        client.OnResponse += (sender, request, response) =>
                        {
                            //Track null and unknown responses
                            if (response != null)
                            {
                                string output = "Client Received " + response.MessageType + " :" + response.ToString();
                                logWriter.Log(output);
                                Console.ForegroundColor = ConsoleColor.DarkGreen;
                                Console.WriteLine(output);
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                            }
                            else
                            {
                                string output = "Null Response";
                                if (request != null)
                                {
                                    if (request.MessageType == Media.Rtsp.RtspMessageType.Request)
                                        output = "Client Received Server Sent " + request.MessageType + " :" + request.ToString();
                                    else
                                        output = "Client Received " + request.MessageType + " :" + request.ToString();
                                }

                                logWriter.Log(output);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(output);
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                            }
                        };

                        //Define an event to handle what happens when a Media is played.
                        //args are null if the event applies to all all playing Media.
                        client.OnPlay += (sender, args) =>
                        {
                            //There is a single intentional duality in the design of the pattern utilized for the RtpClient such that                    
                            //client.Client.MaximumRtcpBandwidthPercentage = 25;
                            ///It SHOULD also subsequently limit the maximum amount of CPU the client will be able to use

                            if (args != null)
                            {
                                Console.WriteLine("\t*****************Playing `" + args.ToString() + "`");
                                return;
                            }

                            //If there is no sdp we have not attached events yet
                            if (false == System.IO.File.Exists("current.sdp"))
                            {
                                //Write the sdp that we are playing
                                System.IO.File.WriteAllText("current.sdp", client.SessionDescription.ToString());
                            }

                            //Indicate if LivePlay
                            if (client.LivePlay)
                            {
                                Console.WriteLine("\t*****************Playing from Live Source");
                            }
                            else
                            {
                                //Indicate if StartTime is found
                                if (client.StartTime.HasValue)
                                {
                                    Console.WriteLine("\t*****************Media Start Time:" + client.StartTime);
                                }

                                //Indicate if EndTime is found
                                if (client.EndTime.HasValue)
                                {
                                    Console.WriteLine("\t*****************Media End Time:" + client.EndTime);
                                }
                            }

                            //Show context information
                            foreach (Media.Rtp.RtpClient.TransportContext tc in client.Client.GetTransportContexts())
                            {
                                Console.WriteLine("\t*****************Local Id " + tc.SynchronizationSourceIdentifier + "\t*****************Remote Id " + tc.RemoteSynchronizationSourceIdentifier);
                            }
                        };

                        //Define an event to handle what happens when a Media is paused.
                        //args are null if the event applies to all all playing Media.
                        client.OnPause += (sender, args) =>
                        {
                            if (args != null) Console.WriteLine("\t*****************Pausing Playback `" + args.ToString() + "`(Press Q To Exit)");
                            else Console.WriteLine("\t*****************Pausing All Playback. (Press Q To Exit)");
                        };

                        //Define an event to handle what happens when a Media is stopped.
                        //args are null if the event applies to all all playing Media.
                        client.OnStop += (sender, args) =>
                        {
                            if (args != null) Console.WriteLine("\t*****************Stopping Playback of `" + args.ToString() + "`(Press Q To Exit)");
                            else Console.WriteLine("\t*****************Stopping All Playback. (Press Q To Exit)");
                        };

                        //Attach a logger
                        client.Logger = new Media.Common.Loggers.ConsoleLogger();
                        //Attach RtpFrame events
                        client.Client.RtpFrameChanged += new RtpClient.RtpFrameHandler(Client_RtpFrameChanged);
                        //Connect the RtspClient
                        client.Connect();
                        RtspClient = client;
                        
                        //Indicate waiting and commands the program accepts
                        TimeSpan playingfor = TimeSpan.Zero;
                        DateTime lastNotice = DateTime.MinValue;
                        while (false == shouldStop)
                        {
                            System.Threading.Thread.Sleep(0);
                            if (client.IsPlaying)
                            {
                                playingfor = (DateTime.UtcNow - (client.StartedPlaying ?? lastNotice));
                                if ((DateTime.UtcNow - lastNotice).TotalSeconds > 1)
                                {
                                    if (client.IsPlaying)
                                    {
                                        Console.WriteLine("Client Playing for :" + playingfor.ToString());

                                    }
                                    if (false == client.LivePlay && client.EndTime.HasValue)
                                    {
                                        var remaining = playingfor.Subtract(client.EndTime.Value).Negate();
                                        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                                        Console.WriteLine("Remaining Time in media:" + remaining.ToString());
                                    }
                                    lastNotice = DateTime.UtcNow + TimeSpan.FromSeconds(1);

                                }
                            }
                            else if ((DateTime.UtcNow - lastNotice).TotalSeconds > 1)
                            {
                                Console.WriteLine("Client Not Playing");
                                lastNotice = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                            }
                        }
                    }
                }
            }
        }
    }
}
