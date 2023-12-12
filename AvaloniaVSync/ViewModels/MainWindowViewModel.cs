using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace AvaloniaVSync.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public Subject<float[]> Spectrum { get; } = new();

        private double phase = 0;

        private Random rand = new Random();
        public MainWindowViewModel()
        {
   
        }

        public void GenerateData()
        {
            float[] spec = new float[1024];
           
            for (int i = 0; i < spec.Length / 4; i++)
            {
                spec[i] = (float)rand.NextDouble();
            }
            for (int i = spec.Length / 4; i < spec.Length / 2; i++)
            {
                 spec[i] =(float)(rand.Next(64) > 60 ? 1.0 : 0.0);               
            }
            for (int i = spec.Length / 2; i < spec.Length; i++)
            {
                spec[i] = (float)(Math.Sin((double)i / 10 + phase)*0.5 + 0.5);               
            }
            phase += 0.1;
            if (phase > 2 * Math.PI) phase -= 2 * Math.PI;

            Spectrum.OnNext(spec);
        }
    }
}

