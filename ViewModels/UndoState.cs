using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TournamentWizard.ViewModels
{
    record UndoState(int TierIndex, int CurrentPosition, string Choice1, string Choice2, string Chosen, int CurrentTotal, int TotalToal, int CurrentProgress, int TotalProgress, ObservableCollection<string> InputItems, ObservableCollection<string> OutputItems, List<string> CurrentOutputs, bool ReplacementMode, string? ReplacementItem, Tier? ReplacementTier);
}
