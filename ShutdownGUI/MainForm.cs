﻿/*
 * FOG Service : A computer management client for the FOG Project
 * Copyright (C) 2014-2016 FOG Project
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 3
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Zazzles;
using Zazzles.Data;

namespace FOG
{
    public partial class MainForm : Form
    {
        private const string LogName = "Shutdown GUI";
        private const int OSXTitleBarHeight = 22;

        // Provide 5 different delay options, starting at 15 minutes
        private const int NumberOfDelays = 5;
        private const int StartingDelay = 15;

        private readonly int _gracePeriod = 600;
        private readonly dynamic _transport;
        private Power.ShutdownOptions _options;
        private int _aggregatedDelayTime;

        public MainForm(string[] args)
        {
            if (args.Length == 0) Environment.Exit(1);
            Log.Output = Log.Mode.Quiet;

            //_transport = new JObject();
            //_transport.options = 1;
            //_transport.aggregatedDelayTime = 0;
            //_transport.period = 600;
            _transport = JObject.Parse(Transform.DecodeBase64(args[0]));
            InitializeComponent();

            // Retrieve what configuration the prompt should use
            _options = Enum.Parse(typeof(Power.ShutdownOptions), _transport.options.ToString());

            btnAbort.Text = (_options == Power.ShutdownOptions.Abort)
                ? "Cancel"
                : "Hide";

            _aggregatedDelayTime = _transport.aggregatedDelayTime;

            if (_transport.period == null) return;
            _gracePeriod = _transport.period;

            Log.Entry(LogName, _gracePeriod.ToString());
            if (_gracePeriod == 0)
                throw new Exception("Invaid gracePeriod");

            textBox1.Text = GenerateMessage();
            textBox1.Select(0, 0);

            progressBar1.Maximum = _gracePeriod - 1;
            label1.Text = Time.FormatSeconds(_gracePeriod);

            GenerateDelays();
            PositionForm();

            Bus.SetMode(Bus.Mode.Client);
            Bus.Subscribe(Bus.Channel.Power, onPower);
        }

        private void GenerateDelays()
        {
            var delays = new SortedDictionary<int, string>();
            var currentDelay = StartingDelay;

            for (var i = 0; i < NumberOfDelays; i++)
            {
                if (currentDelay + _aggregatedDelayTime > Power.MaxDelayTime)
                    break;

                var readableTime = Time.FormatMinutes(currentDelay);
                // Delays are processed in minutes
                delays.Add(currentDelay, readableTime);

                // Double the delay for the next increment
                currentDelay = currentDelay*2;
            }

            if (delays.Count == 0)
            {
                comboPostpone.Enabled = false;
                btnPostpone.Enabled = false;
                return;
            }

            comboPostpone.DataSource = new BindingSource(delays, null);
            comboPostpone.DisplayMember = "Value";
            comboPostpone.ValueMember = "Key";

        }

        private void PositionForm()
        {
            var workingArea = Screen.GetWorkingArea(this);
            var height = workingArea.Bottom - Size.Height;

            // Account for the title bar on OSX which offsets the height
            if (Settings.OS == Settings.OSType.Mac) height = height - OSXTitleBarHeight;

            Location = new Point(workingArea.Right - Size.Width, height);
        }

        private string GenerateMessage()
        {
            string message = (_transport.message != null) 
                ? _transport.message.ToString() 
                : "This computer needs to perform maintenance.";
            message += " Please save all work and close programs.";

            return message;
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (progressBar1.Value >= progressBar1.Maximum)
                Environment.Exit(0);
            progressBar1.Value++;
            progressBar1.Update();
            label1.Text = Time.FormatSeconds(_gracePeriod - progressBar1.Value);
        }

        private void BtnNowClick(object sender, EventArgs e)
        {
            _transport.action = "now";
            Bus.Emit(Bus.Channel.Power, _transport, true);
            Environment.Exit(0);
        }

        private void BtnAbortClick(object sender, EventArgs e)
        {
            if (_options != Power.ShutdownOptions.Abort)
                Environment.Exit(0);

            _transport.action = "abort";
            Bus.Emit(Bus.Channel.Power, _transport, true);
            Environment.Exit(1);
        }

        private void BtnPostponeClick(object sender, EventArgs e)
        {
            _transport.action = "delay";
            _transport.delay = comboPostpone.SelectedValue;
            Bus.Emit(Bus.Channel.Power, _transport, true);
            Environment.Exit(1);
        }

        private void onPower(dynamic data)
        {
            if (data.action == null) return;

            if (data.action.ToString() == "abort" || data.action.ToString() == "delay")
                Environment.Exit(2);
        }
    }
}
