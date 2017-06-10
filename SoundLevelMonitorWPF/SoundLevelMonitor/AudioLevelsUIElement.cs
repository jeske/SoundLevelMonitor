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

namespace SoundLevelMonitorWPF
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

    // how to make better quality lines
    // https://stackoverflow.com/questions/22283687/how-to-make-wpf-lines-a-better-quality-drawing

    public class AudioLevelsUIElement : UIElement
    {
        public AudioLevelMonitor AudioMonitor { get; set; }        
        List<Pen> pens = new List<Pen>();
        Dictionary<int,Pen> pidToPen = new Dictionary<int,Pen>();
        DrawingGroup backingStore;
        Pen greenPen = new Pen(Brushes.Green, 1.0);

        public AudioLevelsUIElement() {
            // this is pretty useless, as it makes some lines disappear!
            // RenderOptions.SetEdgeMode((DependencyObject)this, EdgeMode.Aliased);

            // this doesn't seem to do anything
            // RenderOptions.SetBitmapScalingMode((DependencyObject)this,BitmapScalingMode.HighQuality);
            
            // this helps make thin lines a little clearer
            SnapsToDevicePixels = true;


            // populate pens
            pens.Add(new Pen(Brushes.Crimson, 1.0f));
            pens.Add(new Pen(Brushes.DarkKhaki, 1.0f));
            pens.Add(new Pen(Brushes.FloralWhite, 1.0f));
            pens.Add(new Pen(Brushes.HotPink, 1.0f));
            pens.Add(new Pen(Brushes.Yellow, 1.0f));
            pens.Add(new Pen(Brushes.Lavender, 1.0f));
            pens.Add(new Pen(Brushes.Cyan, 1.0f));            


            foreach (var pen in pens) {
                pen.Freeze();
            }
            greenPen.Freeze();

            backingStore = new DrawingGroup();            
            Render();

            CompositionTarget.Rendering += CompositionTarget_Rendering;
            // SizeChanged += AudioLevelsUIElement_SizeChanged;            
            
        }

        // protected override void OnRenderSizeChanged(SizeChangedInfo info) {
            // base.OnRenderSizeChanged(info);
            // Render();
        // }


#if false
        private void AudioLevelsUIElement_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (RenderSize.Width == 0) {
                Source = backingStore = null;
            } else {
                Source = backingStore = 
                    new RenderTargetBitmap((int)RenderSize.Width, (int)RenderSize.Height, 97, 97, PixelFormats.Pbgra32);
            }
        }
