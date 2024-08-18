using CSCore.CoreAudioAPI;
using SharpHook;
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
            MessageBox.Show("Press CTRL+SHIFT+N to bring the NativeBridge UI back.", "Freedeck NativeBridge is running!");
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
            MessageBox.Show("Press CTRL+SHIFT+N to bring the window back.");
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
            if(e.Data.KeyCode == SharpHook.Native.KeyCode.VcLeftControl)
            {
                mod = "CTRL";
            }
            if(e.Data.KeyCode == SharpHook.Native.KeyCode.VcLeftShift)
            {
                mod2 = "SHIFT";
            }
            if(e.Data.KeyCode == SharpHook.Native.KeyCode.VcN)
            {
                if(mod == "CTRL" && mod2 == "SHIFT")
                {
                    this.Invoke(delegate
                    {
                        this.Show();
                    });
                }
            }
            if (e.Data.KeyCode == SharpHook.Native.KeyCode.VcR)
            {
                if (mod == "CTRL" && mod2 == "SHIFT")
                {
                    Task.Run(() => Calc());
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
