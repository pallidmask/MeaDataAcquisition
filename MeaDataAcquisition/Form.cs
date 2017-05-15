﻿using Mcs.Usb;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace MeaDataAcquisition
{
    public partial class Form : System.Windows.Forms.Form
    {
        private BackgroundWorker dataAcquisitionWorker;
        private BackgroundWorker dataBackupWorker;
        private BackgroundWorker dataBroadcastWorker;

        private BlockingCollection<ushort[]> dataBackupBuffer = new BlockingCollection<ushort[]>();
        private BlockingCollection<ushort[]> dataBroadcastBuffer = new BlockingCollection<ushort[]>();

        private CMcsUsbListNet usbList = new CMcsUsbListNet(); // TODO understand MCS black magic.
        private CMcsUsbListEntryNet usb = null; // TODO understand MCS black magic.
        private CMeaDeviceNet device = null; // TODO understand MCS black magic.

        private int buf_size = 0;
        private int buf_acq_nb = 0; // number of acquired buffers

        private int nb_channels = 0; // TODO understand MCS black magic.
        private int sample_rate = 0; // TODO understand MCS black magic.
        private int gain = 0; // TODO understand MCS black magic.
        private int channels_in_block = 0; // TODO understand MCS black magic.
        private bool[] selected_channels = null; // TODO understand MCS black magic.
        private int queue_size = 0; // TODO understand MCS black magic.
        private int threshold = 0; // TODO understand MCS black magic.
        private SampleSizeNet sample_size = SampleSizeNet.SampleSize16Unsigned; // TODO understand MCS black magic.

        private string dataBackupFilename = null;

        private int udpClientPort = 40005;
        private string udpClientHostname = IPAddress.Broadcast.ToString();
        private IPAddress udpClientIPAddress = IPAddress.Parse("192.168.10.101");


        public Form()
        {
            InitializeComponent();
            InitializeDataAcquisitionWorker();
            InitializeDataBackupWorker();
            InitializeDataBroadcastWorker();
        }


        // Set up DataAcquisitionWorker object by attaching event handlers.
        private void InitializeDataAcquisitionWorker()
        {
            this.dataAcquisitionWorker = new BackgroundWorker();
            this.dataAcquisitionWorker.WorkerSupportsCancellation = true;
            this.dataAcquisitionWorker.DoWork += new DoWorkEventHandler(dataAcquisitionWorker_DoWork);
        }

        // This event handler is where the data acquisition is done.
        private void dataAcquisitionWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the worker that raised this event.
            var worker = sender as BackgroundWorker;
            // ...
            e.Result = AcquireData(worker, e);
        }

        // TODO add docstring.
        private int AcquireData(BackgroundWorker worker, DoWorkEventArgs e)
        {
            var result = 0;
            
            // TODO understand MCS black magic.
            this.device.SetSelectedData(selected_channels, queue_size, threshold, sample_size, channels_in_block);
            // Update the number of acquired buffers.
            buf_acq_nb = 0;
            // Update the display of the number of acquired buffers.
            textBoxBufferAcquired.Text = buf_acq_nb.ToString();
            // Start the data acquisition thread and sampling.
            //var timeout = 150; // ms
            //var numSubmittedUsbBuffers = 100;
            //var numUsbBuffers = 300;
            //var packetsInUrb = 8;
            //this.device.StartDacq(timeout, numSubmittedUsbBuffers, numUsbBuffers, packetsInUrb);
            // TODO ask MCS to know and understand the default values of the parameters for the data acquisition.
            device.StartDacq();
            
            while (true)
            {
                if (worker.CancellationPending)
                {
                    // Stop data acquisition operation.
                    try
                    {
                        // TODO understand MCS black magic.
                        this.device.StopDacq();
                    }
                    catch (CUsbExceptionNet cUsbExceptionNet)
                    {
                        // Log exception.
                        textBoxLog.Text += cUsbExceptionNet.ToString() + "\r\n";
                    }
                    e.Cancel = true;
                    break;
                }
            }

            return result;
        }


        // Set up DataBackupWorker object by attaching event handlers.
        private void InitializeDataBackupWorker()
        {
            this.dataBackupWorker = new BackgroundWorker();
            this.dataBackupWorker.WorkerSupportsCancellation = true;
            this.dataBackupWorker.DoWork += new DoWorkEventHandler(dataBackupWorker_DoWork);
        }

        // This event handler is where the data backup is done.
        private void dataBackupWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the worker that raised this event.
            var worker = sender as BackgroundWorker;
            // ...
            e.Result = BackupData(worker, e);
        }

        // TODO add docstring.
        private object BackupData(BackgroundWorker worker, DoWorkEventArgs e)
        {
            var result = 0;

            // Update the number of backuped buffers.
            var buf_bck_nb = 0;
            // Update the display of the number of broadcasted buffers.
            this.textBoxBufferAcquired.Text = buf_bck_nb.ToString();
            // Create backup file.
            var dataBackupFile = File.Open(dataBackupFilename, FileMode.Create);
            // Create backup writer.
            var dataBackupWriter = new BinaryWriter(dataBackupFile);

            while (true)
            {
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
                else
                {
                    ushort[] data = dataBackupBuffer.Take();
                    var bytes_nb = 2 * data.Length;
                    byte[] buffer = new byte[bytes_nb];
                    Buffer.BlockCopy(data, 0, buffer, 0, bytes_nb);
                    dataBackupWriter.Write(buffer);
                    // Update the number of backuped buffers.
                    buf_bck_nb = buf_bck_nb + 1;
                    // Update the display of the number of backuped buffers.
                    this.textBoxBufferBackuped.Text = buf_bck_nb.ToString();
                }
            }

            return result;
        }


        // Set up DataBroadcastWorker object by attaching event handlers.
        private void InitializeDataBroadcastWorker()
        {
            this.dataBroadcastWorker = new BackgroundWorker();
            this.dataBroadcastWorker.WorkerSupportsCancellation = true;
            this.dataBroadcastWorker.DoWork += new DoWorkEventHandler(dataBroadcastWorker_DoWork);
        }

        // This event handler is where the data broadcast is done.
        private void dataBroadcastWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Get the worker that raised this event.
            var worker = sender as BackgroundWorker;
            // ...
            e.Result = BroadcastData(worker, e);
        }

        // TODO add docstring.
        private int BroadcastData(BackgroundWorker worker, DoWorkEventArgs e)
        {
            var result = 0;

            // Update the number of broadcasted buffers.
            var buf_brd_nb = 0;
            // Update the display of the number of broadcasted buffer.
            this.textBoxBufferAcquired.Text = buf_brd_nb.ToString();
            // Create TCP listener.
            var address = IPAddress.Parse("192.168.10.100");
            var port = 40006;
            TcpListener tcpListener = new TcpListener(address, port);
            //TcpListener tcpListener = new TcpListener(IPAddress.Any, port);
            this.textBoxLog.Text += "The server will run at " + address + ":" + port + "\r\n"; // TODO remove.
            // Start listenning.
            tcpListener.Start();
            this.textBoxLog.Text += "The server is running at " + tcpListener.LocalEndpoint + "\r\n"; // TODO remove.
            this.textBoxLog.Text += "Waiting for a connection...\r\n"; // TODO remove.
            // Accept connection.
            TcpClient tcpClient = tcpListener.AcceptTcpClient();
            this.textBoxLog.Text += "Connection accepted from " + tcpClient.Client.RemoteEndPoint + "\r\n"; // TODO remove.
            // Set the send buffer size.
            tcpClient.SendBufferSize = 261 * 2000 * 2;
            // Get stream.
            Stream stream = tcpClient.GetStream();

            while (true)
            {
                if (worker.CancellationPending)
                {
                    tcpClient.Close();
                    tcpListener.Stop();
                    e.Cancel = true;
                    break;
                }
                else
                {
                    // Retrieve data.
                    ushort[] data = dataBroadcastBuffer.Take();
                    // Write data to stream.
                    var nb_bytes = 2 * data.Length;
                    this.textBoxLog.Text += nb_bytes + " bytes\r\n"; // TODO remove.
                    var buffer = new byte[nb_bytes];
                    Buffer.BlockCopy(data, 0, buffer, 0, nb_bytes);
                    var offset = 0;
                    var count = nb_bytes;
                    try
                    {
                        stream.Write(buffer, offset, count);
                        //stream.Flush(); // TODO check performance.
                    }
                    catch (Exception exception)
                    {
                        this.textBoxLog.Text += "Exception\r\n";
                        this.textBoxLog.Text += exception.ToString() + "\r\n";
                    }
                    // Update the number of broadcasted buffers.
                    buf_brd_nb = buf_brd_nb + 1;
                    // Update the display of the number of broadcasted buffers.
                    this.textBoxBufferBroadcasted.Text = buf_brd_nb.ToString();                }
                }

                return result;
        }


        // Occurs whenever the user loads the form.
        private void Form_Load(object sender, EventArgs e)
        {
            // Initialize the combo box which lists all the MEA devices.
            ComboBoxMeaDevices_Initialize();
            // Select the only one MEA device listed by the combo box (id possible).
            if (this.comboBoxMeaDevices.Items.Count == 1)
            {
                this.comboBoxMeaDevices.SelectedIndex = 0;
            }
            // Initialize the text box which shows the hostname targeted by the UDP client.
            TextBoxDataBroadcastHostname_Initialize();
            // Initialize the text box which shows the port used by the UDP client.
            TextBoxDataBroadcastPort_Initialize();
        }
        
        // Occurs when the drop-down portion of the combo box is shown.
        private void ComboBoxMeaDevices_DropDown(object sender, EventArgs e)
        {
            ComboBoxMeaDevices_Initialize();
        }

        // TODO add docstring.
        private void ComboBoxMeaDevices_Initialize()
        {
            // Clear all the MEA devices listed by the combo box.
            comboBoxMeaDevices.Items.Clear();
            // TODO understand MCS black magic.
            usbList.Initialize(DeviceEnumNet.MCS_MEA_DEVICE);
            // Add each MEA device to the combo box.
            for (var i = 0; i < usbList.Count; i++)
            {
                var index = (uint) i;
                var usbEntry = usbList.GetUsbListEntry(index);
                var deviceName = usbEntry.DeviceName;
                var serialNumber = usbEntry.SerialNumber;
                var item = deviceName + " / " + serialNumber;
                comboBoxMeaDevices.Items.Add(item);
            }
        }

        // Occurs when the value of the selected index of the combo box (i.e. the seleted MEA device) changes.
        private void ComboBoxMeaDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            var index = (uint) comboBoxMeaDevices.SelectedIndex;
            // TODO understand MCS black magic.
            usb = usbList.GetUsbListEntry(index);
            // TODO understand MCS black magic.
            device = new CMeaDeviceNet(usb.DeviceId.BusType, ChannelDataCallback, ErrorCallback);
            // Establish a connection to the required DAQ device.
            device.Connect(usb);
            // TODO understand MCS black magic.
            device.SendStop();
            // TODO understand MCS black magic.
            var hardwareInfo = device.HWInfo();
            // Get the number of available analog hardware channels.
            hardwareInfo.GetNumberOfHWADCChannels(out nb_channels);
            // Set the number of analog hardware channels to the maximum.
            device.SetNumberOfChannels(nb_channels);
            // Update the text box which displays the number of analog hardware channels.
            textBoxNumberOfChannels.Text = nb_channels.ToString();
            // TODO understand MCS black magic.
            //hardwareInfo.GetAvailableSampleRates(out List<int> sample_rates);
            sample_rate = 20000;
            // TODO understand MCS black magic.
            var oversample = (uint) 1;
            var virtualDevice = 0;
            device.SetSampleRate(sample_rate, oversample, virtualDevice);
            textBoxSampleRate.Text = sample_rate.ToString();
            // TODO understand MCS black magic.
            gain = device.GetGain();
            textBoxGain.Text = gain.ToString();
            // TODO understand MCS black magic.
            //hardwareInfo.GetAvailableVoltageRangesInMicroVoltAndStringsInMilliVolt(out List<CMcsUsbDacqNet.CHWInfo.CVoltageRangeInfoNet> voltage_ranges);
            //var voltage_range = 10;
            // TODO understand MCS black magic.
            //device.SetVoltageRangeInMicroVolt(voltage_range);
            // TODO understand MCS black magic.
            device.EnableDigitalIn(true, virtualDevice);
            // TODO understand MCS black magic.
            device.EnableChecksum(true, virtualDevice);
            // TODO understand MCS black magic.
            //device.EnableTimestamp(false);
            // TODO understand MCS black magic.
            int analog_channels;
            int digital_channels;
            int checksum_channels;
            int timestamp_channels;
            device.GetChannelLayout(out analog_channels, out digital_channels, out checksum_channels, out timestamp_channels, out channels_in_block, (uint)virtualDevice);
            channels_in_block = device.GetChannelsInBlock();
            textBoxChannelsInBlock.Text = channels_in_block.ToString();
            // ...
            selected_channels = new bool[channels_in_block];
            for (var i = 0; i < channels_in_block; i++)
            {
                selected_channels[i] = true;
            }
            // TODO check the value of the buffer size.
            buf_size = sample_rate / 10;
            //buf_size = sample_rate / 100;
            queue_size = 20 * buf_size;
            textBoxQueueSize.Text = queue_size.ToString();
            // ...
            threshold = buf_size;
            textBoxThreshold.Text = threshold.ToString();
            // ...
            sample_size = SampleSizeNet.SampleSize16Unsigned;
            textBoxSampleSize.Text = sample_size.ToString();
            // TODO understand MCS black magic.
            device.ChannelBlock_SetCheckChecksum((uint)checksum_channels, (uint)timestamp_channels);
            // Enable control.
            textBoxQueueSize.Enabled = true;
            textBoxThreshold.Enabled = true;
            buttonDataAcquisitionStart.Enabled = true;
        }

        // TODO polish member function for the new DLL version.
        void ChannelDataCallback(CMcsUsbDacqNet UsbDacq, int cb_handle, int num_frames)
        {
            // Acquire raw data.
            int handle;
            // TODO understand MCS black magic.
            handle = 0;
            var channelEntry = 0;
            int totalChannels;
            int offset;
            int channels;
            device.ChannelBlock_GetChannel(handle, channelEntry, out totalChannels, out offset, out channels);
            // TODO understand MCS black magic.
            handle = 0;
            var frames = buf_size;
            int frames_ret;
            ushort[] data = device.ChannelBlock_ReadFramesUI16(handle, frames, out frames_ret);
            // TODO remove the following line.
            //this.textBoxLog.Text += (2 * data.Length) + " bytes\r\n";
            // Update the number of acquired buffers.
            buf_acq_nb = buf_acq_nb + 1;
            // Update the display of the number of acquired buffers.
            textBoxBufferAcquired.Text = buf_acq_nb.ToString();
            // Send data to backup if necessary.
            if (dataBackupWorker.IsBusy)
            {
                dataBackupBuffer.Add(data);
            }
            // Send data to broadcast if necessary.
            if (dataBroadcastWorker.IsBusy)
            {
                dataBroadcastBuffer.Add(data);
            }
        }
        
        // TODO add docstring.
        void ErrorCallback(string message, int action)
        {
            // throw new NotImplementedException();
            this.textBoxLog.Text += "ErrorCallback\r\n";
            this.textBoxLog.Text += message + "\r\n";
        }

        // Occurs when the 'Start' button of the 'Data acquisition' group is clicked.
        private void ButtonDataAcquisitionStart_Click(object sender, EventArgs e)
        {
            // Disable controls.
            this.comboBoxMeaDevices.Enabled = false;
            this.textBoxQueueSize.Enabled = false;
            this.textBoxThreshold.Enabled = false;
            this.buttonDataAcquisitionStart.Enabled = false;
            // Start asynchronously the data acquisition operation.
            this.dataAcquisitionWorker.RunWorkerAsync();
            // Enable controls.
            this.buttonStop.Enabled = true;
            this.groupBoxDataBackup.Enabled = true;
            this.groupBoxDataBroadcast.Enabled = true;
            this.checkBoxLockBackupBroadcast.Enabled = true;
        }

        // Occurs when the 'Stop' button of the 'Data acquisition' group is clicked.
        private void ButtonDataAcquisitionStop_Click(object sender, EventArgs e)
        {
            // Disable controls.
            this.checkBoxLockBackupBroadcast.Enabled = false;
            this.groupBoxDataBroadcast.Enabled = false;
            this.groupBoxDataBackup.Enabled = false;
            this.buttonStop.Enabled = false;
            // Stop asynchronously the data acquisition operation.
            this.dataAcquisitionWorker.CancelAsync();
            // Enable controls.
            this.buttonDataAcquisitionStart.Enabled = true;
            this.textBoxThreshold.Enabled = true;
            this.textBoxQueueSize.Enabled = true;
            this.comboBoxMeaDevices.Enabled = true;
        }

        // Occurs whe the 'Browse' button of the 'Data backup' group is clicked.
        private void ButtonDataBackupBrowse_Click(object sender, EventArgs e)
        {
            // Dialog to select file for backup.
            SaveFileDialog dataBackupFileDialog = new SaveFileDialog();
            // Default filename.
            dataBackupFileDialog.FileName = "data_backuped.raw";
            // Show backup file dialog box.
            DialogResult dialogResult = dataBackupFileDialog.ShowDialog();
            // Process backup file dialog box results.
            if (dialogResult == DialogResult.OK)
            {
                // Retrieve filename.
                var filename = dataBackupFileDialog.FileName;
                // TODO assert data backup filename is correct?
                // Set data backup filename.
                dataBackupFilename = filename;
                // Display backup filename.
                textBoxDataBackupPath.Text = dataBackupFilename;
            }
        }

        // Occurs when the value ot the text in the 'Path' box is changed.
        private void TextBoxDataBackupPath_TextChanged(object sender, EventArgs e)
        {
            // Retrieve filename.
            var filename = textBoxDataBackupPath.Text;
            // TODO addset data backup filename is correct?
            dataBackupFilename = filename;
        }

        // Occurs when the 'Start' button of the 'Data backup' group is clicked.
        private void ButtonDataBackupStart_Click(object sender, EventArgs e)
        {
            // Disable controls.
            this.buttonDataBackupStart.Enabled = false;
            this.buttonDataBackupBrowse.Enabled = false;
            this.checkBoxLockBackupBroadcast.Enabled = false;
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.buttonDataBroadcastStart.Enabled = false;
            }
            // Start asynchronously the data backup operation.
            this.dataBackupWorker.RunWorkerAsync();
            // Start asynchronously the data broadcast operation if necessary.
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.dataBroadcastWorker.RunWorkerAsync();
            }
            // Enable controls.
            this.buttonDataBackupStop.Enabled = true;
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.buttonDataBroadcastStop.Enabled = true;
            }
        }

        // Occurs when the 'Stop' button of the 'Data backup' group is clicked.
        private void ButtonDataBackupStop_Click(object sender, EventArgs e)
        {
            // Disable controls.
            this.buttonDataBackupStop.Enabled = false;
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.buttonDataBroadcastStop.Enabled = false;
            }
            // Stop asynchronously the data backup operation.
            this.dataBackupWorker.CancelAsync();
            // Stop asynchronously the data broadcast operation if necessary.
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.dataBroadcastWorker.CancelAsync();
            }
            // Enable controls.
            this.buttonDataBackupBrowse.Enabled = true;
            this.buttonDataBackupStart.Enabled = true;
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.buttonDataBroadcastStart.Enabled = true;
            }
            this.checkBoxLockBackupBroadcast.Enabled = true;
        }

        // TODO add docstring.
        private void TextBoxDataBroadcastHostname_Initialize()
        {
            textBoxDataBroadcastHostname.Text = udpClientHostname.ToString();
        }

        // TODO add docstring.
        private void TextBoxDataBroadcastPort_Initialize()
        {
            textBoxDataBroadcastPort.Text = udpClientPort.ToString();
        }

        // Occurs when the 'Start' button of the 'Data broadcast' group is clicked.
        private void ButtonDataBroadcastStart_Click(object sender, EventArgs e)
        {
            // Disable controls.
            this.buttonDataBroadcastStart.Enabled = false;
            this.checkBoxLockBackupBroadcast.Enabled = false;
            if (this.checkBoxLockBackupBroadcast.Enabled)
            {
                this.buttonDataBackupStart.Enabled = false;
                this.buttonDataBackupBrowse.Enabled = false;
            }
            // Start asynchronously the data broadcast operation.
            this.dataBroadcastWorker.RunWorkerAsync();
            // Start asynchronously the data backup operation if necessary.
            if (this.checkBoxLockBackupBroadcast.Enabled)
            {
                this.dataBackupWorker.RunWorkerAsync();
            }
            // Enable controls.
            this.buttonDataBroadcastStop.Enabled = true;
            if (this.checkBoxLockBackupBroadcast.Enabled)
            {
                this.buttonDataBackupStop.Enabled = true;
            }
        }

        // Occurs when the 'Stop' button of the 'Data broadcast' group is clicked.
        private void ButtonDataBroadcastStop_Click(object sender, EventArgs e)
        {
            // Disable controls.
            this.buttonDataBroadcastStop.Enabled = false;
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.buttonDataBackupStop.Enabled = false;
            }
            // Stop asynchronously the data broadcast operation.
            this.dataBroadcastWorker.CancelAsync();
            // Stop asynchronously the data backup operation if necessary.
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.dataBackupWorker.CancelAsync();
            }
            // Enable controls.
            this.buttonDataBroadcastStart.Enabled = true;
            if (this.checkBoxLockBackupBroadcast.Checked)
            {
                this.buttonDataBackupBrowse.Enabled = true;
                this.buttonDataBackupStart.Enabled = true;
            }
            this.checkBoxLockBackupBroadcast.Enabled = true;
        }

        // Occurs when the 'Queue size' text box is no longer active.
        private void TextBoxQueueSize_Leave(object sender, EventArgs e)
        {
            var text = textBoxQueueSize.Text;
            try
            {
                // Parse the content of the text box.
                queue_size = Int32.Parse(text);
            }
            catch (FormatException)
            {
                // Reinitialize the content of the text box.
                text = queue_size.ToString();
                textBoxQueueSize.Text = text;
            }
        }

        // Occurs when the 'Threshold' text box is no longer active.
        private void TextBoxThreshold_Leave(object sender, EventArgs e)
        {
            var text = textBoxThreshold.Text;
            try
            {
                // Parse the content of the text box.
                threshold = Int32.Parse(text);
            }
            catch (FormatException)
            {
                // Reinitialize the content of the text box.
                text = threshold.ToString();
                textBoxThreshold.Text = text;
            }
        }
    }
}
