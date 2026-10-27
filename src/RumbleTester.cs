using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class XInput {
    [StructLayout(LayoutKind.Sequential)] public struct Gamepad {
        public ushort Buttons; public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }
    [StructLayout(LayoutKind.Sequential)] public struct State { public uint Packet; public Gamepad Pad; }
    [StructLayout(LayoutKind.Sequential)] public struct Vibration { public ushort Large, Small; }
    [DllImport("xinput1_4.dll", EntryPoint="XInputGetState")]
    public static extern uint GetState(uint index, out State state);
    [DllImport("xinput1_4.dll", EntryPoint="XInputSetState")]
    private static extern uint SetState(uint index, ref Vibration vibration);
    public static uint Send(int index, int large, int small) {
        if (index < 0 || index > 3 || large < 0 || large > 100 || small < 0 || small > 100)
            throw new ArgumentOutOfRangeException();
        Vibration v = new Vibration();
        v.Large = (ushort)Math.Round(large * 65535.0 / 100.0);
        v.Small = (ushort)Math.Round(small * 65535.0 / 100.0);
        return SetState((uint)index, ref v);
    }
}

internal sealed class Slot {
    public int Index;
    public override string ToString() { return "XInput " + (Index + 1) + "  (slot " + Index + ")"; }
}
internal sealed class Step {
    public int Large, Small, Milliseconds;
    public string Label;
    public Step(int large, int small, int milliseconds, string label) {
        Large=large; Small=small; Milliseconds=milliseconds; Label=label;
    }
}

internal sealed class Tester : Form {
    private ComboBox devices = new ComboBox();
    private TrackBar large = new TrackBar(), small = new TrackBar();
    private Label largeValue = new Label(), smallValue = new Label();
    private Label connection = new Label(), current = new Label();
    private NumericUpDown duration = new NumericUpDown();
    private TextBox log = new TextBox();
    private List<Button> actions = new List<Button>();
    private Timer timer = new Timer();
    private Stopwatch clock = Stopwatch.StartNew();
    private Queue<Step> steps = new Queue<Step>();
    private int activeSlot = -1, tick = 0;
    private long deadline;
    private string deviceSignature = "";
    private bool updatingDevices, running;
    private string activeLabel = "";
    private readonly string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rumble-tests.log");

