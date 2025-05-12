using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using TournamentWizard.ViewModels;

namespace TournamentWizard.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();            
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            CurrentApp.TopLevel = TopLevel.GetTopLevel(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape && DataContext is MainViewModel model)
            {
                model.DeselectItem();
            }
        }
    }
}