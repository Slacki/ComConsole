﻿using GlobalHotkeys;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Ports;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

namespace ComConsole
{
    public partial class MainForm : Form
    {
        private ComPort cPort;
        private List<GlobalHotkey> ghList = new List<GlobalHotkey>();

        public MainForm()
        {
            InitializeComponent();

            this.AddComPorts();
            this.AddBitrate();
            this.AddDataBits();
            this.AddStopBits();
            this.AddParity();
            this.AddHandshake();

            this.RevokePreviousSettings();

            this.cPort = new ComPort();
            this.cPort.OnStatusChanged += this.OnStatusChanged;
            this.cPort.OnDataRecieved += this.OnDataRecieved;
            this.FireOpen();

            this.DeserializeHotkeys();
            this.RenewAllHotkeys();
            this.FillListViewWithRenewedHotkeys();
        }

        private void FireOpen()
        {
            string port = comboBoxPort.Text.ToString();
            int rate = Convert.ToInt32(comboBoxRate.Text);
            int databits = Convert.ToInt32(comboBoxDataBits.Text);
            StopBits stopbits = (StopBits)Enum.Parse(typeof(StopBits), comboBoxStopBits.Text);
            Parity parity = (Parity)Enum.Parse(typeof(Parity), comboBoxParity.Text);
            Handshake handshake = (Handshake)Enum.Parse(typeof(Handshake), comboBoxHandshake.Text);

            this.cPort.Open(port, rate, parity, databits, stopbits, handshake);
        }

        private void SavePortInfo()
        {
            Properties.Settings.Default["port"] = this.comboBoxPort.Text.ToString();
            Properties.Settings.Default["rate"] = Convert.ToInt32(this.comboBoxRate.Text);
            Properties.Settings.Default["databits"] = Convert.ToInt32(this.comboBoxDataBits.Text);
            Properties.Settings.Default["stopbits"] = (StopBits)Enum.Parse(typeof(StopBits), this.comboBoxStopBits.Text);
            Properties.Settings.Default["parity"] = (Parity)Enum.Parse(typeof(Parity), this.comboBoxParity.Text);
            Properties.Settings.Default["handshake"] = (Handshake)Enum.Parse(typeof(Handshake), this.comboBoxHandshake.Text);

            Properties.Settings.Default.Save();
        }

        private void RevokePreviousSettings()
        {
            this.comboBoxPort.Text = Properties.Settings.Default.port;
            this.comboBoxRate.Text = Convert.ToString(Properties.Settings.Default.rate);
            this.comboBoxDataBits.Text = Convert.ToString(Properties.Settings.Default.databits);
            this.comboBoxStopBits.Text = Convert.ToString(Properties.Settings.Default.stopbits);
            this.comboBoxParity.Text = Convert.ToString(Properties.Settings.Default.parity);
            this.comboBoxHandshake.Text = Convert.ToString(Properties.Settings.Default.handshake);

            // append to text
            switch (Properties.Settings.Default.append) {
                case (int)AppendToText.CR:
                    this.radioButtonAppendCR.Checked = true; break;
                case (int)AppendToText.LF:
                    this.radioButtonAppendLF.Checked = true; break;
                case (int)AppendToText.CRLF:
                    this.radioButtonAppendCRLF.Checked = true; break;
                default:
                    this.radioButtonAppendNothing.Checked = true; break;
            }
        }


        #region Global hotkeys handling

        protected override void WndProc(ref Message m)
        {
            // we check if the message is about out hotkey
            var hotkeyInfo = HotkeyInfo.GetFromMessage(m);
            if (hotkeyInfo != null) {
                this.HandleHotkey(hotkeyInfo);
            }

            base.WndProc(ref m);
        }

