using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using Avalonia.Animation;

namespace AvaloniaVSync.Views
{
    internal class VsyncTest:Control
    {
        private int _frame = 0;
        
        public VsyncTest()
        {
      //      Clock ??= new Clock();
     //       Clock.Subscribe(t => InvalidateVisual());
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
        public override void Render(DrawingContext context)
        {
            // this should appear as gray if vsync is correct
            context.FillRectangle((_frame++ & 1) == 0 ? Brushes.Cyan : Brushes.Red, Bounds);
        }
    }
}
