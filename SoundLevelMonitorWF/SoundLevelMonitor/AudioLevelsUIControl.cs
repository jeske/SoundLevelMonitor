// Copyright (C) 2017 by David W. Jeske
// Released to the Public Domain

using System;
using System.Collections.Generic;

using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SoundLevelMonitor
{
    class AudioLevelsUIControl : Control
    {
        AudioLevelMonitor _audioMonitor;
        List<Pen> pens = new List<Pen>();
        Dictionary<int, Pen> pidToPen = new Dictionary<int, Pen>();
        Timer dispatcherTimer;
        Pen greenPen = new Pen(Brushes.Green, 0.5f);


        public AudioLevelsUIControl() {
            DoubleBuffered = true;
            dispatcherTimer = new Timer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = 100;            
            dispatcherTimer.Start();

            // populate pens                        
            pens.Add(new Pen(Brushes.Crimson, 1.0f));
            pens.Add(new Pen(Brushes.DarkKhaki, 1.0f));
            pens.Add(new Pen(Brushes.FloralWhite, 1.0f));
            pens.Add(new Pen(Brushes.HotPink, 1.0f));
            pens.Add(new Pen(Brushes.Yellow, 1.0f));
            pens.Add(new Pen(Brushes.Lavender, 1.0f));
            pens.Add(new Pen(Brushes.Cyan, 1.0f));
            pens.Add(new Pen(Brushes.Maroon, 1.0f));


            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e) {
            dispatcherTimer.Stop();
            this.Invalidate();
        }

        public AudioLevelMonitor AudioMonitor {
            get { return _audioMonitor; }
            set {
                _audioMonitor = value; 
                if (_audioMonitor != null) {
                }
            }
        }

        private void _audioMonitor_NewAudioSamplesEventListeners(AudioLevelMonitor monitor) {
        }      

        private void RenderVUMeterGrid(Graphics g, double maxSample) {
            // make it look like a VU meter
            // g.FillRectangle(Brushes.Black,this.Bounds);   
            g.FillRectangle(Brushes.Black,0,0,Size.Width,Size.Height);

            // draw gridlines every 0.1
            for (double x = 0.0; x < maxSample; x += 0.01) {
                int y = (int)(Size.Height - (Size.Height * (x / maxSample)));
                g.DrawLine(greenPen,
                    new Point(0, y),
                    new Point(Size.Width, y));
            }

        }

        // this feels expensive, but i'm not sure how else to do it
        private double computeMaxSampleLastN(IDictionary<int, AudioLevelMonitor.SampleInfo> sampleMap, int lastN) {
            double maxSample = 0.0;
            foreach (var kvp in sampleMap) {
                var samples = kvp.Value.samples;
                for (int i = 1; i <= samples.Length; i++) {
                    if (i > lastN) {
                        goto next_process;
                    }

                    var val = samples[samples.Length - i];
                    maxSample = Math.Max(maxSample, val);
                    if (maxSample > 0.9) {
                        return 1.0; // save time
                    }
                    next_process:;
                }

            }
            return maxSample;
        }
        int nextPenToAllocate = -1;
        private Pen penForPid(int pid) {
            if (nextPenToAllocate < 0) {
                nextPenToAllocate = Math.Abs((int)DateTime.Now.Ticks) % (pens.Count - 1);
            }

            if (pidToPen.ContainsKey(pid)) {
                return pidToPen[pid];
            }
            else {
                // allocate a new pen
                var allocatedPen = pidToPen[pid] = pens[nextPenToAllocate];
                nextPenToAllocate = (nextPenToAllocate + 1) % (pens.Count - 1);
                return allocatedPen;
            }
        }      

        protected override void OnPaint(PaintEventArgs pe) {
            base.OnPaint(pe);
            var g = pe.Graphics;

            // if we have no AudioMonitor draw a blank grid
            if (AudioMonitor == null) {
                RenderVUMeterGrid(g, 1.0);
                return;
            }
            // otherwise get samples, and draw a scaled rgid            
            var activeSamples = AudioMonitor.GetActiveSamples();
            double maxSample = computeMaxSampleLastN(activeSamples, this.Size.Width);           
            maxSample = Math.Max(maxSample, 0.05); // make sure we don't divide by zero
            RenderVUMeterGrid(g, maxSample);            

            // now draw the individual sample lines                        
            foreach (var kvp in activeSamples) {
                Pen audioLevelPen = penForPid(kvp.Value.pid);
                string name = kvp.Value.WindowTitle;
                double[] samples = kvp.Value.samples;

                double last_sample = samples[samples.Length - 1];
                for (int x = 0; x < samples.Length - 1; x++) {
                    if (x > Size.Width) {
                        goto next_process;
                    }
                    var sample = samples[samples.Length - (x + 1)];
                    g.DrawLine(audioLevelPen,
                        new Point(Size.Width - x, (int)(Size.Height - (Size.Height * (last_sample / maxSample)))),
                        new Point(Size.Width - (x + 1), (int)(Size.Height - (Size.Height * (sample / maxSample)))));
                    last_sample = sample;
                }
                next_process:;
            }


            
            // and finally draw the legend
            // http://csharphelper.com/blog/2015/05/get-font-metrics-in-a-wpf-program-using-c/
            // http://csharphelper.com/blog/2015/04/render-text-easily-in-a-wpf-program-using-c/
            List<int> pidList = activeSamples.Keys.ToList();
            pidList.Sort();
            var font = SystemFonts.DefaultFont;
            // first time is to measure the height to draw the legend box
            {
                float y_start = 5;
                float max_label_width = 0;

                foreach (int pid in pidList) {
                    string name = activeSamples[pid].WindowTitle;            
                    var measure = g.MeasureString(name, font);
                    y_start += measure.Height;             
                    max_label_width = Math.Max(max_label_width,measure.Width);
                    y_start += 10; // vertical padding
                }
                // draw the legend box
                g.FillRectangle(Brushes.Black,5,10,max_label_width + 10,y_start);
                g.DrawRectangle(greenPen,5,10,max_label_width + 10,y_start);
            }


            // now draw the legend labels
            {
                float y_start = 5;

                foreach (int pid in pidList) {
                    string name = activeSamples[pid].WindowTitle;
                    Pen pen = this.penForPid(pid);
                    var brush = pen.Brush;

                    var measure = g.MeasureString(name,font);
                    y_start += measure.Height;
                    g.DrawString(name, font, brush, new PointF(10, y_start));

                    y_start += 10; // vertical padding
                }
            }

            dispatcherTimer.Start();
        }

    }
}
