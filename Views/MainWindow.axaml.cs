using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

        protected override void OnResized(WindowResizedEventArgs e)
        {
            base.OnResized(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is MainViewModel model)
            {
                model.Flyout = Rename.Flyout;
                model.TextBox = RenameBox;
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
                //else if (e.Key == Key.Up)
                //    model.Choose1();
                //else if (e.Key == Key.Down)
                //    model.Choose2();
                //else if (e.Key == Key.NumPad1 || e.Key == Key.D1)
                //    model.Choose1();
                //else if (e.Key == Key.NumPad2 || e.Key == Key.D2)
                //    model.Choose2();
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
            //Check if any of the window edges are snapped to the screen edges, and if so, maximize and restore the window to unsnap it before saving the position and size

            if (WindowState == WindowState.Maximized || (Screens.Primary is not null && (Position.X <= 0 || Position.Y <= 0 || Position.X + Width >= Screens.Primary.WorkingArea.Width || Position.Y + Height >= Screens.Primary.WorkingArea.Height)))
            {
                WindowState = WindowState.Maximized;
                WindowState = WindowState.Normal;
            }
            else if (Screens.Primary is null)
            {
                // If Screens.Primary is null, we can't check the working area, but we can still maximize and restore the window to unsnap it
                WindowState = WindowState.Maximized;
                WindowState = WindowState.Normal;
            }


            Preferences.Set("Top", Position.Y);
            Preferences.Set("Left", Position.X);
            Preferences.Set("Width", Width);
            Preferences.Set("Height", Height);

            base.OnClosing(e);            
        }
    }
}