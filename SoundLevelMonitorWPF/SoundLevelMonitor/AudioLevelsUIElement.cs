// Copyright (C) 2017 by David W. Jeske
// Released to the Public Domain

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SD = System.Drawing;

namespace SoundManager
{
    // https://stackoverflow.com/questions/3952887/gdi-like-drawing-in-wpf

    // https://stackoverflow.com/questions/16107877/fast-2d-graphics-in-wpf

    // https://stackoverflow.com/questions/21891004/how-to-add-text-to-a-bitmap-image-programmatically-wpf

    // https://stackoverflow.com/questions/16037753/wpf-drawing-on-canvas-with-mouse-events

    // https://www.codeproject.com/Articles/28526/Introduction-to-D-DImage

    // it seems possible that WritableBitmap.AddDirtyRect() is fast
    //  so one would have to:
    //   (1) make an Image
    //   (2) set the image source to a WritableBitmap
    //   (3) draw to a drawing Visual, render that to a RenderTargetBitmap..
    //   (4) then copy that to the WritableBitmap

    // https://social.msdn.microsoft.com/Forums/vstudio/en-US/84299fec-94a1-49a1-b3bc-ec48b8bdf04f/getting-a-drawingcontext-for-a-writeablebitmap?forum=wpf
    // https://msdn.microsoft.com/en-us/library/system.windows.media.imaging.writeablebitmap_methods%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396


    // or you can actually just use System.Drawing to draw on a WritableBitmap
    // https://stackoverflow.com/a/797519/519568

    // or there is the WriteableBitmapEx extension
    // https://github.com/teichgraf/WriteableBitmapEx/

    public class AudioLevelsUIElement : Image
    {
        public AudioLevelMonitor AudioMonitor { get; set; }
        System.Windows.Threading.DispatcherTimer dispatcherTimer;
        List<SD.Pen> pens = new List<SD.Pen>();
        Dictionary<int,SD.Pen> pidToPen = new Dictionary<int,SD.Pen>();
        WriteableBitmap backingStore;

        SD.Pen greenPen = new SD.Pen(SD.Brushes.Green, 0.5f);


        public AudioLevelsUIElement() {
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 10); // 10ms
            dispatcherTimer.Start();

            // populate pens
            pens.Add(new SD.Pen(SD.Brushes.Crimson,1.0f));
            pens.Add(new SD.Pen(SD.Brushes.DarkKhaki,1.0f));
            pens.Add(new SD.Pen(SD.Brushes.FloralWhite,1.0f));
            pens.Add(new SD.Pen(SD.Brushes.HotPink,1.0f));

            backingStore = new WriteableBitmap(500,500,97,97,PixelFormats.Bgr24,null);
            Redraw();
            Source = backingStore;
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e) {
            // this is really expensive, because it causes re-layout, but it's the only way to
            // get a UIElement to repaint in WPF...
            // this.InvalidateVisual();
            Redraw();
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
        private SD.Pen penForPid(int pid) {
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

        private void Redraw() {
            var wb = backingStore;
            wb.Lock();
            var bmp = new SD.Bitmap(wb.PixelWidth, wb.PixelHeight,
                                                 wb.BackBufferStride,
                                                 SD.Imaging.PixelFormat.Format24bppRgb,
                                                 wb.BackBuffer);

            SD.Graphics g = SD.Graphics.FromImage(bmp); // Good old Graphics

            try {
                var size = new SD.Size(wb.PixelWidth,wb.PixelHeight);
                Redraw(size,g);
            } finally {
            
                g.Dispose();
                bmp.Dispose();
                wb.AddDirtyRect(new Int32Rect(0,0,wb.PixelWidth,wb.PixelHeight));
                wb.Unlock();
            }
        }

       
        private void RenderVUMeterGrid(SD.Size size, SD.Graphics g, double maxSample) {
            // make it look like a VU meter
            // g.FillRectangle(Brushes.Black,this.Bounds);   
            g.FillRectangle(SD.Brushes.Black, 0, 0, size.Width, size.Height);

            // draw gridlines every 0.1
            for (double x = 0.0; x < maxSample; x += 0.01) {
                int y = (int)(size.Height - (size.Height * (x / maxSample)));
                g.DrawLine(greenPen,
                    new SD.Point(0, y),
                    new SD.Point(size.Width, y));
            }
        }

        private void Redraw(SD.Size size, SD.Graphics g) {
            // g.FillRectangle(System.Drawing.Brushes.Red, new System.Drawing.Rectangle(0,0,size.Width,size.Height));

            // if we have no AudioMonitor draw a blank grid
            if (AudioMonitor == null) {
                RenderVUMeterGrid(size,g, 1.0);
                return;
            }
            // otherwise get samples, and draw a scaled rgid            
            var activeSamples = AudioMonitor.GetActiveSamples();
            double maxSample = computeMaxSampleLastN(activeSamples, size.Width);
            maxSample = Math.Max(maxSample, 0.05); // make sure we don't divide by zero
            RenderVUMeterGrid(size,g, maxSample);


            // now draw the individual sample lines                        
            foreach (var kvp in activeSamples) {
                SD.Pen audioLevelPen = penForPid(kvp.Value.pid);
                string name = kvp.Value.WindowTitle;
                double[] samples = kvp.Value.samples;

                double last_sample = samples[samples.Length - 1];
                for (int x = 0; x < samples.Length - 1; x++) {
                    if (x > size.Width) {
                        goto next_process;
                    }
                    var sample = samples[samples.Length - (x + 1)];
                    g.DrawLine(audioLevelPen,
                        new SD.Point(size.Width - x, (int)(size.Height - (size.Height * (last_sample / maxSample)))),
                        new SD.Point(size.Width - (x + 1), (int)(size.Height - (size.Height * (sample / maxSample)))));
                    last_sample = sample;
                }
                next_process:;
            }



            // and finally draw the legend
            // http://csharphelper.com/blog/2015/05/get-font-metrics-in-a-wpf-program-using-c/
            // http://csharphelper.com/blog/2015/04/render-text-easily-in-a-wpf-program-using-c/
            List<int> pidList = activeSamples.Keys.ToList();
            pidList.Sort();
            var font = SD.SystemFonts.DefaultFont;
            // first time is to measure the height to draw the legend box
            {
                float y_start = 5;
                float max_label_width = 0;

                foreach (int pid in pidList) {
                    string name = activeSamples[pid].WindowTitle;
                    var measure = g.MeasureString(name, font);
                    y_start += measure.Height;
                    max_label_width = Math.Max(max_label_width, measure.Width);
                    y_start += 10; // vertical padding
                }
                // draw the legend box
                g.FillRectangle(SD.Brushes.Black, 5, 10, max_label_width + 10, y_start);
                g.DrawRectangle(greenPen, 5, 10, max_label_width + 10, y_start);
            }


            // now draw the legend labels
            {
                float y_start = 5;

                foreach (int pid in pidList) {
                    string name = activeSamples[pid].WindowTitle;
                    SD.Pen pen = this.penForPid(pid);
                    var brush = pen.Brush;

                    var measure = g.MeasureString(name, font);
                    y_start += measure.Height;
                    g.DrawString(name, font, brush, new SD.PointF(10, y_start));

                    y_start += 10; // vertical padding
                }
            }

        }
    }
}
