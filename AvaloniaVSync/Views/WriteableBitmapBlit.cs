using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Threading;
#if AVALONIA_0_10
using Avalonia.Visuals.Media.Imaging;
#endif
namespace AvaloniaVSync.Views
{
    public partial class WriteableBitmapBlit : Control
    {
        private WriteableBitmap _bitmap;
        private int _scroll = 0;
        public WriteableBitmapBlit()
        {
            CreateImage();
            UseLayoutRounding = true;
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

        private void CreateImage()
        {
            int width = 2048;
            int height = 512;

            var map = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96));

            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888,AlphaFormat.Premul);

            unsafe
            {
                using (var pixels = _bitmap.Lock())
                {
                    for (int y = 0; y < height; y++)
                    {
                        uint* p = (uint*)pixels.Address + y * pixels.RowBytes / 4;
                        for (int x = 0; x < width; x++)
                        {
                            uint bri = (uint)((x * y) % 256);
                            if(x>width/2)
                            {
                                bri = 4*(x-width/2) > y ? 255u : 0u;
                            }

                            *(p++) = 0xff000000U + (bri << 16) + (bri << 8) + bri;
                        }
                    }
                }
            }
        }
        public override void Render(DrawingContext context)
        {
            
            context.DrawImage(_bitmap,
                         new Rect(0, _scroll, _bitmap.PixelSize.Width , _bitmap.PixelSize.Height / 2),
                         new Rect(0, 0, _bitmap.PixelSize.Width , _bitmap.PixelSize.Height / 2));

            _scroll = (_scroll + 1) % 256;

        }
    }
}

