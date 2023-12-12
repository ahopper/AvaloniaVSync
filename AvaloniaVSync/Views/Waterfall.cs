using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using AvaloniaVSync.ViewModels;
using SkiaSharp;
using static System.Formats.Asn1.AsnWriter;

namespace AvaloniaVSync.Views
{
    struct palEntry
    {
        public int index;
        public int red;
        public int green;
        public int blue;
    }
    public class Waterfall : Control
    {
        private ConcurrentQueue<float[]> _traceQueue = new();
        private bool _running = false;

        public Waterfall()
        {
            ClipToBounds = true;
            _palette = BuildPallete();
        }

        private UInt32[] _palette;
        private SKBitmap? _bitmap;
        private TopLevel? _topLevel = null;

        public static readonly DirectProperty<Waterfall, float[]> TraceProperty =
            AvaloniaProperty.RegisterDirect<Waterfall, float[]>(
                nameof(Trace),
                o => o.Trace,
                (o, v) => o.Trace = v);

        private float[] _trace = new float[0];

        public float[] Trace
        {
            get { return _trace; }
            set { SetAndRaise(TraceProperty, ref _trace, value); }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            
            base.OnAttachedToVisualTree(e);
            _topLevel = TopLevel.GetTopLevel(this);
            _topLevel?.RequestAnimationFrame(BuildFrame);
            _running = true;
            
        }
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _running = false;
            _topLevel = null;


        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == TraceProperty)
            {
                if (_running)
                {
                    _traceQueue.Enqueue(Trace);
                    InvalidateVisual();
                }
            }
            base.OnPropertyChanged(change);
        }
       

        private void BuildFrame(TimeSpan time)
        {
        
            
            if (Trace != null)
            {
                if (_bitmap == null || _bitmap.Width != (int)Bounds.Width)
                {
                    _bitmap = new SKBitmap((int)Bounds.Width, 1, true);
                }
                var trace = Trace;

                if (trace.Length > 0)
                {
                    unsafe
                    {
                        UInt32* pPixels = (UInt32*)_bitmap.GetPixels();

       
                        for (int i = 0; i < Math.Min(trace.Length, _bitmap.Width); i++)
                        {
                             *(pPixels++)=_palette[(int)(trace[i]*767)];
                      
                        }
                        System.Threading.Thread.Sleep(10);

                    }
                }

      
                InvalidateVisual();
            }
            _topLevel?.RequestAnimationFrame(BuildFrame);

        }
        class CustomDrawOp : ICustomDrawOperation
        {
            private readonly FormattedText _noSkia;

            private SKBitmap _bitmap;
    
            private SKImage _lastImage;

            private Waterfall _waterfall;

            private float[]? _lastTrace;
            public CustomDrawOp(Rect bounds, FormattedText noSkia, Waterfall waterfall)
            {
                _noSkia = noSkia;
                Bounds = bounds;
                _waterfall=waterfall;

               
                _bitmap = new SKBitmap((int)Bounds.Width, 1, true);
                
            }

            public void Dispose()
            {
                // No-op
            }

            public Rect Bounds { get; }
            public bool HitTest(Point p) => false;
            public bool Equals(ICustomDrawOperation other) => false;
            static Stopwatch St = Stopwatch.StartNew();

            public void Render(ImmediateDrawingContext context)
            {
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();

                if (leaseFeature == null)
                {
                    //    using (var c = new DrawingContext(context, false))
                    //    {
                    //        c.DrawText(_noSkia, new Point());
                    //   }
                }
                else
                {


                    using var lease = leaseFeature.Lease();
                    var canvas = lease.SkCanvas;

                    unsafe
                    {
                        UInt32* pPixels = (UInt32*)_bitmap.GetPixels();
                        float[]? trace;

                        if (!_waterfall._traceQueue.TryDequeue(out trace))
                        {
                            // draw may not be triggered by new data
                            trace = _lastTrace;

                        }
                        if (trace != null)
                        {
                            for (int i = 0; i < Math.Min(trace.Length, _bitmap.Width); i++)
                            {
                                //     _bitmap.SetPixel(i, 0, new SKColor(_palette[(int)(trace[i] * 767)]));
                                *(pPixels++) = _waterfall._palette[(int)(trace[i] * 767)];

                            }
                            _lastTrace = trace;
                        }
                    
                        // keep queue from growing if a render is dropped for any reason
                        if (_waterfall._traceQueue.Count > 3)
                        {
                            Console.WriteLine($"R {_waterfall._traceQueue.Count}");
                            _waterfall._traceQueue.TryDequeue(out trace);


                        }
                    }

                    canvas.Save();

                    canvas.DrawBitmap(_bitmap, 0, 0);
                   
                    var scale = canvas.TotalMatrix.ScaleX;
                    
                    canvas.Scale(1.0F/scale);

                    if(_lastImage!=null)
                    {
                        canvas.DrawImage(_lastImage, 0, 1);
                        _lastImage.Dispose();
                    }

                    
                    SKRectI rect;
                    canvas.GetDeviceClipBounds(out rect);

                    //snapshot will be at final screen coords and scale
                    //i.e. post all transforms
                    // we need to blit it at that scale
                    
                    _lastImage = lease.SkSurface.Snapshot(
                       rect);

                    canvas.Restore();
                    
                 
                }              
            }  
        }

        CustomDrawOp customDrawOp = null;
        public override void Render(DrawingContext context)
        {
            if (_bitmap != null)
            {

                if (customDrawOp == null || Bounds != customDrawOp.Bounds)
                {
                    _bitmap = new SKBitmap((int)Bounds.Width, 1, true);
                    var noSkia = new FormattedText("Current rendering API is not Skia", CultureInfo.CurrentCulture,
                     FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.Black);
                    customDrawOp = new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), noSkia, this);
                }

                context.Custom(customDrawOp);
            }
//            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }
        private UInt32[] BuildPallete()
        {
            
            List<palEntry> k = new List<palEntry> {
                new palEntry() { index = 0, red = 0,green=0,blue=0 },
                new palEntry() { index = 150, red = 0,green=0,blue=255 },
                new palEntry() { index = 300, red = 0,green=255,blue=255 },
                new palEntry() { index = 450, red = 0,green=255,blue=0 },
                new palEntry() { index = 600, red = 255,green=255,blue=0 },
                new palEntry() { index = 767, red = 255,green=0,blue=0 },
                      };

            UInt32[] palete = new UInt32[768];

            
            int idx = 0;
            for (int i = 0; i < k.Count - 1; i++)
            {
                var start = k[i];
                var end = k[i + 1];

                while (idx <= end.index)
                {
                    var pos = idx - start.index;
                    var range = end.index - start.index;

                    var r = Math.Clamp(start.red * (range - pos) / range + end.red * pos / range, 0, 255);
                    var g = Math.Clamp(start.green * (range - pos) / range + end.green * pos / range, 0, 255);
                    var b = Math.Clamp(start.blue * (range - pos) / range + end.blue * pos / range, 0, 255);

                    var val = (uint)(0xff000000 + b + 256 * g + 256 * 256 * r);
                    palete[idx++] = val;
                }
            }
            return palete;
        }
    }
}