    public Tester() {
        Text = "XInput Rumble Tester";
        ClientSize = new Size(820, 635); MinimumSize = new Size(836, 674);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F); BackColor = Color.FromArgb(246,248,251);
        AutoScaleMode = AutoScaleMode.Dpi;
        Label title = LabelAt("XInput Rumble Tester", 22,16,775,36);
        title.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
        LabelAt("Test a local controller or a virtual controller from a Sunshine / Moonlight stream.",22,58,775,26);
        LabelAt("XInput controller",22,100,170,28);
        devices.SetBounds(196,98,235,30); devices.DropDownStyle=ComboBoxStyle.DropDownList;
        Controls.Add(devices);
        devices.SelectedIndexChanged += delegate { if (!updatingDevices) { StopEffect("Controller selection changed"); UpdateEnabled(); } };
        connection.SetBounds(22,139,775,42); Controls.Add(connection);
        LabelAt("Large motor / low-frequency rumble",22,193,530,26);
        ConfigureSlider(large,largeValue,225);
        LabelAt("Small motor / high-frequency rumble",22,277,530,26);
        ConfigureSlider(small,smallValue,309);
        LabelAt("Duration (seconds)",22,367,230,28);
        duration.SetBounds(252,365,85,30); duration.Minimum=0.25M; duration.Maximum=10;
        duration.DecimalPlaces=2; duration.Increment=0.25M; duration.Value=2; Controls.Add(duration);
        AddAction("Small motor only",22,410,170,delegate { StartPulse(0,small.Value,"Small motor only"); });
        AddAction("Large motor only",204,410,170,delegate { StartPulse(large.Value,0,"Large motor only"); });
        AddAction("Both motors",386,410,170,delegate { StartPulse(large.Value,small.Value,"Both motors"); });
        Button stop = new Button(); stop.Text="STOP / Esc"; stop.SetBounds(568,410,226,38);
        stop.BackColor=Color.FromArgb(170,32,45); stop.ForeColor=Color.White;
        stop.Click += delegate { StopEffect("STOP"); }; Controls.Add(stop);
        AddAction("Sequence: small then large, 25% / 50% / 100%",22,460,534,delegate { StartSequence(); });
        current.SetBounds(22,506,775,28); current.Text="Ready. No rumble requested."; Controls.Add(current);
        log.SetBounds(22,542,772,72); log.Multiline=true; log.ReadOnly=true;
        log.ScrollBars=ScrollBars.Vertical; log.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom|AnchorStyles.Top;
        Controls.Add(log);
        KeyPreview=true;
        KeyDown += delegate(object sender, KeyEventArgs e) { if(e.KeyCode==Keys.Escape) { StopEffect("STOP / Esc"); e.Handled=true; } };
        FormClosing += delegate { timer.Stop(); StopEffect("Application closing"); };
        timer.Interval=50; timer.Tick += delegate { OnTick(); };
        PollDevices(); timer.Start();
    }

    private Label LabelAt(string text,int x,int y,int width,int height) {
        Label label=new Label(); label.Text=text; label.SetBounds(x,y,width,height); Controls.Add(label); return label;
    }
    private void ConfigureSlider(TrackBar slider,Label value,int y) {
        slider.SetBounds(22,y,660,45); slider.Minimum=0; slider.Maximum=100;
        slider.TickFrequency=10; slider.LargeChange=10; slider.Value=50;
        value.SetBounds(700,y,94,35); value.Text="50%"; value.Font=new Font(Font,FontStyle.Bold);
        slider.ValueChanged += delegate { value.Text=slider.Value+"%"; };
        Controls.Add(slider); Controls.Add(value);
    }
    private void AddAction(string text,int x,int y,int width,EventHandler handler) {
        Button button=new Button(); button.Text=text; button.SetBounds(x,y,width,38);
        button.Click+=handler; Controls.Add(button); actions.Add(button);
    }
    private void WriteLog(string message) {
        string line=DateTime.Now.ToString("HH:mm:ss.fff")+"  "+message;
        log.AppendText(line+Environment.NewLine);
        try { File.AppendAllText(logFile,DateTimeOffset.Now.ToString("o")+"  "+message+Environment.NewLine); }
        catch(IOException) { } catch(UnauthorizedAccessException) { }
    }
    private void UpdateEnabled() {
        bool enabled=devices.SelectedItem is Slot;
        foreach(Button button in actions) button.Enabled=enabled;
    }
    private void PollDevices() {
        List<int> found=new List<int>(); string signature="";
        for(int i=0;i<4;i++) { XInput.State s; if(XInput.GetState((uint)i,out s)==0) { found.Add(i); signature+=i+","; } }
        if(signature!=deviceSignature) {
            deviceSignature=signature;
            if(running && !found.Contains(activeSlot)) StopEffect("Controller disconnected. Test cancelled.");
            Slot old=devices.SelectedItem as Slot;
            updatingDevices=true; devices.Items.Clear();
            foreach(int i in found) devices.Items.Add(new Slot {Index=i});
            devices.SelectedIndex=-1;
            if(old!=null) for(int i=0;i<devices.Items.Count;i++) if(((Slot)devices.Items[i]).Index==old.Index) devices.SelectedIndex=i;
            // Auto-select only if there is exactly one device; never choose among multiple pads.
            if(devices.SelectedIndex<0 && devices.Items.Count==1) devices.SelectedIndex=0;
            updatingDevices=false; UpdateEnabled();
        }
        Slot selected=devices.SelectedItem as Slot;
        if(selected!=null) {
            XInput.State s;
            if(XInput.GetState((uint)selected.Index,out s)==0) {
                connection.Text="Connected | buttons: 0x"+s.Pad.Buttons.ToString("X4")+" | packet: "+s.Packet+
                    "\nPress a controller button to verify that you selected the intended device.";
                connection.ForeColor=Color.DarkGreen;
            }
        } else {
            connection.ForeColor=Color.FromArgb(130,73,0);
            connection.Text=found.Count==0 ? "No XInput controller detected. Connect a controller or start your Desktop stream.\nFor Sunshine, check controller input and Xbox 360 (x360) emulation." :
                "Multiple controllers detected. Select the intended device from the list.";
        }
        UpdateEnabled();
    }
    private void StartPulse(int a,int b,string label) {
        StopEffect(null);
        steps.Enqueue(new Step(a,b,(int)(duration.Value*1000),label));
        Begin();
    }
    private void StartSequence() {
        StopEffect(null);
        foreach(bool useSmall in new bool[]{true,false}) foreach(int level in new int[]{25,50,100}) {
            steps.Enqueue(new Step(useSmall?0:level,useSmall?level:0,2000,(useSmall?"Small ":"Large ")+level+"%"));
            steps.Enqueue(new Step(0,0,1000,"Pause"));
        }
        Begin();
    }
    private void Begin() {
        Slot selected=devices.SelectedItem as Slot;
        if(selected==null) { steps.Clear(); return; }
        activeSlot=selected.Index; running=true; NextStep();
    }
    private void NextStep() {
        if(steps.Count==0) { StopEffect("Completed. Stop sent."); return; }
        Step step=steps.Dequeue();
        uint result=XInput.Send(activeSlot,step.Large,step.Small);
        if(result!=0) { StopEffect("XInput error "+result+" - cancelled"); return; }
        activeLabel=step.Label+" | large "+step.Large+"%, small "+step.Small+"%";
        deadline=clock.ElapsedMilliseconds+step.Milliseconds;
        WriteLog("slot="+activeSlot+" large="+step.Large+"% small="+step.Small+"% duration="+step.Milliseconds+"ms; XInput accepted (physical result unconfirmed)");
    }
    private void OnTick() {
        try {
            if(running) {
                if(clock.ElapsedMilliseconds>=deadline) NextStep();
                if(running) current.Text=activeLabel+" · "+Math.Max(0,(deadline-clock.ElapsedMilliseconds)/1000.0).ToString("0.0")+" s";
            }
            if(++tick%10==0) PollDevices();
        } catch(Exception ex) { StopEffect("Error: "+ex.Message); }
    }
    public void StopEffect(string message) {
        steps.Clear(); running=false;
        int target=activeSlot; activeSlot=-1;
        if(target>=0) {
            uint result=XInput.Send(target,0,0);
            WriteLog("slot="+target+" large=0 small=0 STOP result="+result);
        }
        current.Text=message??"Ready. No rumble requested.";
    }
}

internal static class Program {
    [STAThread] public static void Main(string[] args) {
        if(args.Length>0 && args[0]=="--probe") {
            List<string> found=new List<string>();
            for(int i=0;i<4;i++) { XInput.State state; uint result=XInput.GetState((uint)i,out state); found.Add("slot="+i+" result="+result); }
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"xinput-probe.txt"),found.ToArray());
            return; // Read-only: never sends any vibration.
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Tester form=new Tester();
        Application.ThreadException += delegate(object sender,System.Threading.ThreadExceptionEventArgs e) {
            form.StopEffect("Error. Stop requested.");
            MessageBox.Show(e.Exception.Message,"XInput Rumble Tester");
        };
        try { Application.Run(form); } finally { form.StopEffect(null); }
    }
}
