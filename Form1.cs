using CSCore.CoreAudioAPI;
using SharpHook;
using SharpHook.Native;
using System.Diagnostics;
using System.Text.Json.Nodes;
namespace NativeBridgeUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
            this.Shown += Form1_Shown;
        }

        private void Form1_Shown(object? sender, EventArgs e)
        {
            this.Hide();
        }

        Task NBTask;
        CancellationTokenSource ct = new CancellationTokenSource();

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            ct.Cancel();
            Environment.Exit(0);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label3.Text = "Starting NativeBridge";

            NBTask = Task.Run(() =>
            {
                ct.Token.ThrowIfCancellationRequested();
                NativeBridge.HTTP.Start();
            }, ct.Token);
            label3.Text = "NativeBridge is started!";
            var hook = new TaskPoolGlobalHook();
            hook.KeyPressed += Hook_KeyPressed;
            Task task = Task.Run(async () =>
            {
                await hook.RunAsync();
            });

            Task.Run(() => Calc());
        }

        public void Calc()
        {
            this.Invoke(delegate
            {
                listBox1.Items.Clear();
            });
            using var device = MMDeviceEnumerator.DefaultAudioEndpoint(
                        DataFlow.Render,
                        Role.Multimedia);

            using var master = AudioEndpointVolume.FromDevice(device);

            using var sessionManager = AudioSessionManager2.FromMMDevice(device);
            using var enumerator = sessionManager.GetSessionEnumerator();

            int i = 0;
            foreach (var sessionControl in enumerator.Skip(1))
            {
                Console.WriteLine($"Session {i++}");
                using var sessionControl2 = sessionControl.QueryInterface<AudioSessionControl2>();

                using var process = Process.GetProcessById(sessionControl2.ProcessID);
                using var volume = sessionControl.QueryInterface<SimpleAudioVolume>();

                String friendly = process.MainWindowTitle;
                if (process.ProcessName == "Idle") friendly = "System Sounds";

                Console.WriteLine($"Volume of {process.ProcessName} : {volume.MasterVolume}");
                this.Invoke(delegate
                {
                    listBox1.Items.Add(process.ProcessName + "/" + friendly + " @ " + volume.MasterVolume);
                });
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var hook = new TaskPoolGlobalHook();
            hook.KeyPressed += Hook_KeyPressed;
            Task task = Task.Run(async () =>
            {
                await hook.RunAsync();
            });
            this.Hide();
        }

        EventSimulator sim = new EventSimulator();
        String mod = "";
        String mod2 = "";

        private void Hook_KeyPressed(object? sender, KeyboardHookEventArgs e)
        {
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
