using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.SimplePreferences;
using System;
using System.Timers;
using TournamentWizard.ViewModels;

namespace TournamentWizard.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            ResetTimer.Elapsed += (s, e) => Waiting = false;

            InitializeComponent();

            if (Preferences.Get("Top", default(int?)) is int top && Preferences.Get("Left", default(int?)) is int left && Preferences.Get("Width", default(int?)) is int width && Preferences.Get("Height", default(int?)) is int height)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Width = width;
                Height = height;
                Position = new PixelPoint(left, top);                
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            CurrentApp.TopLevel = TopLevel.GetTopLevel(this);
        }

        // Timer to prevent multiple key presses
        Timer ResetTimer = new(150) { AutoReset = false };

        bool Waiting = false;

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (!Waiting && DataContext is MainViewModel model)
            {
                if (e.Key == Key.Escape)
                    model.DeselectItem();
                else if (e.Key == Key.Left)
                    model.Choose1();
                else if (e.Key == Key.Right)
                    model.Choose2();
                else if (e.Key == Key.Up)
                    model.Choose1();
                else if (e.Key == Key.Down)
                    model.Choose2();
                else if (e.Key == Key.NumPad1 || e.Key == Key.D1)
                    model.Choose1();
                else if (e.Key == Key.NumPad2 || e.Key == Key.D2)
                    model.Choose2();
                else
                    base.OnKeyUp(e);

                Waiting = true;
                ResetTimer.Start();
            }
            else if (!(DataContext is MainViewModel))
                base.OnKeyUp(e);
        }


        protected override void OnClosing(WindowClosingEventArgs e)
        {
            Preferences.Set("Top", Position.Y);
            Preferences.Set("Left", Position.X);
            Preferences.Set("Width", Width);
            Preferences.Set("Height", Height);

            base.OnClosing(e);            
        }
    }
}