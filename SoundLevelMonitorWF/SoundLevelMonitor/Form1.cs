// Copyright (C) 2017 by David W. Jeske
// Released to the Public Domain

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoundLevelMonitor
{
    public partial class Form1 : Form
    {
        AudioLevelMonitor audioMonitor;

        public Form1() {
            InitializeComponent();
            this.Text = "SoundLevelMonitor";
            audioMonitor = new AudioLevelMonitor();
            audioLevelsControl.AudioMonitor = audioMonitor;
        }
    }
}