        private void HandleKeyBindAdd()
        {
            int globalHotkeyId;
           
            var key = (Keys)Enum.Parse(typeof(Keys), textBoxKey.Text.ToUpper());
            // check if mod keys are checked
            var mod = Modifiers.NoMod;
            if (this.checkBoxAlt.Checked) { mod = mod | Modifiers.Alt; }
            if (this.checkBoxCtlr.Checked) { mod = mod | Modifiers.Ctrl; }
            if (this.checkBoxShift.Checked) { mod = mod | Modifiers.Shift; }
            if (this.checkBoxWinKey.Checked) { mod = mod | Modifiers.Win; }

            try {
                GlobalHotkey gh = new GlobalHotkey(mod, key, this, true);
                globalHotkeyId = gh.Id;
                gh.command = this.textBoxCommand.Text;
                this.ghList.Add(gh);
            } catch (GlobalHotkeyException e) {
                MessageBox.Show(e.Message);
                return;
            } 

            // when shortcut registered successfully
            ListViewItem listItem = new ListViewItem(String.Format("{0} {1}", mod, key));
            listItem.SubItems.Add(this.textBoxCommand.Text);
            listItem.SubItems.Add(globalHotkeyId.ToString());

            this.listView1.Items.Add(listItem);
        }

        private void HandleKeyBindRemove()
        {
            // it was checked if anything is selected
            // and we know for sure only one item can be selected 
            int globalHotkeyId = Int32.Parse(this.listView1.SelectedItems[0].SubItems[2].Text);
            foreach (GlobalHotkey gh in this.ghList) {
                if (gh.Id.Equals(globalHotkeyId)) {
                    gh.Dispose();
                }
            }
            this.listView1.SelectedItems[0].Remove();
        }

        private void HandleHotkey(HotkeyInfo hotkeyInfo)
        {
            richTextBox1.AppendText(string.Format("{0} : Hotkey Proc! {1}, {2}{3}", DateTime.Now.ToString("hh:MM:ss.fff"),
                                           hotkeyInfo.Key, hotkeyInfo.Modifiers, Environment.NewLine));
        }

        private void buttonAddHotkey_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(this.textBoxKey.Text) ||
                String.IsNullOrWhiteSpace(this.textBoxCommand.Text)) {
                return;
            }

