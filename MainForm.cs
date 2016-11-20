using System;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Media;


namespace Flying47
{
    public partial class MainForm : Form
    {
        // Base address value for pointers.
        int baseAddress = 0x00000000;
        int gCoreModule = 0x00000000;

        bool gCoreRequiresRefresh = false;
        bool freezeHealthEnabled = false;


        // Other variables.
        System.Text.Encoding enc = System.Text.Encoding.UTF8;
        Process[] myProcess;
        string processName;
        codeInjection inj;

        float readCoordX = 0;
        float readCoordY = 0;
        float readCoordZ = 0;
        float readSinAlpha = 0;
        float readCosAlpha = 0;

        float storedCoordX = 0;
        float storedCoordY = 0;
        float storedCoordZ = 0;

        //Block related to position
        int adressCoord = 0x003F295C;
        int[] offsetSinAlpha = new int[] { 0x18 };
        int[] offsetCosAlpha = new int[] { 0x20 };
        int[] offsetX = new int[] { 0x24 };
        int[] offsetY = new int[] { 0x2c };
        int[] offsetZ = new int[] { 0x28 };

        Keys kStorePosition = Keys.NumPad7;
        Keys kLoadPosition = Keys.NumPad9;
        Keys kUp = Keys.Add;
        Keys kDown = Keys.Subtract;
        Keys kForward = Keys.NumPad8;
        Keys kFreezeHealthToggle = Keys.NumPad1;

        uint CurrentKeyChange;

        bool settingInputKey = false;

        Bitmap bitmap;
        Graphics gBuffer;


        /*------------------
        -- INITIALIZATION --
        ------------------*/
        public MainForm()
        {
            InitializeComponent();
            processName = "indy";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitHotkey();
            TTimer.Start();

            B_StorePosition.Text = kStorePosition.ToString();
            B_LoadPosition.Text = kLoadPosition.ToString();
            B_KeyForward.Text = kForward.ToString();
            B_KeyUp.Text = kUp.ToString();
            B_KeyDown.Text = kDown.ToString();
            B_kFreezeHealthToggle.Text = kFreezeHealthToggle.ToString();

            bitmap = new Bitmap(vectorDisplay.Width, vectorDisplay.Height);
            gBuffer = Graphics.FromImage(bitmap);
        }

        enum KeysUsed : uint
        {
            storePosition,
            loadPosition,
            up,
            down,
            forward,
            healthFreezeToggle
        }

