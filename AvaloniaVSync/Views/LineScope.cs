using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace AvaloniaVSync.Views
{
    public class LineScope : Control
    {
        private ConcurrentQueue<float[]> _traceQueue = new();
        private bool _running=false;

        public static readonly DirectProperty<LineScope, float[]> TraceProperty =
           AvaloniaProperty.RegisterDirect<LineScope, float[]>(
               nameof(Trace),
               o => o.Trace,
               (o, v) => o.Trace = v);

        private float[] _trace = new float[0];

        public float[] Trace
        {
            get { return _trace; }
            set { SetAndRaise(TraceProperty, ref _trace, value); }
        }
        public LineScope()
        {
            ClipToBounds = true;
 
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
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _running = true;
        }
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _running = false;
            base.OnDetachedFromVisualTree(e);
        }


        class CustomDrawOp : ICustomDrawOperation
        {
            private readonly FormattedText _noSkia;

            private SKBitmap _bitmap;
            
            private SKImage _lastImage;

            SKRuntimeEffect effect;

            private LineScope _scope;
            
            private float[]? _lastTrace;

            public CustomDrawOp(Rect bounds, FormattedText noSkia, LineScope scope)
            {
                _noSkia = noSkia;
                Bounds = bounds;
                _bitmap = new SKBitmap((int)Bounds.Width, 1, true);
                _scope = scope;
                
                //
                var src = @"
in fragmentProcessor trace_points;


half4 main(float2 p) {

//get y values on either side
	half now = sample(trace_points, p).r*256;
    half prev = sample(trace_points, float2(p.x-1,0)).r*256;
    half next = sample(trace_points, float2(p.x+1,0)).r*256;

    half4 color=half4(0,0,0,1);


    float sep = (prev - now);
	float dist;
	if (abs(sep)<=0.1)
	{
		dist = abs(p.y - now);
	}
	else
	{
		dist = (p.y - now) / sep;
	}
	if (dist >= 0.0 && dist <= 1.0) color.g = color.g + (1-dist*0.9);


	sep = (next - now);
	if (abs(sep) <= 0.1)
	{
		dist = abs(p.y - now);
	}
	else
	{
		dist = (p.y - now)/sep;
	}

	if (dist >= 0.0 && dist <= 1.0) color.g = color.g + (1-dist*0.9);

	color.r = color.g;
    
    
    return color;
}";


                effect = SKRuntimeEffect.Create(src, out var errorText);
                if(effect==null && errorText!=null)
                {
                    Console.WriteLine(errorText);
                }
            }

            public void Dispose()
            {
                // No-op
            }

            public Rect Bounds { get; }
            public bool HitTest(Point p) => false;
            public bool Equals(ICustomDrawOperation other) => false;

            double phase = 0;
            Random rand = new();
            
            public void Render(ImmediateDrawingContext context)
            {
                unsafe
                {
                    UInt32* pPixels = (UInt32*)_bitmap.GetPixels();
                    float[]? trace;

                    if (!_scope._traceQueue.TryDequeue(out trace))
                    {
                        // draw may not be triggered by new data
                        trace = _lastTrace;
                        
                    }
                    if(trace!=null)
                    { 
                        for (int i = 0; i < trace.Length; i++)
                        {
                            *(pPixels++) = (UInt32)(0xff000000u + ((int)(trace[i] * 255)) * 256 * 256);
                        }
                        _lastTrace = trace;
                    }
                   
                    // keep queue from growing if a render is dropped for any reason
                    if(_scope._traceQueue.Count>3)
                    {    
                        _scope._traceQueue.TryDequeue(out trace);
                    }
                }

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
                    if (canvas != null)
                    {
                        canvas.Save();

                        Shade(canvas);

                        canvas.Restore();
                    }
                    
                }              
            }  
            private void Shade(SKCanvas canvas)
            {

                var inputs = new SKRuntimeEffectUniforms(effect);
                
                using var textureShader = _bitmap.ToShader();
                var children = new SKRuntimeEffectChildren(effect);
                children["trace_points"]= textureShader;

                using var shader = effect.ToShader(true,inputs, children);

                using var paint = new SKPaint { Shader = shader };
                canvas.DrawRect(0,0,(float)Bounds.Width,(float)Bounds.Height, paint);
                
            }
        }

        CustomDrawOp customDrawOp = null;
        public override void Render(DrawingContext context)
        {
       
            if(customDrawOp == null || Bounds!=customDrawOp.Bounds)
            {
                var noSkia = new FormattedText("Current rendering API is not Skia", CultureInfo.CurrentCulture,
                 FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.Black);
                customDrawOp = new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), noSkia, this);
            }

            context.Custom(customDrawOp);
        }
    }
}