            this.HandleKeyBindAdd();
        }

        private void buttonDeleteHotkey_Click(object sender, EventArgs e)
        {
            if (this.listView1.SelectedItems.Count == 0) {
                return;
            }

            this.HandleKeyBindRemove();
        }

        private void SerializeHotkeys()
        {
            DeflateStream defStream = new DeflateStream(File.OpenWrite("./hotkeys.dat"), CompressionMode.Compress);
            BinaryFormatter bFormatter = new BinaryFormatter();
            bFormatter.Serialize(defStream, this.ghList);

            defStream.Flush();
            defStream.Close();
            defStream.Dispose();

            bFormatter = null;
        }

        private void DeserializeHotkeys()
        {
            DeflateStream defStream = new DeflateStream(File.OpenRead("./hotkeys.dat"), CompressionMode.Decompress);
            BinaryFormatter bFormatter = new BinaryFormatter();
            object list = bFormatter.Deserialize(defStream);
            defStream.Close();
            defStream.Dispose();

            this.ghList = null;
            this.ghList = list as List<GlobalHotkey>;
        }

        private void RenewAllHotkeys()
        {
            foreach (GlobalHotkey gh in this.ghList) {
                gh.Renew(gh, this);
            }
        }

        private void FillListViewWithRenewedHotkeys()
        {
            foreach (GlobalHotkey gh in this.ghList) {
                var key = (Keys)Enum.Parse(typeof(Keys), gh.Key.ToString());
                ListViewItem listItem = new ListViewItem(String.Format("{0} {1}", gh.Modifier, key));
                listItem.SubItems.Add(gh.command);
                listItem.SubItems.Add(gh.Id.ToString());
                this.listView1.Items.Add(listItem);
            }
        }

        private void UnregisterAllHotkeys()
        {
            foreach (GlobalHotkey gh in this.ghList) {
                gh.Unregister();
            }
        }

        #endregion


        #region Filling form with data

        private void AddComPorts()
        {
            string[] comPortsNames = null;
            comPortsNames = SerialPort.GetPortNames();
            string comPortName = null;
            int index = -1;
            if (!(comPortsNames == null || comPortsNames.Length == 0)) {
                do {
                    index = index + 1;
                    this.comboBoxPort.Items.Add(comPortsNames[index]);
                } while (!((comPortsNames[index] == comPortName) ||
                    (index == comPortsNames.GetUpperBound(0))));
                Array.Sort(comPortsNames);

                this.comboBoxPort.Text = comboBoxPort.Items[0].ToString();
            }
        }

        private void AddBitrate()
        {
            this.comboBoxRate.Items.Add(300);
            this.comboBoxRate.Items.Add(600);
            this.comboBoxRate.Items.Add(1200);
            this.comboBoxRate.Items.Add(2400);
            this.comboBoxRate.Items.Add(9600);
            this.comboBoxRate.Items.Add(14400);
            this.comboBoxRate.Items.Add(19200);
            this.comboBoxRate.Items.Add(38400);
            this.comboBoxRate.Items.Add(57600);
            this.comboBoxRate.Items.Add(115200);

            comboBoxRate.Text = comboBoxRate.Items[4].ToString();
        }

        private void AddDataBits()
        {
            this.comboBoxDataBits.Items.Add(5);
            this.comboBoxDataBits.Items.Add(6);
            this.comboBoxDataBits.Items.Add(7);
            this.comboBoxDataBits.Items.Add(8);

            this.comboBoxDataBits.Text = this.comboBoxDataBits.Items[3].ToString();
        }

        private void AddStopBits()
        {
            this.comboBoxStopBits.Items.Add(StopBits.None.ToString());
            this.comboBoxStopBits.Items.Add(StopBits.One.ToString());
            this.comboBoxStopBits.Items.Add(StopBits.OnePointFive.ToString());
            this.comboBoxStopBits.Items.Add(StopBits.Two.ToString());

            this.comboBoxStopBits.Text = this.comboBoxStopBits.Items[1].ToString();
        }

        private void AddParity()
        {
            this.comboBoxParity.Items.Add(Parity.None.ToString());
            this.comboBoxParity.Items.Add(Parity.Even.ToString());
            this.comboBoxParity.Items.Add(Parity.Odd.ToString());
            this.comboBoxParity.Items.Add(Parity.Mark.ToString());
            this.comboBoxParity.Items.Add(Parity.Space.ToString());

            this.comboBoxParity.Text = this.comboBoxParity.Items[0].ToString();
        }

        private void AddHandshake()
        {
            this.comboBoxHandshake.Items.Add(Handshake.None.ToString());
            this.comboBoxHandshake.Items.Add(Handshake.RequestToSend.ToString());
            this.comboBoxHandshake.Items.Add(Handshake.RequestToSendXOnXOff.ToString());
            this.comboBoxHandshake.Items.Add(Handshake.XOnXOff.ToString());

            this.comboBoxHandshake.Text = this.comboBoxHandshake.Items[0].ToString();
        }

        #endregion


        #region Receiving and sending data

        // delegates used for Invoke()
        internal delegate void DataRecievedDelegate(object sender, DataRecievedEventArgs e);
        internal delegate void StatusChangedDelegate(object sender, StatusChangedEventArgs e);

        private string partialLine = null;

        public void OnDataRecieved(object sender, DataRecievedEventArgs e)
        {
            if (InvokeRequired) {
                Invoke(new DataRecievedDelegate(this.OnDataRecieved), new object[] { sender, e });
                return;
            }

            string dataIn = e.data;

            // if we detect a line terminator, add line to output
            int index;
            while (dataIn.Length > 0 &&
                ((index = dataIn.IndexOf("\r")) != -1 ||
                (index = dataIn.IndexOf("\n")) != -1)) {
                string stringIn = dataIn.Substring(0, index);
                dataIn = dataIn.Remove(0, index + 1);

                this.PrintLine(this.AddDataToPartialLine(stringIn));

                partialLine = null;	// terminate partial line
            }

            // but if we have partial line, we add to it
            if (dataIn.Length > 0) {
                this.partialLine = AddDataToPartialLine(dataIn);
            }
        }

        public void OnStatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (InvokeRequired) {
                Invoke(new StatusChangedDelegate(this.OnStatusChanged), new object[] { sender, e });
                return;
            }

            this.richTextBox1.Clear();
            this.richTextBox1.AppendText("# " + e.status + "\n");
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            this.SendData();
        }

        private void richTextBox2_KeyPress_1(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13) {
                this.SendData();
            }
        }


        private String PrepareData(string stringIn)
        {
            // The names of the first 32 characters
            string[] charNames = { "NUL", "SOH", "STX", "ETX", "EOT",
				"ENQ", "ACK", "BEL", "BS", "TAB", "LF", "VT", "FF", "CR", "SO", "SI",
				"DLE", "DC1", "DC2", "DC3", "DC4", "NAK", "SYN", "ETB", "CAN", "EM", "SUB",
				"ESC", "FS", "GS", "RS", "US", "Space"};

            string StringOut = "";

            foreach (char c in stringIn) {
                if (c < 32 && c != 9) {
                    StringOut = StringOut + "<" + charNames[c] + ">";
                } else {
                    StringOut = StringOut + c;
                }
            }
            return StringOut;
        }

        private String AddDataToPartialLine(string stringIn)
        {
            string stringOut = this.PrepareData(stringIn);

            // if there is a partial line, we add to it
            if (this.partialLine != null) {
                this.partialLine += stringOut;
                return this.partialLine;
            }

            // we dont have partial line, and we push whole line 
            this.PrintLine(stringOut);
            return "";
        }

        private void SendData()
        {
            string command = this.richTextBox2.Text.ToString();
            this.richTextBox2.Text = "";
            this.richTextBox2.Focus();

            if (command.Length > 0) {
                this.cPort.Send(command);
                // local echo
                this.richTextBox1.AppendText(String.Format("[S] {0} \n", command));
            }
        }

        private void PrintLine(string dataIn)
        {
            if (dataIn.Length > 0) {
                this.richTextBox1.AppendText("[R] " + dataIn + "\n");
            }
        }

        #endregion


        #region The rest of events

        private void button1_Click(object sender, EventArgs e)
        {
            this.SavePortInfo();
            // reopen the port after changeing it's parameters
            this.FireOpen();
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            this.richTextBox1.ScrollToCaret();
        }

        private void OnRadioButtonCheck(object sender, EventArgs e)
        {
            if (this.radioButtonAppendCR.Checked) {
                Properties.Settings.Default["append"] = (int)AppendToText.CR;
            } else if (this.radioButtonAppendLF.Checked) {
                Properties.Settings.Default["append"] = (int)AppendToText.LF;
            } else if (this.radioButtonAppendCRLF.Checked) {
                Properties.Settings.Default["append"] = (int)AppendToText.CRLF;
            } else {
                Properties.Settings.Default["append"] = (int)AppendToText.Nothing;
            }
            Properties.Settings.Default.Save();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized) {
                this.Hide();

                this.notifyIcon1.BalloonTipTitle = "ComConsole hidden";
                this.notifyIcon1.BalloonTipText = "The application is now hidden and is waiting to take your oders.";
                this.notifyIcon1.ShowBalloonTip(3000);
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            WindowState = FormWindowState.Normal;
        }

        protected override void OnClosed(EventArgs e)
        {
            this.cPort.Close();
            this.SerializeHotkeys();
            this.UnregisterAllHotkeys();

            base.OnClosed(e);
        }

        #endregion

    }
}
