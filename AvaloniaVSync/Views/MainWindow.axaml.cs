using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaVSync.ViewModels;
using System;

namespace AvaloniaVSync.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
           // this.AttachDevTools();
#endif
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
       
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
        }

        private void Tick(TimeSpan time)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.GenerateData();
            }
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);

        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