#endif

        private void CompositionTarget_Rendering(object sender, EventArgs e) {
            this.Render();
        }

        private void RenderVUMeterGrid(DrawingContext drawingContext, double maxSample) {
            // make it look like a VU meter
            drawingContext.DrawRectangle(Brushes.Black, new Pen(),
                new Rect(this.RenderSize));

            double gridLine_step = 0.01;
            double gridLine_log = gridLine_step * 2;
            
            for(double x=0.0; x<maxSample;x+= gridLine_step) {                
                double y = this.RenderSize.Height - (this.RenderSize.Height * (x/maxSample));
                
                // make the grid-lines pixel boundary aligned.
                y = Math.Ceiling(y) +0.5;
                
                drawingContext.DrawLine(greenPen,
                    new Point(0,y),
                    new Point(this.RenderSize.Width,y));

                // logarithmic gridlines
                if (x >= gridLine_log) {
                    gridLine_step *= 2;
                    gridLine_log = gridLine_step * 2;
                }
            }

        }

        // this feels expensive, but i'm not sure how else to do it
        private double computeMaxSampleLastN(IDictionary<int,AudioLevelMonitor.SampleInfo> sampleMap,int lastN) {
            double maxSample = 0.0;
            foreach (var kvp in sampleMap) {
                var samples = kvp.Value.samples;
                for (int i=1;i <= samples.Length; i++) {
                    if (i > lastN) {
                        goto next_process;
                    }

                    var val = samples[samples.Length-i];
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
                nextPenToAllocate = Math.Abs((int)DateTime.Now.Ticks) % (pens.Count -1);
            }

            if (pidToPen.ContainsKey(pid)) {
                return pidToPen[pid];
            } else {
                // allocate a new pen
                var allocatedPen = pidToPen[pid] = pens[nextPenToAllocate];
                nextPenToAllocate = (nextPenToAllocate+1) % (pens.Count-1);
                return allocatedPen;
            }
        }

        protected override void OnRender(DrawingContext drawingContext) {
            Render();
            base.OnRender(drawingContext);            
            drawingContext.DrawDrawing(backingStore);
        }

        private void Render() {            
            var drawingContext = backingStore.Open();
            Render(drawingContext);
            drawingContext.Close();            
        }

        private void Render(DrawingContext drawingContext) {                                              
            // if we have no AudioMonitor draw a blank grid
            if (AudioMonitor == null) {
                RenderVUMeterGrid(drawingContext,1.0);
                return;
            }
            // otherwise get samples, and draw a scaled rgid            
            var activeSamples = AudioMonitor.GetActiveSamples();
            double maxSample = computeMaxSampleLastN(activeSamples,(int)this.RenderSize.Width);
            maxSample = Math.Max(maxSample, 0.05); // make sure we don't divide by zero
            RenderVUMeterGrid(drawingContext, maxSample);

            // now draw the individual sample lines                        
            foreach (var kvp in activeSamples) {
                Pen audioLevelPen = penForPid(kvp.Value.pid);
                string name = kvp.Value.WindowTitle;
                double[] samples = kvp.Value.samples;

                StreamGeometry geometry = new StreamGeometry();
                StreamGeometryContext gc = geometry.Open();

                double last_sample = samples[samples.Length-1];
                for (int x=0;x<samples.Length-1;x++) {
                    if (x > (int)this.RenderSize.Width) {
                        goto next_process;
                    }
                    var sample = samples[samples.Length-(x+1)];
                    var p1 = new Point(this.RenderSize.Width - x, (int)(this.RenderSize.Height - (this.RenderSize.Height * (last_sample / maxSample))));
                    var p2 = new Point(this.RenderSize.Width - (x + 1), (int)(this.RenderSize.Height - (this.RenderSize.Height * (sample / maxSample))));
                    // drawingContext.DrawLine(audioLevelPen,p1, p2);

                    // is this faster than DrawLine() ??? 
                    { 
                        gc.BeginFigure(p1, false, false);
                        gc.LineTo(p2,true, true);
                    }

                    last_sample = sample;
                }
                next_process: ;

                gc.Close();
                geometry.Freeze();
                drawingContext.DrawGeometry(null, audioLevelPen, geometry);

            }

            // and finally draw the legend
            // http://csharphelper.com/blog/2015/05/get-font-metrics-in-a-wpf-program-using-c/
            // http://csharphelper.com/blog/2015/04/render-text-easily-in-a-wpf-program-using-c/
            List<int> pidList = activeSamples.Keys.ToList();
            pidList.Sort();
            var typeface = new Typeface("Ariel");            
            // first figure out how tall it is
            {
                double y_start = 5;
                double max_text_width = 0;
                
                foreach(int pid in pidList) {
                    var brush = penForPid(pid).Brush;
                    var formattedText = 
                        new FormattedText(
                            activeSamples[pid].WindowTitle,
                            CultureInfo.CurrentUICulture,
                            FlowDirection.LeftToRight,
                            typeface, 12, brush);

                    max_text_width = Math.Max(max_text_width,formattedText.Width);
                    y_start += formattedText.Height;                    
                    y_start += 10; // vertical padding
                }                
                // draw the box
                drawingContext.DrawRectangle(
                    Brushes.Black,greenPen,
                    new Rect(5,10,max_text_width + 10,y_start));
            }

            // Now draw the text
            {
                double y_start = 5;

                foreach (int pid in pidList) {
                    var brush = penForPid(pid).Brush;
                    var formattedText =
                        new FormattedText(
                            activeSamples[pid].WindowTitle,
                            CultureInfo.CurrentUICulture,
                            FlowDirection.LeftToRight,
                            typeface, 12, brush);

                    y_start += formattedText.Height;
                    drawingContext.DrawText(formattedText, new Point(10, y_start));
                    y_start += 10; // vertical padding
                }
            }


        }
    }
}
