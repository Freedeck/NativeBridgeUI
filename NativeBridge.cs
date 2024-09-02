using System;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Security.Cryptography;
using System.Reflection.Metadata.Ecma335;
using CSCore.CoreAudioAPI;
using System.Diagnostics;
using System.Numerics;
namespace NativeBridge
{
    public class Commands
    {
        public enum CommandOutput
        {
            SUCCESS,
            WARN,
            ERROR,
            FAIL
        }
    }
    class GenericCommand
    {
        public Commands.CommandOutput Status { get; set; }
        public string? Message { get; set; }
        public bool Error { get; set; }
        public GenericCommand()
        {
            Status = Commands.CommandOutput.SUCCESS;
            Error = Status != Commands.CommandOutput.SUCCESS;
        }
        public GenericCommand(Stream message)
        {
            Status = Commands.CommandOutput.SUCCESS;
            Error = Status != Commands.CommandOutput.SUCCESS;
            Message = message.ToString();
        }
        public GenericCommand(string message)
        {
            Status = Commands.CommandOutput.SUCCESS;
            Error = Status != Commands.CommandOutput.SUCCESS;
            Message = message;
        }
    }

    class VolumeCommand : GenericCommand
    {
        public int Volume { get; set; }
        public VolumeCommand()
        {
            Volume = 0;
        }
        public VolumeCommand(int volume)
        {
            Volume = volume;
        }
    }

    class SystemControl
    {
        public static void setVolumeApp(string procName, int vol)
        {
            using var device = MMDeviceEnumerator.DefaultAudioEndpoint(
    DataFlow.Render,
    Role.Multimedia);

            using var master = AudioEndpointVolume.FromDevice(device);

            using var sessionManager = AudioSessionManager2.FromMMDevice(device);
            using var enumerator = sessionManager.GetSessionEnumerator();

            //skipping first as it seems to be some default system session
            int i = 0;

            foreach (var sessionControl in enumerator.Skip(1))
            {
                Console.WriteLine($"Session {i++}");
                using var sessionControl2 = sessionControl.QueryInterface<AudioSessionControl2>();

                using var process = Process.GetProcessById(sessionControl2.ProcessID);
                using var volume = sessionControl.QueryInterface<SimpleAudioVolume>();

                Console.WriteLine($"Volume of {process.ProcessName} : {volume.MasterVolume}");

                if (process.ProcessName == procName)
                {
                    volume.MasterVolume = vol / 100.0f;
                }

            }
        }
        public static void setVolumeMaster(int vol)
        {
            using var device = MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            using var master = AudioEndpointVolume.FromDevice(device);
            master.MasterVolumeLevelScalar = vol / 100.0f;
        }
    }
    class HTTP
    {
        public static HttpListener listener;
        public static string url = "http://localhost:5756/";
        public static int pageViews = 0;
        public static int requestCount = 0;
        public static bool runServer = true;


        public static async Task HandleIncomingConnections()
        {
            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995) // ERROR_OPERATION_ABORTED
                {
                    break;
                }

                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;


                GenericCommand cmdOut = new GenericCommand("ZHi");
                byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cmdOut));
                if (!runServer)
                {
                    listener.Close();
                    resp.ContentType = "application/json";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;
                    resp.AddHeader("Access-Control-Allow-Origin", "*");
                    resp.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                    resp.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                    resp.AddHeader("Access-Control-Max-Age", "1728000");

                    // Write out to the response stream (asynchronously), then close it
                    GenericCommand exiting = new GenericCommand("The server is exiting.");
                    data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cmdOut));
                    await resp.OutputStream.WriteAsync(data, 0, data.Length);
                    resp.Close();
                    break;
                }

                if ((req.HttpMethod == "POST") && (req.Url.AbsolutePath == "/shutdown"))
                {
                    Console.WriteLine("Shutdown requested");
                    MessageBox.Show("NativeBridge is exiting!");

                    runServer = false;
                }

                if (req.Url.AbsolutePath.StartsWith("/volume/sys/"))
                {
                    string volume = req.Url.AbsolutePath.Substring(12);
                    Console.WriteLine("Volume level: " + volume);

                    VolumeCommand cmd = new VolumeCommand(int.Parse(volume.Split(".")[0]));
                    SystemControl.setVolumeMaster(int.Parse(volume.Split(".")[0]));

                    data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cmd));
                }

                if (req.Url.AbsolutePath.StartsWith("/volume/app/"))
                {
                    string volume = req.Url.AbsolutePath.Substring(12).Split("/")[0];
                    string app = req.Url.AbsolutePath.Substring(12).Split("/")[1];
                    VolumeCommand cmd = new VolumeCommand(int.Parse(volume.Split(".")[0]));
                    SystemControl.setVolumeApp(app, int.Parse(volume.Split(".")[0]));
                    data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cmd));
                }

                if (req.Url.AbsolutePath.Equals("/volume/apps"))
                {
                    using var device = MMDeviceEnumerator.DefaultAudioEndpoint(
                        DataFlow.Render,
                        Role.Multimedia);

                    using var master = AudioEndpointVolume.FromDevice(device);

                    using var sessionManager = AudioSessionManager2.FromMMDevice(device);
                    using var enumerator = sessionManager.GetSessionEnumerator();

                    int i = 0;
                    JsonArray apps = new JsonArray();
                    JsonObject masterApp = new JsonObject();
                    masterApp.Add("name", "_fd.System");
                    masterApp.Add("volume", master.MasterVolumeLevelScalar);
                    apps.Add(masterApp);
                    foreach (var sessionControl in enumerator.Skip(1))
                    {
                        Console.WriteLine($"Session {i++}");
                        using var sessionControl2 = sessionControl.QueryInterface<AudioSessionControl2>();

                        using var process = Process.GetProcessById(sessionControl2.ProcessID);
                        using var volume = sessionControl.QueryInterface<SimpleAudioVolume>();
                        
                        String friendly = process.MainWindowTitle;
                        if (process.ProcessName == "Idle") friendly = "System Sounds";

                        Console.WriteLine($"Volume of {process.ProcessName} : {volume.MasterVolume}");

                        JsonObject app = new JsonObject();
                        app.Add("name", process.ProcessName);
                        app.Add("friendly", friendly);
                        app.Add("volume", volume.MasterVolume);
                        apps.Add(app);
                    }

                    data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(apps));
                }

                resp.ContentType = "application/json";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.AddHeader("Access-Control-Allow-Origin", "*");
                resp.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                resp.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                resp.AddHeader("Access-Control-Max-Age", "1728000");

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }


        public static void Start()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            Console.WriteLine("Listening for connections on {0}", url);

            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            listener.Close();
        }

        public static void Stop() {
            listener.Stop();
        }
        public static void Dispose() {
            listener.Close();
        }
    }
}
