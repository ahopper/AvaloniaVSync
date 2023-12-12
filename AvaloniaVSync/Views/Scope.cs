using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace AvaloniaVSync.Views
{
    public class Scope : Control
    {
        public Scope()
        {
            ClipToBounds = true;
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);         
        }
        public override void ApplyTemplate()
        {
            base.ApplyTemplate();
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(ClockTick);
        }
        private void ClockTick(TimeSpan time)
        {
            InvalidateVisual();
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(ClockTick);
        }

        class CustomDrawOp : ICustomDrawOperation
        {
            private readonly FormattedText _noSkia;

            private SKBitmap _bitmap;
            
            private SKImage _lastImage;

            SKRuntimeEffect effect;

            public CustomDrawOp(Rect bounds, FormattedText noSkia)
            {
                _noSkia = noSkia;
                Bounds = bounds;
                _bitmap = new SKBitmap((int)Bounds.Width, 1, true);
                for(int i=0;i<Bounds.Width;i++)
                {
                    _bitmap.SetPixel(i, 0, new SKColor((byte)i, (byte)(i*2), (byte)(i*3), 255));
                }

                var src = @"
in fragmentProcessor color_map;

uniform float scale;
uniform half exp;
uniform float3 in_colors0;

half4 main(float2 p) {
	half4 texColor = sample(color_map, p/8);
	if(texColor.r*400 > p.y)
    {
        texColor.r=1;
        texColor.g=1;
        texColor.b=1;
    }
    return texColor;
}";
                effect = SKRuntimeEffect.Create(src, out var errorText);
            }

            public void Dispose()
            {
                // No-op
            }

            public Rect Bounds { get; }
            public bool HitTest(Point p) => false;
            public bool Equals(ICustomDrawOperation other) => false;

            int phase = 0;
            Random rand = new();
            public void Render(ImmediateDrawingContext context)
            {               
                phase++;
                unsafe
                {
                    UInt32* pPixels = (UInt32*)_bitmap.GetPixels();
                    for (int i = 0; i < Bounds.Width; i++)
                    {
                        var y = (byte)rand.Next(255);
                        //_bitmap.SetPixel(i, 0, new SKColor(y, (byte)(i * 2), (byte)(i * 3), 255));
                        *(pPixels++) = (UInt32)(0xff000000u + y * 256 * 256);
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

                    canvas.Save();
                    
                    var scale = canvas.TotalMatrix.ScaleX;
                    
                    Shade(canvas);

                    canvas.Restore();
                    
                 
                }              
            }  
            private void Shade(SKCanvas canvas)
            {
                // input values
                 
                float threshold = 1.05f;
                float exponent = 1.5f;

                // input values
                

                var inputs = new SKRuntimeEffectUniforms(effect);
                inputs["scale"]= threshold;
                inputs["exp"]= exponent;
                inputs["in_colors0"]= new[] { 1f, 0f, 1f };

                // shader values
               using var textureShader = _bitmap.ToShader();
                var children = new SKRuntimeEffectChildren(effect);
                children["color_map"]= textureShader;

                // create actual shader
                using var shader = effect.ToShader(true,inputs, children);

                // draw as normal
             //   canvas.Clear(SKColors.Black);
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
                customDrawOp = new CustomDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height), noSkia);
            }

            context.Custom(customDrawOp);
        }
    }
}
