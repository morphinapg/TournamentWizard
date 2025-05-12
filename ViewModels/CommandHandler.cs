using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TournamentWizard.ViewModels
{
    public class CommandHandler : ICommand
    {
        private Action _action;
        public CommandHandler(Action action1)
        {
            _action = action1;
        }

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            _action();
        }
#pragma warning disable CS0067 // The event 'CommandHandler.CanExecuteChanged' is never used
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067 // The event 'CommandHandler.CanExecuteChanged' is never used
    }
}