        bool foundProcess = false;

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                myProcess = Process.GetProcessesByName(processName);
                if (myProcess.Length > 0 )
                {
                    if(foundProcess == false || baseAddress == 0x0 || gCoreModule == 0x0 || gCoreRequiresRefresh )
                    {
                        TTimer.Interval = 1000;
                        IntPtr startOffset = myProcess[0].MainModule.BaseAddress;
                        IntPtr endOffset = IntPtr.Add(startOffset, myProcess[0].MainModule.ModuleMemorySize);
                        baseAddress = startOffset.ToInt32();
                        foreach(ProcessModule module in myProcess[0].Modules)
                        {
                            if (module.ModuleName.ToLower() == "GCore.dll".ToLower())
                                gCoreModule = module.BaseAddress.ToInt32();
                        }
                        if (gCoreModule != 0x0)
                            gCoreRequiresRefresh = false;
                        Debug.WriteLine("Trying to get baseAddresses");
                    }

                    foundProcess = true;
                }
                else
                {
                    foundProcess = false;
                    inj = null;
                }

                
                if (foundProcess)
                {
                    // The game is running, ready for memory reading.
                    LB_Running.Text = "IJET is running";
                    LB_Running.ForeColor = Color.Green;
                    
                    readCoordX = Trainer.ReadPointerFloat(myProcess, gCoreModule + adressCoord, offsetX);
                    L_X.Text = readCoordX.ToString();
                    readCoordY = Trainer.ReadPointerFloat(myProcess, gCoreModule + adressCoord, offsetY);
                    L_Y.Text = readCoordY.ToString();
                    readCoordZ = Trainer.ReadPointerFloat(myProcess, gCoreModule + adressCoord, offsetZ);
                    L_Z.Text = readCoordZ.ToString();
                    readSinAlpha = Trainer.ReadPointerFloat(myProcess, gCoreModule + adressCoord, offsetSinAlpha);
                    readCosAlpha = -Trainer.ReadPointerFloat(myProcess, gCoreModule + adressCoord, offsetCosAlpha); //this one we invert to negative value
                    drawVectorDisplay();
                    vectorDisplay.Image = bitmap;

                    if (readCoordX == 0.0f && readCoordY == 0.0f && readCoordZ == 0.0f)
                    {
                        gCoreRequiresRefresh = true;
                        Debug.WriteLine("Requested module refresh");
                    }
                    else
                    {
                        InitHotkey();
                        TTimer.Interval = 100;
                    }
                }
                else
                {
                    // The game process has not been found, reseting values.
                    LB_Running.Text = "IJET is not running";
                    LB_Running.ForeColor = Color.Red;
                    ResetValues();
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void drawVectorDisplay()
        {
            gBuffer.Clear(Color.Black);
            float width = bitmap.Width;
            float height = bitmap.Height;
            float half = width/2; //make sure it's square
            gBuffer.DrawLine(new Pen(Color.Blue, 2), half, half, half + half*readSinAlpha, half + half * readCosAlpha);
        }

        // Called when the game is not running or no mission is active.
        // Used to reset all the values.
        private void ResetValues()
        {
            L_X.Text = "NaN";
            L_Y.Text = "NaN";
            L_Z.Text = "NaN";
            gCoreRequiresRefresh = true;
        }

        public void InitHotkey()
        {
            if (!KeyGrabber.Hooked)
            {
                KeyGrabber.key.Clear();
                KeyGrabber.keyPressEvent += KeyGrabber_KeyPress;
                if (kStorePosition != Keys.None)
                    KeyGrabber.key.Add(kStorePosition);

                if (kLoadPosition != Keys.None)
                    KeyGrabber.key.Add(kLoadPosition);

                if (kDown != Keys.None)
                    KeyGrabber.key.Add(kDown);

                if (kUp != Keys.None)
                    KeyGrabber.key.Add(kUp);

                if (kForward != Keys.None)
                    KeyGrabber.key.Add(kForward);

                if (kFreezeHealthToggle != Keys.None)
                    KeyGrabber.key.Add(kFreezeHealthToggle);

                KeyGrabber.SetHook();
            }
            else
            {
                if (kStorePosition != Keys.None || kLoadPosition != Keys.None || kDown != Keys.None || kUp != Keys.None || kForward != Keys.None || kFreezeHealthToggle != Keys.None)
                {
                    KeyGrabber.key.Clear();
                    KeyGrabber.key.Add(kStorePosition);
                    KeyGrabber.key.Add(kLoadPosition);
                    KeyGrabber.key.Add(kDown);
                    KeyGrabber.key.Add(kUp);
                    KeyGrabber.key.Add(kForward);
                    KeyGrabber.key.Add(kFreezeHealthToggle);
                }
            }
        }

        public void UnHook()
        {
            if(KeyGrabber.Hooked)
            {
                KeyGrabber.keyPressEvent -= KeyGrabber_KeyPress;
                KeyGrabber.UnHook();
            }

        }


        private void KeyGrabber_KeyPress(object sender, EventArgs e)
        {
            if (((Keys)sender) == kStorePosition)
            {
                save_Position();
            }
            else if(((Keys)sender)== kLoadPosition)
            {
                load_Position();
            }

            if (((Keys)sender) == kUp)
            {
                SendMeUp();
            }
            else if (((Keys)sender) == kDown)
            {
                SendMeDown();
            }

            if (((Keys)sender) == kForward)
            {
                SendMeForward();
            }

            if (((Keys)sender) == kFreezeHealthToggle)
            {
                FrezeHealthToggle();
            }
        }

        private void FrezeHealthToggle()
        {
            if(!freezeHealthEnabled)
            {
                SoundPlayer snd = new SoundPlayer(Properties.Resources.f_on);
                snd.Play();

                if (inj == null)
                {
                    byte[] injCode = codeInjection.stringBytesToArray("66 81 3E 70 07 0F 85 12 00 00 00 C7 86 84 00 00 00 00 00 16 44 DD D8 D9 86 84 00 00 00 D9 9E 84 00 00 00");
                    inj = new codeInjection(myProcess[0], (uint)gCoreModule + 0x14aef4, 6, injCode);
                    System.Threading.Thread.Sleep(100);
                    Debug.WriteLine("Injected status: " + inj.result + " adress: 0x" + inj.alocAdress.ToString("X4"));
                }
                else
                {
                    inj.updateJmpInstructions((uint)gCoreModule + 0x14aef4, 6);
                    inj.EnableHook();
                }
                freezeHealthEnabled = true;
            }
            else
            {
                SoundPlayer snd = new SoundPlayer(Properties.Resources.f_off);
                snd.Play();
                freezeHealthEnabled = false;
                inj.DisableHook();
            }
        }

        private void SendMeForward()
        {
            Trainer.WritePointerFloat(myProcess, gCoreModule + adressCoord, offsetX, readCoordX + readSinAlpha*100);
            Trainer.WritePointerFloat(myProcess, gCoreModule + adressCoord, offsetY, readCoordY - readCosAlpha*100);
        }

        private void SendMeDown()
        {
            Trainer.WritePointerFloat(myProcess, gCoreModule + adressCoord, offsetZ, readCoordZ - 100);
        }

        private void SendMeUp()
        {
            Trainer.WritePointerFloat(myProcess, gCoreModule + adressCoord, offsetZ, readCoordZ + 100);
        }

        private void load_Position()
        {
            Trainer.WritePointerFloat(myProcess, gCoreModule + adressCoord, offsetX, storedCoordX);
            Trainer.WritePointerFloat(myProcess, gCoreModule + adressCoord, offsetY, storedCoordY);
            Trainer.WritePointerFloat(myProcess, gCoreModule + adressCoord, offsetZ, storedCoordZ);
        }

        private void save_Position()
        {
            storedCoordX = readCoordX;
            storedCoordY = readCoordY;
            storedCoordZ = readCoordZ;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (settingInputKey)
            {
                if (CurrentKeyChange == (uint)KeysUsed.storePosition)
                {
                    kStorePosition = keyData;
                    B_StorePosition.Text = kStorePosition.ToString();
                    B_StorePosition.Checked = false;
                }
                else if (CurrentKeyChange == (uint)KeysUsed.loadPosition)
                {
                    kLoadPosition = keyData;
                    B_LoadPosition.Text = kLoadPosition.ToString();
                    B_LoadPosition.Checked = false;
                }
                else if (CurrentKeyChange == (uint)KeysUsed.forward)
                {
                    kForward = keyData;
                    B_KeyForward.Text = kForward.ToString();
                    B_KeyForward.Checked = false;
                }
                else if (CurrentKeyChange == (uint)KeysUsed.up)
                {
                    kUp = keyData;
                    B_KeyUp.Text = kUp.ToString();
                    B_KeyUp.Checked = false;
                }
                else if (CurrentKeyChange == (uint)KeysUsed.down)
                {
                    kDown = keyData;
                    B_KeyDown.Text = kDown.ToString();
                    B_KeyDown.Checked = false;
                }
                else if (CurrentKeyChange == (uint)KeysUsed.healthFreezeToggle)
                {
                    kFreezeHealthToggle = keyData;
                    B_kFreezeHealthToggle.Text = kFreezeHealthToggle.ToString();
                    B_kFreezeHealthToggle.Checked = false;
                }

                InitHotkey();
                return true;
            }
                
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnHook();
        }

        private void B_ChangeButton(object sender, EventArgs e)
        {
            if (((CheckBox)sender).Checked)
            {
                settingInputKey = true;
                if(sender == B_StorePosition)
                {
                    B_StorePosition.Text = "";
                    CurrentKeyChange = (uint)KeysUsed.storePosition;
                }
                else if (sender == B_LoadPosition)
                {
                    B_LoadPosition.Text = "";
                    CurrentKeyChange = (uint)KeysUsed.loadPosition;
                }
                else if (sender == B_KeyForward)
                {
                    B_KeyForward.Text = "";
                    CurrentKeyChange = (uint)KeysUsed.forward;
                }
                else if (sender == B_KeyUp)
                {
                    B_KeyUp.Text = "";
                    CurrentKeyChange = (uint)KeysUsed.up;
                }
                else if (sender == B_KeyDown)
                {
                    B_KeyDown.Text = "";
                    CurrentKeyChange = (uint)KeysUsed.down;
                }
                else if (sender == B_kFreezeHealthToggle)
                {
                    B_kFreezeHealthToggle.Text = "";
                    CurrentKeyChange = (uint)KeysUsed.healthFreezeToggle;
                }
            }
            else
            {
                settingInputKey = false;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FrezeHealthToggle();
        }
    }
}
